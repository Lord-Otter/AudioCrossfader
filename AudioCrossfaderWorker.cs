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

    private AppSettings _settings;
    private readonly Action<AppSettings>? _onRuntimeSettingsMutated;

    private float _targetPosition = 0.5f;
    private float _visualPosition = 0.5f;
    private int _muteState = 0;
    private int _detentClicks = 0;
    private int _detentDirection = 0;
    private int _cfCycleIndex = -1;

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
    }

    private static string NormalizeSourceIdentifier(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        string trimmed = raw.Trim();
        return trimmed.StartsWith("Group: ", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : AppSettings.NormalizeExeName(trimmed);
    }

    private void SyncCycleIndices()
    {
        string normS1 = NormalizeSourceIdentifier(_settings.Source1);
        string normS2 = NormalizeSourceIdentifier(_settings.Source2);

        _cfCycleIndex = _settings.CrossfadeCycleList.FindIndex(c =>
            !string.IsNullOrWhiteSpace(c.Source1) && !string.IsNullOrWhiteSpace(c.Source2) &&
            NormalizeSourceIdentifier(c.Source1).Equals(normS1, StringComparison.OrdinalIgnoreCase) &&
            NormalizeSourceIdentifier(c.Source2).Equals(normS2, StringComparison.OrdinalIgnoreCase) &&
            c.Color1Hex.Equals(_settings.Source1Color, StringComparison.OrdinalIgnoreCase) &&
            c.Color2Hex.Equals(_settings.Source2Color, StringComparison.OrdinalIgnoreCase));

        if (_cfCycleIndex < 0)
        {
            _cfCycleIndex = _settings.CrossfadeCycleList.FindIndex(c =>
                !string.IsNullOrWhiteSpace(c.Source1) && !string.IsNullOrWhiteSpace(c.Source2) &&
                NormalizeSourceIdentifier(c.Source1).Equals(normS1, StringComparison.OrdinalIgnoreCase) &&
                NormalizeSourceIdentifier(c.Source2).Equals(normS2, StringComparison.OrdinalIgnoreCase));
        }

        if (_cfCycleIndex >= 0)
        {
            _settings.Source1Color = _settings.CrossfadeCycleList[_cfCycleIndex].Color1Hex;
            _settings.Source2Color = _settings.CrossfadeCycleList[_cfCycleIndex].Color2Hex;
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
            _muteState = 0;

            if (resetVolumes)
            {
                _targetPosition = 0.5f;
                _visualPosition = 0.5f;
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
            _targetPosition = 0.5f;
            _visualPosition = 0.5f;
            _muteState = 0;
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

        foreach (var pair in _settings.CrossfadeCycleList)
        {
            foreach (var exe in _settings.ResolveExecutables(pair.Source1))
                sessionCache.SetVolumeForProcessKey(AppSettings.NormalizeProcessKey(exe), 1.0f);
            foreach (var exe in _settings.ResolveExecutables(pair.Source2))
                sessionCache.SetVolumeForProcessKey(AppSettings.NormalizeProcessKey(exe), 1.0f);
        }
    }

    private (List<CycleDisplayItem> items, int activeIndex) PerformCycleStep(AudioSessionCache sessionCache, int direction)
    {
        List<CycleDisplayItem> displayItems = new List<CycleDisplayItem>();
        int newActiveIdx = 0;

        lock (_stateLock)
        {
            ResetAllConfiguredExecutables(sessionCache);

            var validPairs = _settings.CrossfadeCycleList
                .Where(p => !string.IsNullOrWhiteSpace(p.Source1) && !string.IsNullOrWhiteSpace(p.Source2))
                .Select(p => new CrossfadeCyclePair
                {
                    Source1 = NormalizeSourceIdentifier(p.Source1),
                    Color1Hex = p.Color1Hex,
                    Source2 = NormalizeSourceIdentifier(p.Source2),
                    Color2Hex = p.Color2Hex
                })
                .ToList();

            if (validPairs.Count > 0)
            {
                string normS1 = NormalizeSourceIdentifier(_settings.Source1);
                string normS2 = NormalizeSourceIdentifier(_settings.Source2);

                int currentIdx = _cfCycleIndex;

                if (currentIdx < 0 || currentIdx >= validPairs.Count ||
                    !validPairs[currentIdx].Source1.Equals(normS1, StringComparison.OrdinalIgnoreCase) ||
                    !validPairs[currentIdx].Source2.Equals(normS2, StringComparison.OrdinalIgnoreCase))
                {
                    currentIdx = validPairs.FindIndex(p =>
                        p.Source1.Equals(normS1, StringComparison.OrdinalIgnoreCase) &&
                        p.Source2.Equals(normS2, StringComparison.OrdinalIgnoreCase));
                }

                if (currentIdx < 0)
                {
                    newActiveIdx = direction >= 0 ? 0 : validPairs.Count - 1;
                }
                else
                {
                    newActiveIdx = ((currentIdx + direction) % validPairs.Count + validPairs.Count) % validPairs.Count;
                }

                var nextPair = validPairs[newActiveIdx];
                _settings.Source1 = nextPair.Source1;
                _settings.Source1Color = nextPair.Color1Hex;
                _settings.Source2 = nextPair.Source2;
                _settings.Source2Color = nextPair.Color2Hex;
                _cfCycleIndex = newActiveIdx;
                _muteState = 0;

                if (_pendingPreviewSettings != null)
                {
                    _pendingPreviewSettings.Source1 = nextPair.Source1;
                    _pendingPreviewSettings.Source1Color = nextPair.Color1Hex;
                    _pendingPreviewSettings.Source2 = nextPair.Source2;
                    _pendingPreviewSettings.Source2Color = nextPair.Color2Hex;
                }

                foreach (var p in validPairs)
                {
                    displayItems.Add(new CycleDisplayItem(
                        GetResolvedLabel(p.Source1),
                        p.Source1,
                        p.Color1Hex,
                        GetResolvedLabel(p.Source2),
                        p.Source2,
                        p.Color2Hex));
                }

                _onRuntimeSettingsMutated?.Invoke(_settings.Clone());
            }
            else
            {
                (_settings.Source1, _settings.Source2) = (_settings.Source2, _settings.Source1);
                (_settings.Source1Color, _settings.Source2Color) = (_settings.Source2Color, _settings.Source1Color);
                _muteState = 0;

                if (_pendingPreviewSettings != null)
                {
                    _pendingPreviewSettings.Source1 = _settings.Source1;
                    _pendingPreviewSettings.Source1Color = _settings.Source1Color;
                    _pendingPreviewSettings.Source2 = _settings.Source2;
                    _pendingPreviewSettings.Source2Color = _settings.Source2Color;
                }

                displayItems.Add(new CycleDisplayItem(
                    GetResolvedLabel(_settings.Source1),
                    _settings.Source1,
                    _settings.Source1Color,
                    GetResolvedLabel(_settings.Source2),
                    _settings.Source2,
                    _settings.Source2Color));

                displayItems.Add(new CycleDisplayItem(
                    GetResolvedLabel(_settings.Source2),
                    _settings.Source2,
                    _settings.Source2Color,
                    GetResolvedLabel(_settings.Source1),
                    _settings.Source1,
                    _settings.Source1Color));

                newActiveIdx = 0;
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
        string initLabel2 = GetResolvedLabel(initialOverlaySettings.Source2);

        Thread uiThread = new Thread(() =>
        {
            RebuildOverlayForms(initLabel1, initLabel2, initialOverlaySettings, openCarouselOnInit: true);
            Application.Run();
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.IsBackground = true;
        uiThread.Start();

        Thread.Sleep(150);
        ApplyVolumes(sessionCache, true, true);

        DateTime lastAudioApplyTime = DateTime.MinValue;
        DateTime lastVisualPushTime = DateTime.MinValue;
        bool audioUpdatePending = false;

        KeyBindingState upTracker = default;
        KeyBindingState downTracker = default;
        KeyBindingState muteTracker = default;
        KeyBindingState nextTracker = default;
        KeyBindingState prevTracker = default;

        Dictionary<int, short> keySnapshot = new Dictionary<int, short>(8);

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
                ApplyVolumes(sessionCache, true, true);
            }

            if (_previewRequestedFlag)
            {
                _previewRequestedFlag = false;
                AppSettings? previewCfg = null;
                lock (_stateLock)
                {
                    previewCfg = _pendingPreviewSettings?.Clone();
                }

                if (previewCfg != null)
                {
                    string l1 = GetResolvedLabel(previewCfg.Source1);
                    string l2 = GetResolvedLabel(previewCfg.Source2);
                    foreach (var ov in _overlays.ToList())
                    {
                        if (!ov.IsDisposed && ov.IsHandleCreated)
                        {
                            ov.Invoke(new Action(() => RebuildOverlayForms(l1, l2, previewCfg, openCarouselOnInit: false)));
                            break;
                        }
                    }
                }

                bool pinned = _settingsWindowOpen && (previewCfg?.KeepOverlayOnWhileSettingsOpen ?? _settings.KeepOverlayOnWhileSettingsOpen);
                foreach (var ov in _overlays.ToList())
                {
                    ov.ApplyLivePreviewSettings(previewCfg ?? _settings, pinned, forcePopup: true);
                }
                ApplyVolumes(sessionCache, false, true);
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
            bool isAnimating = false;

            var now = DateTime.UtcNow;
            bool frameTickReady = (now - lastVisualPushTime).TotalMilliseconds >= 15;

            int activeKeyUp = _settings.CrossfadeKeyUp;
            int activeKeyDown = _settings.CrossfadeKeyDown;
            int activeKeyMute = _settings.CrossfadeKeyMute;

            int upVk = KeybindHelper.GetPrimaryVk(activeKeyUp);
            int downVk = KeybindHelper.GetPrimaryVk(activeKeyDown);
            int muteVk = KeybindHelper.GetPrimaryVk(activeKeyMute);

            bool upModsOk = ModifiersMatch(activeKeyUp, ctrlDown, altDown, shiftDown);
            bool downModsOk = ModifiersMatch(activeKeyDown, ctrlDown, altDown, shiftDown);
            bool muteModsOk = ModifiersMatch(activeKeyMute, ctrlDown, altDown, shiftDown);

            int upSteps = upTracker.PollSteps(SampleKeyOnce(upVk), upModsOk, allowHoldRepeat: true);
            int downSteps = downTracker.PollSteps(SampleKeyOnce(downVk), downModsOk, allowHoldRepeat: true);
            int muteSteps = muteTracker.PollSteps(SampleKeyOnce(muteVk), muteModsOk, allowHoldRepeat: false);

            if (muteSteps > 0)
            {
                _muteState = _muteState switch
                {
                    1 => 2,
                    2 => 0,
                    _ => 1
                };
                physicalInputRegistered = true;
                immediateAudioUpdate = true;
            }

            if (upSteps > 0)
            {
                StepCrossfade(stepIncrement * upSteps);
                physicalInputRegistered = true;
            }
            if (downSteps > 0)
            {
                StepCrossfade(-stepIncrement * downSteps);
                physicalInputRegistered = true;
            }

            if (_settings.EnableInterpolation)
            {
                float diff = _targetPosition - _visualPosition;
                if (Math.Abs(diff) > 0.001f)
                {
                    if (frameTickReady) _visualPosition += diff * lerpFactor;
                    isAnimating = true;
                }
                else
                {
                    _visualPosition = _targetPosition;
                }
            }
            else
            {
                _visualPosition = _targetPosition;
            }

            if (_visualPosition <= 0.002f) _visualPosition = 0.0f;
            if (_visualPosition >= 0.998f) _visualPosition = 1.0f;

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
                ApplyVolumes(sessionCache, shouldUpdateAudio, physicalInputRegistered);
            }

            Thread.Sleep(2);
        }

        ResetAllConfiguredExecutables(sessionCache);
    }

    private void StepCrossfade(float delta)
    {
        float stepIncrement = (Math.Clamp(_settings.StepSize, 1, 100) / 2.0f) / 100.0f;
        float snapEpsilon = Math.Min(0.015f, stepIncrement * 0.45f);

        float SnapEndpoints(float val)
        {
            if (val <= snapEpsilon) return 0.0f;
            if (val >= 1.0f - snapEpsilon) return 1.0f;
            return Math.Clamp(val, 0.0f, 1.0f);
        }

        if (!_settings.EnableEqualizationBuffer)
        {
            _targetPosition = SnapEndpoints(_targetPosition + delta);
            return;
        }

        if (Math.Abs(_targetPosition - 0.5f) < 0.001f)
        {
            bool isFromRest = _overlays.All(o => o.IsFadedOut);
            int currentDeltaSign = Math.Sign(delta);

            if (isFromRest || (currentDeltaSign != 0 && currentDeltaSign != _detentDirection))
            {
                _targetPosition = SnapEndpoints(_targetPosition + delta);
                _detentClicks = 0;
                _detentDirection = currentDeltaSign;
            }
            else
            {
                _detentClicks++;
                if (_detentClicks >= 3)
                {
                    _detentClicks = 0;
                    _targetPosition = SnapEndpoints(_targetPosition + delta);
                    _detentDirection = currentDeltaSign;
                }
            }
        }
        else
        {
            float newTarget = _targetPosition + delta;
            bool crossingCenter = (_targetPosition < 0.5f && newTarget >= 0.5f - snapEpsilon) ||
                                  (_targetPosition > 0.5f && newTarget <= 0.5f + snapEpsilon);
            if (crossingCenter)
            {
                _targetPosition = 0.5f;
                _detentClicks = 0;
                _detentDirection = Math.Sign(delta);
            }
            else
            {
                _targetPosition = SnapEndpoints(newTarget);
            }
        }
    }

    private void ApplyVolumes(AudioSessionCache sessionCache, bool updateAudioSessions, bool forceShowPopup)
    {
        AppSettings effectiveSettings;
        lock (_stateLock)
        {
            effectiveSettings = (_settingsWindowOpen && _pendingPreviewSettings != null)
                ? _pendingPreviewSettings
                : _settings;
        }

        float audioApp1Vol = (_targetPosition >= 0.5f) ? 1.0f : (_targetPosition * 2.0f);
        float audioApp2Vol = (_targetPosition >= 0.5f) ? (1.0f - ((_targetPosition - 0.5f) * 2.0f)) : 1.0f;

        audioApp1Vol = Math.Clamp(audioApp1Vol, 0.0f, 1.0f);
        audioApp2Vol = Math.Clamp(audioApp2Vol, 0.0f, 1.0f);

        if (audioApp1Vol <= 0.01f) audioApp1Vol = 0.0f;
        if (audioApp1Vol >= 0.99f) audioApp1Vol = 1.0f;
        if (audioApp2Vol <= 0.01f) audioApp2Vol = 0.0f;
        if (audioApp2Vol >= 0.99f) audioApp2Vol = 1.0f;

        float actualApp1Vol = (_muteState == 2) ? 0.0f : audioApp1Vol;
        float actualApp2Vol = (_muteState == 1) ? 0.0f : audioApp2Vol;

        string s1;
        string s2;
        string c1;
        string c2;

        lock (_stateLock)
        {
            s1 = effectiveSettings.Source1;
            s2 = effectiveSettings.Source2;
            c1 = effectiveSettings.Source1Color;
            c2 = effectiveSettings.Source2Color;

            if (updateAudioSessions)
            {
                var source1Targets = _settings.ResolveExecutables(_settings.Source1);
                var source1Keys = new HashSet<string>(source1Targets.Select(AppSettings.NormalizeProcessKey), StringComparer.OrdinalIgnoreCase);

                foreach (var exe in source1Targets)
                {
                    sessionCache.SetVolumeForProcessKey(AppSettings.NormalizeProcessKey(exe), actualApp1Vol);
                }

                var source2Targets = _settings.ResolveExecutables(_settings.Source2);
                foreach (var exe in source2Targets)
                {
                    string key = AppSettings.NormalizeProcessKey(exe);
                    if (!source1Keys.Contains(key))
                    {
                        sessionCache.SetVolumeForProcessKey(key, actualApp2Vol);
                    }
                }
            }
        }

        float visualApp1Vol = (_visualPosition >= 0.5f) ? 1.0f : (_visualPosition * 2.0f);
        float visualApp2Vol = (_visualPosition >= 0.5f) ? (1.0f - ((_visualPosition - 0.5f) * 2.0f)) : 1.0f;

        visualApp1Vol = Math.Clamp(visualApp1Vol, 0.0f, 1.0f);
        visualApp2Vol = Math.Clamp(visualApp2Vol, 0.0f, 1.0f);

        foreach (var ov in _overlays.ToList())
        {
            ov.UpdateDynamicOverlay(
                GetResolvedLabel(s1),
                GetResolvedLabel(s2),
                s1,
                s2,
                c1,
                c2,
                visualApp1Vol,
                visualApp2Vol,
                _muteState,
                forceShowPopup);
        }
    }
}