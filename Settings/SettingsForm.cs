using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

public partial class SettingsForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int ENUM_CURRENT_SETTINGS = -1;

    private Panel _monitorRowsPanel = null!;

    private bool _isResizing = false;
    private bool _resizePending = false;
    private System.Windows.Forms.Timer? _resizeThrottleTimer;
    private const int WM_ENTERSIZEMOVE = 0x0231;
    private const int WM_EXITSIZEMOVE = 0x0232;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ENTERSIZEMOVE)
        {
            _isResizing = true;
            if (_resizeThrottleTimer == null)
            {
                _resizeThrottleTimer = new System.Windows.Forms.Timer { Interval = 55 };
                _resizeThrottleTimer.Tick += (s, e) =>
                {
                    if (_resizePending)
                    {
                        _resizePending = false;
                        PerformWindowResizeLayout();
                    }
                };
            }
            _resizeThrottleTimer.Start();
        }
        else if (m.Msg == WM_EXITSIZEMOVE)
        {
            _isResizing = false;
            _resizePending = false;
            _resizeThrottleTimer?.Stop();
            PerformWindowResizeLayout();
            Invalidate();
        }

        base.WndProc(ref m);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    private static (int width, int height) GetPhysicalMonitorResolution(Screen scr)
    {
        try
        {
            DEVMODE dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            if (EnumDisplaySettings(scr.DeviceName, ENUM_CURRENT_SETTINGS, ref dm) && dm.dmPelsWidth > 0 && dm.dmPelsHeight > 0)
            {
                return (dm.dmPelsWidth, dm.dmPelsHeight);
            }
        }
        catch { }
        return (scr.Bounds.Width, scr.Bounds.Height);
    }

    private static readonly object[] GlobalMuteModeOptions = new object[]
    {
        "Cycle R → L",
        "Cycle L → R",
        "Mute Both",
        "Mute Left",
        "Mute Right"
    };

    private static readonly object[] PairMuteModeOptions = new object[]
    {
        "Use Global",
        "Cycle R → L",
        "Cycle L → R",
        "Mute Both",
        "Mute Left",
        "Mute Right"
    };

    private Win11CardPanel _pillTrack = null!;
    private ModernButton _navBtnGeneral = null!;
    private ModernButton _navBtnGroups = null!;
    private ModernButton _navBtnOverlay = null!;
    private ModernButton _navBtnBehavior = null!;
    private DarkScrollPanel _pageGeneral = null!;
    private DarkScrollPanel _pageGroups = null!;
    private DarkScrollPanel _pageOverlay = null!;
    private DarkScrollPanel _pageBehavior = null!;

    private Panel _cycleSlotsHost = null!;
    private const int GlobalKeysHeaderHeight = 30;

    private Panel _standaloneSlotsHost = null!;
    private Win11CardPanel _groupsMasterCard = null!;
    private ModernButton _btnAddGroupEntry = null!;
    private Panel _groupsSlotsHost = null!;

    private Win11CardPanel _previewCard = null!;
    private Win11CardPanel _overlayStyleCard = null!;
    private Win11CardPanel _layoutCard = null!;
    private Win11CardPanel _behaviorCard = null!;

    private ModernButton _btnRevert = null!;
    private ModernButton _btnSave = null!;

    private KeybindBox _kbKey1 = null!;
    private KeybindBox _kbKey2 = null!;
    private KeybindBox _kbKey3 = null!;
    private KeybindBox _kbKeyCycleNext = null!;
    private KeybindBox _kbKeyCyclePrev = null!;
    private ModernComboBox _cmbGlobalMuteMode = null!;

    private CheckBox _chkKeepOverlayAlwaysOn = null!;
    private ModernComboBox _cmbIdentityMode = null!;
    private ModernComboBox _cmbLevelMode = null!;
    private ModernComboBox _cmbCarouselStyle = null!;
    private CheckBox _chkUseSourceBgColors = null!;
    private CheckBox _chkFadeBgWithVolume = null!;
    private CheckBox _chkEnableAnimations = null!;
    private ModernSlider _sldCarouselSpeed = null!;
    private CheckBox _chkCarouselBg = null!;

    private ModernComboBox _cmbPosition = null!;
    private TextBox _txtPopupDuration = null!;
    private ModernSlider _sldOverlayOpacity = null!;
    private ModernSlider _sldStepSize = null!;
    private TextBox _txtWidth = null!;
    private TextBox _txtHeight = null!;
    private CheckBox _chkStartup = null!;
    private CheckBox _chkInterpolation = null!;
    private ModernSlider _sldInterpSpeed = null!;
    private CheckBox _chkBuffer = null!;

    private ModernComboBox _cmbCarouselMode = null!;
    private CheckBox _chkInvertCarouselScroll = null!;
    private ModernComboBox _cmbCardOrder = null!;
    private ModernComboBox _cmbNeighborPreviewMode = null!;
    private ModernSlider _sldCarouselOpenSpeed = null!;
    private Action? _updateCarouselModeLayout;

    private RoundedColorSwatch _unmutedColorBox = null!;
    private RoundedColorSwatch _mutedColorBox = null!;

    private readonly TrayApplicationContext _context;
    private readonly ToolTip _toolTip = new ToolTip();

    private AppSettings _draftSettings;
    private AppSettings _savedSettingsSnapshot;
    private DetectedAudioPrograms _detectedAudio = new DetectedAudioPrograms();
    private Label _lblStatus = null!;

    private bool _isRecording = false;
    private KeybindBox? _activeKeybindBox = null;
    private Action<int>? _activeKeyCommitCallback = null;
    private KeyCaptureFilter? _mouseFilter;
    private bool _isInitializing = true;
    private int _activeTabIndex = 0;

    private StateMachine<SettingsTabState> _tabStateMachine = null!;
    private System.Windows.Forms.Timer? _navPillAnimTimer;
    private float _navPillStartX;
    private float _navPillStartW;
    private float _navPillTargetX;
    private float _navPillTargetW;
    private readonly Stopwatch _navPillWatch = new Stopwatch();
    private bool _isPairListAnimating = false;

    public SettingsForm(AppSettings initialSettings, TrayApplicationContext context)
    {
        _context = context;
        _draftSettings = initialSettings.Clone();
        _savedSettingsSnapshot = initialSettings.Clone();
        _detectedAudio = AudioProcessDetector.Scan();

        this.Text = "Audio Crossfader Settings";
        this.Size = new Size(540, 780);
        this.MinimumSize = new Size(340, 420);
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.MaximizeBox = true;
        this.MinimizeBox = true;
        this.ShowInTaskbar = true;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.KeyPreview = true;
        this.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);

        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }
            else
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
        }
        catch { }

        this.BackColor = Color.FromArgb(28, 28, 28);
        this.ForeColor = Color.FromArgb(242, 242, 242);

        Panel topNavHeader = BuildTopNavigationBar();
        Controls.Add(topNavHeader);

        Panel bottomPanel = BuildBottomActionBar();
        Controls.Add(bottomPanel);

        Panel contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(28, 28, 28),
            Padding = new Padding(0)
        };

        _pageGeneral = CreateGeneralPage();
        _pageGroups = CreateGroupsPage();
        _pageOverlay = CreateOverlayPage();
        _pageBehavior = CreateBehaviorPage();

        contentHost.Controls.Add(_pageGeneral);
        contentHost.Controls.Add(_pageGroups);
        contentHost.Controls.Add(_pageOverlay);
        contentHost.Controls.Add(_pageBehavior);
        Controls.Add(contentHost);

        contentHost.BringToFront();

        _mouseFilter = new KeyCaptureFilter(this);
        Application.AddMessageFilter(_mouseFilter);

        this.FormClosed += (s, e) =>
        {
            if (_mouseFilter != null) Application.RemoveMessageFilter(_mouseFilter);
            _context.NotifySettingsWindowState(false);
        };

        this.Resize += (s, e) => HandleWindowResize();
        this.KeyDown += SettingsForm_KeyDown;
        this.KeyUp += SettingsForm_KeyUp;

        _isInitializing = true;
        LoadSettingsIntoUI(_draftSettings);
        _isInitializing = false;

        SwitchTab(0);
        HandleWindowResize();
        MarkClean();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            int useImmersiveDarkMode = 1;
            DwmSetWindowAttribute(this.Handle, 20, ref useImmersiveDarkMode, sizeof(int));
            int cornerPreference = 2;
            DwmSetWindowAttribute(this.Handle, 33, ref cornerPreference, sizeof(int));
        }
        catch { }
    }

    private void HandleWindowResize()
    {
        if (_pillTrack == null) return;

        if (_isResizing)
        {
            _resizePending = true;
            return;
        }

        PerformWindowResizeLayout();
    }

    private void PerformWindowResizeLayout()
    {
        if (_pillTrack == null) return;

        this.SuspendLayout();
        try
        {
            int clientW = ClientSize.Width;
            int effectiveBarW = Math.Max(468, clientW - 36);
            int cardW = effectiveBarW;

            _pillTrack.Width = effectiveBarW;

            int tabGap = 4;
            int availW = effectiveBarW - 8 - (tabGap * 3);
            int tab1W = availW / 4;
            int tab2W = availW / 4;
            int tab3W = availW / 4;
            int tab4W = availW - tab1W - tab2W - tab3W;

            _navBtnGeneral.Left = 4;
            _navBtnGeneral.Width = tab1W;

            _navBtnGroups.Left = _navBtnGeneral.Right + tabGap;
            _navBtnGroups.Width = tab2W;

            _navBtnOverlay.Left = _navBtnGroups.Right + tabGap;
            _navBtnOverlay.Width = tab3W;

            _navBtnBehavior.Left = _navBtnOverlay.Right + tabGap;
            _navBtnBehavior.Width = tab4W;

            ModernButton activeNavBtn = _activeTabIndex switch
            {
                1 => _navBtnGroups,
                2 => _navBtnOverlay,
                3 => _navBtnBehavior,
                _ => _navBtnGeneral
            };
            AnimateNavPillTo(activeNavBtn, immediate: true);

            if (_btnSave != null && _btnRevert != null && _lblStatus != null)
            {
                _lblStatus.Width = effectiveBarW;
                _btnSave.Left = Math.Max(
                    _btnRevert.Right + 12,
                    clientW - 18 - _btnSave.Width);
            }

            if (!_isResizing || _activeTabIndex == 0)
            {
                ResizeExistingCollectionCards(cardW);
                ReflowGeneralPage();
            }

            if (_isInitializing)
                return;

            if (!_isResizing || _activeTabIndex == 1)
            {
                ResizeExistingGroupCards(cardW);
                _pageGroups?.RecalculateScroll();
            }

            if (!_isResizing || _activeTabIndex == 2 || _activeTabIndex == 3)
            {
                StretchPersonalizationControls(cardW);
                ReflowPersonalizationPage();
            }
        }
        finally
        {
            this.ResumeLayout(true);
        }
    }

    private Panel BuildTopNavigationBar()
    {
        Panel header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = Color.FromArgb(28, 28, 28)
        };

        _pillTrack = new Win11CardPanel
        {
            Left = 18,
            Top = 10,
            Width = 468,
            Height = 38,
            FillColor = Color.FromArgb(37, 37, 37),
            BorderColor = Color.FromArgb(52, 52, 52),
            CornerRadius = 8,
            Padding = new Padding(3),
            ShowHighlightPill = true,
            HighlightPillRect = new RectangleF(4, 4, 112, 30)
        };

        _navBtnGeneral = CreateNavTabButton("Sources", 4, 4, 112, () => SwitchTab(0));
        _navBtnGroups = CreateNavTabButton("Groups", 120, 4, 112, () => SwitchTab(1));
        _navBtnOverlay = CreateNavTabButton("Overlay", 236, 4, 112, () => SwitchTab(2));
        _navBtnBehavior = CreateNavTabButton("Behavior", 352, 4, 112, () => SwitchTab(3));

        _pillTrack.Controls.Add(_navBtnGeneral);
        _pillTrack.Controls.Add(_navBtnGroups);
        _pillTrack.Controls.Add(_navBtnOverlay);
        _pillTrack.Controls.Add(_navBtnBehavior);
        header.Controls.Add(_pillTrack);

        return header;
    }

    private void InitializeTabStateMachine()
    {
        if (_tabStateMachine != null) return;

        _pageGeneral.Visible = true;
        _pageGroups.Visible = false;
        _pageOverlay.Visible = false;
        _pageBehavior.Visible = false;

        _tabStateMachine = new StateMachine<SettingsTabState>(SettingsTabState.Sources);

        void UpdateNavButtons(int activeIdx)
        {
            StyleNavButton(_navBtnGeneral, activeIdx == 0);
            StyleNavButton(_navBtnGroups, activeIdx == 1);
            StyleNavButton(_navBtnOverlay, activeIdx == 2);
            StyleNavButton(_navBtnBehavior, activeIdx == 3);
        }

        _tabStateMachine.ConfigureState(
            SettingsTabState.Sources,
            onEnter: () =>
            {
                _activeTabIndex = 0;
                _pageGroups.Visible = false;
                _pageOverlay.Visible = false;
                _pageBehavior.Visible = false;
                _pageGeneral.Visible = true;
                _pageGeneral.BringToFront();

                UpdateNavButtons(0);
                AnimateNavPillTo(_navBtnGeneral, immediate: _isInitializing);

                int cardW = Math.Max(468, ClientSize.Width - 36);
                ResizeExistingCollectionCards(cardW);
                ReflowGeneralPage();
            },
            onExit: () => _pageGeneral.Visible = false);

        _tabStateMachine.ConfigureState(
            SettingsTabState.Groups,
            onEnter: () =>
            {
                _activeTabIndex = 1;
                _pageGeneral.Visible = false;
                _pageOverlay.Visible = false;
                _pageBehavior.Visible = false;
                _pageGroups.Visible = true;
                _pageGroups.BringToFront();

                UpdateNavButtons(1);
                AnimateNavPillTo(_navBtnGroups, immediate: _isInitializing);

                int cardW = Math.Max(468, ClientSize.Width - 36);
                ResizeExistingGroupCards(cardW);
                _pageGroups.RecalculateScroll();
            },
            onExit: () => _pageGroups.Visible = false);

        _tabStateMachine.ConfigureState(
            SettingsTabState.Overlay,
            onEnter: () =>
            {
                _activeTabIndex = 2;
                _pageGeneral.Visible = false;
                _pageGroups.Visible = false;
                _pageBehavior.Visible = false;
                _pageOverlay.Visible = true;
                _pageOverlay.BringToFront();

                UpdateNavButtons(2);
                AnimateNavPillTo(_navBtnOverlay, immediate: _isInitializing);

                int cardW = Math.Max(468, ClientSize.Width - 36);
                StretchPersonalizationControls(cardW);
                ReflowPersonalizationPage();
            },
            onExit: () => _pageOverlay.Visible = false);

        _tabStateMachine.ConfigureState(
            SettingsTabState.Behavior,
            onEnter: () =>
            {
                _activeTabIndex = 3;
                _pageGeneral.Visible = false;
                _pageGroups.Visible = false;
                _pageOverlay.Visible = false;
                _pageBehavior.Visible = true;
                _pageBehavior.BringToFront();

                UpdateNavButtons(3);
                AnimateNavPillTo(_navBtnBehavior, immediate: _isInitializing);

                int cardW = Math.Max(468, ClientSize.Width - 36);
                StretchPersonalizationControls(cardW);
                ReflowPersonalizationPage();
            },
            onExit: () => _pageBehavior.Visible = false);
    }

    private ModernButton CreateNavTabButton(string text, int left, int top, int width, Action onClick)
    {
        ModernButton btn = new ModernButton
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = 30,
            CornerRadius = 6,
            ShowBorder = false,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(200, 200, 200),
            BackColor = Color.FromArgb(37, 37, 37),
            HoverBackColor = Color.FromArgb(50, 50, 50),
            PressedBackColor = Color.FromArgb(44, 44, 44)
        };
        btn.Click += (s, e) => onClick();
        return btn;
    }

    private void SwitchTab(int index)
    {
        bool firstInit = (_tabStateMachine == null);
        InitializeTabStateMachine();
        CancelRecording();
        this.ActiveControl = null;

        SettingsTabState targetState = index switch
        {
            1 => SettingsTabState.Groups,
            2 => SettingsTabState.Overlay,
            3 => SettingsTabState.Behavior,
            _ => SettingsTabState.Sources
        };

        _tabStateMachine?.TransitionTo(targetState, force: _isInitializing || firstInit);
    }

    private void StyleNavButton(ModernButton btn, bool active)
    {
        btn.ForeColor = active ? Color.White : Color.FromArgb(185, 185, 185);
        btn.Invalidate();
    }

    private void AnimateNavPillTo(ModernButton targetBtn, bool immediate = false)
    {
        if (_pillTrack == null || targetBtn == null) return;

        if (immediate || !this.IsHandleCreated)
        {
            _navPillAnimTimer?.Stop();
            _pillTrack.HighlightPillRect = new RectangleF(targetBtn.Left, targetBtn.Top, targetBtn.Width, targetBtn.Height);
            InvalidateNavTrackAndButtons();
            return;
        }

        _navPillStartX = _pillTrack.HighlightPillRect.X;
        _navPillStartW = _pillTrack.HighlightPillRect.Width;
        _navPillTargetX = targetBtn.Left;
        _navPillTargetW = targetBtn.Width;

        const double durationMs = 120.0;
        _navPillWatch.Restart();

        if (_navPillAnimTimer == null)
        {
            _navPillAnimTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _navPillAnimTimer.Tick += (s, e) =>
            {
                if (_pillTrack.IsDisposed)
                {
                    _navPillAnimTimer.Stop();
                    return;
                }

                double t = Math.Clamp(_navPillWatch.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
                float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

                if (t >= 1.0)
                {
                    _pillTrack.HighlightPillRect = new RectangleF(_navPillTargetX, 4f, _navPillTargetW, 30f);
                    _navPillAnimTimer.Stop();
                }
                else
                {
                    float curX = _navPillStartX + (_navPillTargetX - _navPillStartX) * ease;
                    float curW = _navPillStartW + (_navPillTargetW - _navPillStartW) * ease;
                    _pillTrack.HighlightPillRect = new RectangleF(curX, 4f, curW, 30f);
                }

                InvalidateNavTrackAndButtons();
            };
        }

        _navPillAnimTimer.Start();
    }

    private void InvalidateNavTrackAndButtons()
    {
        _pillTrack?.Invalidate();
        _navBtnGeneral?.Invalidate();
        _navBtnGroups?.Invalidate();
        _navBtnOverlay?.Invalidate();
        _navBtnBehavior?.Invalidate();
    }

    private Panel BuildBottomActionBar()
    {
        Panel bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 72,
            BackColor = Color.FromArgb(24, 24, 24)
        };

        bottomPanel.Paint += (s, e) =>
        {
            using Pen dividerPen = new Pen(Color.FromArgb(48, 48, 48), 1);
            e.Graphics.DrawLine(dividerPen, 0, 0, bottomPanel.Width, 0);
        };

        _lblStatus = new Label
        {
            Text = "",
            Left = 18,
            Top = 6,
            Width = 468,
            Height = 18,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(240, 190, 80)
        };
        bottomPanel.Controls.Add(_lblStatus);

        _btnRevert = CreateModernButton("Revert", 18, 29, 160, 33, false);
        _btnRevert.Click += (s, e) => RevertChanges();
        bottomPanel.Controls.Add(_btnRevert);

        _btnSave = CreateModernButton("Save && Apply", 302, 29, 184, 33, true);
        _btnSave.Click += (s, e) => SaveChanges();
        bottomPanel.Controls.Add(_btnSave);

        return bottomPanel;
    }

    private ModernButton CreateModernButton(string text, int left, int top, int width, int height, bool isPrimary, bool isDanger = false)
    {
        Color backCol = isDanger
            ? Color.FromArgb(165, 45, 50)
            : (isPrimary ? Color.FromArgb(0, 120, 212) : Color.FromArgb(52, 52, 52));
        Color hoverCol = isDanger
            ? Color.FromArgb(185, 55, 60)
            : (isPrimary ? Color.FromArgb(20, 138, 230) : Color.FromArgb(66, 66, 66));
        Color pressCol = isDanger
            ? Color.FromArgb(140, 35, 40)
            : (isPrimary ? Color.FromArgb(0, 100, 180) : Color.FromArgb(44, 44, 44));
        Color borderCol = isDanger
            ? Color.FromArgb(195, 70, 75)
            : (isPrimary ? Color.FromArgb(30, 144, 255) : Color.FromArgb(70, 70, 70));

        return new ModernButton
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            CornerRadius = 6,
            BackColor = backCol,
            HoverBackColor = hoverCol,
            PressedBackColor = pressCol,
            BorderColor = borderCol,
            ForeColor = Color.White,
            ShowBorder = true,
            Font = new Font("Segoe UI Semibold", 8.75f, FontStyle.Bold)
        };
    }

    private static Color SafeParseHexColor(string hex, Color fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex))
                return ColorTranslator.FromHtml(hex);
        }
        catch { }
        return fallback;
    }

    private RoundedColorSwatch CreateCompactColorSwatch(
     int left,
     int top,
     int width,
     int height,
     string initialHex,
     Action<string> onColorPicked,
     CrossfadeCyclePair? ownerPair = null,
     PairCollection? ownerCol = null)
 {
     RoundedColorSwatch swatch = new RoundedColorSwatch
     {
         Left = left,
         Top = top,
         Width = width,
         Height = height,
         CornerRadius = 6,
         SwatchColor = SafeParseHexColor(initialHex, Color.FromArgb(27, 59, 90))
     };

     swatch.Click += (s, e) =>
     {
         Color originalColor = swatch.SwatchColor;
         string originalHex = ColorTranslator.ToHtml(originalColor);
         bool isFirstOpen = true;

         void PushColorPreview(Color liveCol, bool keepPinned)
         {
             string liveHex = $"#{liveCol.R:X2}{liveCol.G:X2}{liveCol.B:X2}";
             swatch.SwatchColor = liveCol;
             swatch.Invalidate();
             onColorPicked(liveHex);

             SyncDraftPersonalizationFields();
             var previewDraft = _draftSettings.Clone();
             var targetCol = ownerCol ?? previewDraft.Collections.FirstOrDefault();
             if (targetCol != null)
             {
                 previewDraft.ApplyCollectionToActiveOverlayFields(targetCol, ownerPair);
             }

             _context.TriggerOverlayPreview(
                 previewDraft,
                 isColorPickerLive: keepPinned,
                 visualOnly: true,
                 animateTransition: isFirstOpen);
             isFirstOpen = false;
         }

         using var dlg = new ModernColorPickerDialog(originalColor, _draftSettings, liveCol => PushColorPreview(liveCol, keepPinned: true));
         if (dlg.ShowDialog(this) == DialogResult.OK)
         {
             swatch.SwatchColor = dlg.SelectedColor;
             swatch.Invalidate();
             onColorPicked(dlg.SelectedHex);
             MarkDirty();
             PushColorPreview(dlg.SelectedColor, keepPinned: false);
         }
         else
         {
             swatch.SwatchColor = originalColor;
             swatch.Invalidate();
             onColorPicked(originalHex);
             PushColorPreview(originalColor, keepPinned: false);
         }
     };

     return swatch;
 }

    private Bitmap? ResolveDropdownIcon(string item)
    {
        if (string.IsNullOrWhiteSpace(item))
            return null;
        return AppIconCache.GetIconForSource(item, _draftSettings);
    }

    private BadgeInfo ResolveDropdownBadge(string item)
    {
        if (string.IsNullOrWhiteSpace(item))
            return new BadgeInfo("", Color.Transparent);
        if (item.StartsWith("Group: ", StringComparison.OrdinalIgnoreCase))
            return new BadgeInfo("GROUP", Color.FromArgb(180, 140, 255));
        if (_detectedAudio.PlayingAudio.Contains(item))
            return new BadgeInfo("PLAYING AUDIO", Color.FromArgb(75, 220, 130));
        if (_detectedAudio.ActiveAudioSessions.Contains(item))
            return new BadgeInfo("AUDIO MIXER", Color.FromArgb(80, 185, 255));
        if (_draftSettings.RecentPrograms.Contains(item, StringComparer.OrdinalIgnoreCase))
            return new BadgeInfo("RECENT", Color.FromArgb(210, 175, 95));
        if (_detectedAudio.RunningWindowedApps.Contains(item))
            return new BadgeInfo("RUNNING APP", Color.FromArgb(160, 160, 160));

        return new BadgeInfo("", Color.Transparent);
    }

    private void BrowseForExecutable(Control targetControl)
    {
        using OpenFileDialog dlg = new OpenFileDialog
        {
            Title = "Select an Application (.exe)",
            Filter = "Applications (*.exe)|*.exe|All Files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            string exeName = AppSettings.NormalizeExeName(dlg.FileName);
            if (!string.IsNullOrEmpty(exeName))
            {
                _draftSettings.RecordProgramHistory(exeName, dlg.FileName);
                RefreshAudioDetectionAndDropdowns();
                targetControl.Text = exeName;
                MarkDirty();
            }
        }
    }

    private void RefreshAudioDetectionAndDropdowns()
    {
        _detectedAudio = AudioProcessDetector.Scan();
    }

    private static string ReadComboText(ModernComboBox cmb)
    {
        return (cmb.SelectedItem?.ToString() ?? cmb.Text ?? "").Trim();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        BeginInvoke(new Action(() =>
        {
            HandleWindowResize();
        }));
    }

    private void UpdateHostPairButtonsState(Panel? host, List<CrossfadeCyclePair> list)
    {
        if (host == null) return;
        int totalPairsCount = _draftSettings.CrossfadeCycleList.Count + _draftSettings.StandalonePairs.Count;

        foreach (Control c in host.Controls)
        {
            if (c is not Win11CardPanel card || card.Tag is not CrossfadeCyclePair pair) continue;
            int idx = list.IndexOf(pair);

            foreach (Control sub in card.Controls)
            {
                if (sub is not Panel p || p.Tag as string != "TopBlockPanel") continue;
                foreach (Control btn in p.Controls)
                {
                    if (btn.Tag is not string t) continue;
                    if (t == "UpBtn") btn.Enabled = (idx > 0);
                    else if (t == "DownBtn") btn.Enabled = (idx >= 0 && idx < list.Count - 1);
                    else if (t == "DeleteBtn") btn.Enabled = (list.Count > 1);
                }
            }
        }
    }

    private void RefreshAllPairButtonsState()
    {
        foreach (var colCard in GetOrderedCollectionCards())
        {
            if (colCard.Tag is CollectionCardContext ctx)
            {
                UpdateHostPairButtonsState(ctx.PairsHost, ctx.Collection.Pairs);
                ctx.DeleteCollectionBtn.Enabled = _draftSettings.Collections.Count > 1;
            }
        }
    }

    private void AddSideBySideKeybindColumn(Control parent, string title, int left, int width, int topY, int initialKey, Action<int> onCommit, int colIndex = 0)
    {
        Label lbl = new Label
        {
            Text = title,
            Left = left,
            Top = topY,
            Width = width,
            Height = 15,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8f, FontStyle.Regular),
            ForeColor = Color.FromArgb(190, 190, 190),
            Tag = $"KeyCol_{colIndex}"
        };
        parent.Controls.Add(lbl);

        KeybindBox kb = new KeybindBox
        {
            Left = left,
            Top = topY + 17,
            Width = width,
            Height = 25,
            Binding = initialKey,
            Tag = $"KeyCol_{colIndex}"
        };
        kb.Click += (s, e) => StartRecording(kb, onCommit);
        parent.Controls.Add(kb);
    }

    private bool _isGroupListAnimating = false;

    private List<Win11CardPanel> GetOrderedGroupCards()
    {
        if (_groupsSlotsHost == null) return new List<Win11CardPanel>();
        return _groupsSlotsHost.Controls
            .OfType<Win11CardPanel>()
            .OrderBy(c =>
            {
                if (c.Tag is ProgramGroup grp)
                {
                    int idx = _draftSettings.Groups.IndexOf(grp);
                    if (idx >= 0) return idx;
                }
                return int.MaxValue;
            })
            .ToList();
    }

    private void PopulateSingleComboOptions(ModernComboBox cmb, bool includeGroups)
    {
        string currentText = ReadComboText(cmb);
        var list = includeGroups ? GetAvailableSourceOptions() : GetExecutableSuggestions();
        _isInitializing = true;
        cmb.Items.Clear();
        cmb.Items.AddRange(list.Cast<object>().ToArray());
        cmb.SelectedItem = currentText;
        if (cmb.SelectedIndex == -1) cmb.Text = currentText;
        _isInitializing = false;
    }

    private void MarkDirty()
    {
        if (_isInitializing) return;
        if (_lblStatus != null)
        {
            _lblStatus.ForeColor = Color.FromArgb(255, 200, 90);
            _lblStatus.Text = "● Unsaved changes";
        }
    }

    private void MarkClean()
    {
        if (_lblStatus != null)
        {
            _lblStatus.ForeColor = Color.FromArgb(95, 215, 135);
            _lblStatus.Text = "✓ All settings saved";
        }
    }

    public void SyncActiveSourcesFromCycle(AppSettings mutated)
    {
        _isInitializing = true;

        _draftSettings.Source1 = mutated.Source1;
        _draftSettings.Source1Color = mutated.Source1Color;
        _draftSettings.Source2 = mutated.Source2;
        _draftSettings.Source2Color = mutated.Source2Color;

        _savedSettingsSnapshot.Source1 = mutated.Source1;
        _savedSettingsSnapshot.Source1Color = mutated.Source1Color;
        _savedSettingsSnapshot.Source2 = mutated.Source2;
        _savedSettingsSnapshot.Source2Color = mutated.Source2Color;

        _isInitializing = false;
    }

    private void LoadSettingsIntoUI(AppSettings settings)
    {
        _detectedAudio = AudioProcessDetector.Scan();
        RefreshGroupsTabUI();
        settings.EnsureCollectionsInitialized();
        PopulateCycleListUI();

        _sldStepSize.Value = Math.Clamp(settings.StepSize, 1, 50);
        _chkBuffer.Checked = settings.EnableEqualizationBuffer;
        _chkStartup.Checked = settings.RunOnStartup;

        _chkKeepOverlayAlwaysOn.Checked = settings.KeepOverlayOnWhileSettingsOpen;
        _cmbIdentityMode.SelectedItem = settings.OverlayIdentityMode;
        _cmbLevelMode.SelectedItem = settings.OverlayLevelMode;

        _chkUseSourceBgColors.Checked = settings.UseSourceBackgroundColors;
        _chkFadeBgWithVolume.Checked = settings.FadeBgColorWithVolume;
        _chkFadeBgWithVolume.Enabled = settings.UseSourceBackgroundColors;

        if (_unmutedColorBox != null)
        {
            _unmutedColorBox.SwatchColor = SafeParseHexColor(settings.UnmutedColor, Color.FromArgb(0, 168, 255));
            _unmutedColorBox.Invalidate();
        }
        if (_mutedColorBox != null)
        {
            _mutedColorBox.SwatchColor = SafeParseHexColor(settings.MutedColor, Color.FromArgb(255, 71, 87));
            _mutedColorBox.Invalidate();
        }

        _cmbPosition.SelectedItem = settings.OverlayPosition;
        _txtPopupDuration.Text = settings.PopupDurationMs.ToString();
        _sldOverlayOpacity.Value = Math.Clamp(settings.OverlayOpacity, 10, 100);
        _txtWidth.Text = settings.OverlayWidth.ToString();
        _txtHeight.Text = settings.OverlayHeight.ToString();
        _chkInterpolation.Checked = settings.EnableInterpolation;
        _sldInterpSpeed.Value = Math.Clamp(settings.InterpolationSpeed, 5, 100);
        _sldInterpSpeed.Enabled = settings.EnableInterpolation;

        ReflowGeneralPage();
        ReflowPersonalizationPage();
    }

    private void RevertChanges()
    {
        _isInitializing = true;
        _draftSettings = _savedSettingsSnapshot.Clone();
        LoadSettingsIntoUI(_draftSettings);
        _isInitializing = false;
        MarkClean();
        _context.NotifySettingsWindowState(true, _draftSettings);
    }

    private void RebindGroupExeRows(Win11CardPanel card, ProgramGroup group)
    {
        var rows = card.Controls.Cast<Control>()
            .Where(c => c.Tag as string == "GroupExeRow")
            .OrderBy(c => c.Top)
            .ToList();

        for (int i = 0; i < rows.Count; i++)
        {
            Control row = rows[i];
            foreach (Control rc in row.Controls)
            {
                if (rc.Tag as string == "RemoveExeBtn")
                {
                    rc.Enabled = rows.Count > 1;
                }
            }
        }
    }

    private List<string> GetExecutableSuggestions()
    {
        List<string> list = new List<string>();
        list.AddRange(_detectedAudio.PlayingAudio);
        list.AddRange(_detectedAudio.ActiveAudioSessions);
        list.AddRange(_draftSettings.RecentPrograms);
        list.AddRange(_detectedAudio.RunningWindowedApps);
        return list.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private List<string> GetAvailableSourceOptions()
    {
        List<string> options = new List<string>();

        foreach (var group in _draftSettings.Groups)
        {
            if (!string.IsNullOrWhiteSpace(group.GroupName))
            {
                options.Add($"Group: {group.GroupName}");
            }
        }

        options.AddRange(GetExecutableSuggestions());
        return options.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void AddSectionHeader(Control parent, string text, int left, ref int top)
    {
        Label lbl = new Label
        {
            Text = text,
            Left = left,
            Top = top,
            Width = 380,
            Height = 19,
            Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 205, 255)
        };
        parent.Controls.Add(lbl);
        top += 22;
    }

    private Label AddAlignedLabel(Control parent, string text, int left, int top)
    {
        Label lbl = new Label
        {
            Text = text,
            Left = left,
            Top = top + 3,
            Width = 132,
            Height = 20,
            ForeColor = Color.FromArgb(210, 210, 210)
        };
        parent.Controls.Add(lbl);
        return lbl;
    }

    private KeybindBox AddKeybindBoxControl(Control parent, int left, int width, ref int top, Action<int> onCommit)
    {
        KeybindBox kb = new KeybindBox
        {
            Left = left,
            Top = top - 1,
            Width = width,
            Height = 25
        };
        kb.Click += (s, e) => StartRecording(kb, onCommit);
        parent.Controls.Add(kb);

        top += 30;
        return kb;
    }

    private RoundedColorSwatch AddColorPickerRow(Control parent, string labelText, string initialHex, Action<string> onColorChanged, ref int top)
    {
        Label lbl = new Label
        {
            Text = labelText,
            Left = 14,
            Top = top + 3,
            Width = 165,
            ForeColor = Color.FromArgb(200, 200, 200)
        };
        parent.Controls.Add(lbl);

        RoundedColorSwatch colorBox = new RoundedColorSwatch
        {
            Left = 185,
            Top = top,
            Width = 265,
            Height = 22,
            CornerRadius = 6,
            SwatchColor = SafeParseHexColor(initialHex, Color.FromArgb(0, 168, 255))
        };

        colorBox.Click += (s, e) =>
        {
            Color originalColor = colorBox.SwatchColor;
            string originalHex = ColorTranslator.ToHtml(originalColor);
            bool isFirstOpen = true;

            using var dlg = new ModernColorPickerDialog(originalColor, _draftSettings, liveCol =>
            {
                string liveHex = $"#{liveCol.R:X2}{liveCol.G:X2}{liveCol.B:X2}";
                colorBox.SwatchColor = liveCol;
                colorBox.Invalidate();
                onColorChanged(liveHex);
                SyncDraftPersonalizationFields();
                _context.TriggerOverlayPreview(_draftSettings, isColorPickerLive: true, visualOnly: true, animateTransition: isFirstOpen);
                isFirstOpen = false;
            });

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                colorBox.SwatchColor = dlg.SelectedColor;
                colorBox.Invalidate();
                onColorChanged(dlg.SelectedHex);
                MarkDirty();
                SyncDraftPersonalizationFields();
                _context.TriggerOverlayPreview(_draftSettings, isColorPickerLive: false, visualOnly: true, animateTransition: false);
            }
            else
            {
                colorBox.SwatchColor = originalColor;
                colorBox.Invalidate();
                onColorChanged(originalHex);
                SyncDraftPersonalizationFields();
                _context.TriggerOverlayPreview(_draftSettings, isColorPickerLive: false, visualOnly: true, animateTransition: false);
            }
        };

        parent.Controls.Add(colorBox);
        top += 30;
        return colorBox;
    }

    private void SettingsForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isRecording || _activeKeybindBox == null) return;

        // Pressing Esc clears the keybind
        if (e.KeyCode == Keys.Escape)
        {
            _activeKeybindBox.Binding = 0;
            _activeKeyCommitCallback?.Invoke(0);
            MarkDirty();
            StopRecording();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu ||
            e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.RControlKey ||
            e.KeyCode == Keys.LShiftKey || e.KeyCode == Keys.RShiftKey ||
            e.KeyCode == Keys.LMenu || e.KeyCode == Keys.RMenu)
        {
            _activeKeybindBox.Text = KeybindHelper.FormatLiveModifiers(e.Modifiers);
            _activeKeybindBox.Invalidate();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        int combinedBinding = KeybindHelper.CreateBinding(e.KeyCode, e.Modifiers);
        _activeKeybindBox.Binding = combinedBinding;
        _activeKeyCommitCallback?.Invoke(combinedBinding);
        MarkDirty();

        StopRecording();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void SettingsForm_KeyUp(object? sender, KeyEventArgs e)
    {
        if (!_isRecording || _activeKeybindBox == null) return;
        _activeKeybindBox.Text = KeybindHelper.FormatLiveModifiers(e.Modifiers);
        _activeKeybindBox.Invalidate();
    }

    private void StartRecording(KeybindBox targetBox, Action<int>? onCommit = null)
    {
        if (_isRecording && _activeKeybindBox == targetBox)
        {
            CancelRecording();
            return;
        }

        if (_isRecording) CancelRecording();
        _isRecording = true;
        _activeKeybindBox = targetBox;
        _activeKeyCommitCallback = onCommit;

        targetBox.IsRecording = true;
        targetBox.Text = "Press keys... (Esc = Clear)";
        targetBox.Invalidate();
    }

    private void StopRecording()
    {
        if (!_isRecording) return;
        if (_activeKeybindBox != null)
        {
            _activeKeybindBox.IsRecording = false;
        }
        _isRecording = false;
        _activeKeybindBox = null;
        _activeKeyCommitCallback = null;
    }

    private void CancelRecording()
    {
        if (!_isRecording) return;
        StopRecording();
    }

    private class KeyCaptureFilter : IMessageFilter
    {
        private readonly SettingsForm _form;
        public KeyCaptureFilter(SettingsForm form) => _form = form;

        public bool PreFilterMessage(ref Message m)
        {
            if ((m.Msg == 0x0201 || m.Msg == 0x0204) && _form._isRecording)
            {
                Control? clicked = Control.FromHandle(m.HWnd);
                if (clicked != _form._activeKeybindBox)
                {
                    _form.CancelRecording();
                }
            }
            return false;
        }
    }

    private void SaveChanges()
    {
        _draftSettings.EnsureCollectionsInitialized();
        if (_draftSettings.Collections.Count == 0 || _draftSettings.Collections.All(c => c.Pairs.Count == 0))
        {
            MessageBox.Show("You must have at least one collection with at least one source pair configured.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        for (int cIdx = 0; cIdx < _draftSettings.Collections.Count; cIdx++)
        {
            var col = _draftSettings.Collections[cIdx];
            string colTitle = string.IsNullOrWhiteSpace(col.Name) ? $"Collection #{cIdx + 1}" : col.Name;
            if (!ValidateAndNormalizePairList(col.Pairs, colTitle)) return;
        }

        int ParseInt(string text, int defaultVal) => int.TryParse(text, out int val) ? val : defaultVal;

        _draftSettings.RunOnStartup = _chkStartup.Checked;
        _draftSettings.KeepOverlayOnWhileSettingsOpen = _chkKeepOverlayAlwaysOn.Checked;
        _draftSettings.OverlayPosition = _cmbPosition.SelectedItem?.ToString() ?? "Bottom Center";
        _draftSettings.PopupDurationMs = Math.Clamp(ParseInt(_txtPopupDuration.Text, 2500), 500, 10000);
        _draftSettings.OverlayOpacity = _sldOverlayOpacity.Value;
        _draftSettings.StepSize = _sldStepSize.Value;
        _draftSettings.OverlayWidth = Math.Clamp(ParseInt(_txtWidth.Text, 360), 120, 2000);
        _draftSettings.OverlayHeight = Math.Clamp(ParseInt(_txtHeight.Text, 80), 50, 1000);
        _draftSettings.EnableInterpolation = _chkInterpolation.Checked;
        _draftSettings.InterpolationSpeed = _sldInterpSpeed.Value;
        _draftSettings.EnableEqualizationBuffer = _chkBuffer.Checked;
        _draftSettings.OverlayIdentityMode = _cmbIdentityMode.SelectedItem?.ToString() ?? "Both";
        _draftSettings.OverlayLevelMode = _cmbLevelMode.SelectedItem?.ToString() ?? "Both";
        _draftSettings.UseSourceBackgroundColors = _chkUseSourceBgColors.Checked;
        _draftSettings.FadeBgColorWithVolume = _chkFadeBgWithVolume.Checked;

        _draftSettings.StandalonePairs.Clear();
        var activeCol = _draftSettings.Collections[Math.Clamp(_draftSettings.ActiveCollectionIndex, 0, _draftSettings.Collections.Count - 1)];
        _draftSettings.ApplyCollectionToActiveOverlayFields(activeCol);

        foreach (var group in _draftSettings.Groups)
        {
            group.Executables = group.Executables
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(AppSettings.NormalizeExeName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var exe in group.Executables)
            {
                _draftSettings.RecordProgramHistory(exe);
            }
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (_draftSettings.RunOnStartup) key?.SetValue("AudioCrossfader", $"\"{exePath}\"");
            else key?.DeleteValue("AudioCrossfader", false);
        }
        catch { }

        _savedSettingsSnapshot = _draftSettings.Clone();
        _context.SaveSettings(_draftSettings);

        RefreshGroupsTabUI();
        PopulateCycleListUI();
        ReflowGeneralPage();
        ReflowPersonalizationPage();
        MarkClean();
    }
}