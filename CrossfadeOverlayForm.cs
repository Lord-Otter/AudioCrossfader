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

    private float _stationaryCardOffsetY = 0f;
    private double _masterOpacity = 0.0;

    private System.Windows.Forms.Timer? _fadeTimer;
    private int _displayCountdownMs = 0;
    private int _totalDurationMs = 2500;
    private bool _isFadingOut = false;
    private bool _pinnedBySettings = false;

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

        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.ShowInTaskbar = false;
        this.TopMost = true;

        _totalDurationMs = Math.Max(500, _settings.PopupDurationMs);
        SnapInterpolatedColorsImmediate();

        SyncCycleItemsFromSettings(_settings, out int activeIdx);
        _cycleActiveIndex = activeIdx;

        UpdateFixedCanvasGeometry();

        // Trigger the carousel to open up gracefully on startup if requested and carousel items exist
        if (openCarouselOnInit && _settings.CarouselVisibleNeighbors > 0.05f && _cycleItems.Count > 1)
        {
            _cycleCountdownMs = _totalDurationMs;
            _wheelExpansion = 0.05f;
        }

        _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _fadeTimer.Tick += OnAnimationTick;
        _fadeTimer.Start();
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

        bool carouselAllowed = _settings.CarouselVisibleNeighbors > 0.05f && _cycleItems.Count > 1;
        float targetExpansion = (carouselAllowed && _cycleCountdownMs > 0) ? 1.0f : 0.0f;

        if (!_settings.EnableCarouselAnimation)
        {
            _wheelExpansion = targetExpansion;
            _wheelScrollOffset = 0f;
        }
        else
        {
            float lerpSpeed = _settings.EnableInterpolation
                ? Math.Clamp(_settings.InterpolationSpeed, 5, 100) / 100.0f
                : 1.0f;
            float wheelLerp = Math.Clamp(lerpSpeed * 0.55f, 0.16f, 0.80f);

            if (Math.Abs(targetExpansion - _wheelExpansion) > 0.003f)
            {
                _wheelExpansion += (targetExpansion - _wheelExpansion) * wheelLerp;
            }
            else
            {
                _wheelExpansion = targetExpansion;
            }

            if (Math.Abs(_wheelScrollOffset) > 0.004f)
            {
                _wheelScrollOffset *= (1.0f - Math.Clamp(wheelLerp * 0.78f, 0.18f, 0.72f));
            }
            else
            {
                _wheelScrollOffset = 0f;
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

        int shadowAlpha = (int)Math.Clamp(textColor.A * 0.88f, 0, 225);
        using SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha, 8, 8, 12));

        ReadOnlySpan<PointF> offsets = stackalloc PointF[]
        {
            new PointF(-1f, 0f),
            new PointF(1f, 0f),
            new PointF(0f, -1f),
            new PointF(0f, 1f),
            new PointF(0f, 1.6f)
        };

        foreach (var off in offsets)
        {
            RectangleF sr = new RectangleF(layoutRect.X + off.X, layoutRect.Y + off.Y, layoutRect.Width, layoutRect.Height);
            if (sf != null) g.DrawString(text, font, shadowBrush, sr, sf);
            else g.DrawString(text, font, shadowBrush, sr.Location);
        }

        using SolidBrush fgBrush = new SolidBrush(textColor);
        if (sf != null) g.DrawString(text, font, fgBrush, layoutRect, sf);
        else g.DrawString(text, font, fgBrush, layoutRect.Location);
    }

    private (Color targetLeft, Color targetRight, Color targetBar1, Color targetBar2) ComputeTargetColors()
    {
        Color unmutedColor = ParseOverlayColor(_settings.UnmutedColor, Color.FromArgb(0, 168, 255));
        Color mutedColor = ParseOverlayColor(_settings.MutedColor, Color.FromArgb(255, 71, 87));

        bool app1Muted = (_muteState == 2);
        bool app2Muted = (_muteState == 1);

        Color bar1 = app1Muted ? mutedColor : unmutedColor;
        Color bar2 = app2Muted ? mutedColor : unmutedColor;

        if (!_settings.UseSourceBackgroundColors)
        {
            return (DefaultDarkGrey, DefaultDarkGrey, bar1, bar2);
        }

        Color rawC1 = ParseOverlayColor(_color1Hex, Color.FromArgb(27, 59, 90));
        float effVol1 = app1Muted ? 0f : Math.Clamp(_app1Vol, 0f, 1f);
        Color effectiveC1 = _settings.FadeBgColorWithVolume
            ? ColorLerp(DefaultDarkGrey, rawC1, effVol1)
            : rawC1;

        Color rawC2 = ParseOverlayColor(_color2Hex, Color.FromArgb(74, 32, 64));
        float effVol2 = app2Muted ? 0f : Math.Clamp(_app2Vol, 0f, 1f);
        Color effectiveC2 = _settings.FadeBgColorWithVolume
            ? ColorLerp(DefaultDarkGrey, rawC2, effVol2)
            : rawC2;

        return (effectiveC1, effectiveC2, bar1, bar2);
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
            .Where(p => !string.IsNullOrWhiteSpace(p.Source1) && !string.IsNullOrWhiteSpace(p.Source2))
            .ToList();

        for (int i = 0; i < validPairs.Count; i++)
        {
            var p = validPairs[i];
            list.Add(new CycleDisplayItem(
                AudioCrossfaderWorker.GetResolvedLabel(p.Source1),
                p.Source1,
                p.Color1Hex,
                AudioCrossfaderWorker.GetResolvedLabel(p.Source2),
                p.Source2,
                p.Color2Hex));

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

            UpdateFixedCanvasGeometry();

            if (forcePopup || _pinnedBySettings)
            {
                _displayCountdownMs = _totalDurationMs;
                if (_settings.CarouselVisibleNeighbors > 0.05f && _cycleItems.Count > 1)
                {
                    _cycleCountdownMs = _totalDurationMs;
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
            _cycleItems = items ?? new List<CycleDisplayItem>();
            _cycleActiveIndex = Math.Max(0, activeIndex);
            _cycleCountdownMs = Math.Max(1650, (int)(_totalDurationMs * 0.85f));
            _displayCountdownMs = Math.Max(_displayCountdownMs, _cycleCountdownMs + 250);
            _isFadingOut = false;
            _wheelScrollOffset = stepDirection >= 0 ? 1.0f : -1.0f;

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

    private void UpdateFixedCanvasGeometry()
    {
        int baseW = Math.Max(120, _settings.OverlayWidth);
        int baseH = Math.Max(50, _settings.OverlayHeight);

        int maxWingPx = (int)Math.Ceiling(baseH * 1.32f) + 16;
        int totalH = baseH + (maxWingPx * 2);
        int canvasW = baseW + 24;

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
                baseX = bounds.Right - canvasW - 40; baseY = bottomSafeY; break;
            case "Bottom Left":
                baseX = bounds.Left + 40; baseY = bottomSafeY; break;
            case "Top Right":
                baseX = bounds.Right - canvasW - 40; baseY = topSafeY; break;
            case "Top Left":
                baseX = bounds.Left + 40; baseY = topSafeY; break;
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
        float wingExtent = ((maxOrbitDist - baseH / 2f) + (pillH * 0.92f)) * _wheelExpansion;

        float containerW = baseW + 14f;
        float containerX = (this.Width - containerW) / 2f;
        float topY = _stationaryCardOffsetY - wingExtent;
        float height = baseH + (wingExtent * 2f);

        return new RectangleF(containerX, topY, containerW - 1f, height - 1f);
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

        bool carouselAllowed = _settings.CarouselVisibleNeighbors > 0.05f && _cycleItems.Count > 1;

        if (carouselAllowed && _wheelExpansion > 0.01f && _cycleItems.Count > 0)
        {
            int count = _cycleItems.Count;
            string style = _settings.CarouselMotionStyle;

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

            if (style == "Fluid Moving Deck")
            {
                int[] deckOrder = new[] { -2, 2, -1, 1, 0 };
                foreach (int offset in deckOrder)
                {
                    if (offset == 0 && _wheelExpansion > 0.02f)
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

                    int itemIdx = ((_cycleActiveIndex + offset) % count + count) % count;
                    float visualOffset = offset + _wheelScrollOffset;
                    DrawFluidDeckCard(g, _cycleItems[itemIdx], offset == 0, visualOffset, heroRect, baseH, scale);
                }
            }
            else // "3D Floating Pills"
            {
                int[] drawOrder = new[] { -2, 2, -1, 1 };
                foreach (int offset in drawOrder)
                {
                    int itemIdx = ((_cycleActiveIndex + offset) % count + count) % count;
                    float visualOffset = offset + _wheelScrollOffset;
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

            g.Restore(clipState);

            if (_settings.CarouselBackgroundEnabled)
            {
                using Pen outerFramePen = new Pen(Color.FromArgb((int)(85 * _wheelExpansion), 255, 255, 255), 1.3f);
                g.DrawPath(outerFramePen, containerPath);
            }
        }
        else
        {
            using (GraphicsPath heroPath = Win11CardPanel.CreateRoundRectF(heroRect, GetWin11CornerRadius(scale)))
            {
                FillRoundedCardGradient(g, heroPath, heroRect, _renderedBgLeft, _renderedBgRight);
                DrawActiveHUDContent(g, heroRect, _app1Label, _app2Label, _rawSource1, _rawSource2, _app1Vol, _app2Vol, scale, 1.0f);
                using Pen heroBorderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f);
                g.DrawPath(heroBorderPen, heroPath);
            }
        }
    }

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
        bool forceShow)
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

        if (forceShow)
        {
            _displayCountdownMs = _totalDurationMs;
            _isFadingOut = false;
        }

        if (!this.IsDisposed && this.IsHandleCreated)
        {
            this.BeginInvoke(new Action(() =>
            {
                if (forceShow)
                {
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
                g.DrawString(namePart, font, textBrush, nameRect, sfRight);
                curRight -= (nameSize.Width + pctGap);
            }

            if (showInlinePercent && curRight > bounds.X + 8f)
            {
                RectangleF pctRect = new RectangleF(bounds.X, bounds.Y, curRight - bounds.X, bounds.Height);
                g.DrawString(pctText, font, textBrush, pctRect, sfRight);
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
                g.DrawString(namePart, font, textBrush, nameRect, sfLeft);
                curLeft += (nameSize.Width + pctGap);
            }

            if (showInlinePercent && bounds.Right > curLeft + 8f)
            {
                RectangleF pctRect = new RectangleF(curLeft, bounds.Y, bounds.Right - curLeft, bounds.Height);
                g.DrawString(pctText, font, textBrush, pctRect, sfLeft);
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

        int divAlpha = (int)Math.Clamp(cardAlpha * 0.55f, 0, 140);
        int centerLineTop = (int)topY + (int)(height * 0.16f);
        int centerLineHeight = Math.Max(8, height - (int)(height * 0.32f));
        using (SolidBrush dividerBrush = new SolidBrush(Color.FromArgb(divAlpha, 235, 235, 240)))
        {
            g.FillRectangle(dividerBrush, centerX - 1, centerLineTop, 2, centerLineHeight);
        }

        bool app1Muted = (_muteState == 2) && activeHudEmphasis > 0.5f;
        bool app2Muted = (_muteState == 1) && activeHudEmphasis > 0.5f;
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

        float pillCenterY = heroCenterY + Math.Sign(signedVisualOffset) * (orbitSpacing * _wheelExpansion);
        RectangleF pillRect = new RectangleF(pillX, pillCenterY - pillH / 2f, pillW, pillH);

        if (pillRect.Bottom < 0 || pillRect.Top > this.Height) return;

        float visibility = Math.Clamp((1.0f - Math.Max(0f, dist - 1.0f) * 0.55f) * _wheelExpansion, 0f, 1f);
        int cardAlpha = (int)Math.Clamp(visibility * 235f, 0f, 235f);
        if (cardAlpha < 12) return;

        Color rawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120));
        Color rawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85));

        Color surfaceBase = Color.FromArgb(30, 31, 36);
        Color tintLeft = _settings.UseSourceBackgroundColors ? ColorLerp(surfaceBase, rawC1, 0.48f) : surfaceBase;
        Color tintRight = _settings.UseSourceBackgroundColors ? ColorLerp(surfaceBase, rawC2, 0.48f) : surfaceBase;

        Color finalLeft = Color.FromArgb(cardAlpha, tintLeft);
        Color finalRight = Color.FromArgb(cardAlpha, tintRight);

        float radius = Math.Min(pillH / 2f, 12f * scale);
        using GraphicsPath pillPath = Win11CardPanel.CreateRoundRectF(pillRect, radius);
        FillRoundedCardGradient(g, pillPath, pillRect, finalLeft, finalRight);

        using (Pen rimPen = new Pen(Color.FromArgb((int)(cardAlpha * 0.45f), 255, 255, 255), 1f))
        {
            g.DrawPath(rimPen, pillPath);
        }

        float centerX = pillRect.X + pillRect.Width / 2f;
        float centerY = pillRect.Y + pillRect.Height / 2f;
        int textAlpha = (int)Math.Clamp(visibility * 240f, 0f, 240f);

        using Font arrowFont = new Font("Segoe UI Symbol", Math.Clamp(9.0f * scale, 7f, 16f), FontStyle.Bold);
        using SolidBrush arrowBrush = new SolidBrush(Color.FromArgb((int)(textAlpha * 0.65f), 200, 210, 225));
        using StringFormat centerSf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("⇄", arrowFont, arrowBrush, new PointF(centerX, centerY), centerSf);

        bool showName = _settings.OverlayIdentityMode == "Name" || _settings.OverlayIdentityMode == "Both";
        bool showIcon = _settings.OverlayIdentityMode == "Icon" || _settings.OverlayIdentityMode == "Both";

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

        float orbitSpacing;
        if (dist <= 1.0f)
        {
            orbitSpacing = dist * minOrbitDist;
        }
        else
        {
            orbitSpacing = minOrbitDist + (dist - 1.0f) * (pillH + 5f * scale);
        }

        float cardCenterY = heroCenterY + Math.Sign(signedVisualOffset) * (orbitSpacing * _wheelExpansion);
        RectangleF cardRect = new RectangleF(cardX, cardCenterY - cardH / 2f, cardW, cardH);

        if (cardRect.Bottom < 0 || cardRect.Top > this.Height) return;

        float visibility = isActiveSlot
            ? 1.0f
            : Math.Clamp((1.0f - Math.Max(0f, dist - 1.0f) * 0.55f) * _wheelExpansion, 0f, 1f);
        int cardAlpha = (int)Math.Clamp(visibility * (235f + 20f * activeBlend), 0f, 255f);
        if (cardAlpha < 10) return;

        Color rawC1 = EnsureReadableBackdrop(ParseOverlayColor(item.Color1Hex, Color.FromArgb(40, 80, 120)));
        Color rawC2 = EnsureReadableBackdrop(ParseOverlayColor(item.Color2Hex, Color.FromArgb(95, 45, 85)));

        Color surfaceBase = Color.FromArgb(30, 31, 36);
        Color pillLeft = _settings.UseSourceBackgroundColors ? ColorLerp(surfaceBase, rawC1, 0.48f) : surfaceBase;
        Color pillRight = _settings.UseSourceBackgroundColors ? ColorLerp(surfaceBase, rawC2, 0.48f) : surfaceBase;

        Color bgLeft = isActiveSlot ? ColorLerp(pillLeft, EnsureReadableBackdrop(_renderedBgLeft), activeBlend) : pillLeft;
        Color bgRight = isActiveSlot ? ColorLerp(pillRight, EnsureReadableBackdrop(_renderedBgRight), activeBlend) : pillRight;

        Color finalLeft = Color.FromArgb(cardAlpha, bgLeft);
        Color finalRight = Color.FromArgb(cardAlpha, bgRight);

        float pillRadius = Math.Min(cardH / 2f, 12f * scale);
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

    private void RenderLayeredOverlay()
    {
        if (this.IsDisposed || !this.IsHandleCreated || this.Width <= 10 || this.Height <= 10) return;

        using Bitmap canvas = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(canvas))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

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
            hBitmap = canvas.GetHbitmap(Color.FromArgb(0));
            oldBitmap = SelectObject(memDc, hBitmap);

            Point topPos = new Point(this.Left, this.Top);
            Size winSize = new Size(this.Width, this.Height);
            Point srcPos = new Point(0, 0);

            BLENDFUNCTION blend = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = (byte)Math.Clamp((int)Math.Round(_masterOpacity * 255.0), 0, 255),
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