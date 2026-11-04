using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public class CrossfadeOverlayForm : Form
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hwnd,
        IntPtr hdcDst,
        ref Point pptDst,
        ref Size psize,
        IntPtr hdcSrc,
        ref Point pptSrc,
        uint crKey,
        ref BLENDFUNCTION pblend,
        uint dwFlags);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    private const byte AC_SRC_OVER = 0x00;
    private const byte AC_SRC_ALPHA = 0x01;
    private const uint ULW_ALPHA = 0x00000002;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly Color DefaultDarkGrey = Color.FromArgb(24, 24, 28);
    private static readonly Color BackdropCharcoal = Color.FromArgb(15, 15, 18);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private string _app1Label;
    private string _app2Label;
    private string _rawSource1;
    private string _rawSource2;
    private string _color1Hex;
    private string _color2Hex;
    private AppSettings _settings;
    private float _app1Vol = 0.5f;
    private float _app2Vol = 0.5f;
    private int _muteState = 0;

    private Color _renderedBgLeft = DefaultDarkGrey;
    private Color _renderedBgRight = DefaultDarkGrey;
    private Color _renderedBar1 = Color.FromArgb(0, 168, 255);
    private Color _renderedBar2 = Color.FromArgb(0, 168, 255);

    private List<CycleDisplayItem> _cycleItems = new List<CycleDisplayItem>();
    private int _cycleActiveIndex = 0;
    private int _cycleCountdownMs = 0;
    private float _wheelExpansion = 0f;
    private float _wheelScrollOffset = 0f;
    private float _neighborPreviewVisibility = 0f;

    private float _compactTransitionProgress = 0f;
    private int _compactTransitionDir = 1;
    private CycleDisplayItem _outgoingCompactItem;
    private bool _hasOutgoingCompactItem = false;

    private readonly StateMachine<CarouselStyleState> _carouselStyleMachine;
    private Action<Graphics, RectangleF, int, float>? _activeStyleRenderAction;

    private float _stationaryCardOffsetY = 0f;
    private double _masterOpacity = 0.0;

    private System.Windows.Forms.Timer? _fadeTimer;
    private int _displayCountdownMs = 0;
    private int _totalDurationMs = 2500;
    private bool _isFadingOut = false;
    private bool _pinnedBySettings = false;
    private bool _suppressCarousel = false;

    private readonly string? _targetDeviceName;
    private string _assignedPosition;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsFadedOut => _masterOpacity <= 0.001;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_LAYERED;
            return cp;
        }
    }

    private static float GetWin11CornerRadius(float scale, bool large = false)
    {
        float baseRadius = large ? 16f : 8f;
        return Math.Clamp(baseRadius * scale, 6f, 20f);
    }

    public CrossfadeOverlayForm(string app1, string app2, AppSettings settings, string? targetDeviceName = null, string? assignedPosition = null, bool openCarouselOnInit = false)
    {
        _app1Label = app1;
        _app2Label = app2;
        _rawSource1 = settings.Source1;
        _rawSource2 = settings.Source2;
        _color1Hex = settings.Source1Color;
        _color2Hex = settings.Source2Color;
        _settings = settings.Clone();
        _targetDeviceName = targetDeviceName;
        _assignedPosition = assignedPosition ?? settings.OverlayPosition;
        _carouselStyleMachine = new StateMachine<CarouselStyleState>(
            CarouselStyleStateMachine.ParseState(_settings.CarouselMotionStyle, _settings.CarouselMode));

        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.ShowInTaskbar = false;
        this.TopMost = true;

        _totalDurationMs = Math.Max(500, _settings.PopupDurationMs);
        SnapInterpolatedColorsImmediate();

        SyncCycleItemsFromSettings(_settings, out int activeIdx);
        _cycleActiveIndex = activeIdx;
        
        ConfigureCarouselStyleStateMachine();

        UpdateFixedCanvasGeometry();

        if (openCarouselOnInit && _cycleItems.Count > 1)
        {
            _cycleCountdownMs = _totalDurationMs;
            if (CarouselStyleStateMachine.IsWideCarousel(_carouselStyleMachine.CurrentState))
            {
                _wheelExpansion = 0.05f;
            }
        }

        _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _fadeTimer.Tick += OnAnimationTick;
        _fadeTimer.Start();
    }

    private void ConfigureCarouselStyleStateMachine()
    {
        // Full Carousel Styles
        _carouselStyleMachine.ConfigureState(CarouselStyleState.FloatingPills3D,
            onEnter: () => _activeStyleRenderAction = RenderFloatingPillsStyle);

        _carouselStyleMachine.ConfigureState(CarouselStyleState.FluidMovingDeck,
            onEnter: () => _activeStyleRenderAction = (g, heroRect, baseH, scale) =>
                RenderWideDeckOrder(g, heroRect, baseH, scale, DrawFluidDeckCard, drawHeroDropShadow: true));

        _carouselStyleMachine.ConfigureState(CarouselStyleState.CoverflowArc3D,
            onEnter: () => _activeStyleRenderAction = (g, heroRect, baseH, scale) =>
                RenderWideDeckOrder(g, heroRect, baseH, scale, DrawCoverflowArcCard));

        _carouselStyleMachine.ConfigureState(CarouselStyleState.StackedRolodex,
            onEnter: () => _activeStyleRenderAction = (g, heroRect, baseH, scale) =>
                RenderWideDeckOrder(g, heroRect, baseH, scale, DrawStackedRolodexCard));

        _carouselStyleMachine.ConfigureState(CarouselStyleState.CylinderFacets3D,
            onEnter: () => _activeStyleRenderAction = (g, heroRect, baseH, scale) =>
                RenderWideDeckOrder(g, heroRect, baseH, scale, DrawCylinderFacetCard));

        // Compact Carousel Styles (Top & Bottom Previews + Vertical Transitions)
        _carouselStyleMachine.ConfigureState(CarouselStyleState.CompactTopBottomMorph,
            onEnter: () => _activeStyleRenderAction = DrawCompactTopBottomMorphHero);

        _carouselStyleMachine.ConfigureState(CarouselStyleState.CompactCardFlip,
            onEnter: () => _activeStyleRenderAction = DrawCompactCardFlipHero);

        _carouselStyleMachine.ConfigureState(CarouselStyleState.CompactVerticalClockFlip,
            onEnter: () => _activeStyleRenderAction = DrawCompactClockFlipHero);

        _carouselStyleMachine.ConfigureState(CarouselStyleState.CompactVerticalSwoosh,
            onEnter: () => _activeStyleRenderAction = DrawCompactVerticalSwooshHero);

        _carouselStyleMachine.ConfigureState(CarouselStyleState.CompactVerticalLaserWipe,
            onEnter: () => _activeStyleRenderAction = DrawCompactVerticalLaserWipeHero);

        _carouselStyleMachine.ConfigureState(CarouselStyleState.CompactVerticalSquashStretch,
            onEnter: () => _activeStyleRenderAction = DrawCompactVerticalSquashStretchHero);

        _carouselStyleMachine.ConfigureState(CarouselStyleState.CompactVerticalInstantCut,
            onEnter: () => _activeStyleRenderAction = (g, heroRect, baseH, scale) =>
                DrawStandardHeroCard(g, heroRect, scale));

        // Minimal Carousel Styles (Left & Right Side Previews + Horizontal Transitions)
        _carouselStyleMachine.ConfigureState(CarouselStyleState.MinimalSidePreviewMorph,
            onEnter: () => _activeStyleRenderAction = DrawMinimalSidePreviewMorphHero);

        _carouselStyleMachine.ConfigureState(CarouselStyleState.MinimalCardFlip,
            onEnter: () => _activeStyleRenderAction = DrawMinimalCardFlipHero);

        _carouselStyleMachine.ConfigureState(CarouselStyleState.MinimalDirectionalSwoosh,
            onEnter: () => _activeStyleRenderAction = DrawCompactSwooshHero);

        _carouselStyleMachine.ConfigureState(CarouselStyleState.MinimalLaserWipe,
            onEnter: () => _activeStyleRenderAction = DrawCompactLaserWipeHero);

        _carouselStyleMachine.ConfigureState(CarouselStyleState.MinimalSquashAndStretch,
            onEnter: () => _activeStyleRenderAction = DrawCompactSquashStretchHero);

        _carouselStyleMachine.ConfigureState(CarouselStyleState.MinimalInstantCut,
            onEnter: () => _activeStyleRenderAction = (g, heroRect, baseH, scale) =>
                DrawStandardHeroCard(g, heroRect, scale));

        _carouselStyleMachine.TransitionTo(_carouselStyleMachine.CurrentState, force: true);
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (_pinnedBySettings)
        {
            _isFadingOut = false;
            _displayCountdownMs = _totalDurationMs;
        }

        if (_cycleCountdownMs > 0)
        {
            _cycleCountdownMs -= 16;
        }

        CarouselStyleState currentStyle = _carouselStyleMachine.CurrentState;
        bool isWideStyle = CarouselStyleStateMachine.IsWideCarousel(currentStyle);
        bool wideCarouselAllowed = !_suppressCarousel && isWideStyle && _cycleItems.Count > 1;
        float targetExpansion = (wideCarouselAllowed && _cycleCountdownMs > 0) ? 1.0f : 0.0f;

        string previewMode = _settings.NeighborPreviewVisibility ?? "While Switching";
        float targetPreviewVis = (_suppressCarousel || _cycleItems.Count <= 1 || previewMode == "Off")
            ? 0f
            : (previewMode == "Always" ? 1f : (_cycleCountdownMs > 0 ? 1f : 0f));

        int rawOpenSpeed = Math.Clamp(_settings.CarouselOpenSpeed, 5, 100);
        int rawScrollSpeed = Math.Clamp(_settings.CarouselAnimationSpeed, 5, 100);
        bool isInstant = CarouselStyleStateMachine.IsInstantCut(currentStyle);

        // 1. Carousel Opening / Closing Animation Speed
        if (!_settings.EnableCarouselAnimation || isInstant || rawOpenSpeed >= 100)
        {
            _wheelExpansion = targetExpansion;
            _neighborPreviewVisibility = targetPreviewVis;
        }
        else
        {
            float oSpeed = rawOpenSpeed / 100.0f;
            float openLerp = Math.Clamp(oSpeed * 0.65f, 0.04f, 0.88f);

            if (Math.Abs(targetExpansion - _wheelExpansion) > 0.003f)
                _wheelExpansion += (targetExpansion - _wheelExpansion) * openLerp;
            else
                _wheelExpansion = targetExpansion;

            if (Math.Abs(targetPreviewVis - _neighborPreviewVisibility) > 0.004f)
                _neighborPreviewVisibility += (targetPreviewVis - _neighborPreviewVisibility) * openLerp;
            else
                _neighborPreviewVisibility = targetPreviewVis;
        }

        // 2. Carousel Scrolling / Switching Speed
        if (!_settings.EnableCarouselAnimation || isInstant || rawScrollSpeed >= 100)
        {
            _wheelScrollOffset = 0f;
            _compactTransitionProgress = 0f;
        }
        else
        {
            float sSpeed = rawScrollSpeed / 100.0f;
            float scrollLerp = Math.Clamp(sSpeed * 0.65f, 0.04f, 0.88f);

            if (Math.Abs(_wheelScrollOffset) > 0.004f)
                _wheelScrollOffset *= (1.0f - Math.Clamp(scrollLerp * 0.82f, 0.05f, 0.85f));
            else
                _wheelScrollOffset = 0f;

            if (_compactTransitionProgress > 0.002f)
            {
                float step = Math.Clamp(0.015f + sSpeed * 0.165f, 0.022f, 0.18f);
                _compactTransitionProgress = Math.Max(0f, _compactTransitionProgress - step);
            }
            else
            {
                _compactTransitionProgress = 0f;
            }
        }

        StepColorInterpolations(1.0f);

        if (!_isFadingOut)
        {
            EnsureTopMost();

            if (_masterOpacity < 1.0)
            {
                _masterOpacity = Math.Min(1.0, _masterOpacity + 0.22);
            }
            if (!_pinnedBySettings && _displayCountdownMs > 0)
            {
                _displayCountdownMs -= 16;
                if (_displayCountdownMs <= 0) _isFadingOut = true;
            }
        }
        else
        {
            if (_masterOpacity > 0.0)
            {
                _masterOpacity = Math.Max(0.0, _masterOpacity - 0.12);
            }
            else
            {
                _masterOpacity = 0.0;
                _wheelExpansion = 0f;
                _compactTransitionProgress = 0f;
                _cycleCountdownMs = 0;
                RenderLayeredOverlay();
                _fadeTimer?.Stop();
                return;
            }
        }

        RenderLayeredOverlay();
    }

    private static Color ColorLerp(Color from, Color to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        int a = (int)Math.Round(from.A + (to.A - from.A) * t);
        int r = (int)Math.Round(from.R + (to.R - from.R) * t);
        int g = (int)Math.Round(from.G + (to.G - from.G) * t);
        int b = (int)Math.Round(from.B + (to.B - from.B) * t);
        return Color.FromArgb(Math.Clamp(a, 0, 255), Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));
    }

    private static Color EnsureReadableBackdrop(Color c)
    {
        float lum = (0.299f * c.R + 0.587f * c.G + 0.114f * c.B) / 255f;
        const float maxAllowedLum = 0.52f;
        if (lum > maxAllowedLum && lum > 0.001f)
        {
            float factor = maxAllowedLum / lum;
            return Color.FromArgb(
                c.A,
                Math.Clamp((int)Math.Round(c.R * factor), 0, 255),
                Math.Clamp((int)Math.Round(c.G * factor), 0, 255),
                Math.Clamp((int)Math.Round(c.B * factor), 0, 255));
        }
        return c;
    }

    private static void DrawReadableString(
        Graphics g,
        string text,
        Font font,
        Color textColor,
        RectangleF layoutRect,
        StringFormat? sf = null)
    {
        if (string.IsNullOrEmpty(text) || textColor.A <= 4) return;

        int shadowAlpha = (int)Math.Clamp(textColor.A * 0.65f, 0, 180);
        using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha, 0, 0, 0));
        RectangleF shadowRect = new RectangleF(layoutRect.X + 1f, layoutRect.Y + 1.2f, layoutRect.Width, layoutRect.Height);

        if (sf != null) g.DrawString(text, font, shadowBrush, shadowRect, sf);
        else g.DrawString(text, font, shadowBrush, shadowRect.Location);

        using SolidBrush fgBrush = new SolidBrush(textColor);
        if (sf != null) g.DrawString(text, font, fgBrush, layoutRect, sf);
        else g.DrawString(text, font, fgBrush, layoutRect.Location);
    }

    private (Color targetLeft, Color targetRight) ComputeItemHeroColors(string c1Hex, string c2Hex, bool isSingleSource)
    {
        if (!_settings.UseSourceBackgroundColors)
        {
            return (DefaultDarkGrey, DefaultDarkGrey);
        }

        bool app1Muted = (_muteState == 2 || _muteState == 3);
        bool app2Muted = (_muteState == 1 || _muteState == 3);

        Color rawC1 = ParseOverlayColor(c1Hex, Color.FromArgb(27, 59, 90));
        float effVol1 = app1Muted ? 0f : Math.Clamp(_app1Vol, 0f, 1f);
        Color effectiveC1 = EnsureReadableBackdrop(_settings.FadeBgColorWithVolume
            ? ColorLerp(DefaultDarkGrey, rawC1, effVol1)
            : rawC1);

        if (isSingleSource)
        {
            return (effectiveC1, effectiveC1);
        }

        Color rawC2 = ParseOverlayColor(c2Hex, Color.FromArgb(74, 32, 64));
        float effVol2 = app2Muted ? 0f : Math.Clamp(_app2Vol, 0f, 1f);
        Color effectiveC2 = EnsureReadableBackdrop(_settings.FadeBgColorWithVolume
            ? ColorLerp(DefaultDarkGrey, rawC2, effVol2)
            : rawC2);

        return (effectiveC1, effectiveC2);
    }

    private (Color targetLeft, Color targetRight, Color targetBar1, Color targetBar2) ComputeTargetColors()
    {
        Color unmutedColor = ParseOverlayColor(_settings.UnmutedColor, Color.FromArgb(0, 168, 255));
        Color mutedColor = ParseOverlayColor(_settings.MutedColor, Color.FromArgb(255, 71, 87));

        bool app1Muted = (_muteState == 2 || _muteState == 3);
        bool app2Muted = (_muteState == 1 || _muteState == 3);

        Color bar1 = app1Muted ? mutedColor : unmutedColor;
        Color bar2 = app2Muted ? mutedColor : unmutedColor;

        var (leftBg, rightBg) = ComputeItemHeroColors(_color1Hex, _color2Hex, string.IsNullOrEmpty(_rawSource2));
        return (leftBg, rightBg, bar1, bar2);
    }

    private void SnapInterpolatedColorsImmediate()
    {
        var targets = ComputeTargetColors();
        _renderedBgLeft = targets.targetLeft;
        _renderedBgRight = targets.targetRight;
        _renderedBar1 = targets.targetBar1;
        _renderedBar2 = targets.targetBar2;
    }

    private void StepColorInterpolations(float factor)
    {
        var targets = ComputeTargetColors();
        float cLerp = _settings.EnableInterpolation ? Math.Clamp(factor * 0.55f, 0.12f, 1.0f) : 1.0f;

        _renderedBgLeft = ColorLerp(_renderedBgLeft, targets.targetLeft, cLerp);
        _renderedBgRight = ColorLerp(_renderedBgRight, targets.targetRight, cLerp);
        _renderedBar1 = ColorLerp(_renderedBar1, targets.targetBar1, cLerp);
        _renderedBar2 = ColorLerp(_renderedBar2, targets.targetBar2, cLerp);
    }

    private void SyncCycleItemsFromSettings(AppSettings cfg, out int matchedIndex)
    {
        var list = new List<CycleDisplayItem>();
        matchedIndex = 0;

        string normS1 = AppSettings.NormalizeExeName(cfg.Source1);
        string normS2 = AppSettings.NormalizeExeName(cfg.Source2);

        var validPairs = cfg.CrossfadeCycleList
            .Where(p => !string.IsNullOrWhiteSpace(p.Source1) && (p.IsSingleMode || !string.IsNullOrWhiteSpace(p.Source2)))
            .ToList();

        for (int i = 0; i < validPairs.Count; i++)
        {
            var p = validPairs[i];
            list.Add(new CycleDisplayItem(
                AudioCrossfaderWorker.GetResolvedLabel(p.Source1),
                p.Source1,
                p.Color1Hex,
                p.IsSingleMode ? "" : AudioCrossfaderWorker.GetResolvedLabel(p.Source2),
                p.IsSingleMode ? "" : p.Source2,
                p.Color2Hex,
                p.IsSingleMode));

            if (AppSettings.NormalizeExeName(p.Source1).Equals(normS1, StringComparison.OrdinalIgnoreCase) &&
                AppSettings.NormalizeExeName(p.Source2).Equals(normS2, StringComparison.OrdinalIgnoreCase))
            {
                matchedIndex = i;
            }
        }

        if (list.Count == 1)
        {
            var p = validPairs[0];
            list.Add(new CycleDisplayItem(
                AudioCrossfaderWorker.GetResolvedLabel(p.Source2),
                p.Source2,
                p.Color2Hex,
                AudioCrossfaderWorker.GetResolvedLabel(p.Source1),
                p.Source1,
                p.Color1Hex));
        }

        _cycleItems = list;
    }

    public void ApplyLivePreviewSettings(AppSettings newSettings, bool pinnedWhileSettingsOpen, bool forcePopup)
    {
        if (this.IsDisposed || !this.IsHandleCreated) return;

        this.BeginInvoke(new Action(() =>
        {
            _settings = newSettings.Clone();
            _pinnedBySettings = pinnedWhileSettingsOpen;
            _totalDurationMs = Math.Max(500, _settings.PopupDurationMs);
            _color1Hex = _settings.Source1Color;
            _color2Hex = _settings.Source2Color;
            _rawSource1 = _settings.Source1;
            _rawSource2 = _settings.Source2;
            _app1Label = AudioCrossfaderWorker.GetResolvedLabel(_rawSource1);
            _app2Label = AudioCrossfaderWorker.GetResolvedLabel(_rawSource2);

            SyncCycleItemsFromSettings(_settings, out int activeIdx);
            _cycleActiveIndex = activeIdx;

            CarouselStyleState nextStyle = CarouselStyleStateMachine.ParseState(_settings.CarouselMotionStyle, _settings.CarouselMode);
            _carouselStyleMachine.TransitionTo(nextStyle, force: true);

            UpdateFixedCanvasGeometry();

            if (forcePopup || _pinnedBySettings)
            {
                _displayCountdownMs = _totalDurationMs;
                if (_cycleItems.Count > 1)
                {
                    _cycleCountdownMs = _totalDurationMs;

                    if (forcePopup && !CarouselStyleStateMachine.IsWideCarousel(nextStyle) && !CarouselStyleStateMachine.IsInstantCut(nextStyle))
                    {
                        int prevIdx = (_cycleActiveIndex - 1 + _cycleItems.Count) % _cycleItems.Count;
                        _outgoingCompactItem = _cycleItems[prevIdx];
                        _hasOutgoingCompactItem = true;
                        _compactTransitionDir = 1;
                        _compactTransitionProgress = 1.0f;
                    }
                }
                _isFadingOut = false;
                EnsureTopMost();
                if (_fadeTimer != null && !_fadeTimer.Enabled)
                {
                    _fadeTimer.Start();
                }
            }
            RenderLayeredOverlay();
        }));
    }

    public void TriggerCycleWheel(List<CycleDisplayItem> items, int activeIndex, int stepDirection)
    {
        if (this.IsDisposed || !this.IsHandleCreated) return;

        this.BeginInvoke(new Action(() =>
        {
            _suppressCarousel = false;
            var newList = items ?? new List<CycleDisplayItem>();
            int newActive = Math.Max(0, activeIndex);

            if (newList.Count > 1)
            {
                int prevIdx = ((newActive - stepDirection) % newList.Count + newList.Count) % newList.Count;
                _outgoingCompactItem = newList[prevIdx];
                _hasOutgoingCompactItem = true;
            }
            else
            {
                _hasOutgoingCompactItem = false;
            }

            if (newActive >= 0 && newActive < newList.Count)
            {
                var activeItem = newList[newActive];
                _app1Label = activeItem.Label1;
                _app2Label = activeItem.Label2;
                _rawSource1 = activeItem.RawSource1;
                _rawSource2 = activeItem.RawSource2;
                _color1Hex = activeItem.Color1Hex;
                _color2Hex = activeItem.Color2Hex;
            }

            _cycleItems = newList;
            _cycleActiveIndex = newActive;
            _cycleCountdownMs = Math.Max(1650, (int)(_totalDurationMs * 0.85f));
            _displayCountdownMs = Math.Max(_displayCountdownMs, _cycleCountdownMs + 250);
            _isFadingOut = false;

            float scrollSign = _settings.InvertCarouselScroll ? -1.0f : 1.0f;

            _wheelScrollOffset = (stepDirection >= 0 ? 1.0f : -1.0f) * scrollSign;
            _compactTransitionDir = (stepDirection >= 0 ? 1 : -1) * (_settings.InvertCarouselScroll ? -1 : 1);

            var styleState = CarouselStyleStateMachine.ParseState(_settings.CarouselMotionStyle, _settings.CarouselMode);
            if (_carouselStyleMachine.CurrentState != styleState)
            {
                _carouselStyleMachine.TransitionTo(styleState, force: true);
            }

            bool isNoneMode = string.Equals(_settings.CarouselMode, "None", StringComparison.OrdinalIgnoreCase);
            _compactTransitionProgress = (!isNoneMode && _settings.EnableCarouselAnimation && !CarouselStyleStateMachine.IsInstantCut(styleState))
                ? 1.0f
                : 0.0f;

            foreach (var item in _cycleItems)
            {
                AppIconCache.GetIconForSource(item.RawSource1, _settings);
                AppIconCache.GetIconForSource(item.RawSource2, _settings);
            }

            UpdateFixedCanvasGeometry();

            if (_fadeTimer != null && !_fadeTimer.Enabled)
            {
                _fadeTimer.Start();
            }
            EnsureTopMost();
            RenderLayeredOverlay();
        }));
    }

    public void HideCarouselImmediate()
    {
        if (this.IsDisposed || !this.IsHandleCreated) return;
        this.BeginInvoke(new Action(() =>
        {
            _cycleCountdownMs = 0;
            _wheelExpansion = 0f;
            _wheelScrollOffset = 0f;
            _compactTransitionProgress = 0f;
        }));
    }

    private void UpdateFixedCanvasGeometry()
    {
        int baseW = Math.Max(120, _settings.OverlayWidth);
        int baseH = Math.Max(50, _settings.OverlayHeight);

        int maxWingPx = (int)Math.Ceiling(baseH * 1.36f) + 18;
        int totalH = baseH + (maxWingPx * 2);
        int canvasW = baseW + 124;

        Screen targetScreen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        if (!string.IsNullOrEmpty(_targetDeviceName))
        {
            var matched = Screen.AllScreens.FirstOrDefault(s => s.DeviceName.Equals(_targetDeviceName, StringComparison.OrdinalIgnoreCase));
            if (matched != null) targetScreen = matched;
        }
        else if (_settings.FollowActiveMonitor)
        {
            try
            {
                IntPtr fgHwnd = GetForegroundWindow();
                if (fgHwnd != IntPtr.Zero) targetScreen = Screen.FromHandle(fgHwnd);
            }
            catch { }
        }

        string effectivePos = !string.IsNullOrEmpty(_assignedPosition) ? _assignedPosition : _settings.OverlayPosition;
        Rectangle bounds = targetScreen.Bounds;
        int baseX = bounds.Left + (bounds.Width - canvasW) / 2;
        int bottomSafeY = bounds.Bottom - baseH - maxWingPx + 25;
        int topSafeY = bounds.Top + maxWingPx - 25;
        int baseY = bottomSafeY;

        switch (effectivePos)
        {
            case "Top Center":
                baseX = bounds.Left + (bounds.Width - canvasW) / 2; baseY = topSafeY; break;
            case "Bottom Right":
                baseX = bounds.Right - canvasW - 20; baseY = bottomSafeY; break;
            case "Bottom Left":
                baseX = bounds.Left + 20; baseY = bottomSafeY; break;
            case "Top Right":
                baseX = bounds.Right - canvasW - 20; baseY = topSafeY; break;
            case "Top Left":
                baseX = bounds.Left + 20; baseY = topSafeY; break;
            case "Bottom Center":
            default:
                baseX = bounds.Left + (bounds.Width - canvasW) / 2; baseY = bottomSafeY; break;
        }

        int desiredWindowTop = baseY - maxWingPx;
        int clampedWindowTop = Math.Clamp(desiredWindowTop, bounds.Top + 8, bounds.Bottom - baseH);

        _stationaryCardOffsetY = maxWingPx;

        if (this.Width != canvasW || this.Height != totalH || this.Left != baseX || this.Top != clampedWindowTop)
        {
            this.Bounds = new Rectangle(baseX, clampedWindowTop, canvasW, totalH);
        }
    }

    private RectangleF GetCarouselContainerRect(int baseW, int baseH, float scale)
    {
        float pillH = Math.Clamp(baseH * 0.50f, 28f, 64f);
        float maxOrbitDist = (baseH / 2f) + (pillH / 2f) + (6f * scale);
        float fullWingExtent = (maxOrbitDist - baseH / 2f) + (pillH * 0.94f);

        bool alwaysPreviews = string.Equals(_settings.NeighborPreviewVisibility, "Always", StringComparison.OrdinalIgnoreCase);
        float minWingExtent = alwaysPreviews ? (Math.Clamp(26f * scale, 20f, 40f) * 0.62f + 4f) : 0f;
        float wingExtent = minWingExtent + (fullWingExtent - minWingExtent) * _wheelExpansion;

        float extraW = 18f;
        float containerW = baseW + extraW;
        float containerX = (this.Width - containerW) / 2f;
        float topY = _stationaryCardOffsetY - wingExtent;
        float height = baseH + (wingExtent * 2f);

        return new RectangleF(containerX, topY, containerW - 1f, height - 1f);
    }

    private (RectangleF leftBox, RectangleF rightBox) GetMinimalSidePreviewBoxRects(RectangleF heroRect, float scale, float bump = 0f)
    {
        float boxW = Math.Clamp(36f * scale, 28f, 52f);
        float boxH = Math.Clamp(heroRect.Height * 0.68f, 34f, heroRect.Height - 8f);
        float boxY = heroRect.Y + (heroRect.Height - boxH) / 2f;

        RectangleF leftBox = new RectangleF(heroRect.Left - (boxW * 0.5f) - bump, boxY, boxW, boxH);
        RectangleF rightBox = new RectangleF(heroRect.Right - (boxW * 0.5f) + bump, boxY, boxW, boxH);
        return (leftBox, rightBox);
    }

    private (RectangleF topBox, RectangleF bottomBox) GetCompactTopBottomPreviewBoxRects(RectangleF heroRect, float scale, float bump = 0f)
    {
        float boxW = heroRect.Width * 0.84f;
        float boxH = Math.Clamp(26f * scale, 20f, 40f);
        float boxX = heroRect.X + (heroRect.Width - boxW) / 2f;
        float exposedH = boxH * 0.56f;

        RectangleF topBox = new RectangleF(boxX, heroRect.Top - exposedH - bump, boxW, boxH);
        RectangleF bottomBox = new RectangleF(boxX, heroRect.Bottom - (boxH - exposedH) + bump, boxW, boxH);
        return (topBox, bottomBox);
    }

    private void DrawOverlayScene(Graphics g)
    {
        int baseW = Math.Max(120, _settings.OverlayWidth);
        int baseH = Math.Max(50, _settings.OverlayHeight);
        float scale = Math.Clamp((float)baseH / 80f, 0.5f, 5.0f);

        float heroX = (this.Width - baseW) / 2f;
        RectangleF heroRect = new RectangleF(
            heroX,
            _stationaryCardOffsetY,
            baseW - 1f,
            baseH - 1f);

        if (string.Equals(_settings.CarouselMode, "None", StringComparison.OrdinalIgnoreCase))
        {
            DrawStandardHeroCard(g, heroRect, scale);
            return;
        }

        CarouselStyleState desiredState = CarouselStyleStateMachine.ParseState(_settings.CarouselMotionStyle, _settings.CarouselMode);
        if (_carouselStyleMachine.CurrentState != desiredState || _activeStyleRenderAction == null)
        {
            _carouselStyleMachine.TransitionTo(desiredState, force: true);
        }

        bool isWideStyle = CarouselStyleStateMachine.IsWideCarousel(desiredState);
        bool isCompactStyle = CarouselStyleStateMachine.IsCompactCarousel(desiredState);
        bool alwaysShowFullPreviews = isWideStyle &&
            string.Equals(_settings.NeighborPreviewVisibility, "Always", StringComparison.OrdinalIgnoreCase) &&
            !_suppressCarousel && _cycleItems.Count > 1;

        // --- 1. FULL CAROUSEL STYLES ---
        if (isWideStyle && !_suppressCarousel && _cycleItems.Count > 1 && (_wheelExpansion > 0.01f || alwaysShowFullPreviews))
        {
            if (_wheelExpansion > 0.01f)
            {
                RectangleF containerRect = GetCarouselContainerRect(baseW, baseH, scale);
                float containerRadius = GetWin11CornerRadius(scale, large: true);

                using GraphicsPath containerPath = Win11CardPanel.CreateRoundRectF(containerRect, containerRadius);

                if (_settings.CarouselBackgroundEnabled)
                {
                    int backdropAlpha = (int)Math.Clamp(242 * _wheelExpansion, 0, 242);
                    using SolidBrush backdropBrush = new SolidBrush(Color.FromArgb(backdropAlpha, BackdropCharcoal));
                    g.FillPath(backdropBrush, containerPath);
                }

                GraphicsState clipState = g.Save();
                g.SetClip(containerPath, CombineMode.Intersect);

                _activeStyleRenderAction?.Invoke(g, heroRect, baseH, scale);

                g.Restore(clipState);

                if (_settings.CarouselBackgroundEnabled)
                {
                    using Pen outerFramePen = new Pen(Color.FromArgb((int)(85 * _wheelExpansion), 255, 255, 255), 1.3f);
                    g.DrawPath(outerFramePen, containerPath);
                }
            }
            else
            {
                DrawCompactTopBottomPreviewBoxes(g, heroRect, scale, 1.0f);
                DrawStandardHeroCard(g, heroRect, scale);
            }
            return;
        }

        // --- 2. COMPACT CAROUSEL STYLES (TOP & BOTTOM PREVIEWS) ---
        if (isCompactStyle)
        {
            bool styleDrawsOwnPreviews = (desiredState == CarouselStyleState.CompactTopBottomMorph) &&
                                         _compactTransitionProgress > 0.005f && _hasOutgoingCompactItem;

            if (_neighborPreviewVisibility > 0.01f && !styleDrawsOwnPreviews && _cycleItems.Count > 1)
            {
                DrawCompactTopBottomPreviewBoxes(g, heroRect, scale, _neighborPreviewVisibility);
            }

            if (_compactTransitionProgress > 0.005f && _hasOutgoingCompactItem)
            {
                _activeStyleRenderAction?.Invoke(g, heroRect, baseH, scale);
            }
            else
            {
                DrawStandardHeroCard(g, heroRect, scale);
            }
            return;
        }

        // --- 3. MINIMAL CAROUSEL STYLES (LEFT & RIGHT SIDE PREVIEWS) ---
        bool minimalStyleDrawsOwnPreviews = (desiredState == CarouselStyleState.MinimalSidePreviewMorph) &&
                                            _compactTransitionProgress > 0.005f && _hasOutgoingCompactItem;

        if (_neighborPreviewVisibility > 0.01f && !minimalStyleDrawsOwnPreviews && _cycleItems.Count > 1)
        {
            DrawMinimalSidePreviewBoxes(g, heroRect, scale, _neighborPreviewVisibility);
        }

        if (_compactTransitionProgress > 0.005f && _hasOutgoingCompactItem)
        {
            _activeStyleRenderAction?.Invoke(g, heroRect, baseH, scale);
        }
        else
        {
            DrawStandardHeroCard(g, heroRect, scale);
        }
    }

    private void DrawStandardHeroCard(Graphics g, RectangleF heroRect, float scale)
    {
        float radius = GetWin11CornerRadius(scale);

        if (!_suppressCarousel && _neighborPreviewVisibility > 0.01f && _cycleItems.Count > 1)
        {
            for (int s = 2; s >= 1; s--)
            {
                RectangleF shadowRect = RectangleF.Inflate(heroRect, s * 1.8f, s * 1.8f);
                using GraphicsPath shadowPath = Win11CardPanel.CreateRoundRectF(shadowRect, radius + s);
                using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb((int)(75 * _neighborPreviewVisibility) / s, 0, 0, 0));
                g.FillPath(shadowBrush, shadowPath);
            }
        }

        using GraphicsPath heroPath = Win11CardPanel.CreateRoundRectF(heroRect, radius);
        FillRoundedCardGradient(g, heroPath, heroRect, _renderedBgLeft, _renderedBgRight);
        DrawActiveHUDContent(g, heroRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f);
        using Pen heroBorderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawPath(heroBorderPen, heroPath);
    }

    private delegate void WideCardDrawer(
        Graphics g,
        CycleDisplayItem item,
        bool isActiveSlot,
        float signedVisualOffset,
        RectangleF heroCardRect,
        int baseH,
        float scale);

    private int GetCarouselItemIndexForSlot(int slotOffset)
    {
        int count = _cycleItems.Count;
        if (count <= 0) return 0;
        int orderSign = string.Equals(_settings.CardOrderDirection, "Descending", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
        int listDelta = slotOffset * orderSign;
        return ((_cycleActiveIndex + listDelta) % count + count) % count;
    }

    private void RenderWideDeckOrder(
        Graphics g,
        RectangleF heroRect,
        int baseH,
        float scale,
        WideCardDrawer drawer,
        bool drawHeroDropShadow = false)
    {
        int[] deckOrder = new[] { -3, 3, -2, 2, -1, 1, 0 };
        foreach (int offset in deckOrder)
        {
            if (drawHeroDropShadow && offset == 0 && _wheelExpansion > 0.02f)
            {
                float heroSlideOffset = Math.Clamp(1.0f - Math.Abs(_wheelScrollOffset), 0f, 1f);
                int shadowAlpha = (int)Math.Clamp(115 * _wheelExpansion * heroSlideOffset, 0, 125);
                if (shadowAlpha > 5)
                {
                    for (int s = 3; s >= 1; s--)
                    {
                        RectangleF shadowRect = RectangleF.Inflate(heroRect, s * 1.5f, s * 2.2f);
                        using GraphicsPath shadowPath = Win11CardPanel.CreateRoundRectF(shadowRect, 11f * scale);
                        using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha / s, 0, 0, 0));
                        g.FillPath(shadowBrush, shadowPath);
                    }
                }
            }

            int itemIdx = GetCarouselItemIndexForSlot(offset);
            float visualOffset = offset - _wheelScrollOffset;
            drawer(g, _cycleItems[itemIdx], offset == 0, visualOffset, heroRect, baseH, scale);
        }
    }

    private void RenderFloatingPillsStyle(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        int[] drawOrder = new[] { -2, 2, -1, 1 };
        foreach (int offset in drawOrder)
        {
            int itemIdx = GetCarouselItemIndexForSlot(offset);
            float visualOffset = offset - _wheelScrollOffset;
            DrawFloatingCarouselPill(g, _cycleItems[itemIdx], visualOffset, heroRect, baseH, scale);
        }

        if (_wheelExpansion > 0.02f)
        {
            int shadowAlpha = (int)Math.Clamp(115 * _wheelExpansion, 0, 125);
            for (int s = 3; s >= 1; s--)
            {
                RectangleF shadowRect = RectangleF.Inflate(heroRect, s * 1.5f, s * 2.2f);
                using GraphicsPath shadowPath = Win11CardPanel.CreateRoundRectF(shadowRect, 11f * scale);
                using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha / s, 0, 0, 0));
                g.FillPath(shadowBrush, shadowPath);
            }
        }

        float heroCornerRadius = GetWin11CornerRadius(scale);
        using (GraphicsPath heroPath = Win11CardPanel.CreateRoundRectF(heroRect, heroCornerRadius))
        {
            FillRoundedCardGradient(g, heroPath, heroRect, _renderedBgLeft, _renderedBgRight);
            DrawActiveHUDContent(g, heroRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f);
            int rimAlpha = 105 + (int)Math.Round(90 * _wheelExpansion);
            using Pen heroBorderPen = new Pen(Color.FromArgb(Math.Clamp(rimAlpha, 0, 220), 255, 255, 255), 1.3f);
            g.DrawPath(heroBorderPen, heroPath);
        }
    }

    private void DrawMinimalSidePreviewBoxes(Graphics g, RectangleF heroRect, float scale, float visibility)
    {
        int count = _cycleItems.Count;
        if (count < 2 || visibility <= 0.01f) return;

        int leftIdx = GetCarouselItemIndexForSlot(-1);
        int rightIdx = GetCarouselItemIndexForSlot(1);

        float bump = MathF.Sin((1f - _compactTransitionProgress) * MathF.PI) * 2.5f * scale * (_compactTransitionProgress > 0f ? 1f : 0f);
        var (leftBox, rightBox) = GetMinimalSidePreviewBoxRects(heroRect, scale, bump);

        int alpha = (int)Math.Clamp(230f * visibility, 0f, 230f);
        DrawSingleNeighborPreviewBox(g, leftBox, _cycleItems[leftIdx], scale, isLeftSide: true, alpha: alpha);
        DrawSingleNeighborPreviewBox(g, rightBox, _cycleItems[rightIdx], scale, isLeftSide: false, alpha: alpha);
    }

    private void DrawCompactTopBottomPreviewBoxes(Graphics g, RectangleF heroRect, float scale, float visibility)
    {
        int count = _cycleItems.Count;
        if (count < 2 || visibility <= 0.01f) return;

        int topIdx = GetCarouselItemIndexForSlot(-1);
        int botIdx = GetCarouselItemIndexForSlot(1);

        float bump = MathF.Sin((1f - _compactTransitionProgress) * MathF.PI) * 2.5f * scale * (_compactTransitionProgress > 0f ? 1f : 0f);
        var (topBox, bottomBox) = GetCompactTopBottomPreviewBoxRects(heroRect, scale, bump);

        int alpha = (int)Math.Clamp(235f * visibility, 0f, 235f);
        DrawSingleTopBottomPreviewBar(g, topBox, _cycleItems[topIdx], scale, isTopSide: true, alpha: alpha);
        DrawSingleTopBottomPreviewBar(g, bottomBox, _cycleItems[botIdx], scale, isTopSide: false, alpha: alpha);
    }

    private void DrawSingleTopBottomPreviewBar(Graphics g, RectangleF barRect, CycleDisplayItem item, float scale, bool isTopSide, int alpha = 235)
    {
        if (barRect.Width <= 8f || barRect.Height <= 4f || alpha <= 8) return;

        float radius = Math.Clamp(7f * scale, 5f, 12f);
        Color rawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120));
        Color rawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85));

        Color surfaceBase = Color.FromArgb(26, 27, 32);
        Color col1 = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(surfaceBase, rawC1, 0.68f))
            : surfaceBase;
        Color col2 = item.IsSingleSource
            ? col1
            : (_settings.UseSourceBackgroundColors
                ? EnsureReadableBackdrop(ColorLerp(surfaceBase, rawC2, 0.68f))
                : surfaceBase);

        using GraphicsPath barPath = Win11CardPanel.CreateRoundRectF(barRect, radius);
        FillRoundedCardGradient(g, barPath, barRect, Color.FromArgb(alpha, col1), Color.FromArgb(alpha, col2));

        float exposedH = barRect.Height * 0.56f;
        RectangleF visibleLip = isTopSide
            ? new RectangleF(barRect.X, barRect.Y + 1f, barRect.Width, exposedH)
            : new RectangleF(barRect.X, barRect.Bottom - exposedH - 1f, barRect.Width, exposedH);

        int textAlpha = (int)Math.Clamp(alpha * 0.96f, 0f, 245f);
        bool showName = _settings.OverlayIdentityMode == "Name" || _settings.OverlayIdentityMode == "Both";
        bool showIcon = _settings.OverlayIdentityMode == "Icon" || _settings.OverlayIdentityMode == "Both";

        if (item.IsSingleSource)
        {
            RectangleF centerZone = new RectangleF(visibleLip.X + 12f * scale, visibleLip.Y, visibleLip.Width - 24f * scale, visibleLip.Height);
            DrawCenteredPillIdentity(g, centerZone, item.Label1, item.RawSource1, textAlpha, scale * 0.72f, showName, showIcon);
        }
        else
        {
            float cx = visibleLip.X + visibleLip.Width / 2f;
            RectangleF leftZone = new RectangleF(visibleLip.X + 10f * scale, visibleLip.Y, (visibleLip.Width / 2f) - 18f * scale, visibleLip.Height);
            RectangleF rightZone = new RectangleF(cx + 8f * scale, visibleLip.Y, (visibleLip.Width / 2f) - 18f * scale, visibleLip.Height);
            DrawPillSideIdentity(g, leftZone, item.Label1, item.RawSource1, textAlpha, scale * 0.72f, showName, showIcon, isRightSide: false);
            DrawPillSideIdentity(g, rightZone, item.Label2, item.RawSource2, textAlpha, scale * 0.72f, showName, showIcon, isRightSide: true);
        }

        using Pen borderPen = new Pen(Color.FromArgb((int)(alpha * 0.60f), 255, 255, 255), 1.1f);
        g.DrawPath(borderPen, barPath);
    }

    private static RectangleF LerpRectF(RectangleF a, RectangleF b, float t)
    {
        return new RectangleF(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Width + (b.Width - a.Width) * t,
            a.Height + (b.Height - a.Height) * t);
    }

    private void DrawMinimalSidePreviewMorphHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 0.5f - 0.5f * MathF.Cos(t * MathF.PI);

        var (leftPreviewRect, rightPreviewRect) = GetMinimalSidePreviewBoxRects(heroRect, scale);

        RectangleF incomingStartRect = _compactTransitionDir >= 0 ? leftPreviewRect : rightPreviewRect;
        RectangleF outgoingEndRect = _compactTransitionDir >= 0 ? rightPreviewRect : leftPreviewRect;

        if (!_suppressCarousel && _neighborPreviewVisibility > 0.01f && _cycleItems.Count > 1)
        {
            int leftIdx = GetCarouselItemIndexForSlot(-1);
            int rightIdx = GetCarouselItemIndexForSlot(1);

            // 1a. The newly arriving preview box on the side that just vacated fades in
            int inPreviewAlpha = (int)(230 * ease * _neighborPreviewVisibility);
            if (_compactTransitionDir >= 0)
                DrawSingleNeighborPreviewBox(g, leftPreviewRect, _cycleItems[leftIdx], scale, isLeftSide: true, alpha: inPreviewAlpha);
            else
                DrawSingleNeighborPreviewBox(g, rightPreviewRect, _cycleItems[rightIdx], scale, isLeftSide: false, alpha: inPreviewAlpha);

            // 1b. The old neighbor preview being replaced by the hero card recedes outward, shrinks slightly, and fades out smoothly!
            float recedeFade = Math.Clamp(1.0f - ease * 1.15f, 0f, 1f);
            int outPreviewAlpha = (int)(230 * recedeFade * _neighborPreviewVisibility);
            if (outPreviewAlpha > 8)
            {
                int oldReplacedIdx = _compactTransitionDir >= 0
                    ? GetCarouselItemIndexForSlot(2)
                    : GetCarouselItemIndexForSlot(-2);
                float shiftOutX = (_compactTransitionDir >= 0 ? 1f : -1f) * (14f * scale * ease);
                float shrinkH = outgoingEndRect.Height * (1.0f - 0.25f * ease);
                float shrinkW = outgoingEndRect.Width * (1.0f - 0.20f * ease);
                RectangleF recedingRect = new RectangleF(
                    outgoingEndRect.X + shiftOutX + (outgoingEndRect.Width - shrinkW) / 2f,
                    outgoingEndRect.Y + (outgoingEndRect.Height - shrinkH) / 2f,
                    shrinkW,
                    shrinkH);
                DrawSingleNeighborPreviewBox(g, recedingRect, _cycleItems[oldReplacedIdx], scale * (1f - 0.15f * ease), isLeftSide: _compactTransitionDir < 0, alpha: outPreviewAlpha);
            }
        }

        // 2. Outgoing hero card shrinks into outgoingEndRect
        RectangleF outRect = LerpRectF(heroRect, outgoingEndRect, ease);
        float outEmphasis = Math.Clamp(1.0f - ease * 1.25f, 0f, 1f);
        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        float outRadius = Math.Clamp(6f * scale + (GetWin11CornerRadius(scale) - 6f * scale) * (1f - ease), 4f, 16f);
        using (GraphicsPath outPath = Win11CardPanel.CreateRoundRectF(outRect, outRadius))
        {
            FillRoundedCardGradient(g, outPath, outRect, oldLeftCol, oldRightCol);
            if (outEmphasis > 0.08f)
            {
                DrawActiveHUDContent(
                    g, outRect,
                    _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                    _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                    _app1Vol, _app2Vol, scale * (0.65f + 0.35f * (1f - ease)), outEmphasis, 240);
            }
            using Pen outBorderPen = new Pen(Color.FromArgb(170, 255, 255, 255), 1.2f);
            g.DrawPath(outBorderPen, outPath);
        }

        // 3. Incoming preview box expands into heroRect
        RectangleF inRect = LerpRectF(incomingStartRect, heroRect, ease);
        float inRadius = Math.Clamp(6f * scale + (GetWin11CornerRadius(scale) - 6f * scale) * ease, 4f, 16f);

        for (int s = 2; s >= 1; s--)
        {
            RectangleF shadowRect = RectangleF.Inflate(inRect, s * 1.8f, s * 1.8f);
            using GraphicsPath shadowPath = Win11CardPanel.CreateRoundRectF(shadowRect, inRadius + s);
            using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb((int)(80 * ease) / s, 0, 0, 0));
            g.FillPath(shadowBrush, shadowPath);
        }

        using (GraphicsPath inPath = Win11CardPanel.CreateRoundRectF(inRect, inRadius))
        {
            FillRoundedCardGradient(g, inPath, inRect, _renderedBgLeft, _renderedBgRight);
            DrawActiveHUDContent(
                g, inRect,
                _app1Label, _app2Label,
                _rawSource1, _rawSource2,
                _app1Vol, _app2Vol,
                scale * (0.65f + 0.35f * ease), ease, 255);
            using Pen inBorderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.4f);
            g.DrawPath(inBorderPen, inPath);
        }
    }

    private void DrawCompactTopBottomMorphHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 0.5f - 0.5f * MathF.Cos(t * MathF.PI);

        var (topPreviewRect, botPreviewRect) = GetCompactTopBottomPreviewBoxRects(heroRect, scale);

        RectangleF incomingStartRect = _compactTransitionDir >= 0 ? topPreviewRect : botPreviewRect;
        RectangleF outgoingEndRect = _compactTransitionDir >= 0 ? botPreviewRect : topPreviewRect;

        if (!_suppressCarousel && _neighborPreviewVisibility > 0.01f && _cycleItems.Count > 1)
        {
            int topIdx = GetCarouselItemIndexForSlot(-1);
            int botIdx = GetCarouselItemIndexForSlot(1);

            // 1a. Newly arriving preview bar on the vacated side fades in
            int inAlpha = (int)(235 * ease * _neighborPreviewVisibility);
            if (_compactTransitionDir >= 0)
                DrawSingleTopBottomPreviewBar(g, topPreviewRect, _cycleItems[topIdx], scale, isTopSide: true, alpha: inAlpha);
            else
                DrawSingleTopBottomPreviewBar(g, botPreviewRect, _cycleItems[botIdx], scale, isTopSide: false, alpha: inAlpha);

            // 1b. Old neighbor preview bar being replaced by the hero card slides back, shrinks, and fades out smoothly!
            float recedeFade = Math.Clamp(1.0f - ease * 1.15f, 0f, 1f);
            int outPreviewAlpha = (int)(235 * recedeFade * _neighborPreviewVisibility);
            if (outPreviewAlpha > 8)
            {
                int oldReplacedIdx = _compactTransitionDir >= 0
                    ? GetCarouselItemIndexForSlot(2)
                    : GetCarouselItemIndexForSlot(-2);
                float shiftOutY = (_compactTransitionDir >= 0 ? 1f : -1f) * (12f * scale * ease);
                float shrinkW = outgoingEndRect.Width * (1.0f - 0.14f * ease);
                RectangleF recedingRect = new RectangleF(
                    outgoingEndRect.X + (outgoingEndRect.Width - shrinkW) / 2f,
                    outgoingEndRect.Y + shiftOutY,
                    shrinkW,
                    outgoingEndRect.Height * (1.0f - 0.15f * ease));
                DrawSingleTopBottomPreviewBar(g, recedingRect, _cycleItems[oldReplacedIdx], scale * (1f - 0.12f * ease), isTopSide: _compactTransitionDir < 0, alpha: outPreviewAlpha);
            }
        }

        // 2. Outgoing hero card shrinks into outgoingEndRect
        RectangleF outRect = LerpRectF(heroRect, outgoingEndRect, ease);
        float outEmphasis = Math.Clamp(1.0f - ease * 1.25f, 0f, 1f);
        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        float outRadius = Math.Clamp(7f * scale + (GetWin11CornerRadius(scale) - 7f * scale) * (1f - ease), 5f, 16f);
        using (GraphicsPath outPath = Win11CardPanel.CreateRoundRectF(outRect, outRadius))
        {
            FillRoundedCardGradient(g, outPath, outRect, oldLeftCol, oldRightCol);
            if (outEmphasis > 0.08f)
            {
                DrawActiveHUDContent(
                    g, outRect,
                    _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                    _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                    _app1Vol, _app2Vol, scale * (0.72f + 0.28f * (1f - ease)), outEmphasis, 240);
            }
            using Pen outBorderPen = new Pen(Color.FromArgb(170, 255, 255, 255), 1.2f);
            g.DrawPath(outBorderPen, outPath);
        }

        // 3. Incoming preview bar expands into heroRect
        RectangleF inRect = LerpRectF(incomingStartRect, heroRect, ease);
        float inRadius = Math.Clamp(7f * scale + (GetWin11CornerRadius(scale) - 7f * scale) * ease, 5f, 16f);

        for (int s = 2; s >= 1; s--)
        {
            RectangleF shadowRect = RectangleF.Inflate(inRect, s * 1.8f, s * 1.8f);
            using GraphicsPath shadowPath = Win11CardPanel.CreateRoundRectF(shadowRect, inRadius + s);
            using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb((int)(85 * ease) / s, 0, 0, 0));
            g.FillPath(shadowBrush, shadowPath);
        }

        using (GraphicsPath inPath = Win11CardPanel.CreateRoundRectF(inRect, inRadius))
        {
            FillRoundedCardGradient(g, inPath, inRect, _renderedBgLeft, _renderedBgRight);
            DrawActiveHUDContent(
                g, inRect,
                _app1Label, _app2Label,
                _rawSource1, _rawSource2,
                _app1Vol, _app2Vol,
                scale * (0.72f + 0.28f * ease), ease, 255);
            using Pen inBorderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.4f);
            g.DrawPath(inBorderPen, inPath);
        }
    }

    // =========================================================================
    // COMPACT & MINIMAL CARD FLIP (HERO FLIPS VERTICALLY IN PLACE, PREVIEWS STAY STILL)
    // =========================================================================
    private void DrawCompactCardFlipHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 0.5f - 0.5f * MathF.Cos(t * MathF.PI);
        bool showNewHalf = (ease >= 0.5f);
        float flipScaleY = Math.Max(0.04f, Math.Abs(MathF.Cos(ease * MathF.PI)));

        // Directional 3D tilt shading: top/bottom edge brightens or darkens depending on flip direction
        int shadeDelta = (int)((1.0f - flipScaleY) * 145f) * (_compactTransitionDir >= 0 ? (showNewHalf ? 1 : -1) : (showNewHalf ? -1 : 1));

        float radius = GetWin11CornerRadius(scale);

        // Draw elevation shadow behind the flipping hero card so it pops over stationary previews
        if (!_suppressCarousel && _neighborPreviewVisibility > 0.01f && _cycleItems.Count > 1)
        {
            for (int s = 2; s >= 1; s--)
            {
                RectangleF shadowRect = RectangleF.Inflate(heroRect, s * 1.8f, s * 1.8f * flipScaleY);
                using GraphicsPath shadowPath = Win11CardPanel.CreateRoundRectF(shadowRect, radius + s);
                using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb((int)(75 * _neighborPreviewVisibility) / s, 0, 0, 0));
                g.FillPath(shadowBrush, shadowPath);
            }
        }

        GraphicsState stHero = g.Save();
        float heroCy = heroRect.Y + heroRect.Height / 2f;
        g.TranslateTransform(0f, heroCy);
        g.ScaleTransform(1f, flipScaleY);
        g.TranslateTransform(0f, -heroCy);

        using GraphicsPath heroPath = Win11CardPanel.CreateRoundRectF(heroRect, radius);
        if (showNewHalf)
        {
            FillRoundedCardGradient(g, heroPath, heroRect, _renderedBgLeft, _renderedBgRight);
            DrawActiveHUDContent(g, heroRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f);
        }
        else
        {
            var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
                _outgoingCompactItem.Color1Hex,
                _outgoingCompactItem.Color2Hex,
                _outgoingCompactItem.IsSingleSource);
            FillRoundedCardGradient(g, heroPath, heroRect, oldLeftCol, oldRightCol);
            DrawActiveHUDContent(
                g, heroRect,
                _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                _app1Vol, _app2Vol, scale, 1.0f);
        }

        if (Math.Abs(shadeDelta) > 4)
        {
            Color shadeCol = shadeDelta > 0
                ? Color.FromArgb(Math.Clamp(shadeDelta, 0, 110), 255, 255, 255)
                : Color.FromArgb(Math.Clamp(-shadeDelta, 0, 165), 0, 0, 0);
            using SolidBrush shadeBrush = new SolidBrush(shadeCol);
            g.FillPath(shadeBrush, heroPath);
        }

        using Pen borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawPath(borderPen, heroPath);
        g.Restore(stHero);
    }

    private void DrawMinimalCardFlipHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        // Minimal Card Flip uses the exact same vertical flip direction as Compact Card Flip!
        DrawCompactCardFlipHero(g, heroRect, baseH, scale);
    }

    private void DrawCompactVerticalSwooshHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 1.0f - MathF.Pow(1.0f - t, 3f);
        float radius = GetWin11CornerRadius(scale);

        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        using GraphicsPath clipPath = Win11CardPanel.CreateRoundRectF(heroRect, radius);
        using SolidBrush baseFill = new SolidBrush(BackdropCharcoal);
        g.FillPath(baseFill, clipPath);

        GraphicsState st = g.Save();
        g.SetClip(clipPath, CombineMode.Intersect);

        float travel = heroRect.Height * 0.95f;
        // Next (+1): Top card swooshes DOWN into place
        float outShiftY = _compactTransitionDir * travel * ease;
        float inShiftY = -_compactTransitionDir * travel * (1.0f - ease);

        int outAlpha = (int)Math.Clamp((1.0f - ease) * 255f, 0f, 255f);
        if (outAlpha > 8)
        {
            RectangleF outRect = new RectangleF(heroRect.X, heroRect.Y + outShiftY, heroRect.Width, heroRect.Height);
            using GraphicsPath outPath = Win11CardPanel.CreateRoundRectF(outRect, radius);
            FillRoundedCardGradient(g, outPath, outRect, Color.FromArgb(outAlpha, oldLeftCol), Color.FromArgb(outAlpha, oldRightCol));
            DrawActiveHUDContent(
                g, outRect,
                _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                _app1Vol, _app2Vol, scale, 1.0f, outAlpha);
        }

        int inAlpha = (int)Math.Clamp(ease * 255f, 0f, 255f);
        if (inAlpha > 8)
        {
            RectangleF inRect = new RectangleF(heroRect.X, heroRect.Y + inShiftY, heroRect.Width, heroRect.Height);
            using GraphicsPath inPath = Win11CardPanel.CreateRoundRectF(inRect, radius);
            FillRoundedCardGradient(g, inPath, inRect, Color.FromArgb(inAlpha, _renderedBgLeft), Color.FromArgb(inAlpha, _renderedBgRight));
            DrawActiveHUDContent(g, inRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f, inAlpha);
        }

        g.Restore(st);

        using Pen borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawPath(borderPen, clipPath);
    }

    private void DrawCompactVerticalLaserWipeHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 0.5f - 0.5f * MathF.Cos(t * MathF.PI);
        float radius = GetWin11CornerRadius(scale);

        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        using GraphicsPath cardPath = Win11CardPanel.CreateRoundRectF(heroRect, radius);

        float lineY = _compactTransitionDir >= 0
            ? heroRect.Top + heroRect.Height * ease
            : heroRect.Bottom - heroRect.Height * ease;

        RectangleF newRegion = _compactTransitionDir >= 0
            ? new RectangleF(heroRect.Left - 2f, heroRect.Top - 2f, heroRect.Width + 4f, (lineY - heroRect.Top) + 2f)
            : new RectangleF(heroRect.Left - 2f, lineY, heroRect.Width + 4f, (heroRect.Bottom - lineY) + 2f);

        RectangleF oldRegion = _compactTransitionDir >= 0
            ? new RectangleF(heroRect.Left - 2f, lineY, heroRect.Width + 4f, (heroRect.Bottom - lineY) + 2f)
            : new RectangleF(heroRect.Left - 2f, heroRect.Top - 2f, heroRect.Width + 4f, (lineY - heroRect.Top) + 2f);

        if (oldRegion.Height > 1f)
        {
            GraphicsState stOld = g.Save();
            g.SetClip(cardPath, CombineMode.Intersect);
            g.SetClip(oldRegion, CombineMode.Intersect);
            FillRoundedCardGradient(g, cardPath, heroRect, oldLeftCol, oldRightCol);
            DrawActiveHUDContent(
                g, heroRect,
                _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                _app1Vol, _app2Vol, scale, 1.0f);
            g.Restore(stOld);
        }

        if (newRegion.Height > 1f)
        {
            GraphicsState stNew = g.Save();
            g.SetClip(cardPath, CombineMode.Intersect);
            g.SetClip(newRegion, CombineMode.Intersect);
            FillRoundedCardGradient(g, cardPath, heroRect, _renderedBgLeft, _renderedBgRight);
            DrawActiveHUDContent(g, heroRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f);
            g.Restore(stNew);
        }

        GraphicsState stLaser = g.Save();
        g.SetClip(cardPath, CombineMode.Intersect);
        Color accentGlow = ParseOverlayColor(_settings.UnmutedColor, Color.FromArgb(0, 195, 255));
        for (int w = 3; w >= 1; w--)
        {
            using Pen glowPen = new Pen(Color.FromArgb(75 / w, accentGlow), w * 3.0f * scale);
            g.DrawLine(glowPen, heroRect.Left, lineY, heroRect.Right, lineY);
        }
        using (Pen corePen = new Pen(Color.FromArgb(245, 255, 255, 255), 1.8f * scale))
        {
            g.DrawLine(corePen, heroRect.Left, lineY, heroRect.Right, lineY);
        }
        g.Restore(stLaser);

        using Pen borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawPath(borderPen, cardPath);
    }

    private void DrawCompactVerticalSquashStretchHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 0.5f - 0.5f * MathF.Cos(t * MathF.PI);
        float radius = GetWin11CornerRadius(scale);

        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        float totalH = heroRect.Height;
        float inRatio = Math.Clamp(ease, 0.001f, 1.0f);
        float outRatio = Math.Clamp(1.0f - inRatio, 0.001f, 1.0f);

        float splitY = _compactTransitionDir < 0
            ? heroRect.Top + totalH * outRatio
            : heroRect.Top + totalH * inRatio;

        RectangleF topSlice = new RectangleF(heroRect.Left, heroRect.Top, heroRect.Width, splitY - heroRect.Top);
        RectangleF botSlice = new RectangleF(heroRect.Left, splitY, heroRect.Width, heroRect.Bottom - splitY);

        RectangleF inRect = _compactTransitionDir < 0 ? botSlice : topSlice;
        RectangleF outRect = _compactTransitionDir < 0 ? topSlice : botSlice;

        using GraphicsPath outerClipPath = Win11CardPanel.CreateRoundRectF(heroRect, radius);
        using (SolidBrush backdropBrush = new SolidBrush(BackdropCharcoal))
        {
            g.FillPath(backdropBrush, outerClipPath);
        }

        GraphicsState outerClipState = g.Save();
        g.SetClip(outerClipPath, CombineMode.Intersect);

        void DrawVertSquashedCard(RectangleF targetBox, float scaleY, bool isIncoming, int alpha)
        {
            if (targetBox.Height < 0.5f || scaleY < 0.005f || alpha < 4) return;

            GraphicsState st = g.Save();
            g.SetClip(targetBox, CombineMode.Intersect);

            float centerX = heroRect.X + heroRect.Width / 2f;
            float pivotY = targetBox.Y;

            g.TranslateTransform(centerX, pivotY);
            g.ScaleTransform(1.0f, scaleY);
            g.TranslateTransform(-centerX, -heroRect.Y);

            if (isIncoming)
            {
                FillRoundedCardGradient(g, outerClipPath, heroRect, Color.FromArgb(alpha, _renderedBgLeft), Color.FromArgb(alpha, _renderedBgRight));
                DrawActiveHUDContent(g, heroRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f, alpha);
            }
            else
            {
                FillRoundedCardGradient(g, outerClipPath, heroRect, Color.FromArgb(alpha, oldLeftCol), Color.FromArgb(alpha, oldRightCol));
                DrawActiveHUDContent(
                    g, heroRect,
                    _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                    _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                    _app1Vol, _app2Vol, scale, 1.0f, alpha);
            }

            g.Restore(st);
        }

        int outAlpha = (int)Math.Clamp((1.0f - ease * 0.65f) * 255f, 0f, 255f);
        if (outRatio > 0.005f)
        {
            DrawVertSquashedCard(outRect, outRatio, isIncoming: false, outAlpha);
        }
        DrawVertSquashedCard(inRect, inRatio, isIncoming: true, 255);

        if (ease > 0.02f && ease < 0.98f)
        {
            int seamAlpha = (int)(MathF.Sin(ease * MathF.PI) * 210f);
            using Pen seamPen = new Pen(Color.FromArgb(seamAlpha, 255, 255, 255), 1.5f * scale);
            g.DrawLine(seamPen, heroRect.Left + 1f, splitY, heroRect.Right - 1f, splitY);
        }

        g.Restore(outerClipState);

        using Pen borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawPath(borderPen, outerClipPath);
    }

    // =========================================================================
    // COMPACT NEIGHBOR PREVIEW BOXES (HALF-OVERLAPPING LEFT & RIGHT EDGES)
    // =========================================================================

    private void DrawSingleNeighborPreviewBox(Graphics g, RectangleF boxRect, CycleDisplayItem item, float scale, bool? isLeftSide = null, int alpha = 230)
    {
        if (boxRect.Width <= 4f || boxRect.Height <= 4f || alpha <= 8) return;

        float radius = Math.Clamp(6f * scale, 4f, 10f);
        Color rawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120));
        Color rawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85));

        Color surfaceBase = Color.FromArgb(26, 27, 32);
        Color col1 = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(surfaceBase, rawC1, 0.75f))
            : surfaceBase;
        Color col2 = item.IsSingleSource
            ? col1
            : (_settings.UseSourceBackgroundColors
                ? EnsureReadableBackdrop(ColorLerp(surfaceBase, rawC2, 0.75f))
                : surfaceBase);

        using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb((int)(alpha * 0.35f), 0, 0, 0)))
        {
            RectangleF sRect = new RectangleF(boxRect.X + 1f, boxRect.Y + 2f, boxRect.Width, boxRect.Height);
            using GraphicsPath sPath = Win11CardPanel.CreateRoundRectF(sRect, radius);
            g.FillPath(shadowBrush, sPath);
        }

        using GraphicsPath boxPath = Win11CardPanel.CreateRoundRectF(boxRect, radius);
        GraphicsState clipState = g.Save();
        g.SetClip(boxPath, CombineMode.Intersect);

        // Because the preview box is 50% overlapped by the main card, we place the color split & icons
        // in the exposed outer half (left half for the left box, right half for the right box) so they are clearly visible!
        RectangleF visibleZone = isLeftSide == true
            ? new RectangleF(boxRect.X, boxRect.Y, boxRect.Width * 0.54f, boxRect.Height)
            : (isLeftSide == false
                ? new RectangleF(boxRect.X + boxRect.Width * 0.46f, boxRect.Y, boxRect.Width * 0.54f, boxRect.Height)
                : boxRect);

        if (item.IsSingleSource)
        {
            using SolidBrush b1 = new SolidBrush(Color.FromArgb(alpha, col1));
            g.FillRectangle(b1, boxRect);
        }
        else if (isLeftSide.HasValue)
        {
            // Top/bottom split inside the exposed wing so both source colors and both icons are visible!
            float halfH = boxRect.Height / 2f;
            using SolidBrush b1 = new SolidBrush(Color.FromArgb(alpha, col1));
            using SolidBrush b2 = new SolidBrush(Color.FromArgb(alpha, col2));
            g.FillRectangle(b1, boxRect.X, boxRect.Y, boxRect.Width, halfH + 0.5f);
            g.FillRectangle(b2, boxRect.X, boxRect.Y + halfH, boxRect.Width, halfH);

            using Pen splitPen = new Pen(Color.FromArgb((int)(alpha * 0.40f), 255, 255, 255), 1f);
            g.DrawLine(splitPen, visibleZone.Left + 2f, boxRect.Y + halfH, visibleZone.Right - 2f, boxRect.Y + halfH);
        }
        else
        {
            float halfW = boxRect.Width / 2f;
            using SolidBrush b1 = new SolidBrush(Color.FromArgb(alpha, col1));
            using SolidBrush b2 = new SolidBrush(Color.FromArgb(alpha, col2));
            g.FillRectangle(b1, boxRect.X, boxRect.Y, halfW + 0.5f, boxRect.Height);
            g.FillRectangle(b2, boxRect.X + halfW, boxRect.Y, halfW, boxRect.Height);
        }

        bool showIcon = _settings.OverlayIdentityMode == "Icon" || _settings.OverlayIdentityMode == "Both";
        if (showIcon)
        {
            Bitmap? icon1 = AppIconCache.GetIconForSource(item.RawSource1, _settings);
            Bitmap? icon2 = item.IsSingleSource ? null : AppIconCache.GetIconForSource(item.RawSource2, _settings);
            float icoOpacity = (alpha / 255f) * 0.92f;

            if (item.IsSingleSource && icon1 != null)
            {
                float icoSz = Math.Clamp(13f * scale, 10f, Math.Min(visibleZone.Width - 3f, visibleZone.Height - 4f));
                RectangleF icoRect = new RectangleF(
                    visibleZone.X + (visibleZone.Width - icoSz) / 2f,
                    visibleZone.Y + (visibleZone.Height - icoSz) / 2f,
                    icoSz, icoSz);
                DrawImageWithAlpha(g, icon1, icoRect, icoOpacity);
            }
            else if (!item.IsSingleSource)
            {
                float icoSz = Math.Clamp(11f * scale, 9f, Math.Min(visibleZone.Width - 3f, (visibleZone.Height / 2f) - 3f));
                float cx = visibleZone.X + (visibleZone.Width - icoSz) / 2f;
                if (icon1 != null)
                {
                    float y1 = visibleZone.Y + (visibleZone.Height * 0.25f) - (icoSz / 2f);
                    DrawImageWithAlpha(g, icon1, new RectangleF(cx, y1, icoSz, icoSz), icoOpacity);
                }
                if (icon2 != null)
                {
                    float y2 = visibleZone.Y + (visibleZone.Height * 0.75f) - (icoSz / 2f);
                    DrawImageWithAlpha(g, icon2, new RectangleF(cx, y2, icoSz, icoSz), icoOpacity);
                }
            }
        }

        g.Restore(clipState);

        using Pen borderPen = new Pen(Color.FromArgb((int)(alpha * 0.65f), 255, 255, 255), 1.1f);
        g.DrawPath(borderPen, boxPath);
    }

    // =========================================================================
    // COMPACT STYLE 1: SPLIT-FLAP DIGITAL ALARM CLOCK FLIP (REVERSIBLE)
    // =========================================================================
    private void DrawCompactClockFlipHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 1.0f - MathF.Pow(1.0f - t, 3f);
        float radius = GetWin11CornerRadius(scale);
        float midY = heroRect.Y + heroRect.Height / 2f;
        float halfH = heroRect.Height / 2f;

        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        void DrawCardHalf(RectangleF clipHalf, bool isIncoming, int shadeDelta)
        {
            GraphicsState st = g.Save();
            g.SetClip(clipHalf, CombineMode.Intersect);

            using GraphicsPath path = Win11CardPanel.CreateRoundRectF(heroRect, radius);
            if (isIncoming)
            {
                FillRoundedCardGradient(g, path, heroRect, _renderedBgLeft, _renderedBgRight);
                DrawActiveHUDContent(g, heroRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f);
            }
            else
            {
                FillRoundedCardGradient(g, path, heroRect, oldLeftCol, oldRightCol);
                DrawActiveHUDContent(
                    g, heroRect,
                    _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                    _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                    _app1Vol, _app2Vol, scale, 1.0f);
            }

            if (shadeDelta != 0)
            {
                Color shadeCol = shadeDelta > 0
                    ? Color.FromArgb(Math.Clamp(shadeDelta, 0, 140), 255, 255, 255)
                    : Color.FromArgb(Math.Clamp(-shadeDelta, 0, 180), 0, 0, 0);
                using SolidBrush shadeBrush = new SolidBrush(shadeCol);
                g.FillPath(shadeBrush, path);
            }

            using Pen borderPen = new Pen(Color.FromArgb(200, 255, 255, 255), 1.4f);
            g.DrawPath(borderPen, path);
            g.Restore(st);
        }

        RectangleF topHalfRect = new RectangleF(heroRect.X - 2f, heroRect.Y - 2f, heroRect.Width + 4f, halfH + 2f);
        RectangleF botHalfRect = new RectangleF(heroRect.X - 2f, midY, heroRect.Width + 4f, halfH + 2f);

        if (_compactTransitionDir >= 0)
        {
            // NEXT PAIR (+1): Folds Top -> Bottom
            DrawCardHalf(topHalfRect, isIncoming: true, shadeDelta: 0);
            DrawCardHalf(botHalfRect, isIncoming: false, shadeDelta: -35);

            if (ease < 0.5f)
            {
                float flapScaleY = Math.Max(0.04f, 1.0f - (ease * 2.0f));
                GraphicsState flapState = g.Save();
                g.TranslateTransform(0f, midY);
                g.ScaleTransform(1.0f, flapScaleY);
                g.TranslateTransform(0f, -midY);

                int shadow = -(int)((1.0f - flapScaleY) * 130f);
                DrawCardHalf(topHalfRect, isIncoming: false, shadeDelta: shadow);
                g.Restore(flapState);
            }
            else
            {
                float flapScaleY = Math.Max(0.04f, (ease - 0.5f) * 2.0f);
                GraphicsState flapState = g.Save();
                g.TranslateTransform(0f, midY);
                g.ScaleTransform(1.0f, flapScaleY);
                g.TranslateTransform(0f, -midY);

                int highlight = (int)((1.0f - flapScaleY) * 90f);
                DrawCardHalf(botHalfRect, isIncoming: true, shadeDelta: highlight);
                g.Restore(flapState);
            }
        }
        else
        {
            // PREVIOUS PAIR (-1): Reversed! Folds Bottom -> Top
            DrawCardHalf(topHalfRect, isIncoming: false, shadeDelta: -35);
            DrawCardHalf(botHalfRect, isIncoming: true, shadeDelta: 0);

            if (ease < 0.5f)
            {
                float flapScaleY = Math.Max(0.04f, 1.0f - (ease * 2.0f));
                GraphicsState flapState = g.Save();
                g.TranslateTransform(0f, midY);
                g.ScaleTransform(1.0f, flapScaleY);
                g.TranslateTransform(0f, -midY);

                int highlight = (int)((1.0f - flapScaleY) * 90f);
                DrawCardHalf(botHalfRect, isIncoming: false, shadeDelta: highlight);
                g.Restore(flapState);
            }
            else
            {
                float flapScaleY = Math.Max(0.04f, (ease - 0.5f) * 2.0f);
                GraphicsState flapState = g.Save();
                g.TranslateTransform(0f, midY);
                g.ScaleTransform(1.0f, flapScaleY);
                g.TranslateTransform(0f, -midY);

                int shadow = -(int)((1.0f - flapScaleY) * 130f);
                DrawCardHalf(topHalfRect, isIncoming: true, shadeDelta: shadow);
                g.Restore(flapState);
            }
        }

        // Mechanical center split-flap hinge line
        using Pen hingePen = new Pen(Color.FromArgb(190, 10, 10, 14), Math.Max(1.5f, 1.8f * scale));
        g.DrawLine(hingePen, heroRect.Left + 1f, midY, heroRect.Right - 1f, midY);
    }

    // =========================================================================
    // COMPACT STYLE 2: DIRECTIONAL SWOOSH (INVERTED DIRECTION)
    // =========================================================================
    private void DrawCompactSwooshHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 1.0f - MathF.Pow(1.0f - t, 3f);
        float radius = GetWin11CornerRadius(scale);

        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        using GraphicsPath clipPath = Win11CardPanel.CreateRoundRectF(heroRect, radius);
        using SolidBrush baseFill = new SolidBrush(BackdropCharcoal);
        g.FillPath(baseFill, clipPath);

        GraphicsState st = g.Save();
        g.SetClip(clipPath, CombineMode.Intersect);

        float travel = heroRect.Width * 0.92f;
        // Inverted direction: Next (+1) swooshes from Left -> Right
        float outShiftX = _compactTransitionDir * travel * ease;
        float inShiftX = -_compactTransitionDir * travel * (1.0f - ease);

        int outAlpha = (int)Math.Clamp((1.0f - ease) * 255f, 0f, 255f);
        if (outAlpha > 8)
        {
            RectangleF outRect = new RectangleF(heroRect.X + outShiftX, heroRect.Y, heroRect.Width, heroRect.Height);
            using GraphicsPath outPath = Win11CardPanel.CreateRoundRectF(outRect, radius);
            FillRoundedCardGradient(g, outPath, outRect, Color.FromArgb(outAlpha, oldLeftCol), Color.FromArgb(outAlpha, oldRightCol));
            DrawActiveHUDContent(
                g, outRect,
                _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                _app1Vol, _app2Vol, scale, 1.0f, outAlpha);
        }

        int inAlpha = (int)Math.Clamp(ease * 255f, 0f, 255f);
        if (inAlpha > 8)
        {
            RectangleF inRect = new RectangleF(heroRect.X + inShiftX, heroRect.Y, heroRect.Width, heroRect.Height);
            using GraphicsPath inPath = Win11CardPanel.CreateRoundRectF(inRect, radius);
            FillRoundedCardGradient(g, inPath, inRect, Color.FromArgb(inAlpha, _renderedBgLeft), Color.FromArgb(inAlpha, _renderedBgRight));
            DrawActiveHUDContent(g, inRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f, inAlpha);
        }

        g.Restore(st);

        using Pen borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawPath(borderPen, clipPath);
    }

    // =========================================================================
    // COMPACT STYLE 3: LASER WIPE REVEAL (INVERTED DIRECTION)
    // =========================================================================
    private void DrawCompactLaserWipeHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        float ease = 0.5f - 0.5f * MathF.Cos(t * MathF.PI);
        float radius = GetWin11CornerRadius(scale);

        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        using GraphicsPath cardPath = Win11CardPanel.CreateRoundRectF(heroRect, radius);

        // Inverted direction: Next (+1) sweeps Left -> Right; Previous (-1) sweeps Right -> Left
        float lineX = _compactTransitionDir >= 0
            ? heroRect.Left + heroRect.Width * ease
            : heroRect.Right - heroRect.Width * ease;

        RectangleF newRegion = _compactTransitionDir >= 0
            ? new RectangleF(heroRect.Left - 2f, heroRect.Top - 2f, (lineX - heroRect.Left) + 2f, heroRect.Height + 4f)
            : new RectangleF(lineX, heroRect.Top - 2f, (heroRect.Right - lineX) + 2f, heroRect.Height + 4f);

        RectangleF oldRegion = _compactTransitionDir >= 0
            ? new RectangleF(lineX, heroRect.Top - 2f, (heroRect.Right - lineX) + 2f, heroRect.Height + 4f)
            : new RectangleF(heroRect.Left - 2f, heroRect.Top - 2f, (lineX - heroRect.Left) + 2f, heroRect.Height + 4f);

        if (oldRegion.Width > 1f)
        {
            GraphicsState stOld = g.Save();
            g.SetClip(cardPath, CombineMode.Intersect);
            g.SetClip(oldRegion, CombineMode.Intersect);
            FillRoundedCardGradient(g, cardPath, heroRect, oldLeftCol, oldRightCol);
            DrawActiveHUDContent(
                g, heroRect,
                _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                _app1Vol, _app2Vol, scale, 1.0f);
            g.Restore(stOld);
        }

        if (newRegion.Width > 1f)
        {
            GraphicsState stNew = g.Save();
            g.SetClip(cardPath, CombineMode.Intersect);
            g.SetClip(newRegion, CombineMode.Intersect);
            FillRoundedCardGradient(g, cardPath, heroRect, _renderedBgLeft, _renderedBgRight);
            DrawActiveHUDContent(g, heroRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f);
            g.Restore(stNew);
        }

        GraphicsState stLaser = g.Save();
        g.SetClip(cardPath, CombineMode.Intersect);
        Color accentGlow = ParseOverlayColor(_settings.UnmutedColor, Color.FromArgb(0, 195, 255));
        for (int w = 3; w >= 1; w--)
        {
            using Pen glowPen = new Pen(Color.FromArgb(75 / w, accentGlow), w * 3.2f * scale);
            g.DrawLine(glowPen, lineX, heroRect.Top, lineX, heroRect.Bottom);
        }
        using (Pen corePen = new Pen(Color.FromArgb(245, 255, 255, 255), 2.0f * scale))
        {
            g.DrawLine(corePen, lineX, heroRect.Top, lineX, heroRect.Bottom);
        }
        g.Restore(stLaser);

        using Pen borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawPath(borderPen, cardPath);
    }

    // =========================================================================
    // COMPACT STYLE 4: SQUASH & STRETCH PUSH (CLIPPED TO HERO BOX + INVERTED DIR)
    // =========================================================================
    private void DrawCompactSquashStretchHero(Graphics g, RectangleF heroRect, int baseH, float scale)
    {
        float t = 1.0f - Math.Clamp(_compactTransitionProgress, 0f, 1f);
        // Smooth cosine ease-in-out reaches 1.000f smoothly with zero snap at the end
        float ease = 0.5f - 0.5f * MathF.Cos(t * MathF.PI);
        float radius = GetWin11CornerRadius(scale);

        var (oldLeftCol, oldRightCol) = ComputeItemHeroColors(
            _outgoingCompactItem.Color1Hex,
            _outgoingCompactItem.Color2Hex,
            _outgoingCompactItem.IsSingleSource);

        float totalW = heroRect.Width;
        float inRatio = Math.Clamp(ease, 0.001f, 1.0f);
        float outRatio = Math.Clamp(1.0f - inRatio, 0.001f, 1.0f);

        float splitX = _compactTransitionDir < 0
            ? heroRect.Left + totalW * outRatio
            : heroRect.Left + totalW * inRatio;

        RectangleF leftSlice = new RectangleF(heroRect.Left, heroRect.Y, splitX - heroRect.Left, heroRect.Height);
        RectangleF rightSlice = new RectangleF(splitX, heroRect.Y, heroRect.Right - splitX, heroRect.Height);

        RectangleF inRect = _compactTransitionDir < 0 ? rightSlice : leftSlice;
        RectangleF outRect = _compactTransitionDir < 0 ? leftSlice : rightSlice;

        using GraphicsPath outerClipPath = Win11CardPanel.CreateRoundRectF(heroRect, radius);
        using (SolidBrush backdropBrush = new SolidBrush(BackdropCharcoal))
        {
            g.FillPath(backdropBrush, outerClipPath);
        }

        GraphicsState outerClipState = g.Save();
        g.SetClip(outerClipPath, CombineMode.Intersect);

        void DrawSquashedCard(RectangleF targetBox, float scaleX, bool isIncoming, int alpha)
        {
            if (targetBox.Width < 0.5f || scaleX < 0.005f || alpha < 4) return;

            GraphicsState st = g.Save();
            g.SetClip(targetBox, CombineMode.Intersect);

            float pivotX = targetBox.X;
            float centerY = heroRect.Y + heroRect.Height / 2f;

            g.TranslateTransform(pivotX, centerY);
            g.ScaleTransform(scaleX, 1.0f);
            g.TranslateTransform(-heroRect.X, -centerY);

            if (isIncoming)
            {
                FillRoundedCardGradient(g, outerClipPath, heroRect, Color.FromArgb(alpha, _renderedBgLeft), Color.FromArgb(alpha, _renderedBgRight));
                DrawActiveHUDContent(g, heroRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f, alpha);
            }
            else
            {
                FillRoundedCardGradient(g, outerClipPath, heroRect, Color.FromArgb(alpha, oldLeftCol), Color.FromArgb(alpha, oldRightCol));
                DrawActiveHUDContent(
                    g, heroRect,
                    _outgoingCompactItem.Label1, _outgoingCompactItem.Label2,
                    _outgoingCompactItem.RawSource1, _outgoingCompactItem.RawSource2,
                    _app1Vol, _app2Vol, scale, 1.0f, alpha);
            }

            g.Restore(st);
        }

        int outAlpha = (int)Math.Clamp((1.0f - ease * 0.65f) * 255f, 0f, 255f);
        if (outRatio > 0.005f)
        {
            DrawSquashedCard(outRect, outRatio, isIncoming: false, outAlpha);
        }
        DrawSquashedCard(inRect, inRatio, isIncoming: true, 255);

        // Crisp moving seam line where the incoming card pushes against the outgoing card
        if (ease > 0.02f && ease < 0.98f)
        {
            int seamAlpha = (int)(MathF.Sin(ease * MathF.PI) * 210f);
            using Pen seamPen = new Pen(Color.FromArgb(seamAlpha, 255, 255, 255), 1.6f * scale);
            g.DrawLine(seamPen, splitX, heroRect.Top + 1f, splitX, heroRect.Bottom - 1f);
        }

        g.Restore(outerClipState);

        using Pen borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
        g.DrawPath(borderPen, outerClipPath);
    }

    // =========================================================================
    // SHARED HELPERS & WIDE CAROUSEL STYLES
    // =========================================================================
    private void EnsureTopMost()
    {
        if (!this.IsDisposed && this.IsHandleCreated)
        {
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        UpdateFixedCanvasGeometry();
        EnsureTopMost();
        RenderLayeredOverlay();
    }

    public void UpdateDynamicOverlay(
        string label1,
        string label2,
        string rawSource1,
        string rawSource2,
        string color1Hex,
        string color2Hex,
        float app1Vol,
        float app2Vol,
        int muteState,
        bool forceShow,
        bool suppressCarousel = false)
    {
        if (this.IsDisposed || !this.IsHandleCreated) return;

        this.BeginInvoke(new Action(() =>
        {
            _app1Label = label1;
            _app2Label = label2;
            _rawSource1 = rawSource1;
            _rawSource2 = rawSource2;
            _color1Hex = color1Hex;
            _color2Hex = color2Hex;
            _app1Vol = app1Vol;
            _app2Vol = app2Vol;
            _muteState = muteState;
            _suppressCarousel = suppressCarousel;

            if (suppressCarousel)
            {
                _cycleCountdownMs = 0;
                _wheelExpansion = 0f;
                _wheelScrollOffset = 0f;
                _compactTransitionProgress = 0f;
            }

            if (forceShow)
            {
                _displayCountdownMs = _totalDurationMs;
                _isFadingOut = false;
                UpdateFixedCanvasGeometry();
                EnsureTopMost();
                if (_fadeTimer != null && !_fadeTimer.Enabled)
                {
                    _fadeTimer.Start();
                }
            }
            if (_masterOpacity > 0.0)
            {
                RenderLayeredOverlay();
            }
        }));
    }

    private static void DrawImageWithAlpha(Graphics g, Bitmap image, RectangleF destRect, float opacity)
    {
        opacity = Math.Clamp(opacity, 0f, 1f);
        if (opacity <= 0.01f) return;

        if (opacity >= 0.99f)
        {
            g.DrawImage(image, destRect);
            return;
        }

        ColorMatrix matrix = new ColorMatrix
        {
            Matrix33 = opacity
        };
        using ImageAttributes attrs = new ImageAttributes();
        attrs.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

        PointF[] destPoints = new PointF[]
        {
            new PointF(destRect.Left, destRect.Top),
            new PointF(destRect.Right, destRect.Top),
            new PointF(destRect.Left, destRect.Bottom)
        };
        g.DrawImage(image, destPoints, new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel, attrs);
    }

    private void DrawHeaderBadge(
        Graphics g,
        RectangleF bounds,
        string label,
        string rawSource,
        float vol,
        bool isMuted,
        float scale,
        bool showName,
        bool showIcon,
        bool showInlinePercent,
        StringAlignment horizontalAlign,
        Color textColorOverride)
    {
        Bitmap? icon = showIcon ? AppIconCache.GetIconForSource(rawSource, _settings) : null;
        float iconPx = icon != null ? Math.Clamp(20f * scale, 12f, 36f) : 0f;

        float fontSize = Math.Clamp(11.5f * scale, 6.5f, 28f);
        using Font font = new Font("Segoe UI", fontSize, FontStyle.Bold);

        string namePart = showName ? label : "";
        string pctText = isMuted ? "0%" : $"{(int)Math.Round(vol * 100)}%";

        float iconGap = (icon != null && (!string.IsNullOrEmpty(namePart) || showInlinePercent)) ? (6f * scale) : 0f;
        float pctGap = (!string.IsNullOrEmpty(namePart) && showInlinePercent) ? (6f * scale) : 0f;

        float centerY = bounds.Y + bounds.Height / 2f;
        float iconOpacity = textColorOverride.A / 255f;

        using SolidBrush textBrush = new SolidBrush(textColorOverride);
        using StringFormat sfRight = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        using StringFormat sfLeft = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };

        if (horizontalAlign == StringAlignment.Far)
        {
            float curRight = bounds.Right;

            if (icon != null)
            {
                DrawImageWithAlpha(g, icon, new RectangleF(curRight - iconPx, centerY - iconPx / 2f, iconPx, iconPx), iconOpacity);
                curRight -= (iconPx + iconGap);
            }

            if (!string.IsNullOrEmpty(namePart))
            {
                SizeF nameSize = g.MeasureString(namePart, font);
                float nameBoxW = Math.Min(nameSize.Width + 4f, Math.Max(10f, curRight - bounds.X));
                RectangleF nameRect = new RectangleF(curRight - nameBoxW, bounds.Y, nameBoxW, bounds.Height);
                DrawReadableString(g, namePart, font, textColorOverride, nameRect, sfRight);
                curRight -= (nameSize.Width + pctGap);
            }

            if (showInlinePercent && curRight > bounds.X + 8f)
            {
                RectangleF pctRect = new RectangleF(bounds.X, bounds.Y, curRight - bounds.X, bounds.Height);
                DrawReadableString(g, pctText, font, textColorOverride, pctRect, sfRight);
            }
        }
        else
        {
            float curLeft = bounds.X;

            if (icon != null)
            {
                DrawImageWithAlpha(g, icon, new RectangleF(curLeft, centerY - iconPx / 2f, iconPx, iconPx), iconOpacity);
                curLeft += (iconPx + iconGap);
            }

            if (!string.IsNullOrEmpty(namePart))
            {
                SizeF nameSize = g.MeasureString(namePart, font);
                float nameBoxW = Math.Min(nameSize.Width + 4f, Math.Max(10f, bounds.Right - curLeft));
                RectangleF nameRect = new RectangleF(curLeft, bounds.Y, nameBoxW, bounds.Height);
                DrawReadableString(g, namePart, font, textColorOverride, nameRect, sfLeft);
                curLeft += (nameSize.Width + pctGap);
            }

            if (showInlinePercent && bounds.Right > curLeft + 8f)
            {
                RectangleF pctRect = new RectangleF(curLeft, bounds.Y, bounds.Right - curLeft, bounds.Height);
                DrawReadableString(g, pctText, font, textColorOverride, pctRect, sfLeft);
            }
        }
    }

    private static int GetSnappedFillWidth(int barWidth, float vol)
    {
        if (vol <= 0.01f) return 0;
        if (vol >= 0.985f) return barWidth;
        return Math.Clamp((int)Math.Round(barWidth * vol), 0, barWidth);
    }

    private static Color ParseOverlayColor(string hex, Color defaultColor)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex))
                return ColorTranslator.FromHtml(hex);
        }
        catch { }
        return defaultColor;
    }

    private void FillRoundedCardGradient(Graphics g, GraphicsPath cardPath, RectangleF bounds, Color leftColor, Color rightColor)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (leftColor.ToArgb() == rightColor.ToArgb())
        {
            using SolidBrush sb = new SolidBrush(leftColor);
            g.FillPath(sb, cardPath);
            return;
        }

        using LinearGradientBrush gradBrush = new LinearGradientBrush(
            new PointF(bounds.Left, bounds.Top),
            new PointF(bounds.Right, bounds.Top),
            leftColor,
            rightColor);

        ColorBlend blend = new ColorBlend
        {
            Colors = new[] { leftColor, leftColor, rightColor, rightColor },
            Positions = new[] { 0.0f, 0.22f, 0.78f, 1.0f }
        };
        gradBrush.InterpolationColors = blend;
        g.FillPath(gradBrush, cardPath);
    }

    private void DrawActiveHUDContent(
        Graphics g,
        RectangleF activeBounds,
        string label1,
        string label2,
        string rawSource1,
        string rawSource2,
        float vol1,
        float vol2,
        float scale,
        float activeHudEmphasis,
        int cardAlpha = 255)
    {
        int width = (int)activeBounds.Width;
        int height = (int)activeBounds.Height;
        float topY = activeBounds.Y;
        int centerX = (int)(activeBounds.X + width / 2f);

        bool showName = _settings.OverlayIdentityMode == "Name" || _settings.OverlayIdentityMode == "Both";
        bool showIcon = _settings.OverlayIdentityMode == "Icon" || _settings.OverlayIdentityMode == "Both";
        bool showBar = (_settings.OverlayLevelMode == "Bar" || _settings.OverlayLevelMode == "Both") && activeHudEmphasis > 0.02f;
        bool showPct = (_settings.OverlayLevelMode == "Percentage" || _settings.OverlayLevelMode == "Both") && activeHudEmphasis > 0.45f;

        Color mutedColor = ParseOverlayColor(_settings.MutedColor, Color.FromArgb(255, 71, 87));

        // --- SINGLE SOURCE MODE ---
        if (string.IsNullOrEmpty(rawSource2))
        {
            bool singleMuted = (_muteState != 0) && activeHudEmphasis > 0.5f;
            Color singleTextColor = Color.FromArgb(cardAlpha, singleMuted ? mutedColor : Color.White);
            int singleSideMargin = (int)Math.Round(16f + 6f * activeHudEmphasis);

            float singleHeaderYOffset = Math.Clamp(activeHudEmphasis, 0f, 1f);
            float singleHeaderHeight = height * (0.48f * singleHeaderYOffset + 0.88f * (1f - singleHeaderYOffset));
            float singleHeaderTop = topY + (6f * scale * singleHeaderYOffset) + ((height - singleHeaderHeight) / 2f * (1f - singleHeaderYOffset));
            float singleContentScale = scale * (0.80f + 0.20f * singleHeaderYOffset);

            RectangleF fullHeader = new RectangleF(activeBounds.X + singleSideMargin, singleHeaderTop, width - (singleSideMargin * 2), singleHeaderHeight);

            Bitmap? icon = showIcon ? AppIconCache.GetIconForSource(rawSource1, _settings) : null;
            float iconPx = icon != null ? Math.Clamp(20f * singleContentScale, 12f, 36f) : 0f;
            float iconGap = (icon != null && (showName || (showPct && showBar))) ? (6f * singleContentScale) : 0f;
            float pctGap = (showName && (showPct && showBar)) ? (8f * singleContentScale) : 0f;

            float fontSize = Math.Clamp(11.5f * singleContentScale, 6.5f, 28f);
            using Font font = new Font("Segoe UI", fontSize, FontStyle.Bold);

            string namePart = showName ? label1 : "";
            string pctText = singleMuted ? "0%" : $"{(int)Math.Round(vol1 * 100)}%";

            SizeF nameSize = !string.IsNullOrEmpty(namePart) ? g.MeasureString(namePart, font) : SizeF.Empty;
            SizeF pctSize = (showPct && showBar) ? g.MeasureString(pctText, font) : SizeF.Empty;

            float fixedPctBoxW = (showPct && showBar) ? Math.Max(pctSize.Width + 4f, 36f * singleContentScale) : 0f;
            float totalGroupW = iconPx + (iconPx > 0 ? iconGap : 0f) + nameSize.Width + (nameSize.Width > 0 && fixedPctBoxW > 0 ? pctGap : 0f) + fixedPctBoxW;

            float startX = fullHeader.X + (fullHeader.Width - totalGroupW) / 2f;
            float curX = startX;
            float centerY = fullHeader.Y + fullHeader.Height / 2f;
            float iconOpacity = singleTextColor.A / 255f;

            if (icon != null)
            {
                DrawImageWithAlpha(g, icon, new RectangleF(curX, centerY - iconPx / 2f, iconPx, iconPx), iconOpacity);
                curX += iconPx + iconGap;
            }

            using SolidBrush textBrush = new SolidBrush(singleTextColor);
            using StringFormat sfLeft = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };

            if (!string.IsNullOrEmpty(namePart))
            {
                RectangleF nameRect = new RectangleF(curX, fullHeader.Y, nameSize.Width + 4f, fullHeader.Height);
                DrawReadableString(g, namePart, font, singleTextColor, nameRect, sfLeft);
                curX += nameSize.Width + (fixedPctBoxW > 0 ? pctGap : 0f);
            }

            if (showPct && showBar)
            {
                RectangleF pctRect = new RectangleF(curX, fullHeader.Y, fixedPctBoxW, fullHeader.Height);
                using StringFormat sfPctBox = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.None,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                DrawReadableString(g, pctText, font, singleTextColor, pctRect, sfPctBox);
            }

            if (showBar)
            {
                float barFade = Math.Clamp((activeHudEmphasis - 0.15f) / 0.85f, 0f, 1f);
                if (barFade > 0.01f)
                {
                    int barAlpha = (int)Math.Clamp(barFade * cardAlpha, 0f, 255f);
                    int trackAlpha = (int)Math.Clamp(barFade * (cardAlpha / 255f) * 135f, 0f, 135f);
                    int barWidth = Math.Max(40, width - (singleSideMargin * 2));
                    int barHeight = Math.Clamp((int)(14 * scale * (0.75f + 0.25f * activeHudEmphasis)), 6, 40);
                    int barY = (int)topY + Math.Clamp((int)(height * 0.60f), 8, Math.Max(8, height - barHeight - 5));
                    int barX = (int)activeBounds.X + singleSideMargin;

                    Color baseBarCol = singleMuted ? mutedColor : ParseOverlayColor(_settings.UnmutedColor, Color.FromArgb(0, 168, 255));
                    using Brush barBrush = new SolidBrush(Color.FromArgb(barAlpha, baseBarCol));
                    using Brush trackBrush = new SolidBrush(Color.FromArgb(trackAlpha, 12, 12, 15));

                    using (GraphicsPath? trackPath = GetSafeRoundedBarPath(barX, barY, barWidth, barHeight, true, true))
                    {
                        if (trackPath != null) g.FillPath(trackBrush, trackPath);
                    }

                    int fillWidth = GetSnappedFillWidth(barWidth, vol1);
                    if (fillWidth > 0)
                    {
                        using (GraphicsPath? fillPath = GetSafeRoundedBarPath(barX, barY, fillWidth, barHeight, true, true))
                        {
                            if (fillPath != null) g.FillPath(barBrush, fillPath);
                        }
                    }
                }
            }
            else if (showPct)
            {
                int pctAlpha = (int)Math.Clamp(activeHudEmphasis * cardAlpha, 0f, 255f);
                string p1 = singleMuted ? "MUTED" : $"{(int)Math.Round(vol1 * 100)}%";
                Color unmutedCol = ParseOverlayColor(_settings.UnmutedColor, Color.FromArgb(0, 168, 255));
                using Font bigFont = new Font("Segoe UI", Math.Clamp(16f * singleContentScale, 9f, 40f), FontStyle.Bold);
                using StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                DrawReadableString(g, p1, bigFont, Color.FromArgb(pctAlpha, singleMuted ? mutedColor : unmutedCol),
                    new RectangleF(activeBounds.X + singleSideMargin, topY + height * 0.44f, width - (singleSideMargin * 2), height * 0.50f), sf);
            }
            return;
        }

        // --- TWO-SOURCE CROSSFADE MODE ---
        int divAlpha = (int)Math.Clamp(cardAlpha * 0.55f, 0, 140);
        int centerLineTop = (int)topY + (int)(height * 0.16f);
        int centerLineHeight = Math.Max(8, height - (int)(height * 0.32f));
        using (SolidBrush dividerBrush = new SolidBrush(Color.FromArgb(divAlpha, 235, 235, 240)))
        {
            g.FillRectangle(dividerBrush, centerX - 1, centerLineTop, 2, centerLineHeight);
        }

        bool app1Muted = (_muteState == 2 || _muteState == 3) && activeHudEmphasis > 0.5f;
        bool app2Muted = (_muteState == 1 || _muteState == 3) && activeHudEmphasis > 0.5f;
        Color text1Color = Color.FromArgb(cardAlpha, app1Muted ? mutedColor : Color.White);
        Color text2Color = Color.FromArgb(cardAlpha, app2Muted ? mutedColor : Color.White);
        int sideMargin = (int)Math.Round(12f + 6f * activeHudEmphasis);

        float headerYOffset = Math.Clamp(activeHudEmphasis, 0f, 1f);
        float headerHeight = height * (0.48f * headerYOffset + 0.88f * (1f - headerYOffset));
        float headerTop = topY + (6f * scale * headerYOffset) + ((height - headerHeight) / 2f * (1f - headerYOffset));

        float contentScale = scale * (0.80f + 0.20f * headerYOffset);

        float halfW = width / 2f;
        RectangleF leftHeader = new RectangleF(activeBounds.X + sideMargin, headerTop, halfW - sideMargin - 10, headerHeight);
        RectangleF rightHeader = new RectangleF(centerX + 10, headerTop, halfW - sideMargin - 10, headerHeight);

        DrawHeaderBadge(g, leftHeader, label1, rawSource1, vol1, app1Muted, contentScale, showName, showIcon, showPct && showBar, StringAlignment.Far, text1Color);
        DrawHeaderBadge(g, rightHeader, label2, rawSource2, vol2, app2Muted, contentScale, showName, showIcon, showPct && showBar, StringAlignment.Near, text2Color);

        if (showBar)
        {
            float barFade = Math.Clamp((activeHudEmphasis - 0.15f) / 0.85f, 0f, 1f);
            if (barFade > 0.01f)
            {
                int barAlpha = (int)Math.Clamp(barFade * cardAlpha, 0f, 255f);
                int trackAlpha = (int)Math.Clamp(barFade * (cardAlpha / 255f) * 135f, 0f, 135f);

                int barWidth = Math.Max(20, (int)halfW - sideMargin - 8);
                int barHeight = Math.Clamp((int)(14 * scale * (0.75f + 0.25f * activeHudEmphasis)), 6, 40);
                int barY = (int)topY + Math.Clamp((int)(height * 0.60f), 8, Math.Max(8, height - barHeight - 5));

                Color baseBarCol1 = app1Muted ? mutedColor : ParseOverlayColor(_settings.UnmutedColor, Color.FromArgb(0, 168, 255));
                Color baseBarCol2 = app2Muted ? mutedColor : ParseOverlayColor(_settings.UnmutedColor, Color.FromArgb(0, 168, 255));
                using Brush app1BarColor = new SolidBrush(Color.FromArgb(barAlpha, baseBarCol1));
                using Brush app2BarColor = new SolidBrush(Color.FromArgb(barAlpha, baseBarCol2));
                using Brush trackBgBrush = new SolidBrush(Color.FromArgb(trackAlpha, 12, 12, 15));

                using (GraphicsPath? app1TrackPath = GetSafeRoundedBarPath(centerX - barWidth, barY, barWidth, barHeight, true, false))
                {
                    if (app1TrackPath != null) g.FillPath(trackBgBrush, app1TrackPath);
                }
                using (GraphicsPath? app2TrackPath = GetSafeRoundedBarPath(centerX, barY, barWidth, barHeight, false, true))
                {
                    if (app2TrackPath != null) g.FillPath(trackBgBrush, app2TrackPath);
                }

                int app1FillWidth = GetSnappedFillWidth(barWidth, vol1);
                if (app1FillWidth > 0)
                {
                    using (GraphicsPath? app1FillPath = GetSafeRoundedBarPath(centerX - app1FillWidth, barY, app1FillWidth, barHeight, true, false))
                    {
                        if (app1FillPath != null) g.FillPath(app1BarColor, app1FillPath);
                    }
                }

                int app2FillWidth = GetSnappedFillWidth(barWidth, vol2);
                if (app2FillWidth > 0)
                {
                    using (GraphicsPath? app2FillPath = GetSafeRoundedBarPath(centerX, barY, app2FillWidth, barHeight, false, true))
                    {
                        if (app2FillPath != null) g.FillPath(app2BarColor, app2FillPath);
                    }
                }
            }
        }
        else if (showPct)
        {
            int pctAlpha = (int)Math.Clamp(activeHudEmphasis * cardAlpha, 0f, 255f);
            string p1 = app1Muted ? "MUTED" : $"{(int)Math.Round(vol1 * 100)}%";
            string p2 = app2Muted ? "MUTED" : $"{(int)Math.Round(vol2 * 100)}%";

            Color unmutedCol = ParseOverlayColor(_settings.UnmutedColor, Color.FromArgb(0, 168, 255));
            using Font bigFont = new Font("Segoe UI", Math.Clamp(16f * contentScale, 9f, 40f), FontStyle.Bold);
            using StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            DrawReadableString(g, p1, bigFont, Color.FromArgb(pctAlpha, app1Muted ? mutedColor : unmutedCol), new RectangleF(activeBounds.X + sideMargin, topY + height * 0.44f, halfW - sideMargin - 6, height * 0.50f), sf);
            DrawReadableString(g, p2, bigFont, Color.FromArgb(pctAlpha, app2Muted ? mutedColor : unmutedCol), new RectangleF(centerX + 6, topY + height * 0.44f, halfW - sideMargin - 6, height * 0.50f), sf);
        }
    }

    private RectangleF ApplyAlwaysPreviewBlendIfActive(RectangleF fullCarouselRect, float signedVisualOffset, RectangleF heroCardRect, float scale)
    {
        bool alwaysPreviews = string.Equals(_settings.NeighborPreviewVisibility, "Always", StringComparison.OrdinalIgnoreCase);
        float dist = Math.Abs(signedVisualOffset);
        if (!alwaysPreviews || dist < 0.05f)
            return fullCarouselRect;

        var (topBox, bottomBox) = GetCompactTopBottomPreviewBoxRects(heroCardRect, scale);
        RectangleF previewRect = signedVisualOffset < 0 ? topBox : bottomBox;

        // Outer ±2 cards tuck slightly further behind the ±1 preview bar when collapsed
        if (dist > 1.35f)
        {
            float extraTuck = (dist - 1.0f) * 3f * scale;
            previewRect = new RectangleF(
                previewRect.X + 6f * scale,
                previewRect.Y + (signedVisualOffset < 0 ? extraTuck : -extraTuck),
                previewRect.Width - 12f * scale,
                previewRect.Height);
        }

        return LerpRectF(previewRect, fullCarouselRect, Math.Clamp(_wheelExpansion, 0f, 1f));
    }

    private void DrawFloatingCarouselPill(
        Graphics g,
        CycleDisplayItem item,
        float signedVisualOffset,
        RectangleF heroCardRect,
        int baseH,
        float scale)
    {
        float dist = Math.Abs(signedVisualOffset);
        if (dist < 0.15f || dist > 2.35f) return;

        float pillWidthRatio = Math.Clamp(0.89f - (dist - 1.0f) * 0.10f, 0.74f, 0.92f);
        float pillW = heroCardRect.Width * pillWidthRatio;
        float pillH = Math.Clamp(baseH * 0.50f, 28f, 64f);
        float pillX = heroCardRect.X + (heroCardRect.Width - pillW) / 2f;

        float heroCenterY = heroCardRect.Y + heroCardRect.Height / 2f;
        float minOrbitDist = (heroCardRect.Height / 2f) + (pillH / 2f) + (6f * scale);
        float orbitSpacing = minOrbitDist + (dist - 1.0f) * (pillH + 5f * scale);

        bool alwaysPreviews = string.Equals(_settings.NeighborPreviewVisibility, "Always", StringComparison.OrdinalIgnoreCase);
        float orbitExpansion = alwaysPreviews ? 1.0f : _wheelExpansion;
        float cardCenterY = heroCenterY + Math.Sign(signedVisualOffset) * (orbitSpacing * orbitExpansion);

        float pillCenterY = heroCenterY + Math.Sign(signedVisualOffset) * (orbitSpacing * _wheelExpansion);
        RectangleF fullPillRect = new RectangleF(pillX, pillCenterY - pillH / 2f, pillW, pillH);
        RectangleF pillRect = ApplyAlwaysPreviewBlendIfActive(fullPillRect, signedVisualOffset, heroCardRect, scale);

        if (pillRect.Bottom < 0 || pillRect.Top > this.Height) return;

        float baseVis = (alwaysPreviews && dist <= 1.25f) ? Math.Max(_wheelExpansion, 0.92f) : _wheelExpansion;
        float visibility = Math.Clamp((1.0f - Math.Max(0f, dist - 1.0f) * 0.55f) * baseVis, 0f, 1f);
        int cardAlpha = (int)Math.Clamp(visibility * 235f, 0f, 235f);
        if (cardAlpha < 12) return;

        Color rawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120));
        Color rawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85));

        Color surfaceBase = Color.FromArgb(30, 31, 36);
        Color tintLeft = _settings.UseSourceBackgroundColors ? ColorLerp(surfaceBase, rawC1, 0.48f) : surfaceBase;
        Color tintRight = _settings.UseSourceBackgroundColors ? ColorLerp(surfaceBase, rawC2, 0.48f) : surfaceBase;

        Color finalLeft = Color.FromArgb(cardAlpha, tintLeft);
        Color finalRight = item.IsSingleSource ? finalLeft : Color.FromArgb(cardAlpha, tintRight);

        float radius = Math.Min(pillRect.Height / 2f, 12f * scale);
        using GraphicsPath pillPath = Win11CardPanel.CreateRoundRectF(pillRect, radius);
        FillRoundedCardGradient(g, pillPath, pillRect, finalLeft, finalRight);

        using (Pen rimPen = new Pen(Color.FromArgb((int)(cardAlpha * 0.45f), 255, 255, 255), 1f))
        {
            g.DrawPath(rimPen, pillPath);
        }

        int textAlpha = (int)Math.Clamp(visibility * 240f, 0f, 240f);
        bool showName = _settings.OverlayIdentityMode == "Name" || _settings.OverlayIdentityMode == "Both";
        bool showIcon = _settings.OverlayIdentityMode == "Icon" || _settings.OverlayIdentityMode == "Both";

        if (item.IsSingleSource)
        {
            RectangleF centerZone = new RectangleF(pillRect.X + 14f * scale, pillRect.Y + 2f, pillRect.Width - 28f * scale, pillRect.Height - 4f);
            DrawCenteredPillIdentity(g, centerZone, item.Label1, item.RawSource1, textAlpha, scale * 0.85f, showName, showIcon);
            return;
        }

        float centerX = pillRect.X + pillRect.Width / 2f;
        float centerY = pillRect.Y + pillRect.Height / 2f;

        using Font arrowFont = new Font("Segoe UI Symbol", Math.Clamp(9.0f * scale, 7f, 16f), FontStyle.Bold);
        using SolidBrush arrowBrush = new SolidBrush(Color.FromArgb((int)(textAlpha * 0.65f), 200, 210, 225));
        using StringFormat centerSf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("⇄", arrowFont, arrowBrush, new PointF(centerX, centerY), centerSf);

        RectangleF leftZone = new RectangleF(pillRect.X + 12f * scale, pillRect.Y + 2f, (pillRect.Width / 2f) - 24f * scale, pillRect.Height - 4f);
        RectangleF rightZone = new RectangleF(centerX + 12f * scale, pillRect.Y + 2f, (pillRect.Width / 2f) - 24f * scale, pillRect.Height - 4f);

        DrawPillSideIdentity(g, leftZone, item.Label1, item.RawSource1, textAlpha, scale * 0.85f, showName, showIcon, isRightSide: false);
        DrawPillSideIdentity(g, rightZone, item.Label2, item.RawSource2, textAlpha, scale * 0.85f, showName, showIcon, isRightSide: true);
    }

    private void DrawPillSideIdentity(
        Graphics g,
        RectangleF zone,
        string label,
        string rawSource,
        int alpha,
        float scale,
        bool showName,
        bool showIcon,
        bool isRightSide)
    {
        if (zone.Width <= 10 || alpha <= 5) return;

        Bitmap? icon = showIcon ? AppIconCache.GetIconForSource(rawSource, _settings) : null;
        int iconPx = icon != null ? (int)Math.Clamp(16 * scale, 12, 24) : 0;
        float gap = 6f * scale;
        float iconOpacity = alpha / 255f;

        using Font font = new Font("Segoe UI Semibold", Math.Clamp(9.5f * scale, 7f, 18f), FontStyle.Bold);
        string text = showName ? label : "";

        float cy = zone.Y + zone.Height / 2f;
        using SolidBrush textBrush = new SolidBrush(Color.FromArgb(alpha, 238, 240, 245));

        if (!isRightSide)
        {
            float curRight = zone.Right;

            if (icon != null)
            {
                DrawImageWithAlpha(g, icon, new RectangleF(curRight - iconPx, cy - iconPx / 2f, iconPx, iconPx), iconOpacity);
                curRight -= (iconPx + gap);
            }

            if (!string.IsNullOrEmpty(text) && curRight > zone.Left + 8)
            {
                RectangleF textRect = new RectangleF(zone.Left, zone.Y, curRight - zone.Left, zone.Height);
                using StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Far,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(text, font, textBrush, textRect, sf);
            }
        }
        else
        {
            float curLeft = zone.Left;

            if (icon != null)
            {
                DrawImageWithAlpha(g, icon, new RectangleF(curLeft, cy - iconPx / 2f, iconPx, iconPx), iconOpacity);
                curLeft += (iconPx + gap);
            }

            if (!string.IsNullOrEmpty(text) && zone.Right > curLeft + 8)
            {
                RectangleF textRect = new RectangleF(curLeft, zone.Y, zone.Right - curLeft, zone.Height);
                using StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(text, font, textBrush, textRect, sf);
            }
        }
    }

    private void DrawFluidDeckCard(
        Graphics g,
        CycleDisplayItem item,
        bool isActiveSlot,
        float signedVisualOffset,
        RectangleF heroCardRect,
        int baseH,
        float scale)
    {
        float dist = Math.Abs(signedVisualOffset);
        if (dist > 2.35f) return;

        float activeBlend = Math.Clamp(1.0f - dist, 0f, 1f);

        float pillH = Math.Clamp(baseH * 0.50f, 28f, 64f);
        float cardH = pillH + (heroCardRect.Height - pillH) * activeBlend;

        float pillWidthRatio = dist <= 1.0f
            ? (1.0f - 0.11f * dist)
            : Math.Clamp(0.89f - (dist - 1.0f) * 0.10f, 0.74f, 0.92f);

        float cardW = heroCardRect.Width * pillWidthRatio;
        float cardX = heroCardRect.X + (heroCardRect.Width - cardW) / 2f;

        float heroCenterY = heroCardRect.Y + heroCardRect.Height / 2f;
        float minOrbitDist = (heroCardRect.Height / 2f) + (pillH / 2f) + (6f * scale);

        float orbitSpacing = dist <= 1.0f
            ? dist * minOrbitDist
            : minOrbitDist + (dist - 1.0f) * (pillH + 5f * scale);

        bool alwaysPreviews = string.Equals(_settings.NeighborPreviewVisibility, "Always", StringComparison.OrdinalIgnoreCase);
        float orbitExpansion = alwaysPreviews ? 1.0f : _wheelExpansion;
        float cardCenterY = heroCenterY + Math.Sign(signedVisualOffset) * (orbitSpacing * orbitExpansion);

        RectangleF fullCardRect = new RectangleF(cardX, cardCenterY - cardH / 2f, cardW, cardH);
        RectangleF cardRect = isActiveSlot
            ? fullCardRect
            : ApplyAlwaysPreviewBlendIfActive(fullCardRect, signedVisualOffset, heroCardRect, scale);

        if (cardRect.Bottom < 0 || cardRect.Top > this.Height) return;

        float baseVis = (alwaysPreviews && dist <= 1.25f) ? Math.Max(_wheelExpansion, 0.92f) : _wheelExpansion;
        float visibility = isActiveSlot
            ? 1.0f
            : Math.Clamp((1.0f - Math.Max(0f, dist - 1.0f) * 0.55f) * baseVis, 0f, 1f);
        int cardAlpha = (int)Math.Clamp(visibility * (235f + 20f * activeBlend), 0f, 255f);
        if (cardAlpha < 10) return;

        Color itemRawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120));
        Color itemRawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85));

        Color surfaceBase = Color.FromArgb(30, 31, 36);
        Color pillLeft = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(surfaceBase, itemRawC1, 0.48f))
            : surfaceBase;
        Color pillRight = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(surfaceBase, itemRawC2, 0.48f))
            : surfaceBase;

        var (activeHeroLeft, activeHeroRight) = ComputeItemHeroColors(item.Color1Hex, item.Color2Hex, item.IsSingleSource);
        Color bgLeft = ColorLerp(pillLeft, activeHeroLeft, activeBlend);
        Color bgRight = ColorLerp(pillRight, activeHeroRight, activeBlend);

        if (item.IsSingleSource) { bgRight = bgLeft; }

        Color finalLeft = Color.FromArgb(cardAlpha, bgLeft);
        Color finalRight = Color.FromArgb(cardAlpha, bgRight);

        float pillRadius = Math.Min(cardRect.Height / 2f, 12f * scale);
        float heroRadius = GetWin11CornerRadius(scale);
        float radius = pillRadius + (heroRadius - pillRadius) * activeBlend;

        using GraphicsPath cardPath = Win11CardPanel.CreateRoundRectF(cardRect, radius);
        FillRoundedCardGradient(g, cardPath, cardRect, finalLeft, finalRight);

        DrawActiveHUDContent(
            g,
            cardRect,
            item.Label1,
            item.Label2,
            item.RawSource1,
            item.RawSource2,
            _app1Vol,
            _app2Vol,
            scale,
            activeBlend,
            cardAlpha);

        int pillRimAlpha = (int)(cardAlpha * 0.45f);
        int heroRimAlpha = 105 + (int)Math.Round(90 * _wheelExpansion);
        int rimAlpha = (int)Math.Clamp(pillRimAlpha + (heroRimAlpha - pillRimAlpha) * activeBlend, 0, 220);

        using Pen rimPen = new Pen(Color.FromArgb(rimAlpha, 255, 255, 255), 1.0f + 0.3f * activeBlend);
        g.DrawPath(rimPen, cardPath);
    }

    private void DrawCoverflowArcCard(
        Graphics g,
        CycleDisplayItem item,
        bool isActiveSlot,
        float signedVisualOffset,
        RectangleF heroCardRect,
        int baseH,
        float scale)
    {
        float dist = Math.Abs(signedVisualOffset);
        if (dist > 2.35f) return;

        float activeBlend = Math.Clamp(1.0f - dist, 0f, 1f);
        float pillH = Math.Clamp(baseH * 0.50f, 28f, 64f);
        float cardH = pillH + (heroCardRect.Height - pillH) * activeBlend;

        float widthRatio = Math.Clamp(1.0f - dist * 0.24f, 0.54f, 1.0f);
        float cardW = heroCardRect.Width * widthRatio;

        float arcSwingX = MathF.Sin(signedVisualOffset * 1.05f) * (58f * scale) * _wheelExpansion;
        float cardX = heroCardRect.X + (heroCardRect.Width - cardW) / 2f + arcSwingX;

        float heroCenterY = heroCardRect.Y + heroCardRect.Height / 2f;
        float minOrbitDist = (heroCardRect.Height / 2f) + (pillH / 2f) + (6f * scale);
        float orbitSpacing = dist <= 1.0f
            ? dist * minOrbitDist
            : minOrbitDist + (dist - 1.0f) * (pillH + 5f * scale);

        bool alwaysPreviews = string.Equals(_settings.NeighborPreviewVisibility, "Always", StringComparison.OrdinalIgnoreCase);
        float orbitExpansion = alwaysPreviews ? 1.0f : _wheelExpansion;
        float cardCenterY = heroCenterY + Math.Sign(signedVisualOffset) * (orbitSpacing * orbitExpansion);

        RectangleF fullCardRect = new RectangleF(cardX, cardCenterY - cardH / 2f, cardW, cardH);
        RectangleF cardRect = isActiveSlot
            ? fullCardRect
            : ApplyAlwaysPreviewBlendIfActive(fullCardRect, signedVisualOffset, heroCardRect, scale);

        if (cardRect.Bottom < 0 || cardRect.Top > this.Height) return;

        float baseVis = (alwaysPreviews && dist <= 1.25f) ? Math.Max(_wheelExpansion, 0.92f) : _wheelExpansion;
        float visibility = isActiveSlot
            ? 1.0f
            : Math.Clamp((1.0f - Math.Max(0f, dist - 1.0f) * 0.52f) * baseVis, 0f, 1f);
        int cardAlpha = (int)Math.Clamp(visibility * (225f + 30f * activeBlend), 0f, 255f);
        if (cardAlpha < 10) return;

        Color itemRawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120));
        Color itemRawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85));
        Color surfaceBase = Color.FromArgb(26, 28, 34);

        Color pillLeft = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(surfaceBase, itemRawC1, 0.50f))
            : surfaceBase;
        Color pillRight = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(surfaceBase, itemRawC2, 0.50f))
            : surfaceBase;

        // Honors volume-based background color dimming on the active card!
        var (activeHeroLeft, activeHeroRight) = ComputeItemHeroColors(item.Color1Hex, item.Color2Hex, item.IsSingleSource);
        Color bgLeft = ColorLerp(pillLeft, activeHeroLeft, activeBlend);
        Color bgRight = ColorLerp(pillRight, activeHeroRight, activeBlend);

        if (item.IsSingleSource) bgRight = bgLeft;

        float radius = GetWin11CornerRadius(scale) * (0.85f + 0.15f * activeBlend);
        using GraphicsPath cardPath = Win11CardPanel.CreateRoundRectF(cardRect, radius);

        using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb((int)(cardAlpha * 0.42f), 0, 0, 0)))
        {
            RectangleF shadowRect = new RectangleF(cardRect.X + 3f, cardRect.Y + 4f, cardRect.Width, cardRect.Height);
            using GraphicsPath shadowPath = Win11CardPanel.CreateRoundRectF(shadowRect, radius);
            g.FillPath(shadowBrush, shadowPath);
        }

        FillRoundedCardGradient(g, cardPath, cardRect, Color.FromArgb(cardAlpha, bgLeft), Color.FromArgb(cardAlpha, bgRight));

        DrawActiveHUDContent(
            g, cardRect,
            item.Label1, item.Label2,
            item.RawSource1, item.RawSource2,
            _app1Vol, _app2Vol,
            scale, activeBlend, cardAlpha);

        int rimAlpha = (int)Math.Clamp(cardAlpha * (0.42f + 0.48f * activeBlend), 0, 235);
        using Pen rimPen = new Pen(Color.FromArgb(rimAlpha, 255, 255, 255), 1.0f + 0.4f * activeBlend);
        g.DrawPath(rimPen, cardPath);
    }

    private void DrawStackedRolodexCard(
        Graphics g,
        CycleDisplayItem item,
        bool isActiveSlot,
        float signedVisualOffset,
        RectangleF heroCardRect,
        int baseH,
        float scale)
    {
        float dist = Math.Abs(signedVisualOffset);
        if (dist > 2.35f) return;

        float activeBlend = Math.Clamp(1.0f - dist, 0f, 1f);

        float cardH = heroCardRect.Height * (0.72f + 0.28f * activeBlend);
        float widthRatio = Math.Clamp(1.0f - dist * 0.09f, 0.76f, 1.0f);
        float cardW = heroCardRect.Width * widthRatio;
        float cardX = heroCardRect.X + (heroCardRect.Width - cardW) / 2f;

        float heroCenterY = heroCardRect.Y + heroCardRect.Height / 2f;
        float peekStep = Math.Clamp(baseH * 0.42f, 22f, 52f);
        float stackedOffset = signedVisualOffset * peekStep * _wheelExpansion;

        float cardCenterY = heroCenterY + stackedOffset;
        RectangleF fullCardRect = new RectangleF(cardX, cardCenterY - cardH / 2f, cardW, cardH);
        RectangleF cardRect = isActiveSlot
            ? fullCardRect
            : ApplyAlwaysPreviewBlendIfActive(fullCardRect, signedVisualOffset, heroCardRect, scale);

        if (cardRect.Bottom < 0 || cardRect.Top > this.Height) return;

        bool alwaysPreviews = string.Equals(_settings.NeighborPreviewVisibility, "Always", StringComparison.OrdinalIgnoreCase);
        float baseVis = (alwaysPreviews && dist <= 1.25f) ? Math.Max(_wheelExpansion, 0.92f) : _wheelExpansion;
        float visibility = isActiveSlot
            ? 1.0f
            : Math.Clamp((1.0f - Math.Max(0f, dist - 1.0f) * 0.50f) * baseVis, 0f, 1f);
        int cardAlpha = (int)Math.Clamp(visibility * (230f + 25f * activeBlend), 0f, 255f);
        if (cardAlpha < 10) return;

        Color itemRawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120));
        Color itemRawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85));
        Color darkDeckBase = Color.FromArgb(20, 21, 25);

        float deckStrength = Math.Clamp(0.38f - (dist * 0.08f), 0.18f, 0.45f);
        Color deckLeft = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(darkDeckBase, itemRawC1, deckStrength))
            : darkDeckBase;
        Color deckRight = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(darkDeckBase, itemRawC2, deckStrength))
            : darkDeckBase;

        // Honors volume-based background color dimming on the active card!
        var (activeHeroLeft, activeHeroRight) = ComputeItemHeroColors(item.Color1Hex, item.Color2Hex, item.IsSingleSource);
        Color bgLeft = ColorLerp(deckLeft, activeHeroLeft, activeBlend);
        Color bgRight = ColorLerp(deckRight, activeHeroRight, activeBlend);

        if (item.IsSingleSource) bgRight = bgLeft;

        float radius = GetWin11CornerRadius(scale);
        using GraphicsPath cardPath = Win11CardPanel.CreateRoundRectF(cardRect, radius);

        if (activeBlend > 0.1f && _wheelExpansion > 0.05f)
        {
            int shadowAlpha = (int)Math.Clamp(110 * activeBlend * _wheelExpansion, 0, 120);
            for (int s = 2; s >= 1; s--)
            {
                RectangleF shadowRect = RectangleF.Inflate(cardRect, s * 1.5f, s * 2.5f);
                using GraphicsPath shadowPath = Win11CardPanel.CreateRoundRectF(shadowRect, radius + s);
                using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha / s, 0, 0, 0));
                g.FillPath(shadowBrush, shadowPath);
            }
        }

        FillRoundedCardGradient(g, cardPath, cardRect, Color.FromArgb(cardAlpha, bgLeft), Color.FromArgb(cardAlpha, bgRight));

        if (dist > 0.35f)
        {
            float lipH = Math.Min(cardRect.Height * 0.45f, peekStep * 0.92f);
            float lipY = signedVisualOffset < 0 ? cardRect.Top + 2f : cardRect.Bottom - lipH - 2f;
            RectangleF lipRect = new RectangleF(cardRect.X, lipY, cardRect.Width, lipH);

            int textAlpha = (int)Math.Clamp(visibility * 235f, 0f, 235f);
            bool showName = _settings.OverlayIdentityMode == "Name" || _settings.OverlayIdentityMode == "Both";
            bool showIcon = _settings.OverlayIdentityMode == "Icon" || _settings.OverlayIdentityMode == "Both";

            if (item.IsSingleSource)
            {
                RectangleF singleZone = new RectangleF(lipRect.X + 14f * scale, lipRect.Y, lipRect.Width - 28f * scale, lipRect.Height);
                DrawCenteredPillIdentity(g, singleZone, item.Label1, item.RawSource1, textAlpha, scale * 0.82f, showName, showIcon);
            }
            else
            {
                float cx = lipRect.X + lipRect.Width / 2f;
                RectangleF leftZone = new RectangleF(lipRect.X + 12f * scale, lipRect.Y, (lipRect.Width / 2f) - 20f * scale, lipRect.Height);
                RectangleF rightZone = new RectangleF(cx + 8f * scale, lipRect.Y, (lipRect.Width / 2f) - 20f * scale, lipRect.Height);
                DrawPillSideIdentity(g, leftZone, item.Label1, item.RawSource1, textAlpha, scale * 0.82f, showName, showIcon, isRightSide: false);
                DrawPillSideIdentity(g, rightZone, item.Label2, item.RawSource2, textAlpha, scale * 0.82f, showName, showIcon, isRightSide: true);
            }
        }
        else
        {
            DrawActiveHUDContent(
                g, cardRect,
                item.Label1, item.Label2,
                item.RawSource1, item.RawSource2,
                _app1Vol, _app2Vol,
                scale, activeBlend, cardAlpha);
        }

        int rimAlpha = (int)Math.Clamp(cardAlpha * (0.38f + 0.50f * activeBlend), 0, 225);
        using Pen rimPen = new Pen(Color.FromArgb(rimAlpha, 255, 255, 255), 1.0f + 0.4f * activeBlend);
        g.DrawPath(rimPen, cardPath);
    }

    private void DrawCenteredPillIdentity(
        Graphics g,
        RectangleF zone,
        string label,
        string rawSource,
        int alpha,
        float scale,
        bool showName,
        bool showIcon)
    {
        if (zone.Width <= 10 || alpha <= 5) return;

        Bitmap? icon = showIcon ? AppIconCache.GetIconForSource(rawSource, _settings) : null;
        int iconPx = icon != null ? (int)Math.Clamp(16 * scale, 12, 24) : 0;
        float gap = 6f * scale;
        float iconOpacity = alpha / 255f;

        using Font font = new Font("Segoe UI Semibold", Math.Clamp(9.5f * scale, 7f, 18f), FontStyle.Bold);
        string text = showName ? label : "";

        SizeF textSz = !string.IsNullOrEmpty(text) ? g.MeasureString(text, font) : SizeF.Empty;
        float textW = Math.Min(textSz.Width + 2f, Math.Max(0f, zone.Width - iconPx - (icon != null ? gap : 0f)));
        float totalW = iconPx + (icon != null && !string.IsNullOrEmpty(text) ? gap : 0f) + textW;

        float startX = zone.X + (zone.Width - totalW) / 2f;
        float cy = zone.Y + zone.Height / 2f;

        if (icon != null)
        {
            DrawImageWithAlpha(g, icon, new RectangleF(startX, cy - iconPx / 2f, iconPx, iconPx), iconOpacity);
            startX += iconPx + gap;
        }

        if (!string.IsNullOrEmpty(text) && textW > 4f)
        {
            RectangleF textRect = new RectangleF(startX, zone.Y, textW + 4f, zone.Height);
            using SolidBrush textBrush = new SolidBrush(Color.FromArgb(alpha, 238, 240, 245));
            using StringFormat sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            g.DrawString(text, font, textBrush, textRect, sf);
        }
    }

    private static GraphicsPath CreateRoundedTrapezoidPath(PointF[] pts, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        if (pts == null || pts.Length != 4 || radius <= 0.4f)
        {
            if (pts != null && pts.Length == 4) path.AddPolygon(pts);
            return path;
        }

        float topW = pts[1].X - pts[0].X;
        float botW = pts[2].X - pts[3].X;
        float h = pts[3].Y - pts[0].Y;
        float r = Math.Min(radius, Math.Min(Math.Min(topW, botW) * 0.45f, h * 0.45f));

        if (r <= 0.4f)
        {
            path.AddPolygon(pts);
            return path;
        }

        float d = r * 2f;
        path.AddArc(pts[0].X, pts[0].Y, d, d, 180f, 90f);
        path.AddArc(pts[1].X - d, pts[1].Y, d, d, 270f, 90f);
        path.AddArc(pts[2].X - d, pts[2].Y - d, d, d, 0f, 90f);
        path.AddArc(pts[3].X, pts[3].Y - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    // =========================================================================
    // FULL CAROUSEL STYLE 5: LARGE-RADIUS 3D FACETED CYLINDER
    // =========================================================================
    private void DrawCylinderFacetCard(
        Graphics g,
        CycleDisplayItem item,
        bool isActiveSlot,
        float signedVisualOffset,
        RectangleF heroCardRect,
        int baseH,
        float scale)
    {
        float dist = Math.Abs(signedVisualOffset);
        if (dist > 2.55f) return;

        bool alwaysPreviews = string.Equals(_settings.NeighborPreviewVisibility, "Always", StringComparison.OrdinalIgnoreCase);
        float activeBlend = Math.Clamp(1.0f - dist, 0f, 1f);

        const float stepAngleRad = 27f * (MathF.PI / 180f);
        const float maxHorizonRad = 55f * (MathF.PI / 180f);
        const float maxGeomRad = 86f * (MathF.PI / 180f);
        float halfStep = stepAngleRad * 0.5f;

        float rawThetaTop = (signedVisualOffset - 0.5f) * stepAngleRad;
        float rawThetaBot = (signedVisualOffset + 0.5f) * stepAngleRad;

        if (rawThetaTop >= maxHorizonRad - 0.005f || rawThetaBot <= -maxHorizonRad + 0.005f)
            return;

        float geomThetaTop = Math.Clamp(rawThetaTop, -maxGeomRad, maxGeomRad);
        float geomThetaBot = Math.Clamp(rawThetaBot, -maxGeomRad, maxGeomRad);
        if (geomThetaBot - geomThetaTop <= 0.015f) return;

        float visThetaTop = Math.Clamp(rawThetaTop, -maxHorizonRad, maxHorizonRad);
        float visThetaBot = Math.Clamp(rawThetaBot, -maxHorizonRad, maxHorizonRad);
        float thetaCenter = (visThetaTop + visThetaBot) * 0.5f;
        float cosCenter = MathF.Cos(thetaCenter);

        float cosTop = MathF.Cos(geomThetaTop);
        float cosBot = MathF.Cos(geomThetaBot);

        float pillH = Math.Clamp(baseH * 0.50f, 28f, 64f);
        float maxOrbitDist = (baseH / 2f) + (pillH / 2f) + (6f * scale);
        float targetMaxWing = (maxOrbitDist - baseH / 2f) + (pillH * 0.74f);

        float heroCenterY = heroCardRect.Y + heroCardRect.Height / 2f;
        float sinHalf0 = MathF.Sin(halfStep);
        float sinHorizon = MathF.Sin(maxHorizonRad);

        float MapCylinderY(float theta)
        {
            float s = MathF.Sin(theta);
            float absS = Math.Abs(s);
            if (absS <= sinHalf0)
            {
                return heroCenterY + (s / sinHalf0) * (heroCardRect.Height * 0.5f);
            }
            float tWing = (absS - sinHalf0) / Math.Max(0.05f, sinHorizon - sinHalf0);
            return heroCenterY + Math.Sign(s) * ((heroCardRect.Height * 0.5f) + tWing * targetMaxWing);
        }

        float yTopFull = MapCylinderY(geomThetaTop);
        float yBotFull = MapCylinderY(geomThetaBot);

        float yHorizonTop = MapCylinderY(-maxHorizonRad);
        float yHorizonBot = MapCylinderY(maxHorizonRad);

        float baseW = heroCardRect.Width;
        float centerX = heroCardRect.X + baseW / 2f;

        float cosHorizon = MathF.Cos(maxHorizonRad);
        float normTop = Math.Clamp((cosTop - cosHorizon * 0.65f) / (1f - cosHorizon * 0.65f), 0f, 1f);
        float normBot = Math.Clamp((cosBot - cosHorizon * 0.65f) / (1f - cosHorizon * 0.65f), 0f, 1f);

        float wTopFull = baseW * (0.78f + 0.22f * MathF.Pow(normTop, 0.70f));
        float wBotFull = baseW * (0.78f + 0.22f * MathF.Pow(normBot, 0.70f));

        float exp = Math.Clamp(_wheelExpansion, 0f, 1f);
        if (!isActiveSlot)
        {
            RectangleF startRect = heroCardRect;
            if (alwaysPreviews)
            {
                var (topBox, bottomBox) = GetCompactTopBottomPreviewBoxRects(heroCardRect, scale);
                startRect = signedVisualOffset < 0 ? topBox : bottomBox;
            }
            yTopFull = startRect.Top + (yTopFull - startRect.Top) * exp;
            yBotFull = startRect.Bottom + (yBotFull - startRect.Bottom) * exp;
            wTopFull = startRect.Width + (wTopFull - startRect.Width) * exp;
            wBotFull = startRect.Width + (wBotFull - startRect.Width) * exp;

            yHorizonTop = startRect.Top + (yHorizonTop - startRect.Top) * exp;
            yHorizonBot = startRect.Bottom + (yHorizonBot - startRect.Bottom) * exp;
        }

        float fullFacetH = Math.Max(1f, yBotFull - yTopFull);
        float visTopY = Math.Clamp(yTopFull, yHorizonTop, yHorizonBot);
        float visBotY = Math.Clamp(yBotFull, yHorizonTop, yHorizonBot);
        float visFacetH = visBotY - visTopY;
        if (visFacetH <= 1.5f || visBotY < 0 || visTopY > this.Height) return;

        float tTopSlice = Math.Clamp((visTopY - yTopFull) / fullFacetH, 0f, 1f);
        float tBotSlice = Math.Clamp((visBotY - yTopFull) / fullFacetH, 0f, 1f);
        float wVisTop = wTopFull + (wBotFull - wTopFull) * tTopSlice;
        float wVisBot = wTopFull + (wBotFull - wTopFull) * tBotSlice;

        float horizonVisibility = Math.Clamp((2.52f - dist) / 0.48f, 0f, 1f);
        float baseVis = (alwaysPreviews && dist <= 1.25f) ? Math.Max(exp, 0.92f) : exp;
        float visibility = isActiveSlot ? 1.0f : Math.Clamp(horizonVisibility * baseVis, 0f, 1f);
        int cardAlpha = (int)Math.Clamp(visibility * (240f + 15f * activeBlend), 0f, 255f);
        if (cardAlpha < 6) return;

        float realCardMorph = _cycleItems.Count <= 2
            ? Math.Clamp(2.0f - dist, 0f, 1f)
            : Math.Clamp((2.35f - dist) / 0.65f, 0f, 1f);

        PointF[] trapPts = new PointF[]
        {
            new PointF(centerX - wVisTop / 2f, visTopY),
            new PointF(centerX + wVisTop / 2f, visTopY),
            new PointF(centerX + wVisBot / 2f, visBotY),
            new PointF(centerX - wVisBot / 2f, visBotY)
        };

        float maxHeroRadius = GetWin11CornerRadius(scale);
        float closeProgress = MathF.Pow(1.0f - exp, 1.35f);
        float dynamicCornerRadius = maxHeroRadius * closeProgress * (0.35f + 0.65f * activeBlend);

        using GraphicsPath facetPath = CreateRoundedTrapezoidPath(trapPts, dynamicCornerRadius);

        Color itemRawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120));
        Color itemRawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85));
        Color cylinderBase = Color.FromArgb(20, 22, 28);
        Color genericEmptyGray = Color.FromArgb(42, 45, 54);

        float lighting = Math.Clamp(MathF.Pow(cosCenter, 0.85f), 0.35f, 1.0f);
        Color emptyLitGray = ColorLerp(cylinderBase, genericEmptyGray, lighting);

        Color sourceFacetLeft = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(cylinderBase, itemRawC1, 0.78f * lighting))
            : ColorLerp(cylinderBase, DefaultDarkGrey, lighting);
        Color sourceFacetRight = _settings.UseSourceBackgroundColors
            ? EnsureReadableBackdrop(ColorLerp(cylinderBase, itemRawC2, 0.78f * lighting))
            : ColorLerp(cylinderBase, DefaultDarkGrey, lighting);

        if (item.IsSingleSource) sourceFacetRight = sourceFacetLeft;

        Color facetLeft = ColorLerp(emptyLitGray, sourceFacetLeft, realCardMorph);
        Color facetRight = ColorLerp(emptyLitGray, sourceFacetRight, realCardMorph);

        var (activeHeroLeft, activeHeroRight) = ComputeItemHeroColors(item.Color1Hex, item.Color2Hex, item.IsSingleSource);
        Color bgLeft = ColorLerp(facetLeft, activeHeroLeft, activeBlend);
        Color bgRight = ColorLerp(facetRight, activeHeroRight, activeBlend);
        if (item.IsSingleSource) bgRight = bgLeft;

        float maxW = Math.Max(wVisTop, wVisBot);
        RectangleF boundsRect = new RectangleF(centerX - maxW / 2f, visTopY, maxW, visFacetH);
        FillRoundedCardGradient(g, facetPath, boundsRect, Color.FromArgb(cardAlpha, bgLeft), Color.FromArgb(cardAlpha, bgRight));

        int horizonDarkAlpha = (int)Math.Clamp((1.0f - cosCenter) * 210f * exp, 0f, 155f);
        if (horizonDarkAlpha > 4)
        {
            Color nearEdgeShade = Color.FromArgb((int)(horizonDarkAlpha * 0.15f), 0, 0, 0);
            Color farEdgeShade = Color.FromArgb(horizonDarkAlpha, 0, 0, 0);
            Color topShade = signedVisualOffset < 0 ? farEdgeShade : nearEdgeShade;
            Color botShade = signedVisualOffset < 0 ? nearEdgeShade : farEdgeShade;

            using LinearGradientBrush depthBrush = new LinearGradientBrush(
                new PointF(centerX, visTopY - 1f),
                new PointF(centerX, visBotY + 1f),
                topShade,
                botShade);
            g.FillPath(depthBrush, facetPath);
        }

        if (visFacetH >= 5f && realCardMorph > 0.04f)
        {
            GraphicsState contentState = g.Save();
            g.SetClip(facetPath, CombineMode.Intersect);

            float unclampedMidY = (yTopFull + yBotFull) / 2f;
            float scaleY = Math.Clamp(fullFacetH / Math.Max(1f, heroCardRect.Height), 0.25f, 1.0f);
            float avgW = (wTopFull + wBotFull) * 0.5f;
            float scaleX = Math.Clamp(avgW / Math.Max(1f, heroCardRect.Width), 0.78f, 1.0f);

            g.TranslateTransform(centerX, unclampedMidY);
            g.ScaleTransform(scaleX, scaleY);
            g.TranslateTransform(-centerX, -(heroCardRect.Y + heroCardRect.Height / 2f));

            int contentAlpha = (int)Math.Clamp(cardAlpha * realCardMorph * Math.Clamp(cosCenter * 1.15f, 0.45f, 1.0f), 0f, 255f);
            if (contentAlpha > 6)
            {
                DrawActiveHUDContent(
                    g,
                    heroCardRect,
                    item.Label1,
                    item.Label2,
                    item.RawSource1,
                    item.RawSource2,
                    _app1Vol,
                    _app2Vol,
                    scale,
                    activeBlend,
                    contentAlpha);
            }

            g.Restore(contentState);
        }

        int sidePenAlpha = (int)Math.Clamp(cardAlpha * (0.38f + 0.52f * activeBlend), 0, 225);
        using (Pen facetBorderPen = new Pen(Color.FromArgb(sidePenAlpha, 255, 255, 255), 1.0f + 0.5f * activeBlend))
        {
            g.DrawPath(facetBorderPen, facetPath);
        }

        int ridgeAlpha = (int)Math.Clamp(cardAlpha * exp * (0.55f + 0.40f * activeBlend), 0, 240);
        if (ridgeAlpha > 8 && dynamicCornerRadius < 2.5f * scale)
        {
            using Pen ridgePen = new Pen(Color.FromArgb(ridgeAlpha, 255, 255, 255), 1.4f * scale);
            if (signedVisualOffset <= 0.1f)
                g.DrawLine(ridgePen, trapPts[3], trapPts[2]);
            if (signedVisualOffset >= -0.1f)
                g.DrawLine(ridgePen, trapPts[0], trapPts[1]);
        }
    }

    private Bitmap? _backbuffer;

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _fadeTimer?.Stop();
        _fadeTimer?.Dispose();
        _backbuffer?.Dispose();
        _backbuffer = null;
        base.OnFormClosed(e);
    }

    private void RenderLayeredOverlay()
    {
        if (this.IsDisposed || !this.IsHandleCreated || this.Width <= 10 || this.Height <= 10) return;

        try
        {
            if (_backbuffer == null || _backbuffer.Width != this.Width || _backbuffer.Height != this.Height)
            {
                _backbuffer?.Dispose();
                _backbuffer = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppPArgb);
            }

            using (Graphics g = Graphics.FromImage(_backbuffer))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                if (_masterOpacity > 0.005)
                {
                    DrawOverlayScene(g);
                }
            }

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                hBitmap = _backbuffer.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);

                Point topPos = new Point(this.Left, this.Top);
                Size winSize = new Size(this.Width, this.Height);
                Point srcPos = new Point(0, 0);
                double maxOpacity = Math.Clamp(_settings.OverlayOpacity, 10, 100) / 100.0;
                double effectiveOpacity = _masterOpacity * maxOpacity;

                BLENDFUNCTION blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = (byte)Math.Clamp((int)Math.Round(effectiveOpacity * 255.0), 0, 255),
                    AlphaFormat = AC_SRC_ALPHA
                };

                UpdateLayeredWindow(this.Handle, screenDc, ref topPos, ref winSize, memDc, ref srcPos, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero) SelectObject(memDc, oldBitmap);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (memDc != IntPtr.Zero) DeleteDC(memDc);
                if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
        catch { }
    }

    private GraphicsPath? GetSafeRoundedBarPath(int x, int y, int width, int height, bool roundLeft, bool roundRight)
    {
        if (width <= 0 || height <= 0) return null;
        GraphicsPath path = new GraphicsPath();

        int cornerRadius = Math.Min(4, height / 2);
        if (width < cornerRadius * 2) cornerRadius = width / 2;
        if (cornerRadius < 1) cornerRadius = 1;

        int left = x;
        int right = x + width;
        int top = y;
        int bottom = y + height;
        int d = cornerRadius * 2;

        try
        {
            if (roundLeft && roundRight && width >= d && height >= d)
            {
                path.AddArc(left, top, d, d, 180, 90);
                path.AddArc(right - d, top, d, d, 270, 90);
                path.AddArc(right - d, bottom - d, d, d, 0, 90);
                path.AddArc(left, bottom - d, d, d, 90, 90);
            }
            else if (roundLeft && width >= d && height >= d)
            {
                path.AddArc(left, top, d, d, 180, 90);
                path.AddLine(left + cornerRadius, top, right, top);
                path.AddLine(right, top, right, bottom);
                path.AddLine(right, bottom, left + cornerRadius, bottom);
                path.AddArc(left, bottom - d, d, d, 90, 90);
            }
            else if (roundRight && width >= d && height >= d)
            {
                path.AddLine(left, top, right - cornerRadius, top);
                path.AddArc(right - d, top, d, d, 270, 90);
                path.AddArc(right - d, bottom - d, d, d, 0, 90);
                path.AddLine(right - cornerRadius, bottom, left, bottom);
                path.AddLine(left, bottom, left, top);
            }
            else
            {
                path.AddRectangle(new Rectangle(left, top, width, height));
            }
            path.CloseFigure();
            return path;
        }
        catch
        {
            path.Dispose();
            return null;
        }
    }
}