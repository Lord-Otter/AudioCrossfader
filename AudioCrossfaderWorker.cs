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

    private sealed class PairKeyTrackers
    {
        public KeyBindingState UpTracker;
        public KeyBindingState DownTracker;
        public KeyBindingState MuteTracker;
    }

    private sealed class PairRuntimeState
    {
        public float TargetPosition = 0.5f;
        public float VisualPosition = 0.5f;
        // 0 = Unmuted, 1 = Source2 (Right) Muted, 2 = Source1 (Left) Muted, 3 = Both Muted
        public int MuteState = 0;
        public int DetentClicks = 0;
        public int DetentDirection = 0;
    }

    private AppSettings _settings;
    private readonly Action<AppSettings>? _onRuntimeSettingsMutated;

    private readonly Dictionary<string, PairRuntimeState> _pairStates = new(StringComparer.OrdinalIgnoreCase);
    private int _cfCycleIndex = -1;

    // Tracks which pair is currently shown on the HUD overlay (can temporarily be a Standalone pair)
    // Tracks which pair is currently shown on the HUD overlay (can temporarily be a Standalone pair)
    private string _hudSource1 = "";
    private string _hudSource1Color = "#1B3B5A";
    private string _hudSource2 = "";
    private string _hudSource2Color = "#4A2040";
    private bool _hudIsStandalone = false;
    private bool _hudIsSingleSource = false;

    private readonly List<CrossfadeOverlayForm> _overlays = new();
    private Thread? _workerThread;
    private CancellationTokenSource? _cts;
    private readonly object _stateLock = new();
    private volatile bool _forceResetAudioFlag = false;
    private volatile bool _settingsWindowOpen = false;
    private volatile bool _previewRequestedFlag = false;
    private AppSettings? _pendingPreviewSettings = null;

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
        _hudIsSingleSource = string.Equals(_settings.ActiveControlMode, "Standard Volume", StringComparison.OrdinalIgnoreCase);
    }

    private void SyncCycleIndices()
    {
        string normS1 = NormalizeSourceIdentifier(_settings.Source1);
        string normS2 = NormalizeSourceIdentifier(_settings.Source2);

        _cfCycleIndex = _settings.CrossfadeCycleList.FindIndex(c =>
            !string.IsNullOrWhiteSpace(c.Source1) && (c.IsSingleMode || !string.IsNullOrWhiteSpace(c.Source2)) &&
            NormalizeSourceIdentifier(c.Source1).Equals(normS1, StringComparison.OrdinalIgnoreCase) &&
            (c.IsSingleMode || NormalizeSourceIdentifier(c.Source2).Equals(normS2, StringComparison.OrdinalIgnoreCase)));

        if (_cfCycleIndex < 0 && _settings.CrossfadeCycleList.Count > 0)
        {
            var firstValid = _settings.CrossfadeCycleList.FindIndex(c =>
                !string.IsNullOrWhiteSpace(c.Source1) && (c.IsSingleMode || !string.IsNullOrWhiteSpace(c.Source2)));
            if (firstValid >= 0)
            {
                _cfCycleIndex = firstValid;
                var p = _settings.CrossfadeCycleList[firstValid];
                _settings.Source1 = NormalizeSourceIdentifier(p.Source1);
                _settings.Source1Color = p.Color1Hex;
                _settings.Source2 = p.IsSingleMode ? "" : NormalizeSourceIdentifier(p.Source2);
                _settings.Source2Color = p.Color2Hex;
                _settings.ActiveControlMode = p.ControlMode;
            }
        }
        else if (_cfCycleIndex >= 0)
        {
            var p = _settings.CrossfadeCycleList[_cfCycleIndex];
            _settings.Source1Color = p.Color1Hex;
            _settings.Source2Color = p.Color2Hex;
            _settings.ActiveControlMode = p.ControlMode;
        }
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
        if (liveDraft != null)
        {
            lock (_stateLock)
            {
                _pendingPreviewSettings = liveDraft.Clone();
            }
        }
        _previewRequestedFlag = true;
    }

    public void PreviewOverlay(AppSettings draftSettings)
    {
        lock (_stateLock)
        {
            _pendingPreviewSettings = draftSettings.Clone();
        }
        _previewRequestedFlag = true;
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
        _hudSource2 = pair.IsSingleMode ? "" : NormalizeSourceIdentifier(pair.Source2);
        _hudSource2Color = pair.Color2Hex;
        _hudIsStandalone = true;
        _hudIsSingleSource = pair.IsSingleMode;
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

        KeyBindingState globalUpTracker = default;
        KeyBindingState globalDownTracker = default;
        KeyBindingState globalMuteTracker = default;
        KeyBindingState nextTracker = default;
        KeyBindingState prevTracker = default;

        List<PairKeyTrackers> customPairTrackers = new();
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
                AppSettings? previewCfg = null;
                lock (_stateLock)
                {
                    previewCfg = _pendingPreviewSettings?.Clone();
                }

                var cfgToShow = previewCfg ?? _settings;
                var firstCarousel = cfgToShow.CrossfadeCycleList.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Source1) && (p.IsSingleMode || !string.IsNullOrWhiteSpace(p.Source2)));
                if (firstCarousel != null)
                {
                    cfgToShow.Source1 = NormalizeSourceIdentifier(firstCarousel.Source1);
                    cfgToShow.Source1Color = firstCarousel.Color1Hex;
                    cfgToShow.Source2 = firstCarousel.IsSingleMode ? "" : NormalizeSourceIdentifier(firstCarousel.Source2);
                    cfgToShow.Source2Color = firstCarousel.Color2Hex;
                    cfgToShow.ActiveControlMode = firstCarousel.ControlMode;
                }

                if (previewCfg != null)
                {
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

                bool pinned = _settingsWindowOpen && cfgToShow.KeepOverlayOnWhileSettingsOpen;
                foreach (var ov in _overlays.ToList())
                {
                    ov.ApplyLivePreviewSettings(cfgToShow, pinned, forcePopup: true);
                }

                lock (_stateLock)
                {
                    _hudSource1 = cfgToShow.Source1;
                    _hudSource1Color = cfgToShow.Source1Color;
                    _hudSource2 = cfgToShow.Source2;
                    _hudSource2Color = cfgToShow.Source2Color;
                    _hudIsStandalone = false;
                    _hudIsSingleSource = string.Equals(cfgToShow.ActiveControlMode, "Standard Volume", StringComparison.OrdinalIgnoreCase);
                }
                ApplyVolumesForPair(sessionCache, _hudSource1, _hudSource2, _hudSource1Color, _hudSource2Color, false, true);
            }

            bool physicalInputRegistered = false;
            bool immediateAudioUpdate = false;

            keySnapshot.Clear();
            bool ctrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            bool altDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            bool shiftDown = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

            int nextBinding = _settings.GetEffectiveCycleNextKey();
            int nextVk = KeybindHelper.GetPrimaryVk(nextBinding);
            bool nextModsOk = ModifiersMatch(nextBinding, ctrlDown, altDown, shiftDown);
            if (nextTracker.PollSteps(SampleKeyOnce(nextVk), nextModsOk, allowHoldRepeat: false) > 0)
            {
                var (cycleItems, activeIdx) = PerformCycleStep(sessionCache, +1);
                foreach (var ov in _overlays.ToList())
                {
                    ov.TriggerCycleWheel(cycleItems, activeIdx, +1);
                }
                physicalInputRegistered = true;
                immediateAudioUpdate = true;
            }

            int prevBinding = _settings.CyclePrevKey;
            int prevVk = KeybindHelper.GetPrimaryVk(prevBinding);
            bool prevModsOk = ModifiersMatch(prevBinding, ctrlDown, altDown, shiftDown);
            if (prevTracker.PollSteps(SampleKeyOnce(prevVk), prevModsOk, allowHoldRepeat: false) > 0)
            {
                var (cycleItems, activeIdx) = PerformCycleStep(sessionCache, -1);
                foreach (var ov in _overlays.ToList())
                {
                    ov.TriggerCycleWheel(cycleItems, activeIdx, -1);
                }
                physicalInputRegistered = true;
                immediateAudioUpdate = true;
            }

            float stepIncrement = (Math.Clamp(_settings.StepSize, 1, 100) / 2.0f) / 100.0f;
            float lerpFactor = Math.Clamp(_settings.InterpolationSpeed, 5, 100) / 100.0f;

            var now = DateTime.UtcNow;
            bool frameTickReady = (now - lastVisualPushTime).TotalMilliseconds >= 15;

            // 1. Poll Global Hotkeys -> ALWAYS target the active Carousel Pair and snap HUD back to it
            int activeKeyUp = _settings.CrossfadeKeyUp;
            int activeKeyDown = _settings.CrossfadeKeyDown;
            int activeKeyMute = _settings.CrossfadeKeyMute;

            int upVk = KeybindHelper.GetPrimaryVk(activeKeyUp);
            int downVk = KeybindHelper.GetPrimaryVk(activeKeyDown);
            int muteVk = KeybindHelper.GetPrimaryVk(activeKeyMute);

            bool upModsOk = ModifiersMatch(activeKeyUp, ctrlDown, altDown, shiftDown);
            bool downModsOk = ModifiersMatch(activeKeyDown, ctrlDown, altDown, shiftDown);
            bool muteModsOk = ModifiersMatch(activeKeyMute, ctrlDown, altDown, shiftDown);

            int upSteps = globalUpTracker.PollSteps(SampleKeyOnce(upVk), upModsOk, allowHoldRepeat: true);
            int downSteps = globalDownTracker.PollSteps(SampleKeyOnce(downVk), downModsOk, allowHoldRepeat: true);
            int muteSteps = globalMuteTracker.PollSteps(SampleKeyOnce(muteVk), muteModsOk, allowHoldRepeat: false);

            if (upSteps > 0 || downSteps > 0 || muteSteps > 0)
            {
                bool invertActive = false;
                lock (_stateLock)
                {
                    SnapHudToCarouselPair();
                    if (_cfCycleIndex >= 0 && _cfCycleIndex < _settings.CrossfadeCycleList.Count)
                    {
                        invertActive = _settings.CrossfadeCycleList[_cfCycleIndex].InvertDirection;
                    }
                }

                // Default for Crossfade is -1.0f (your preferred direction); Default for Single Volume is +1.0f
                float baseSign = _hudIsSingleSource ? 1.0f : -1.0f;
                float dirMultiplier = invertActive ? -baseSign : baseSign;

                var carouselPairState = GetOrCreatePairState(_settings.Source1, _settings.Source2, _hudIsSingleSource);

                if (muteSteps > 0)
                {
                    carouselPairState.MuteState = AdvanceMuteState(carouselPairState.MuteState, GetCarouselActiveMuteMode(), _hudIsSingleSource);
                    immediateAudioUpdate = true;
                }
                if (upSteps > 0)
                {
                    StepCrossfadeState(carouselPairState, dirMultiplier * stepIncrement * upSteps, _hudIsSingleSource);
                }
                if (downSteps > 0)
                {
                    StepCrossfadeState(carouselPairState, -dirMultiplier * stepIncrement * downSteps, _hudIsSingleSource);
                }

                physicalInputRegistered = true;
            }

            // 2. Poll Standalone Pairs' Dedicated Keybinds -> Adjust that pair's audio & show it on HUD without overwriting carousel slot
            var standalonePairs = _settings.StandalonePairs;
            while (customPairTrackers.Count < standalonePairs.Count) customPairTrackers.Add(new PairKeyTrackers());

            for (int i = 0; i < standalonePairs.Count; i++)
            {
                var pair = standalonePairs[i];
                if (string.IsNullOrWhiteSpace(pair.Source1) || (!pair.IsSingleMode && string.IsNullOrWhiteSpace(pair.Source2)))
                    continue;

                var tracker = customPairTrackers[i];
                int pUpVk = KeybindHelper.GetPrimaryVk(pair.KeyUp);
                int pDownVk = KeybindHelper.GetPrimaryVk(pair.KeyDown);
                int pMuteVk = KeybindHelper.GetPrimaryVk(pair.KeyMute);

                bool pUpMods = ModifiersMatch(pair.KeyUp, ctrlDown, altDown, shiftDown);
                bool pDownMods = ModifiersMatch(pair.KeyDown, ctrlDown, altDown, shiftDown);
                bool pMuteMods = ModifiersMatch(pair.KeyMute, ctrlDown, altDown, shiftDown);

                int pUpSteps = tracker.UpTracker.PollSteps(SampleKeyOnce(pUpVk), pUpMods, allowHoldRepeat: true);
                int pDownSteps = tracker.DownTracker.PollSteps(SampleKeyOnce(pDownVk), pDownMods, allowHoldRepeat: true);
                int pMuteSteps = tracker.MuteTracker.PollSteps(SampleKeyOnce(pMuteVk), pMuteMods, allowHoldRepeat: false);

                if (pUpSteps > 0 || pDownSteps > 0 || pMuteSteps > 0)
                {
                    float baseSign = pair.IsSingleMode ? 1.0f : -1.0f;
                    float pairDirMultiplier = pair.InvertDirection ? -baseSign : baseSign;
                    var pState = GetOrCreatePairState(pair.Source1, pair.Source2, pair.IsSingleMode);

                    if (pMuteSteps > 0)
                    {
                        pState.MuteState = AdvanceMuteState(pState.MuteState, ResolveEffectiveMuteMode(pair.MuteMode), pair.IsSingleMode);
                        immediateAudioUpdate = true;
                    }
                    if (pUpSteps > 0)
                    {
                        StepCrossfadeState(pState, pairDirMultiplier * stepIncrement * pUpSteps, pair.IsSingleMode);
                    }
                    if (pDownSteps > 0)
                    {
                        StepCrossfadeState(pState, -pairDirMultiplier * stepIncrement * pDownSteps, pair.IsSingleMode);
                    }

                    lock (_stateLock)
                    {
                        ShowPairOnHudTemporarily(pair);
                    }
                    ApplyPairAudioOnly(sessionCache, pair.Source1, pair.Source2, pState, pair.IsSingleMode);
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

            Thread.Sleep(2);
        }

        ResetAllConfiguredExecutables(sessionCache);
    }

    private void StepCrossfadeState(PairRuntimeState state, float delta, bool isSingleSource = false)
    {
        float stepIncrement = (Math.Clamp(_settings.StepSize, 1, 100) / (isSingleSource ? 1.0f : 2.0f)) / 100.0f;
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
            float actualDelta = isSingleSource ? delta * 2.0f : delta;
            state.TargetPosition = SnapEndpoints(state.TargetPosition + actualDelta);
            return;
        }

        if (!_settings.EnableEqualizationBuffer)
        {
            state.TargetPosition = SnapEndpoints(state.TargetPosition + delta);
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

    private void ApplyPairAudioOnly(AudioSessionCache sessionCache, string source1, string source2, PairRuntimeState state, bool isSingleSource)
    {
        if (isSingleSource)
        {
            float vol = Math.Clamp(state.TargetPosition, 0.0f, 1.0f);
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
        
        float audioApp1Vol = (state.TargetPosition >= 0.5f) ? 1.0f : (state.TargetPosition * 2.0f);
        float audioApp2Vol = (state.TargetPosition >= 0.5f) ? (1.0f - ((state.TargetPosition - 0.5f) * 2.0f)) : 1.0f;

        audioApp1Vol = Math.Clamp(audioApp1Vol, 0.0f, 1.0f);
        audioApp2Vol = Math.Clamp(audioApp2Vol, 0.0f, 1.0f);

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
                ApplyPairAudioOnly(sessionCache, s1, s2, state, _hudIsSingleSource);
            }
        }

        float visualApp1Vol = _hudIsSingleSource
            ? Math.Clamp(state.VisualPosition, 0.0f, 1.0f)
            : Math.Clamp((state.VisualPosition >= 0.5f) ? 1.0f : (state.VisualPosition * 2.0f), 0.0f, 1.0f);

        float visualApp2Vol = _hudIsSingleSource
            ? 0.0f
            : Math.Clamp((state.VisualPosition >= 0.5f) ? (1.0f - ((state.VisualPosition - 0.5f) * 2.0f)) : 1.0f, 0.0f, 1.0f);

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