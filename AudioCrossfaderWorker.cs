using System.Runtime.InteropServices;

public class AudioCrossfaderWorker
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;

    private struct KeyBindingState
    {
        public bool WasComboActive;
        public bool WasPhysicalKeyDown;
        public DateTime NextRepeatTime;

        public int PollSteps(short keyState, bool modifiersMatch, bool allowHoldRepeat)
        {
            bool isPhysicalDown = (keyState & 0x8000) != 0;
            bool hadTransition = (keyState & 0x0001) != 0;

            bool microPulse = !isPhysicalDown && !WasPhysicalKeyDown && hadTransition;
            bool isComboActive = (isPhysicalDown || microPulse) && modifiersMatch;

            int steps = 0;
            var now = DateTime.UtcNow;

            if (isComboActive)
            {
                if (!WasComboActive && !WasPhysicalKeyDown)
                {
                    steps = 1;
                    NextRepeatTime = now.AddMilliseconds(400);
                }
                else if (allowHoldRepeat && WasComboActive && isPhysicalDown && now >= NextRepeatTime)
                {
                    steps = 1;
                    NextRepeatTime = now.AddMilliseconds(55);
                }
            }

            WasComboActive = isComboActive && isPhysicalDown;
            WasPhysicalKeyDown = isPhysicalDown;
            return steps;
        }
    }

    private sealed class CollectionKeyTrackers
    {
        public KeyBindingState UpTracker;
        public KeyBindingState DownTracker;
        public KeyBindingState MuteTracker;
        public KeyBindingState NextTracker;
        public KeyBindingState PrevTracker;
    }

    private sealed class PairRuntimeState
    {
        public float TargetPosition = 0.5f;
        public float VisualPosition = 0.5f;
        public int MuteState = 0;
        public int DetentClicks = 0;
        public int DetentDirection = 0;
    }

    private AppSettings _settings;
    private readonly Action<AppSettings>? _onRuntimeSettingsMutated;

    private readonly Dictionary<string, PairRuntimeState> _pairStates = new(StringComparer.OrdinalIgnoreCase);
    private int _cfCycleIndex = -1;
    private int _activeCollectionIndex = 0;
    private readonly List<int> _collectionPairIndices = new();
    private string _hudSource1 = "";
    private string _hudSource1Color = "#1B3B5A";
    private string _hudSource2 = "";
    private string _hudSource2Color = "#4A2040";
    private bool _hudIsStandalone = false;
    private ControlModeState _hudModeState = ControlModeState.Volume;
    private bool _hudIsSingleSource => _hudModeState == ControlModeState.Volume;

    private readonly List<CrossfadeOverlayForm> _overlays = new();
    private Thread? _workerThread;
    private CancellationTokenSource? _cts;
    private readonly object _stateLock = new();
    private volatile bool _forceResetAudioFlag = false;
    private volatile bool _settingsWindowOpen = false;
    private volatile bool _colorPickerOpen = false;
    private volatile bool _previewVisualOnly = false;
    private volatile bool _previewAnimateTransition = true;
    private volatile bool _previewRequestedFlag = false;
    private AppSettings? _pendingPreviewSettings = null;
    private AppSettings? _lastAppliedOverlayMonitorSettings = null;

    public AudioCrossfaderWorker(AppSettings settings, Action<AppSettings>? onRuntimeSettingsMutated = null)
    {
        _settings = settings.Clone();
        _onRuntimeSettingsMutated = onRuntimeSettingsMutated;
        AppIconCache.LoadPathsFromSettings(_settings);
        SyncCycleIndices();
        SnapHudToCarouselPair();
    }

    private static string NormalizeSourceIdentifier(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        string trimmed = raw.Trim();
        return trimmed.StartsWith("Group: ", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : AppSettings.NormalizeExeName(trimmed);
    }

    private static string GetPairKey(string s1, string s2, bool isSingle)
    {
        return isSingle
            ? $"SINGLE||{NormalizeSourceIdentifier(s1)}"
            : $"{NormalizeSourceIdentifier(s1)}||{NormalizeSourceIdentifier(s2)}";
    }

    private PairRuntimeState GetOrCreatePairState(string s1, string s2, bool isSingle = false)
    {
        string key = GetPairKey(s1, s2, isSingle);
        if (!_pairStates.TryGetValue(key, out var state))
        {
            state = new PairRuntimeState
            {
                // Standard volume starts at 100% (1.0f); Crossfade starts at center (0.5f)
                TargetPosition = isSingle ? 1.0f : 0.5f,
                VisualPosition = isSingle ? 1.0f : 0.5f
            };
            _pairStates[key] = state;
        }
        return state;
    }

    private static int AdvanceMuteState(int currentState, string? muteMode, bool isSingleSource = false)
    {
        if (isSingleSource)
        {
            // Toggle between Unmuted (0) and Source1 Muted (2) regardless of cycle setting
            return currentState == 0 ? 2 : 0;
        }

        return (muteMode ?? "Cycle R → L") switch
        {
            "Cycle L → R" => currentState switch { 2 => 1, 1 => 0, _ => 2 },
            "Mute Both"   => currentState == 3 ? 0 : 3,
            "Mute Left"   => currentState == 2 ? 0 : 2,
            "Mute Right"  => currentState == 1 ? 0 : 1,
            _             => currentState switch { 1 => 2, 2 => 0, _ => 1 }
        };
    }

    private string ResolveEffectiveMuteMode(string? pairMuteMode)
    {
        if (string.IsNullOrWhiteSpace(pairMuteMode) ||
            pairMuteMode.Equals("Use Global", StringComparison.OrdinalIgnoreCase) ||
            pairMuteMode.Equals("Default", StringComparison.OrdinalIgnoreCase))
        {
            return _settings.GlobalMuteMode;
        }
        return pairMuteMode;
    }

    private string GetCarouselActiveMuteMode()
    {
        return _settings.GlobalMuteMode;
    }

    private void SnapHudToCarouselPair()
    {
        _hudSource1 = _settings.Source1;
        _hudSource1Color = _settings.Source1Color;
        _hudSource2 = _settings.Source2;
        _hudSource2Color = _settings.Source2Color;
        _hudIsStandalone = false;
        _hudModeState = ControlModeStateMachine.ParseState(_settings.ActiveControlMode);
    }

    private void SyncCycleIndices()
    {
        _settings.EnsureCollectionsInitialized();
        _activeCollectionIndex = Math.Clamp(_settings.ActiveCollectionIndex, 0, _settings.Collections.Count - 1);

        while (_collectionPairIndices.Count < _settings.Collections.Count)
            _collectionPairIndices.Add(0);

        for (int c = 0; c < _settings.Collections.Count; c++)
        {
            var col = _settings.Collections[c];
            int idx = Math.Clamp(_collectionPairIndices[c], 0, Math.Max(0, col.Pairs.Count - 1));
            _collectionPairIndices[c] = idx;
        }

        ActivateCollectionOnHud(_activeCollectionIndex, _collectionPairIndices[_activeCollectionIndex], pushStyleToOverlays: false);
    }

    private void ActivateCollectionOnHud(int colIndex, int pairIndex, bool pushStyleToOverlays)
    {
        if (_settings.Collections.Count == 0) return;
        colIndex = Math.Clamp(colIndex, 0, _settings.Collections.Count - 1);
        var col = _settings.Collections[colIndex];

        var validPairs = col.Pairs
            .Where(p => !string.IsNullOrWhiteSpace(p.Source1) && (p.IsSingleMode || !string.IsNullOrWhiteSpace(p.Source2)))
            .ToList();
        if (validPairs.Count == 0) return;

        pairIndex = ((pairIndex % validPairs.Count) + validPairs.Count) % validPairs.Count;
        var activePair = validPairs[pairIndex];

        bool collectionChanged = (_activeCollectionIndex != colIndex);
        _activeCollectionIndex = colIndex;
        _settings.ActiveCollectionIndex = colIndex;
        _collectionPairIndices[colIndex] = pairIndex;

        _settings.ApplyCollectionToActiveOverlayFields(col, activePair);
        SnapHudToCarouselPair();

        if (pushStyleToOverlays && collectionChanged)
        {
            bool pinned = _settingsWindowOpen && _settings.KeepOverlayOnWhileSettingsOpen;
            foreach (var ov in _overlays.ToList())
            {
                ov.ApplyLivePreviewSettings(_settings, pinned, forcePopup: false, animateTransition: false);
            }
        }
    }

    private (List<CycleDisplayItem> items, int activeIndex) PerformCollectionCycleStep(AudioSessionCache sessionCache, int colIndex, int direction)
    {
        List<CycleDisplayItem> displayItems = new List<CycleDisplayItem>();
        int newActiveIdx = 0;

        lock (_stateLock)
        {
            var col = _settings.Collections[colIndex];
            var validPairs = col.Pairs
                .Where(p => !string.IsNullOrWhiteSpace(p.Source1) && (p.IsSingleMode || !string.IsNullOrWhiteSpace(p.Source2)))
                .ToList();

            if (validPairs.Count > 0)
            {
                foreach (var cp in validPairs)
                {
                    foreach (var exe in _settings.ResolveExecutables(cp.Source1))
                        sessionCache.SetVolumeForProcessKey(AppSettings.NormalizeProcessKey(exe), 1.0f);
                    if (!cp.IsSingleMode)
                    {
                        foreach (var exe in _settings.ResolveExecutables(cp.Source2))
                            sessionCache.SetVolumeForProcessKey(AppSettings.NormalizeProcessKey(exe), 1.0f);
                    }
                }

                int currentIdx = (_collectionPairIndices[colIndex] >= 0 && _collectionPairIndices[colIndex] < validPairs.Count)
                    ? _collectionPairIndices[colIndex]
                    : 0;
                newActiveIdx = ((currentIdx + direction) % validPairs.Count + validPairs.Count) % validPairs.Count;

                ActivateCollectionOnHud(colIndex, newActiveIdx, pushStyleToOverlays: true);

                foreach (var p in validPairs)
                {
                    string s1 = NormalizeSourceIdentifier(p.Source1);
                    string s2 = p.IsSingleMode ? "" : NormalizeSourceIdentifier(p.Source2);
                    displayItems.Add(new CycleDisplayItem(
                        GetResolvedLabel(s1), s1, p.Color1Hex,
                        p.IsSingleMode ? "" : GetResolvedLabel(s2), s2, p.Color2Hex,
                        p.IsSingleMode));
                }

                _onRuntimeSettingsMutated?.Invoke(_settings.Clone());
            }
        }
        return (displayItems, newActiveIdx);
    }

    private void RebuildOverlayForms(string label1, string label2, AppSettings cfg, bool openCarouselOnInit = false)
    {
        foreach (var old in _overlays.ToList())
        {
            try { old.Close(); old.Dispose(); } catch { }
        }
        _overlays.Clear();

        Screen[] screens = Screen.AllScreens;
        if (cfg.FollowActiveMonitor || screens.Length == 0)
        {
            var form = new CrossfadeOverlayForm(label1, label2, cfg, null, cfg.OverlayPosition, openCarouselOnInit);
            form.Show();
            _overlays.Add(form);
            return;
        }

        foreach (var scr in screens)
        {
            var mc = cfg.MonitorConfigs.FirstOrDefault(m => m.DeviceName.Equals(scr.DeviceName, StringComparison.OrdinalIgnoreCase));
            bool enabled = mc != null ? mc.Enabled : scr.Primary;
            string pos = mc != null ? mc.Position : cfg.OverlayPosition;

            if (enabled)
            {
                var form = new CrossfadeOverlayForm(label1, label2, cfg, scr.DeviceName, pos, openCarouselOnInit);
                form.Show();
                _overlays.Add(form);
            }
        }

        if (_overlays.Count == 0)
        {
            var primary = Screen.PrimaryScreen ?? screens[0];
            var form = new CrossfadeOverlayForm(label1, label2, cfg, primary.DeviceName, cfg.OverlayPosition, openCarouselOnInit);
            form.Show();
            _overlays.Add(form);
        }
    }

    public void Start()
    {
        Stop();

        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        _workerThread = new Thread(() => RunLoop(token));
        _workerThread.SetApartmentState(ApartmentState.STA);
        _workerThread.IsBackground = true;
        _workerThread.Start();
    }

    public void Stop()
    {
        if (_cts != null)
        {
            try { _cts.Cancel(); } catch { }
        }

        if (_workerThread != null && _workerThread.IsAlive)
        {
            try { _workerThread.Join(1500); } catch { }
        }
        _workerThread = null;

        try
        {
            foreach (var ov in _overlays.ToList())
            {
                if (!ov.IsDisposed && ov.IsHandleCreated)
                {
                    ov.Invoke(new Action(() => ov.Close()));
                }
            }
        }
        catch { }
        _overlays.Clear();

        if (_cts != null)
        {
            try { _cts.Dispose(); } catch { }
            _cts = null;
        }
    }

    public void UpdateSettings(AppSettings newSettings, bool resetVolumes)
    {
        Stop();

        lock (_stateLock)
        {
            _settings = newSettings.Clone();
            AppIconCache.LoadPathsFromSettings(_settings);
            SyncCycleIndices();
            SnapHudToCarouselPair();

            if (resetVolumes)
            {
                _pairStates.Clear();
            }
        }

        Start();
    }

    public void SetSettingsWindowOpen(bool isOpen, AppSettings? liveDraft = null)
    {
        _settingsWindowOpen = isOpen;
        if (!isOpen) _colorPickerOpen = false;
        if (liveDraft != null)
        {
            lock (_stateLock)
            {
                _pendingPreviewSettings = liveDraft.Clone();
            }
        }
        _previewVisualOnly = false;
        _previewAnimateTransition = true;
        _previewRequestedFlag = true;
    }

    public void PreviewOverlay(AppSettings draftSettings, bool isColorPickerLive = false, bool visualOnly = false, bool animateTransition = true)
    {
        _colorPickerOpen = isColorPickerOpenOrLive(isColorPickerLive);
        lock (_stateLock)
        {
            _pendingPreviewSettings = draftSettings.Clone();
        }
        _previewVisualOnly = visualOnly;
        _previewAnimateTransition = animateTransition;
        _previewRequestedFlag = true;
    }

    private static bool isColorPickerOpenOrLive(bool isLive) => isLive;

    private static bool MonitorLayoutMatches(AppSettings? a, AppSettings b)
    {
        if (a == null) return false;
        if (a.FollowActiveMonitor != b.FollowActiveMonitor) return false;
        if (a.OverlayPosition != b.OverlayPosition) return false;
        if (a.MonitorConfigs.Count != b.MonitorConfigs.Count) return false;

        for (int i = 0; i < a.MonitorConfigs.Count; i++)
        {
            var ma = a.MonitorConfigs[i];
            var mb = b.MonitorConfigs[i];
            if (!ma.DeviceName.Equals(mb.DeviceName, StringComparison.OrdinalIgnoreCase) ||
                ma.Enabled != mb.Enabled ||
                !ma.Position.Equals(mb.Position, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    public void ResetVolumesToFullAndCenter()
    {
        lock (_stateLock)
        {
            _pairStates.Clear();
        }
        _forceResetAudioFlag = true;
    }

    private static bool ModifiersMatch(int binding, bool ctrlDown, bool altDown, bool shiftDown)
    {
        return KeybindHelper.HasCtrl(binding) == ctrlDown &&
               KeybindHelper.HasAlt(binding) == altDown &&
               KeybindHelper.HasShift(binding) == shiftDown;
    }

    public static string GetResolvedLabel(string sourceTarget)
    {
        if (string.IsNullOrEmpty(sourceTarget)) return "";
        if (sourceTarget.StartsWith("Group: ", StringComparison.OrdinalIgnoreCase))
        {
            return sourceTarget.Substring(7).ToUpperInvariant();
        }
        return sourceTarget.Replace(".exe", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
    }

    private void ResetAllConfiguredExecutables(AudioSessionCache sessionCache)
    {
        sessionCache.ForceRefresh();
        foreach (var exe in _settings.ResolveExecutables(_settings.Source1))
            sessionCache.SetVolumeForProcessKey(AppSettings.NormalizeProcessKey(exe), 1.0f);
        foreach (var exe in _settings.ResolveExecutables(_settings.Source2))
            sessionCache.SetVolumeForProcessKey(AppSettings.NormalizeProcessKey(exe), 1.0f);

        foreach (var pair in _settings.GetAllConfiguredPairs())
        {
            foreach (var exe in _settings.ResolveExecutables(pair.Source1))
                sessionCache.SetVolumeForProcessKey(AppSettings.NormalizeProcessKey(exe), 1.0f);
            foreach (var exe in _settings.ResolveExecutables(pair.Source2))
                sessionCache.SetVolumeForProcessKey(AppSettings.NormalizeProcessKey(exe), 1.0f);
        }
    }

    private void ShowPairOnHudTemporarily(CrossfadeCyclePair pair)
    {
        _hudSource1 = NormalizeSourceIdentifier(pair.Source1);
        _hudSource1Color = pair.Color1Hex;
        _hudSource2 = pair.IsVolumeMode ? "" : NormalizeSourceIdentifier(pair.Source2);
        _hudSource2Color = pair.Color2Hex;
        _hudIsStandalone = true;
        _hudModeState = pair.ModeState;
    }

    private (List<CycleDisplayItem> items, int activeIndex) PerformCycleStep(AudioSessionCache sessionCache, int direction)
    {
        List<CycleDisplayItem> displayItems = new List<CycleDisplayItem>();
        int newActiveIdx = 0;

        lock (_stateLock)
        {
            var validPairs = _settings.CrossfadeCycleList
                .Where(p => !string.IsNullOrWhiteSpace(p.Source1) && (p.IsSingleMode || !string.IsNullOrWhiteSpace(p.Source2)))
                .Select(p => new CrossfadeCyclePair
                {
                    Source1 = NormalizeSourceIdentifier(p.Source1),
                    Color1Hex = p.Color1Hex,
                    Source2 = p.IsSingleMode ? "" : NormalizeSourceIdentifier(p.Source2),
                    Color2Hex = p.Color2Hex,
                    ControlMode = p.ControlMode,
                    InvertDirection = p.InvertDirection
                })
                .ToList();

            if (validPairs.Count > 0)
            {
                foreach (var cp in validPairs)
                {
                    foreach (var exe in _settings.ResolveExecutables(cp.Source1))
                        sessionCache.SetVolumeForProcessKey(AppSettings.NormalizeProcessKey(exe), 1.0f);
                    if (!cp.IsSingleMode)
                    {
                        foreach (var exe in _settings.ResolveExecutables(cp.Source2))
                            sessionCache.SetVolumeForProcessKey(AppSettings.NormalizeProcessKey(exe), 1.0f);
                    }
                }

                int currentIdx = (_cfCycleIndex >= 0 && _cfCycleIndex < validPairs.Count) ? _cfCycleIndex : 0;
                newActiveIdx = ((currentIdx + direction) % validPairs.Count + validPairs.Count) % validPairs.Count;

                var nextPair = validPairs[newActiveIdx];
                _settings.Source1 = nextPair.Source1;
                _settings.Source1Color = nextPair.Color1Hex;
                _settings.Source2 = nextPair.IsSingleMode ? "" : nextPair.Source2;
                _settings.Source2Color = nextPair.Color2Hex;
                _settings.ActiveControlMode = nextPair.ControlMode;
                _cfCycleIndex = newActiveIdx;
                SnapHudToCarouselPair();

                foreach (var p in validPairs)
                {
                    displayItems.Add(new CycleDisplayItem(
                        GetResolvedLabel(p.Source1), p.Source1, p.Color1Hex,
                        p.IsSingleMode ? "" : GetResolvedLabel(p.Source2), p.Source2, p.Color2Hex,
                        p.IsSingleMode));
                }

                _onRuntimeSettingsMutated?.Invoke(_settings.Clone());
            }
        }
        return (displayItems, newActiveIdx);
    }

    private void RunLoop(CancellationToken token)
    {
        using AudioSessionCache sessionCache = new AudioSessionCache();
        sessionCache.ForceRefresh();

        AppSettings initialOverlaySettings = _settings.Clone();
        string initLabel1 = GetResolvedLabel(initialOverlaySettings.Source1);
        string initLabel2 = _hudIsSingleSource ? "" : GetResolvedLabel(initialOverlaySettings.Source2);
        _lastAppliedOverlayMonitorSettings = initialOverlaySettings.Clone();

        Thread uiThread = new Thread(() =>
        {
            RebuildOverlayForms(initLabel1, initLabel2, initialOverlaySettings, openCarouselOnInit: true);
            Application.Run();
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.IsBackground = true;
        uiThread.Start();

        Thread.Sleep(150);
        ApplyVolumesForPair(sessionCache, _hudSource1, _hudSource2, _hudSource1Color, _hudSource2Color, true, true);

        DateTime lastAudioApplyTime = DateTime.MinValue;
        DateTime lastVisualPushTime = DateTime.MinValue;
        bool audioUpdatePending = false;

        Dictionary<int, short> keySnapshot = new Dictionary<int, short>(16);

        short SampleKeyOnce(int vk)
        {
            if (vk == 0) return 0;
            if (!keySnapshot.TryGetValue(vk, out short state))
            {
                state = GetAsyncKeyState(vk);
                keySnapshot[vk] = state;
            }
            return state;
        }

        DateTime lastActiveInputUtc = DateTime.UtcNow;

        List<CollectionKeyTrackers> collectionTrackers = new();

        while (!token.IsCancellationRequested)
        {
            if (_forceResetAudioFlag)
            {
                _forceResetAudioFlag = false;
                ResetAllConfiguredExecutables(sessionCache);
                SnapHudToCarouselPair();
                ApplyVolumesForPair(sessionCache, _hudSource1, _hudSource2, _hudSource1Color, _hudSource2Color, true, true);
            }

            if (_previewRequestedFlag)
            {
                _previewRequestedFlag = false;
                bool visualOnly = _previewVisualOnly;
                bool animTrans = _previewAnimateTransition;

                AppSettings? previewCfg = null;
                lock (_stateLock)
                {
                    previewCfg = _pendingPreviewSettings?.Clone();
                }

                var cfgToShow = previewCfg ?? _settings.Clone();

                if (!visualOnly)
                {
                    var firstCarousel = cfgToShow.CrossfadeCycleList.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Source1) && (p.IsSingleMode || !string.IsNullOrWhiteSpace(p.Source2)));
                    if (firstCarousel != null)
                    {
                        cfgToShow.Source1 = NormalizeSourceIdentifier(firstCarousel.Source1);
                        cfgToShow.Source1Color = firstCarousel.Color1Hex;
                        cfgToShow.Source2 = firstCarousel.IsSingleMode ? "" : NormalizeSourceIdentifier(firstCarousel.Source2);
                        cfgToShow.Source2Color = firstCarousel.Color2Hex;
                        cfgToShow.ActiveControlMode = firstCarousel.ControlMode;
                    }
                }

                if (_overlays.Count == 0 || !MonitorLayoutMatches(_lastAppliedOverlayMonitorSettings, cfgToShow))
                {
                    _lastAppliedOverlayMonitorSettings = cfgToShow.Clone();
                    string l1 = GetResolvedLabel(cfgToShow.Source1);
                    string l2 = GetResolvedLabel(cfgToShow.Source2);
                    foreach (var ov in _overlays.ToList())
                    {
                        if (!ov.IsDisposed && ov.IsHandleCreated)
                        {
                            ov.Invoke(new Action(() => RebuildOverlayForms(l1, l2, cfgToShow, openCarouselOnInit: false)));
                            break;
                        }
                    }
                }

                bool pinned = (_settingsWindowOpen && cfgToShow.KeepOverlayOnWhileSettingsOpen) || _colorPickerOpen;
                foreach (var ov in _overlays.ToList())
                {
                    ov.ApplyLivePreviewSettings(cfgToShow, pinned, forcePopup: true, animateTransition: animTrans);
                }

                if (!visualOnly)
                {
                    lock (_stateLock)
                    {
                        _hudSource1 = cfgToShow.Source1;
                        _hudSource1Color = cfgToShow.Source1Color;
                        _hudSource2 = cfgToShow.Source2;
                        _hudSource2Color = cfgToShow.Source2Color;
                        _hudIsStandalone = false;
                        _hudModeState = ControlModeStateMachine.ParseState(cfgToShow.ActiveControlMode);
                    }
                    ApplyVolumesForPair(sessionCache, _hudSource1, _hudSource2, _hudSource1Color, _hudSource2Color, false, true);
                }
            }

            bool physicalInputRegistered = false;
            bool immediateAudioUpdate = false;

            keySnapshot.Clear();
            bool ctrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            bool altDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            bool shiftDown = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

            float stepIncrement = (Math.Clamp(_settings.StepSize, 1, 100) / 2.0f) / 100.0f;
            float lerpFactor = Math.Clamp(_settings.InterpolationSpeed, 5, 100) / 100.0f;

            var now = DateTime.UtcNow;
            bool frameTickReady = (now - lastVisualPushTime).TotalMilliseconds >= 15;

            var collections = _settings.Collections;
            while (collectionTrackers.Count < collections.Count)
                collectionTrackers.Add(new CollectionKeyTrackers());
            while (_collectionPairIndices.Count < collections.Count)
                _collectionPairIndices.Add(0);

            for (int cIdx = 0; cIdx < collections.Count; cIdx++)
            {
                var col = collections[cIdx];
                var validPairs = col.Pairs
                    .Where(p => !string.IsNullOrWhiteSpace(p.Source1) && (p.IsSingleMode || !string.IsNullOrWhiteSpace(p.Source2)))
                    .ToList();
                if (validPairs.Count == 0) continue;

                var tracker = collectionTrackers[cIdx];

                int nextVk = KeybindHelper.GetPrimaryVk(col.CycleNextKey);
                bool nextMods = ModifiersMatch(col.CycleNextKey, ctrlDown, altDown, shiftDown);
                if (tracker.NextTracker.PollSteps(SampleKeyOnce(nextVk), nextMods, allowHoldRepeat: false) > 0)
                {
                    var (cycleItems, activeIdx) = PerformCollectionCycleStep(sessionCache, cIdx, +1);
                    foreach (var ov in _overlays.ToList())
                        ov.TriggerCycleWheel(cycleItems, activeIdx, +1);
                    physicalInputRegistered = true;
                    immediateAudioUpdate = true;
                }

                int prevVk = KeybindHelper.GetPrimaryVk(col.CyclePrevKey);
                bool prevMods = ModifiersMatch(col.CyclePrevKey, ctrlDown, altDown, shiftDown);
                if (tracker.PrevTracker.PollSteps(SampleKeyOnce(prevVk), prevMods, allowHoldRepeat: false) > 0)
                {
                    var (cycleItems, activeIdx) = PerformCollectionCycleStep(sessionCache, cIdx, -1);
                    foreach (var ov in _overlays.ToList())
                        ov.TriggerCycleWheel(cycleItems, activeIdx, -1);
                    physicalInputRegistered = true;
                    immediateAudioUpdate = true;
                }

                int upVk = KeybindHelper.GetPrimaryVk(col.KeyUp);
                int downVk = KeybindHelper.GetPrimaryVk(col.KeyDown);
                int muteVk = KeybindHelper.GetPrimaryVk(col.KeyMute);

                bool upMods = ModifiersMatch(col.KeyUp, ctrlDown, altDown, shiftDown);
                bool downMods = ModifiersMatch(col.KeyDown, ctrlDown, altDown, shiftDown);
                bool muteMods = ModifiersMatch(col.KeyMute, ctrlDown, altDown, shiftDown);

                int upSteps = tracker.UpTracker.PollSteps(SampleKeyOnce(upVk), upMods, allowHoldRepeat: true);
                int downSteps = tracker.DownTracker.PollSteps(SampleKeyOnce(downVk), downMods, allowHoldRepeat: true);
                int muteSteps = tracker.MuteTracker.PollSteps(SampleKeyOnce(muteVk), muteMods, allowHoldRepeat: false);

                if (upSteps > 0 || downSteps > 0 || muteSteps > 0)
                {
                    int activePairIdx = Math.Clamp(_collectionPairIndices[cIdx], 0, validPairs.Count - 1);
                    var activePair = validPairs[activePairIdx];

                    lock (_stateLock)
                    {
                        ActivateCollectionOnHud(cIdx, activePairIdx, pushStyleToOverlays: true);
                    }

                    float baseSign = activePair.IsSingleMode ? 1.0f : -1.0f;
                    float dirMultiplier = activePair.InvertDirection ? -baseSign : baseSign;
                    float divisor = (activePair.ModeState == ControlModeState.Balance) ? 2.0f : 1.0f;
                    float stepInc = (Math.Clamp(_settings.StepSize, 1, 100) / divisor) / 100.0f;

                    var pState = GetOrCreatePairState(_hudSource1, _hudSource2, _hudIsSingleSource);

                    if (muteSteps > 0)
                    {
                        pState.MuteState = AdvanceMuteState(pState.MuteState, col.MuteMode, _hudIsSingleSource);
                        immediateAudioUpdate = true;
                    }
                    if (upSteps > 0)
                    {
                        StepCrossfadeState(pState, dirMultiplier * stepInc * upSteps, activePair.ModeState);
                    }
                    if (downSteps > 0)
                    {
                        StepCrossfadeState(pState, -dirMultiplier * stepInc * downSteps, activePair.ModeState);
                    }

                    physicalInputRegistered = true;
                }
            }

            // 3. Step visual interpolation for whichever pair is currently displayed on the HUD
            var currentDisplayState = GetOrCreatePairState(_hudSource1, _hudSource2, _hudIsSingleSource);
            bool isAnimating = false;

            if (_settings.EnableInterpolation)
            {
                float diff = currentDisplayState.TargetPosition - currentDisplayState.VisualPosition;
                if (Math.Abs(diff) > 0.001f)
                {
                    if (frameTickReady) currentDisplayState.VisualPosition += diff * lerpFactor;
                    isAnimating = true;
                }
                else
                {
                    currentDisplayState.VisualPosition = currentDisplayState.TargetPosition;
                }
            }
            else
            {
                currentDisplayState.VisualPosition = currentDisplayState.TargetPosition;
            }

            if (currentDisplayState.VisualPosition <= 0.002f) currentDisplayState.VisualPosition = 0.0f;
            if (currentDisplayState.VisualPosition >= 0.998f) currentDisplayState.VisualPosition = 1.0f;

            if (physicalInputRegistered)
            {
                lastActiveInputUtc = now;
                audioUpdatePending = true;
            }

            bool shouldUpdateAudio = audioUpdatePending && (immediateAudioUpdate || (now - lastAudioApplyTime).TotalMilliseconds >= 12);
            if (shouldUpdateAudio)
            {
                if (immediateAudioUpdate)
                {
                    sessionCache.ForceRefresh();
                }
                lastAudioApplyTime = now;
                audioUpdatePending = false;
            }

            if (physicalInputRegistered || shouldUpdateAudio || (isAnimating && frameTickReady))
            {
                if (frameTickReady || physicalInputRegistered) lastVisualPushTime = now;
                ApplyVolumesForPair(sessionCache, _hudSource1, _hudSource2, _hudSource1Color, _hudSource2Color, shouldUpdateAudio, physicalInputRegistered);
            }

            bool turboActive = isAnimating ||
                               audioUpdatePending ||
                               (now - lastActiveInputUtc).TotalMilliseconds < 5000.0 ||
                               _overlays.Any(o => !o.IsFadedOut);

            Thread.Sleep(turboActive ? 2 : 8);
        }

        ResetAllConfiguredExecutables(sessionCache);
    }

    private void StepCrossfadeState(PairRuntimeState state, float delta, ControlModeState modeState)
    {
        bool isSingleSource = modeState == ControlModeState.Volume;
        float divisor = (modeState == ControlModeState.Balance) ? 2.0f : 1.0f;
        float stepIncrement = (Math.Clamp(_settings.StepSize, 1, 100) / divisor) / 100.0f;
        float snapEpsilon = Math.Min(0.015f, stepIncrement * 0.45f);

        float SnapEndpoints(float val)
        {
            if (val <= snapEpsilon) return 0.0f;
            if (val >= 1.0f - snapEpsilon) return 1.0f;
            return Math.Clamp(val, 0.0f, 1.0f);
        }

        // Standard volume mode or disabled center detent -> step linearly without pausing at 50%
        if (isSingleSource || !_settings.EnableEqualizationBuffer)
        {
            state.TargetPosition = SnapEndpoints(state.TargetPosition + delta); // Removed the double * 2.0f multiplier
            return;
        }

        if (Math.Abs(state.TargetPosition - 0.5f) < 0.001f)
        {
            bool isFromRest = _overlays.All(o => o.IsFadedOut);
            int currentDeltaSign = Math.Sign(delta);

            if (isFromRest || (currentDeltaSign != 0 && currentDeltaSign != state.DetentDirection))
            {
                state.TargetPosition = SnapEndpoints(state.TargetPosition + delta);
                state.DetentClicks = 0;
                state.DetentDirection = currentDeltaSign;
            }
            else
            {
                state.DetentClicks++;
                if (state.DetentClicks >= 3)
                {
                    state.DetentClicks = 0;
                    state.TargetPosition = SnapEndpoints(state.TargetPosition + delta);
                    state.DetentDirection = currentDeltaSign;
                }
            }
        }
        else
        {
            float newTarget = state.TargetPosition + delta;
            bool crossingCenter = (state.TargetPosition < 0.5f && newTarget >= 0.5f - snapEpsilon) ||
                                  (state.TargetPosition > 0.5f && newTarget <= 0.5f + snapEpsilon);
            if (crossingCenter)
            {
                state.TargetPosition = 0.5f;
                state.DetentClicks = 0;
                state.DetentDirection = Math.Sign(delta);
            }
            else
            {
                state.TargetPosition = SnapEndpoints(newTarget);
            }
        }
    }

    private void ApplyPairAudioOnly(
        AudioSessionCache sessionCache,
        string source1,
        string source2,
        PairRuntimeState state,
        ControlModeState modeState)
    {
        var (rawVol1, rawVol2) = ControlModeStateMachine.EvaluateVolumes(modeState, state.TargetPosition);

        if (modeState == ControlModeState.Volume)
        {
            float vol = Math.Clamp(rawVol1, 0.0f, 1.0f);
            if (vol <= 0.01f) vol = 0.0f;
            if (vol >= 0.99f) vol = 1.0f;

            bool isMuted = state.MuteState != 0;
            float actualVol = isMuted ? 0.0f : vol;

            foreach (var exe in _settings.ResolveExecutables(source1))
            {
                sessionCache.SetVolumeForProcessKey(AppSettings.NormalizeProcessKey(exe), actualVol);
            }
            return;
        }

        float audioApp1Vol = Math.Clamp(rawVol1, 0.0f, 1.0f);
        float audioApp2Vol = Math.Clamp(rawVol2, 0.0f, 1.0f);

        if (audioApp1Vol <= 0.01f) audioApp1Vol = 0.0f;
        if (audioApp1Vol >= 0.99f) audioApp1Vol = 1.0f;
        if (audioApp2Vol <= 0.01f) audioApp2Vol = 0.0f;
        if (audioApp2Vol >= 0.99f) audioApp2Vol = 1.0f;

        bool app1Muted = state.MuteState == 2 || state.MuteState == 3;
        bool app2Muted = state.MuteState == 1 || state.MuteState == 3;

        float actualApp1Vol = app1Muted ? 0.0f : audioApp1Vol;
        float actualApp2Vol = app2Muted ? 0.0f : audioApp2Vol;

        var source1Targets = _settings.ResolveExecutables(source1);
        var source1Keys = new HashSet<string>(source1Targets.Select(AppSettings.NormalizeProcessKey), StringComparer.OrdinalIgnoreCase);

        foreach (var exe in source1Targets)
        {
            sessionCache.SetVolumeForProcessKey(AppSettings.NormalizeProcessKey(exe), actualApp1Vol);
        }

        var source2Targets = _settings.ResolveExecutables(source2);
        foreach (var exe in source2Targets)
        {
            string key = AppSettings.NormalizeProcessKey(exe);
            if (!source1Keys.Contains(key))
            {
                sessionCache.SetVolumeForProcessKey(key, actualApp2Vol);
            }
        }
    }

    private void ApplyVolumesForPair(
        AudioSessionCache sessionCache,
        string s1,
        string s2,
        string c1,
        string c2,
        bool updateAudioSessions,
        bool forceShowPopup)
    {
        var state = GetOrCreatePairState(s1, s2, _hudIsSingleSource);

        if (updateAudioSessions)
        {
            lock (_stateLock)
            {
                ApplyPairAudioOnly(sessionCache, s1, s2, state, _hudModeState);
            }
        }

        var (visualApp1Vol, visualApp2Vol) = ControlModeStateMachine.EvaluateVolumes(_hudModeState, state.VisualPosition);
        string effectiveS2 = _hudIsSingleSource ? "" : s2;

        foreach (var ov in _overlays.ToList())
        {
            ov.UpdateDynamicOverlay(
                GetResolvedLabel(s1),
                GetResolvedLabel(effectiveS2),
                s1,
                effectiveS2,
                c1,
                c2,
                visualApp1Vol,
                visualApp2Vol,
                state.MuteState,
                forceShowPopup,
                _hudIsStandalone);
        }
    }
}