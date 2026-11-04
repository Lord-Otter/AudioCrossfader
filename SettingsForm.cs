using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

// Windows 11 Dark Mode Settings Window
public class SettingsForm : Form
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
    private static class PairCardLayout
    {
        public const int CardPaddingTop = 7;
        public const int CardPaddingBottom = 7;
        public const int RowHeight = 25;
        public const int RowVerticalGap = 6;
        public const int StandaloneSectionGap = 6;
        public const int KeybindSectionGap = 7;
        public const int KeybindBlockHeight = 42;
        public const int CardListGap = 6;

        public const int ComboOffsetY = 0;
        public const int ButtonOffsetY = 0;
        public const int SwatchOffsetY = 0;
        public const int LabelOffsetY = 4;
        public const int CheckBoxOffsetY = 2;

        public static int RowStep => RowHeight + RowVerticalGap;

        public static int ModeRowY => 0;
        public static int Source1RowY => RowStep;
        public static int TopBlockHeight => Source1RowY + RowHeight + 1;
        public static int DeleteButtonHeight => (Source1RowY + RowHeight) - ButtonOffsetY;

        public static int GetRow2PanelTop(bool isSingle) =>
            isSingle
                ? (CardPaddingTop + Source1RowY)
                : (CardPaddingTop + Source1RowY + RowStep);

        public static int GetBottomPanelTop(bool isSingle) =>
            (isSingle
                ? (CardPaddingTop + Source1RowY + RowHeight)
                : (GetRow2PanelTop(false) + RowHeight)) + StandaloneSectionGap;

        public static int KeybindColumnsTop => RowHeight + KeybindSectionGap;
        public static int BottomPanelHeight => KeybindColumnsTop + KeybindBlockHeight + 1;

        public static int GetCardHeight(bool isCarouselList, bool isSingle)
        {
            if (isCarouselList)
            {
                int contentBottom = isSingle
                    ? (CardPaddingTop + Source1RowY + RowHeight)
                    : (GetRow2PanelTop(false) + RowHeight);
                return contentBottom + CardPaddingBottom;
            }
            else
            {
                return GetBottomPanelTop(isSingle) + BottomPanelHeight + CardPaddingBottom;
            }
        }
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

    private Win11CardPanel _cycleListCard = null!;
    private Label _lblCycleCardTitle = null!;
    private ModernButton _btnAddCycleEntry = null!;
    private Panel _cycleSlotsHost = null!;
    private Panel _globalKeysHost = null!;

    private BufferedPanel _globalKeysHeaderBar = null!;
    private BufferedPanel _globalKeysShutterPanel = null!;
    private bool _isGlobalKeysExpanded = false;
    private bool _isGlobalKeysHovered = false;
    private float _globalKeysAnimProgress = 0f;
    private System.Windows.Forms.Timer? _globalKeysAnimTimer;
    private const int GlobalKeysHeaderHeight = 30;
    private int _globalKeysExpandedContentHeight = 182;

    private Win11CardPanel _standaloneListCard = null!;
    private Label _lblStandaloneCardTitle = null!;
    private ModernButton _btnAddStandaloneEntry = null!;
    private Panel _standaloneSlotsHost = null!;
    private Win11CardPanel _groupsMasterCard = null!;
    private ModernButton _btnAddGroupEntry = null!;
    private Panel _groupsSlotsHost = null!;

    private Win11CardPanel _previewCard = null!;
    private Win11CardPanel _overlayStyleCard = null!;
    private Win11CardPanel _carouselCard = null!;
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
    private System.Windows.Forms.Timer? _carouselCardAnimTimer;

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

            if (_cycleListCard != null)
            {
                _cycleListCard.Width = cardW;
                _btnAddCycleEntry.Left = cardW - 14 - _btnAddCycleEntry.Width;
                _cycleSlotsHost.Width = cardW - 24;

                if (_globalKeysHost != null)
                {
                    _globalKeysHost.Width = cardW - 24;
                    if (_globalKeysHeaderBar != null)
                    {
                        _globalKeysHeaderBar.Width = _globalKeysHost.Width;
                        _globalKeysHeaderBar.Invalidate();
                    }
                    if (_globalKeysShutterPanel != null)
                    {
                        _globalKeysShutterPanel.Width = _globalKeysHost.Width;
                    }

                    int kbWidth = Math.Max(220, _globalKeysHost.Width - 142);

                    _kbKey1.Width = kbWidth;
                    _kbKey2.Width = kbWidth;
                    _kbKey3.Width = kbWidth;
                    _cmbGlobalMuteMode.Width = kbWidth;
                    _kbKeyCycleNext.Width = kbWidth;
                    _kbKeyCyclePrev.Width = kbWidth;
                }
            }

            if (_standaloneListCard != null)
            {
                _standaloneListCard.Width = cardW;
                _btnAddStandaloneEntry.Left = cardW - 14 - _btnAddStandaloneEntry.Width;
                _standaloneSlotsHost.Width = cardW - 24;
            }

            if (_isInitializing)
                return;

            if (!_isResizing || _activeTabIndex == 0)
            {
                ResizeExistingPairCards(_cycleSlotsHost);
                ResizeExistingPairCards(_standaloneSlotsHost);
                ReflowGeneralPage();
            }

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

    private void StretchPersonalizationControls(int cardW)
    {
        if (_previewCard == null) return;

        _previewCard.Width = cardW;
        _overlayStyleCard.Width = cardW;
        _carouselCard.Width = cardW;
        _layoutCard.Width = cardW;
        _behaviorCard.Width = cardW;

        int rightStretchW = Math.Max(265, cardW - 185 - 18);

        _cmbIdentityMode.Width = rightStretchW;
        _cmbLevelMode.Width = rightStretchW;
        _sldOverlayOpacity.Width = rightStretchW;
        _unmutedColorBox.Width = rightStretchW;
        _mutedColorBox.Width = rightStretchW;
        if (_cmbCarouselMode != null) _cmbCarouselMode.Width = rightStretchW;
        _cmbCarouselStyle.Width = rightStretchW;
        if (_cmbNeighborPreviewMode != null) _cmbNeighborPreviewMode.Width = rightStretchW;
        if (_sldCarouselOpenSpeed != null) _sldCarouselOpenSpeed.Width = rightStretchW;
        if (_sldCarouselSpeed != null) _sldCarouselSpeed.Width = rightStretchW;
        if (_cmbCardOrder != null) _cmbCardOrder.Width = rightStretchW;
        _cmbPosition.Width = rightStretchW;
        if (_sldStepSize != null) _sldStepSize.Width = rightStretchW;
        if (_sldInterpSpeed != null) _sldInterpSpeed.Width = rightStretchW;

        _chkCarouselBg.Left = 185;

        if (_monitorRowsPanel != null)
        {
            int panelW = cardW - 32;
            _monitorRowsPanel.Width = panelW;
            int monComboW = Math.Max(160, panelW - 236);
            foreach (Control c in _monitorRowsPanel.Controls)
            {
                if (c is ModernComboBox cmb)
                {
                    cmb.Left = 236;
                    cmb.Width = monComboW;
                }
            }
        }
    }

    private void ResizeExistingGroupCards(int cardW)
    {
        if (_groupsMasterCard == null || _groupsSlotsHost == null) return;

        _groupsMasterCard.Width = cardW;
        _btnAddGroupEntry.Left = cardW - 14 - _btnAddGroupEntry.Width;

        int hostW = Math.Max(442, cardW - 24);
        _groupsSlotsHost.Width = hostW;

        int btnDelW = 104;
        int btnDelGroupLeft = hostW - btnDelW - 8;
        int nameBoxLeft = 106;
        int nameBoxW = Math.Max(130, btnDelGroupLeft - nameBoxLeft - 8);

        int exeComboW = Math.Max(180, hostW - 154);
        int btnBrowseLeft = 10 + exeComboW + 6;
        int btnRemExeLeft = btnBrowseLeft + 94 + 4;

        foreach (Control c in _groupsSlotsHost.Controls)
        {
            if (c is not Win11CardPanel groupCard) continue;
            groupCard.Width = hostW;

            foreach (Control child in groupCard.Controls)
            {
                if (child.Tag is not string tag) continue;
                switch (tag)
                {
                    case "GroupNameTxt":
                        child.Left = nameBoxLeft;
                        child.Width = nameBoxW;
                        break;
                    case "DeleteGroupBtn":
                        child.Left = btnDelGroupLeft;
                        break;
                    case "GroupExeRow":
                        child.Width = hostW - 4;
                        foreach (Control rc in child.Controls)
                        {
                            if (rc.Tag as string == "ExeCombo") rc.Width = exeComboW;
                            else if (rc.Tag as string == "BrowseExeBtn") rc.Left = btnBrowseLeft;
                            else if (rc.Tag as string == "RemoveExeBtn") rc.Left = btnRemExeLeft;
                        }
                        break;
                }
            }
        }

        ReflowGroupsPage();
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

                ResizeExistingPairCards(_cycleSlotsHost);
                ResizeExistingPairCards(_standaloneSlotsHost);
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
        CrossfadeCyclePair? ownerPair = null)
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

            void PushColorPreview(Color liveCol)
            {
                string liveHex = $"#{liveCol.R:X2}{liveCol.G:X2}{liveCol.B:X2}";
                swatch.SwatchColor = liveCol;
                swatch.Invalidate();
                onColorPicked(liveHex);

                SyncDraftPersonalizationFields();
                var previewDraft = _draftSettings.Clone();
                if (ownerPair != null && !string.IsNullOrWhiteSpace(ownerPair.Source1))
                {
                    previewDraft.Source1 = AppSettings.NormalizeExeName(ownerPair.Source1);
                    previewDraft.Source1Color = ownerPair.Color1Hex;
                    previewDraft.Source2 = ownerPair.IsSingleMode ? "" : AppSettings.NormalizeExeName(ownerPair.Source2);
                    previewDraft.Source2Color = ownerPair.Color2Hex;
                    previewDraft.ActiveControlMode = ownerPair.ControlMode;
                }
                _context.TriggerOverlayPreview(previewDraft);
            }

            using var dlg = new ModernColorPickerDialog(originalColor, _draftSettings, PushColorPreview);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                swatch.SwatchColor = dlg.SelectedColor;
                swatch.Invalidate();
                onColorPicked(dlg.SelectedHex);
                MarkDirty();
                PushLivePreviewIfActive(forceTrigger: true);
            }
            else
            {
                // Revert back to original color if user cancels
                swatch.SwatchColor = originalColor;
                swatch.Invalidate();
                onColorPicked(originalHex);
                PushLivePreviewIfActive(forceTrigger: true);
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

    private DarkScrollPanel CreateGeneralPage()
    {
        DarkScrollPanel page = new DarkScrollPanel { Dock = DockStyle.Fill };
        int y = 4;

        _cycleListCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468,
            Height = 44
        };

        _lblCycleCardTitle = new Label
        {
            Text = "Carousel Pairs && Global Hotkeys",
            Left = 14,
            Top = 12,
            Width = 290,
            Height = 20,
            Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 205, 255)
        };
        _cycleListCard.Controls.Add(_lblCycleCardTitle);

        _btnAddCycleEntry = CreateModernButton("+ Add Pair", 334, 9, 120, 26, true);
        _btnAddCycleEntry.Click += (s, e) =>
        {
            if (_isPairListAnimating) return;
            var newPair = new CrossfadeCyclePair
            {
                Source1 = "",
                Color1Hex = "#0E0466",
                Source2 = "",
                Color2Hex = "#DA043A",
                ControlMode = "Volume"
            };
            AnimateAddPairCard(_cycleSlotsHost, _draftSettings.CrossfadeCycleList, newPair, isCarouselList: true);
        };
        _cycleListCard.Controls.Add(_btnAddCycleEntry);

        _cycleSlotsHost = new BufferedPanel
        {
            Left = 12,
            Top = 42,
            Width = 444,
            Height = 0,
            BackColor = Color.FromArgb(39, 39, 39)
        };
        _cycleListCard.Controls.Add(_cycleSlotsHost);

        _globalKeysHost = new BufferedPanel
        {
            Left = 12,
            Top = 42,
            Width = 444,
            Height = GlobalKeysHeaderHeight,
            BackColor = Color.FromArgb(39, 39, 39)
        };

        _globalKeysHeaderBar = new BufferedPanel
        {
            Left = 0,
            Top = 0,
            Width = 444,
            Height = GlobalKeysHeaderHeight,
            BackColor = Color.FromArgb(39, 39, 39),
            Cursor = Cursors.Hand
        };

        _globalKeysHeaderBar.MouseEnter += (s, e) => { _isGlobalKeysHovered = true; _globalKeysHeaderBar.Invalidate(); };
        _globalKeysHeaderBar.MouseLeave += (s, e) => { _isGlobalKeysHovered = false; _globalKeysHeaderBar.Invalidate(); };
        _globalKeysHeaderBar.Click += (s, e) => ToggleGlobalHotkeysShutter();

        _globalKeysHeaderBar.Paint += (s, e) =>
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(39, 39, 39));

            using Pen sepPen = new Pen(Color.FromArgb(62, 62, 62), 1);
            g.DrawLine(sepPen, 2, 2, _globalKeysHeaderBar.Width - 4, 2);

            if (_isGlobalKeysHovered)
            {
                using var hoverPath = Win11CardPanel.CreateRoundRect(new Rectangle(0, 5, _globalKeysHeaderBar.Width - 1, _globalKeysHeaderBar.Height - 6), 5);
                using var hoverBrush = new SolidBrush(Color.FromArgb(46, 46, 46));
                g.FillPath(hoverBrush, hoverPath);
            }

            Color titleColor = _isGlobalKeysHovered
                ? Color.FromArgb(130, 220, 255)
                : Color.FromArgb(96, 205, 255);

            string titleText = _isGlobalKeysExpanded
                ? "Global Hotkeys (Click to bind, Esc to clear)"
                : "Global Hotkeys";

            using Font titleFont = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold);
            TextRenderer.DrawText(
                g,
                titleText,
                titleFont,
                new Rectangle(2, 6, _globalKeysHeaderBar.Width - 36, 22),
                titleColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            float cx = _globalKeysHeaderBar.Width - 14f;
            float cy = 17f;
            using SolidBrush arrowBrush = new SolidBrush(titleColor);
            PointF[] triangle = _isGlobalKeysExpanded
                ? new PointF[] { new PointF(cx - 5f, cy + 2.5f), new PointF(cx + 5f, cy + 2.5f), new PointF(cx, cy - 3.5f) }
                : new PointF[] { new PointF(cx - 5f, cy - 2.5f), new PointF(cx + 5f, cy - 2.5f), new PointF(cx, cy + 3.5f) };
            g.FillPolygon(arrowBrush, triangle);
        };

        _globalKeysShutterPanel = new BufferedPanel
        {
            Left = 0,
            Top = GlobalKeysHeaderHeight,
            Width = 444,
            BackColor = Color.FromArgb(39, 39, 39)
        };

        int ky = 4;
        AddAlignedLabel(_globalKeysShutterPanel, "Fade Left / Down:", 2, ky);
        _kbKey2 = AddKeybindBoxControl(_globalKeysShutterPanel, 136, 304, ref ky, val => _draftSettings.CrossfadeKeyDown = val);

        AddAlignedLabel(_globalKeysShutterPanel, "Fade Right / Up:", 2, ky);
        _kbKey1 = AddKeybindBoxControl(_globalKeysShutterPanel, 136, 304, ref ky, val => _draftSettings.CrossfadeKeyUp = val);

        AddAlignedLabel(_globalKeysShutterPanel, "Mute Toggle:", 2, ky);
        _kbKey3 = AddKeybindBoxControl(_globalKeysShutterPanel, 136, 304, ref ky, val => _draftSettings.CrossfadeKeyMute = val);

        AddAlignedLabel(_globalKeysShutterPanel, "Mute Behavior:", 2, ky);
        _cmbGlobalMuteMode = new ModernComboBox
        {
            Left = 136,
            Top = ky,
            Width = 304,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbGlobalMuteMode.Items.AddRange(GlobalMuteModeOptions);
        _cmbGlobalMuteMode.SelectedItem = _draftSettings.GlobalMuteMode;
        if (_cmbGlobalMuteMode.SelectedIndex == -1) _cmbGlobalMuteMode.SelectedIndex = 0;
        _cmbGlobalMuteMode.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            _draftSettings.GlobalMuteMode = _cmbGlobalMuteMode.SelectedItem?.ToString() ?? "Cycle R → L";
            MarkDirty();
        };
        _globalKeysShutterPanel.Controls.Add(_cmbGlobalMuteMode);
        ky += 31;

        AddAlignedLabel(_globalKeysShutterPanel, "Next Pair:", 2, ky);
        _kbKeyCycleNext = AddKeybindBoxControl(_globalKeysShutterPanel, 136, 304, ref ky, val =>
        {
            _draftSettings.CycleNextKey = val;
            _draftSettings.QuickSwapKey = val;
        });

        AddAlignedLabel(_globalKeysShutterPanel, "Previous Pair:", 2, ky);
        _kbKeyCyclePrev = AddKeybindBoxControl(_globalKeysShutterPanel, 136, 304, ref ky, val => _draftSettings.CyclePrevKey = val);

        _globalKeysExpandedContentHeight = ky + 2;
        _globalKeysShutterPanel.Height = _globalKeysExpandedContentHeight;
        _globalKeysShutterPanel.Top = GlobalKeysHeaderHeight - _globalKeysExpandedContentHeight;
        _globalKeysShutterPanel.Visible = false;

        _globalKeysHost.Controls.Add(_globalKeysShutterPanel);
        _globalKeysHost.Controls.Add(_globalKeysHeaderBar);
        _globalKeysHeaderBar.BringToFront();

        _cycleListCard.Controls.Add(_globalKeysHost);
        page.InnerControls.Add(_cycleListCard);
        y = _cycleListCard.Bottom + 10;

        _standaloneListCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468,
            Height = 44
        };

        _lblStandaloneCardTitle = new Label
        {
            Text = "Standalone Pairs (Dedicated Keybinds)",
            Left = 14,
            Top = 12,
            Width = 300,
            Height = 20,
            Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 205, 255)
        };
        _standaloneListCard.Controls.Add(_lblStandaloneCardTitle);

        _btnAddStandaloneEntry = CreateModernButton("+ Add Pair", 334, 9, 120, 26, true);
        _btnAddStandaloneEntry.Click += (s, e) =>
        {
            if (_isPairListAnimating) return;
            var newPair = new CrossfadeCyclePair
            {
                Source1 = "",
                Color1Hex = "#1B3B5A",
                Source2 = "",
                Color2Hex = "#4A2040",
                UseCustomKeybinds = true,
                ControlMode = "Volume"
            };
            AnimateAddPairCard(_standaloneSlotsHost, _draftSettings.StandalonePairs, newPair, isCarouselList: false);
        };
        _standaloneListCard.Controls.Add(_btnAddStandaloneEntry);

        _standaloneSlotsHost = new BufferedPanel
        {
            Left = 12,
            Top = 42,
            Width = 444,
            Height = 0,
            BackColor = Color.FromArgb(39, 39, 39)
        };
        _standaloneListCard.Controls.Add(_standaloneSlotsHost);
        page.InnerControls.Add(_standaloneListCard);

        return page;
    }

    private static string ReadComboText(ModernComboBox cmb)
    {
        return (cmb.SelectedItem?.ToString() ?? cmb.Text ?? "").Trim();
    }

    private void PopulateCycleListUI()
    {
        PopulatePairListHost(_cycleSlotsHost, _draftSettings.CrossfadeCycleList, _draftSettings.StandalonePairs, isCarouselList: true);
        PopulatePairListHost(_standaloneSlotsHost, _draftSettings.StandalonePairs, _draftSettings.CrossfadeCycleList, isCarouselList: false);
    }

    private void ResizeExistingPairCards(Panel? host)
    {
        if (host == null) return;

        host.SuspendLayout();

        try
        {
            int hostW = Math.Max(442, host.Width);
            int comboW = Math.Max(140, hostW - 234);
            int btnActionLeft = 56 + comboW + 6;
            int btnArrowLeft = btnActionLeft + 94 + 4;
            int btnDelLeft = btnArrowLeft + 30 + 4;

            int invertW = 62;
            int modeComboW = Math.Max(95, comboW - invertW - 6);

            int rowLeft = 6;
            int rowRight = btnDelLeft + 32;
            int totalRowW = rowRight - rowLeft;
            int colGap = 8;
            int colW = (totalRowW - (colGap * 2)) / 3;

            foreach (Control ctrl in host.Controls)
            {
                if (ctrl is not Win11CardPanel pairCard)
                    continue;

                pairCard.SuspendLayout();

                try
                {
                    pairCard.Width = hostW;

                    foreach (Control subPanel in pairCard.Controls)
                    {
                        if (subPanel is not Panel p)
                            continue;

                        p.SuspendLayout();

                        try
                        {
                            p.Width = hostW - 4;

                            foreach (Control child in p.Controls)
                            {
                                if (child.Tag is not string tag)
                                    continue;

                                switch (tag)
                                {
                                    case "Source1Combo":
                                    case "Source2Combo":
                                    case "PairMuteCombo":
                                        child.Width = comboW;
                                        break;

                                    case "ModeCombo":
                                        child.Width = modeComboW;
                                        break;

                                    case "InvertChk":
                                        child.Left = 56 + modeComboW + 6;
                                        break;

                                    case "MoveListBtn":
                                    case "Browse1Btn":
                                    case "Browse2Btn":
                                        child.Left = btnActionLeft;
                                        break;

                                    case "UpBtn":
                                    case "DownBtn":
                                        child.Left = btnArrowLeft;
                                        break;

                                    case "DeleteBtn":
                                        child.Left = btnDelLeft;
                                        break;

                                    case "KeyCol_0":
                                        child.Left = rowLeft;
                                        child.Width = colW;
                                        break;

                                    case "KeyCol_1":
                                        child.Left = rowLeft + colW + colGap;
                                        child.Width = colW;
                                        break;

                                    case "KeyCol_2":
                                        child.Left = rowLeft + (colW + colGap) * 2;
                                        child.Width =
                                            totalRowW - (colW * 2) - (colGap * 2);
                                        break;
                                }
                            }
                        }
                        finally
                        {
                            p.ResumeLayout(false);
                        }
                    }
                }
                finally
                {
                    pairCard.ResumeLayout(false);
                }
            }
        }
        finally
        {
            host.ResumeLayout(true);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        BeginInvoke(new Action(() =>
        {
            HandleWindowResize();
        }));
    }

    private void AnimatePairCardModeChange(
        Win11CardPanel pairCard,
        Panel topBlockPanel,
        Panel row2Panel,
        Control? bottomSection,
        bool toSingleMode,
        bool isCarouselList)
    {
        topBlockPanel.BringToFront();

        int startCardH = pairCard.Height;
        int targetCardH = PairCardLayout.GetCardHeight(isCarouselList, toSingleMode);

        int startRow2Top = row2Panel.Top;
        int targetRow2Top = PairCardLayout.GetRow2PanelTop(toSingleMode);

        int startBottomTop = bottomSection?.Top ?? 0;
        int targetBottomTop = PairCardLayout.GetBottomPanelTop(toSingleMode);

        row2Panel.Visible = true;

        const double durationMs = 120.0;
        Stopwatch sw = Stopwatch.StartNew();
        var animTimer = new System.Windows.Forms.Timer { Interval = 15 };
        animTimer.Tick += (s, e) =>
        {
            if (pairCard.IsDisposed)
            {
                animTimer.Stop();
                animTimer.Dispose();
                return;
            }

            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

            if (t >= 1.0)
            {
                pairCard.Height = targetCardH;
                row2Panel.Top = targetRow2Top;
                if (bottomSection != null) bottomSection.Top = targetBottomTop;
                row2Panel.Visible = !toSingleMode;
                animTimer.Stop();
                animTimer.Dispose();
            }
            else
            {
                pairCard.Height = startCardH + (int)Math.Round((targetCardH - startCardH) * ease);
                row2Panel.Top = startRow2Top + (int)Math.Round((targetRow2Top - startRow2Top) * ease);
                if (bottomSection != null)
                    bottomSection.Top = startBottomTop + (int)Math.Round((targetBottomTop - startBottomTop) * ease);
            }

            int curY = 0;
            if (pairCard.Parent != null)
            {
                foreach (Control sibling in pairCard.Parent.Controls.Cast<Control>().OrderBy(c => c.Top))
                {
                    sibling.Top = curY;
                    curY += sibling.Height + PairCardLayout.CardListGap;
                }
                pairCard.Parent.Height = curY;
            }
            ReflowGeneralPage();
        };
        animTimer.Start();
    }

    private void PopulatePairListHost(
        Panel? host,
        List<CrossfadeCyclePair> targetList,
        List<CrossfadeCyclePair> oppositeList,
        bool isCarouselList)
    {
        if (host == null) return;
        host.SuspendLayout();
        host.Controls.Clear();

        List<string> options = GetAvailableSourceOptions();
        int y = 0;
        int hostW = Math.Max(442, host.Width);

        for (int i = 0; i < targetList.Count; i++)
        {
            var pair = targetList[i];
            Win11CardPanel pairCard = BuildSinglePairCard(host, pair, targetList, oppositeList, isCarouselList, options, hostW);
            pairCard.Top = y;
            host.Controls.Add(pairCard);
            y += pairCard.Height + PairCardLayout.CardListGap;
        }

        host.Height = y;
        UpdateHostPairButtonsState(host, targetList);
        host.ResumeLayout();
    }

    private Win11CardPanel BuildSinglePairCard(
        Panel host,
        CrossfadeCyclePair pair,
        List<CrossfadeCyclePair> targetList,
        List<CrossfadeCyclePair> oppositeList,
        bool isCarouselList,
        List<string> options,
        int hostW)
    {
        bool isSingle = pair.IsSingleMode;

        int comboW = Math.Max(140, hostW - 234);
        int btnActionLeft = 56 + comboW + 6;
        int btnArrowLeft = btnActionLeft + 94 + 4;
        int btnDelLeft = btnArrowLeft + 30 + 4;

        int cardH = PairCardLayout.GetCardHeight(isCarouselList, isSingle);

        Win11CardPanel pairCard = new Win11CardPanel
        {
            Left = 0,
            Top = 0,
            Width = hostW,
            Height = cardH,
            FillColor = Color.FromArgb(48, 48, 48),
            BorderColor = Color.FromArgb(66, 66, 66),
            CornerRadius = 6,
            Padding = new Padding(0),
            Tag = pair
        };

        BufferedPanel topBlockPanel = new BufferedPanel
        {
            Left = 2,
            Top = PairCardLayout.CardPaddingTop,
            Width = hostW - 4,
            Height = PairCardLayout.TopBlockHeight,
            BackColor = Color.FromArgb(48, 48, 48),
            Tag = "TopBlockPanel"
        };

        int r1Y = PairCardLayout.ModeRowY;

        Label lblMode = new Label
        {
            Text = "Mode:",
            Left = 6,
            Top = r1Y + PairCardLayout.LabelOffsetY,
            Width = 46,
            Height = 18,
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(190, 190, 190)
        };
        topBlockPanel.Controls.Add(lblMode);

        int invertW = 62;
        int modeComboW = Math.Max(95, comboW - invertW - 6);

        ModernComboBox cmbMode = new ModernComboBox
        {
            Left = 56,
            Top = r1Y + PairCardLayout.ComboOffsetY,
            Width = modeComboW,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Tag = "ModeCombo"
        };
        cmbMode.Items.AddRange(new object[] { "Volume", "Balance", "Crossfade" });

        if (pair.IsSingleMode)
            cmbMode.SelectedItem = "Volume";
        else if (pair.IsCrossfadeMode)
            cmbMode.SelectedItem = "Crossfade";
        else
            cmbMode.SelectedItem = "Balance";

        topBlockPanel.Controls.Add(cmbMode);

        CheckBox chkInvert = new CheckBox
        {
            Text = "Invert",
            Checked = pair.InvertDirection,
            Left = 56 + modeComboW + 6,
            Top = r1Y + PairCardLayout.CheckBoxOffsetY,
            Width = invertW,
            Height = 22,
            Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
            ForeColor = Color.FromArgb(215, 215, 215),
            Tag = "InvertChk"
        };
        _toolTip.SetToolTip(chkInvert, "Invert Volume Up / Down direction for this pair");
        chkInvert.CheckedChanged += (s, e) =>
        {
            if (_isInitializing) return;
            pair.InvertDirection = chkInvert.Checked;
            MarkDirty();
        };
        topBlockPanel.Controls.Add(chkInvert);

        ModernButton btnMoveList = CreateModernButton(
            isCarouselList ? "To Standalone" : "To Carousel",
            btnActionLeft,
            r1Y + PairCardLayout.ButtonOffsetY,
            94,
            25,
            false);
        btnMoveList.Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);
        btnMoveList.Tag = "MoveListBtn";
        btnMoveList.Click += (s, e) =>
        {
            if (_isPairListAnimating) return;
            int index = targetList.IndexOf(pair);
            if (index < 0) return;
            AnimateRemovePairCard(host, targetList, index, onComplete: () =>
            {
                pair.UseCustomKeybinds = isCarouselList;
                Panel? destHost = isCarouselList ? _standaloneSlotsHost : _cycleSlotsHost;
                AnimateAddPairCard(destHost, oppositeList, pair, isCarouselList: !isCarouselList);
            });
        };
        topBlockPanel.Controls.Add(btnMoveList);

        ModernButton btnUp = CreateModernButton("▲", btnArrowLeft, r1Y + PairCardLayout.ButtonOffsetY, 30, 25, false);
        btnUp.Tag = "UpBtn";
        btnUp.Click += (s, e) =>
        {
            int curIdx = targetList.IndexOf(pair);
            if (_isPairListAnimating || curIdx <= 0) return;
            AnimateSwapPairCards(host, targetList, curIdx, curIdx - 1);
        };
        topBlockPanel.Controls.Add(btnUp);

        int r2Y = PairCardLayout.Source1RowY;

        Label lblS1 = new Label
        {
            Text = isSingle ? "S:" : "L:",
            Left = 6,
            Top = r2Y + PairCardLayout.LabelOffsetY,
            Width = 18,
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(190, 190, 190)
        };
        topBlockPanel.Controls.Add(lblS1);

        RoundedColorSwatch swatch1 = CreateCompactColorSwatch(
            26,
            r2Y + PairCardLayout.SwatchOffsetY,
            24,
            24,
            pair.Color1Hex,
            hex => pair.Color1Hex = hex,
            pair);
        topBlockPanel.Controls.Add(swatch1);

        ModernComboBox cmb1 = new ModernComboBox
        {
            Left = 56,
            Top = r2Y + PairCardLayout.ComboOffsetY,
            Width = comboW,
            DropDownStyle = ComboBoxStyle.DropDown,
            Tag = "Source1Combo",
            BadgeResolver = ResolveDropdownBadge,
            IconResolver = ResolveDropdownIcon
        };
        cmb1.Items.AddRange(options.Cast<object>().ToArray());
        cmb1.SelectedItem = pair.Source1;
        if (cmb1.SelectedIndex == -1) cmb1.Text = pair.Source1;
        cmb1.DropDown += (s, e) =>
        {
            _detectedAudio = AudioProcessDetector.Scan();
            PopulateSingleComboOptions(cmb1, includeGroups: true);
        };
        cmb1.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            pair.Source1 = ReadComboText(cmb1);
            MarkDirty();
            PushLivePreviewIfActive();
        };
        cmb1.TextChanged += (s, e) =>
        {
            if (_isInitializing) return;
            pair.Source1 = ReadComboText(cmb1);
            MarkDirty();
        };
        topBlockPanel.Controls.Add(cmb1);

        ModernButton btnBrowse1 = CreateModernButton("Browse", btnActionLeft, r2Y + PairCardLayout.ButtonOffsetY, 94, 25, false);
        btnBrowse1.Tag = "Browse1Btn";
        btnBrowse1.Click += (s, e) =>
        {
            BrowseForExecutable(cmb1);
            pair.Source1 = ReadComboText(cmb1);
            PushLivePreviewIfActive();
        };
        topBlockPanel.Controls.Add(btnBrowse1);

        ModernButton btnDown = CreateModernButton("▼", btnArrowLeft, r2Y + PairCardLayout.ButtonOffsetY, 30, 25, false);
        btnDown.Tag = "DownBtn";
        btnDown.Click += (s, e) =>
        {
            int curIdx = targetList.IndexOf(pair);
            if (_isPairListAnimating || curIdx < 0 || curIdx >= targetList.Count - 1) return;
            AnimateSwapPairCards(host, targetList, curIdx, curIdx + 1);
        };
        topBlockPanel.Controls.Add(btnDown);

        ModernButton btnDel = CreateModernButton(
            "✕",
            btnDelLeft,
            r1Y + PairCardLayout.ButtonOffsetY,
            32,
            PairCardLayout.DeleteButtonHeight,
            false,
            isDanger: true);
        btnDel.Tag = "DeleteBtn";
        btnDel.Click += (s, e) =>
        {
            int curIdx = targetList.IndexOf(pair);
            if (_isPairListAnimating || curIdx < 0) return;
            if (_draftSettings.CrossfadeCycleList.Count + _draftSettings.StandalonePairs.Count <= 1)
            {
                MessageBox.Show("You must keep at least one source pair.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            AnimateRemovePairCard(host, targetList, curIdx);
        };
        topBlockPanel.Controls.Add(btnDel);

        BufferedPanel row2Panel = new BufferedPanel
        {
            Left = 2,
            Top = PairCardLayout.GetRow2PanelTop(isSingle),
            Width = hostW - 4,
            Height = PairCardLayout.RowHeight + 2,
            Visible = !isSingle,
            BackColor = Color.FromArgb(48, 48, 48),
            Tag = "Row2Panel"
        };

        Label lblS2 = new Label
        {
            Text = "R:",
            Left = 6,
            Top = PairCardLayout.LabelOffsetY,
            Width = 18,
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(190, 190, 190)
        };
        row2Panel.Controls.Add(lblS2);

        RoundedColorSwatch swatch2 = CreateCompactColorSwatch(
            26,
            PairCardLayout.SwatchOffsetY,
            24,
            24,
            pair.Color2Hex,
            hex => pair.Color2Hex = hex,
            pair);
        row2Panel.Controls.Add(swatch2);

        ModernComboBox cmb2 = new ModernComboBox
        {
            Left = 56,
            Top = PairCardLayout.ComboOffsetY,
            Width = comboW,
            DropDownStyle = ComboBoxStyle.DropDown,
            Tag = "Source2Combo",
            BadgeResolver = ResolveDropdownBadge,
            IconResolver = ResolveDropdownIcon
        };
        cmb2.Items.AddRange(options.Cast<object>().ToArray());
        cmb2.SelectedItem = pair.Source2;
        if (cmb2.SelectedIndex == -1) cmb2.Text = pair.Source2;
        cmb2.DropDown += (s, e) =>
        {
            _detectedAudio = AudioProcessDetector.Scan();
            PopulateSingleComboOptions(cmb2, includeGroups: true);
        };
        cmb2.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            pair.Source2 = ReadComboText(cmb2);
            MarkDirty();
            PushLivePreviewIfActive();
        };
        cmb2.TextChanged += (s, e) =>
        {
            if (_isInitializing) return;
            pair.Source2 = ReadComboText(cmb2);
            MarkDirty();
        };
        row2Panel.Controls.Add(cmb2);

        ModernButton btnBrowse2 = CreateModernButton("Browse", btnActionLeft, PairCardLayout.ButtonOffsetY, 94, 25, false);
        btnBrowse2.Tag = "Browse2Btn";
        btnBrowse2.Click += (s, e) =>
        {
            BrowseForExecutable(cmb2);
            pair.Source2 = ReadComboText(cmb2);
            PushLivePreviewIfActive();
        };
        row2Panel.Controls.Add(btnBrowse2);

        BufferedPanel? bottomPanel = null;
        if (!isCarouselList)
        {
            pair.UseCustomKeybinds = true;

            bottomPanel = new BufferedPanel
            {
                Left = 2,
                Top = PairCardLayout.GetBottomPanelTop(isSingle),
                Width = hostW - 4,
                Height = PairCardLayout.BottomPanelHeight,
                BackColor = Color.FromArgb(48, 48, 48),
                Tag = "BottomPanel"
            };

            Label lblMuteMode = new Label
            {
                Text = "Mute:",
                Left = 6,
                Top = PairCardLayout.LabelOffsetY,
                Width = 46,
                Height = 18,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(190, 190, 190)
            };
            bottomPanel.Controls.Add(lblMuteMode);

            ModernComboBox cmbPairMute = new ModernComboBox
            {
                Left = 56,
                Top = PairCardLayout.ComboOffsetY,
                Width = comboW,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Tag = "PairMuteCombo"
            };
            cmbPairMute.Items.AddRange(PairMuteModeOptions);
            cmbPairMute.SelectedItem = string.IsNullOrWhiteSpace(pair.MuteMode) ? "Use Global" : pair.MuteMode;
            if (cmbPairMute.SelectedIndex == -1) cmbPairMute.SelectedIndex = 0;
            cmbPairMute.SelectedIndexChanged += (s, e) =>
            {
                if (_isInitializing) return;
                pair.MuteMode = cmbPairMute.SelectedItem?.ToString() ?? "Use Global";
                MarkDirty();
            };
            bottomPanel.Controls.Add(cmbPairMute);

            int rowLeft = 6;
            int rowRight = btnDelLeft + 32;
            int totalRowW = rowRight - rowLeft;
            int colGap = 8;
            int colW = (totalRowW - (colGap * 2)) / 3;
            int kbTop = PairCardLayout.KeybindColumnsTop;

            AddSideBySideKeybindColumn(bottomPanel, "Fade Left / Down", rowLeft, colW, kbTop, pair.KeyDown, val => pair.KeyDown = val, 0);
            AddSideBySideKeybindColumn(bottomPanel, "Fade Right / Up", rowLeft + colW + colGap, colW, kbTop, pair.KeyUp, val => pair.KeyUp = val, 1);
            AddSideBySideKeybindColumn(bottomPanel, "Mute Toggle", rowLeft + (colW + colGap) * 2, totalRowW - (colW * 2) - (colGap * 2), kbTop, pair.KeyMute, val => pair.KeyMute = val, 2);

            pairCard.Controls.Add(bottomPanel);
        }
        else
        {
            pair.UseCustomKeybinds = false;
        }

        var modeFsm = new StateMachine<ControlModeState>(pair.ModeState);
        modeFsm.ConfigureState(ControlModeState.Volume, onEnter: () =>
        {
            pair.ModeState = ControlModeState.Volume;
            lblS1.Text = "S:";
            AnimatePairCardModeChange(pairCard, topBlockPanel, row2Panel, bottomPanel, true, isCarouselList);
        });
        modeFsm.ConfigureState(ControlModeState.Balance, onEnter: () =>
        {
            pair.ModeState = ControlModeState.Balance;
            lblS1.Text = "L:";
            AnimatePairCardModeChange(pairCard, topBlockPanel, row2Panel, bottomPanel, false, isCarouselList);
        });
        modeFsm.ConfigureState(ControlModeState.Crossfade, onEnter: () =>
        {
            pair.ModeState = ControlModeState.Crossfade;
            lblS1.Text = "L:";
            AnimatePairCardModeChange(pairCard, topBlockPanel, row2Panel, bottomPanel, false, isCarouselList);
        });

        cmbMode.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            ControlModeState nextMode = ControlModeStateMachine.ParseState(cmbMode.SelectedItem?.ToString());
            if (modeFsm.TransitionTo(nextMode))
            {
                MarkDirty();
                PushLivePreviewIfActive();
            }
        };

        pairCard.Controls.Add(row2Panel);
        pairCard.Controls.Add(topBlockPanel);
        topBlockPanel.BringToFront();

        return pairCard;
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
                    else if (t == "DeleteBtn") btn.Enabled = (totalPairsCount > 1);
                }
            }
        }
    }

    private void RefreshAllPairButtonsState()
    {
        UpdateHostPairButtonsState(_cycleSlotsHost, _draftSettings.CrossfadeCycleList);
        UpdateHostPairButtonsState(_standaloneSlotsHost, _draftSettings.StandalonePairs);
    }

    private void AnimateAddPairCard(
        Panel? host,
        List<CrossfadeCyclePair> targetList,
        CrossfadeCyclePair newPair,
        bool isCarouselList)
    {
        if (host == null) return;

        List<CrossfadeCyclePair> oppositeList = isCarouselList ? _draftSettings.StandalonePairs : _draftSettings.CrossfadeCycleList;
        targetList.Add(newPair);

        int hostW = Math.Max(442, host.Width);
        List<string> options = GetAvailableSourceOptions();

        Control? prevCard = null;
        int finalTop = 0;
        foreach (Control c in host.Controls)
        {
            if (prevCard == null || c.Top >= prevCard.Top)
            {
                prevCard = c;
            }
            if (c.Bottom + 6 > finalTop)
            {
                finalTop = c.Bottom + 6;
            }
        }

        Win11CardPanel newCard = BuildSinglePairCard(host, newPair, targetList, oppositeList, isCarouselList, options, hostW);
        int finalCardH = newCard.Height;
        int startTop = prevCard != null ? prevCard.Top : -finalCardH;
        int startHostH = Math.Max(0, finalTop);
        int finalHostH = finalTop + finalCardH + 6;

        host.SuspendLayout();
        newCard.Top = startTop;
        host.Controls.Add(newCard);
        if (prevCard != null)
        {
            prevCard.BringToFront();
        }
        host.Height = startHostH;
        RefreshAllPairButtonsState();
        host.ResumeLayout();

        MarkDirty();
        PushLivePreviewIfActive();
        ReflowGeneralPage();

        _isPairListAnimating = true;
        const double durationMs = 240.0;
        Stopwatch sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };

        timer.Tick += (s, e) =>
        {
            if (newCard.IsDisposed || host.IsDisposed)
            {
                _isPairListAnimating = false;
                timer.Stop();
                timer.Dispose();
                return;
            }

            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

            if (t >= 1.0)
            {
                newCard.Top = finalTop;
                host.Height = finalHostH;
                _isPairListAnimating = false;
                timer.Stop();
                timer.Dispose();
            }
            else
            {
                newCard.Top = startTop + (int)Math.Round((finalTop - startTop) * ease);
                host.Height = startHostH + (int)Math.Round((finalHostH - startHostH) * ease);
            }

            ReflowGeneralPage();
        };
        timer.Start();
    }

    private void AnimateRemovePairCard(
        Panel? host,
        List<CrossfadeCyclePair> targetList,
        int removeIndex,
        Action? onComplete = null)
    {
        if (host == null || removeIndex < 0 || removeIndex >= targetList.Count)
            return;

        CrossfadeCyclePair pairToRemove = targetList[removeIndex];
        CrossfadeCyclePair? pairAbove = removeIndex > 0 ? targetList[removeIndex - 1] : null;

        Control? removingCard = host.Controls.Cast<Control>().FirstOrDefault(c => ReferenceEquals(c.Tag, pairToRemove));
        if (removingCard == null) return;

        Control? cardAbove = pairAbove != null
            ? host.Controls.Cast<Control>().FirstOrDefault(c => ReferenceEquals(c.Tag, pairAbove))
            : null;

        if (cardAbove != null)
        {
            cardAbove.BringToFront();
        }
        else
        {
            removingCard.SendToBack();
        }

        int shiftDelta = removingCard.Height + 6;
        int startRemoveTop = removingCard.Top;
        int targetRemoveTop = cardAbove != null ? cardAbove.Top : -removingCard.Height;

        List<(Control ctrl, int startY, int targetY)> trailingCards = new();
        for (int i = removeIndex + 1; i < targetList.Count; i++)
        {
            var trailingPair = targetList[i];
            Control? c = host.Controls.Cast<Control>().FirstOrDefault(ctrl => ReferenceEquals(ctrl.Tag, trailingPair));
            if (c != null)
            {
                trailingCards.Add((c, c.Top, c.Top - shiftDelta));
            }
        }

        int startHostH = host.Height;
        int targetHostH = Math.Max(0, startHostH - shiftDelta);

        _isPairListAnimating = true;
        const double durationMs = 230.0;
        Stopwatch sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };

        timer.Tick += (s, e) =>
        {
            if (host.IsDisposed)
            {
                _isPairListAnimating = false;
                timer.Stop();
                timer.Dispose();
                return;
            }

            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

            if (t >= 1.0)
            {
                timer.Stop();
                timer.Dispose();

                host.SuspendLayout();
                targetList.RemoveAt(removeIndex);
                host.Controls.Remove(removingCard);
                removingCard.Dispose();

                foreach (var item in trailingCards)
                {
                    item.ctrl.Top = item.targetY;
                }
                host.Height = targetHostH;
                RefreshAllPairButtonsState();
                host.ResumeLayout();

                _isPairListAnimating = false;
                ReflowGeneralPage();
                MarkDirty();
                PushLivePreviewIfActive();
                onComplete?.Invoke();
                return;
            }

            removingCard.Top = startRemoveTop + (int)Math.Round((targetRemoveTop - startRemoveTop) * ease);
            foreach (var item in trailingCards)
            {
                item.ctrl.Top = item.startY + (int)Math.Round((item.targetY - item.startY) * ease);
            }
            host.Height = startHostH + (int)Math.Round((targetHostH - startHostH) * ease);
            ReflowGeneralPage();
        };
        timer.Start();
    }

    private void AnimateSwapPairCards(
        Panel? host,
        List<CrossfadeCyclePair> targetList,
        int indexA,
        int indexB)
    {
        if (host == null || indexA < 0 || indexB < 0 ||
            indexA >= targetList.Count || indexB >= targetList.Count ||
            indexA == indexB)
        {
            return;
        }

        int upperIdx = Math.Min(indexA, indexB);
        int lowerIdx = Math.Max(indexA, indexB);

        CrossfadeCyclePair upperPair = targetList[upperIdx];
        CrossfadeCyclePair lowerPair = targetList[lowerIdx];
        CrossfadeCyclePair initiatedPair = targetList[indexA];

        Control? upperCard = host.Controls.Cast<Control>().FirstOrDefault(c => ReferenceEquals(c.Tag, upperPair));
        Control? lowerCard = host.Controls.Cast<Control>().FirstOrDefault(c => ReferenceEquals(c.Tag, lowerPair));
        Control? initiatedCard = host.Controls.Cast<Control>().FirstOrDefault(c => ReferenceEquals(c.Tag, initiatedPair));

        if (upperCard == null || lowerCard == null || initiatedCard == null)
            return;

        initiatedCard.BringToFront();

        int upperStartTop = upperCard.Top;
        int lowerStartTop = lowerCard.Top;

        int upperTargetTop = upperStartTop + lowerCard.Height + 6;
        int lowerTargetTop = upperStartTop;

        _isPairListAnimating = true;
        const double durationMs = 240.0;
        Stopwatch sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };

        timer.Tick += (s, e) =>
        {
            if (host.IsDisposed || upperCard.IsDisposed || lowerCard.IsDisposed)
            {
                _isPairListAnimating = false;
                timer.Stop();
                timer.Dispose();
                return;
            }

            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

            if (t >= 1.0)
            {
                upperCard.Top = upperTargetTop;
                lowerCard.Top = lowerTargetTop;
                timer.Stop();
                timer.Dispose();

                (targetList[indexA], targetList[indexB]) = (targetList[indexB], targetList[indexA]);
                RefreshAllPairButtonsState();
                _isPairListAnimating = false;
                MarkDirty();
                PushLivePreviewIfActive();
                return;
            }

            upperCard.Top = upperStartTop + (int)Math.Round((upperTargetTop - upperStartTop) * ease);
            lowerCard.Top = lowerStartTop + (int)Math.Round((lowerTargetTop - lowerStartTop) * ease);
            host.Invalidate();
        };
        timer.Start();
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

    private void ToggleGlobalHotkeysShutter()
    {
        CancelRecording();
        _isGlobalKeysExpanded = !_isGlobalKeysExpanded;
        _globalKeysHeaderBar.Invalidate();

        float startProgress = _globalKeysAnimProgress;
        float targetProgress = _isGlobalKeysExpanded ? 1f : 0f;

        _globalKeysShutterPanel.Visible = true;
        _globalKeysHeaderBar.BringToFront();

        _globalKeysAnimTimer?.Stop();
        _globalKeysAnimTimer?.Dispose();

        const double durationMs = 230.0;
        Stopwatch sw = Stopwatch.StartNew();
        _globalKeysAnimTimer = new System.Windows.Forms.Timer { Interval = 15 };
        _globalKeysAnimTimer.Tick += (s, e) =>
        {
            if (_globalKeysHost.IsDisposed)
            {
                _globalKeysAnimTimer.Stop();
                _globalKeysAnimTimer.Dispose();
                return;
            }

            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

            if (t >= 1.0)
            {
                _globalKeysAnimProgress = targetProgress;
                _globalKeysShutterPanel.Top = GlobalKeysHeaderHeight - (int)Math.Round(_globalKeysExpandedContentHeight * (1f - targetProgress));
                _globalKeysHost.Height = GlobalKeysHeaderHeight + (int)Math.Round(_globalKeysExpandedContentHeight * targetProgress);
                _globalKeysShutterPanel.Visible = _isGlobalKeysExpanded;
                _globalKeysAnimTimer.Stop();
                _globalKeysAnimTimer.Dispose();
            }
            else
            {
                _globalKeysAnimProgress = startProgress + (targetProgress - startProgress) * ease;
                _globalKeysShutterPanel.Top = GlobalKeysHeaderHeight - (int)Math.Round(_globalKeysExpandedContentHeight * (1f - _globalKeysAnimProgress));
                _globalKeysHost.Height = GlobalKeysHeaderHeight + (int)Math.Round(_globalKeysExpandedContentHeight * _globalKeysAnimProgress);
            }

            ReflowGeneralPage();
        };
        _globalKeysAnimTimer.Start();
    }

    private void ReflowGeneralPage()
    {
        if (_cycleListCard == null || _standaloneListCard == null || _globalKeysHost == null)
            return;

        int cycleCount = _draftSettings.CrossfadeCycleList.Count;
        _cycleSlotsHost.Visible = (cycleCount > 0);
        _cycleListCard.Top = 4;

        int keysTop = (cycleCount == 0) ? 42 : (44 + _cycleSlotsHost.Height);
        _globalKeysHost.Top = keysTop;

        int cycleContentBottom = cycleCount == 0 ? 42 : _cycleSlotsHost.Bottom;
        int keysContentBottom = _globalKeysHost.Bottom;
        _cycleListCard.Height = Math.Max(cycleContentBottom, keysContentBottom) + 10;

        int standaloneCount = _draftSettings.StandalonePairs.Count;
        _standaloneSlotsHost.Visible = (standaloneCount > 0);
        _standaloneListCard.Top = _cycleListCard.Bottom + 10;
        _standaloneListCard.Height = (standaloneCount == 0) ? 44 : (46 + _standaloneSlotsHost.Height);

        _pageGeneral?.RecalculateScroll();
    }

    private void ReflowPersonalizationPage()
    {
        if (_previewCard != null && _overlayStyleCard != null && _carouselCard != null)
        {
            _previewCard.Top = 4;
            _previewCard.FitHeightToContent(12);

            _overlayStyleCard.Top = _previewCard.Bottom + 10;
            _overlayStyleCard.FitHeightToContent(12);

            _carouselCard.Top = _overlayStyleCard.Bottom + 10;
            _carouselCard.FitHeightToContent(12);

            _pageOverlay?.RecalculateScroll();
        }

        if (_layoutCard != null && _behaviorCard != null)
        {
            _layoutCard.Top = 4;
            _layoutCard.FitHeightToContent(12);

            _behaviorCard.Top = _layoutCard.Bottom + 10;
            _behaviorCard.FitHeightToContent(12);

            _pageBehavior?.RecalculateScroll();
        }
    }

    private bool _isGroupListAnimating = false;

    private DarkScrollPanel CreateGroupsPage()
    {
        DarkScrollPanel page = new DarkScrollPanel { Dock = DockStyle.Fill };

        _groupsMasterCard = new Win11CardPanel
        {
            Left = 18,
            Top = 4,
            Width = 468,
            Height = 56
        };

        Label lblTitle = new Label
        {
            Text = "Application Groups",
            Left = 14,
            Top = 10,
            Width = 280,
            Height = 20,
            Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 205, 255)
        };
        _groupsMasterCard.Controls.Add(lblTitle);

        Label lblSubtitle = new Label
        {
            Text = "Bundle multiple apps to control them together as a single source",
            Left = 14,
            Top = 30,
            Width = 320,
            Height = 16,
            Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
            ForeColor = Color.FromArgb(165, 165, 165)
        };
        _groupsMasterCard.Controls.Add(lblSubtitle);

        _btnAddGroupEntry = CreateModernButton("+ New Group", 334, 11, 120, 26, true);
        _btnAddGroupEntry.Click += (s, e) =>
        {
            if (_isGroupListAnimating) return;
            var newGroup = new ProgramGroup
            {
                GroupName = $"Group {_draftSettings.Groups.Count + 1}",
                Executables = new List<string> { "" }
            };
            AnimateAddGroupCard(newGroup);
        };
        _groupsMasterCard.Controls.Add(_btnAddGroupEntry);

        _groupsSlotsHost = new BufferedPanel
        {
            Left = 12,
            Top = 52,
            Width = 444,
            Height = 0,
            BackColor = Color.FromArgb(39, 39, 39)
        };
        _groupsMasterCard.Controls.Add(_groupsSlotsHost);
        page.InnerControls.Add(_groupsMasterCard);

        return page;
    }

    private void ShowGroupIconPickerMenu(ProgramGroup group, Control iconPreviewControl)
    {
        try
        {
            ContextMenuStrip menu = new ContextMenuStrip
            {
                Renderer = new DarkContextMenuRenderer(),
                BackColor = Color.FromArgb(32, 32, 32),
                ForeColor = Color.FromArgb(240, 240, 240),
                ShowImageMargin = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            string currentPath = string.IsNullOrWhiteSpace(group.CustomIconPath)
                ? "preset:cards"
                : group.CustomIconPath.Trim();

            foreach (var (key, displayName) in AppIconCache.DefaultGroupIconPresets)
            {
                Bitmap? bmp = AppIconCache.GetPresetIconPreview(key);
                var item = new ToolStripMenuItem(displayName, bmp)
                {
                    Checked = currentPath.Equals(key, StringComparison.OrdinalIgnoreCase)
                };
                string capturedKey = key;
                item.Click += (s, e) =>
                {
                    AppIconCache.InvalidateCustomIcon(group.CustomIconPath);
                    group.CustomIconPath = capturedKey;
                    iconPreviewControl.Invalidate();
                    PopulateCycleListUI();
                    MarkDirty();
                    PushLivePreviewIfActive(forceTrigger: true);
                };
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());

            var browseItem = new ToolStripMenuItem("Browse Custom Icon (.ico, .exe, .png)...");
            browseItem.Click += (s, e) => BrowseForGroupIcon(group, iconPreviewControl);
            menu.Items.Add(browseItem);

            menu.Show(iconPreviewControl, new Point(0, iconPreviewControl.Height + 2));
        }
        catch { }
    }

    private void BrowseForGroupIcon(ProgramGroup group, Control iconPreviewControl)
    {
        try
        {
            using OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "Select Custom Group Icon (.ico, .exe, .png, .jpg)",
                Filter = "Icons, Apps & Images (*.ico;*.exe;*.png;*.jpg;*.bmp)|*.ico;*.exe;*.png;*.jpg;*.bmp|All Files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                AppIconCache.InvalidateCustomIcon(group.CustomIconPath);
                group.CustomIconPath = dlg.FileName;
                AppIconCache.InvalidateCustomIcon(group.CustomIconPath);
                iconPreviewControl.Invalidate();
                PopulateCycleListUI();
                MarkDirty();
                PushLivePreviewIfActive(forceTrigger: true);
            }
        }
        catch { }
    }

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

    private void ReflowGroupsPage()
    {
        if (_groupsMasterCard == null || _groupsSlotsHost == null) return;

        if (!_isGroupListAnimating)
        {
            int y = 0;
            foreach (Win11CardPanel c in GetOrderedGroupCards())
            {
                c.Top = y;
                y += c.Height + 6;
            }
            _groupsSlotsHost.Height = y;
        }

        _groupsSlotsHost.Visible = (_groupsSlotsHost.Controls.Count > 0 && _groupsSlotsHost.Height > 0);
        _groupsMasterCard.Height = !_groupsSlotsHost.Visible ? 56 : (54 + _groupsSlotsHost.Height + 6);
        _pageGroups?.RecalculateScroll();
    }

    private void SyncDraftPersonalizationFields()
    {
        if (_isInitializing) return;
        int ParseInt(string text, int defaultVal) => int.TryParse(text, out int val) ? val : defaultVal;

        _draftSettings.OverlayPosition = _cmbPosition?.SelectedItem?.ToString() ?? _draftSettings.OverlayPosition;
        _draftSettings.PopupDurationMs = Math.Clamp(ParseInt(_txtPopupDuration?.Text ?? "2500", 2500), 500, 10000);
        _draftSettings.OverlayOpacity = _sldOverlayOpacity?.Value ?? _draftSettings.OverlayOpacity;
        _draftSettings.OverlayWidth = Math.Clamp(ParseInt(_txtWidth?.Text ?? "360", 360), 120, 2000);
        _draftSettings.OverlayHeight = Math.Clamp(ParseInt(_txtHeight?.Text ?? "80", 80), 50, 1000);
        _draftSettings.EnableInterpolation = _chkInterpolation?.Checked ?? true;
        _draftSettings.InterpolationSpeed = _sldInterpSpeed?.Value ?? _draftSettings.InterpolationSpeed;
        _draftSettings.StepSize = _sldStepSize?.Value ?? _draftSettings.StepSize;
        _draftSettings.CarouselOpenSpeed = _sldCarouselOpenSpeed?.Value ?? _draftSettings.CarouselOpenSpeed;
        _draftSettings.CarouselAnimationSpeed = _sldCarouselSpeed?.Value ?? _draftSettings.CarouselAnimationSpeed;

        string mode = _cmbCarouselMode?.SelectedItem?.ToString() ?? "Full";
        _draftSettings.CarouselMode = mode;
        _draftSettings.InvertCarouselScroll = _chkInvertCarouselScroll?.Checked ?? false;
        _draftSettings.CardOrderDirection = _cmbCardOrder?.SelectedItem?.ToString() ?? "Ascending";
        _draftSettings.NeighborPreviewVisibility = _cmbNeighborPreviewMode?.SelectedItem?.ToString() ?? "While Switching";
        _draftSettings.CompactShowNeighborPreviews = _draftSettings.NeighborPreviewVisibility != "Off";

        if (mode == "None")
        {
            _draftSettings.CarouselVisibleNeighbors = 0.0f;
        }
        else
        {
            var selectedStyleState = CarouselStyleStateMachine.ParseState(_cmbCarouselStyle?.SelectedItem?.ToString(), mode);
            string styleName = CarouselStyleStateMachine.ToDisplayName(selectedStyleState);
            _draftSettings.CarouselMotionStyle = styleName;
            _draftSettings.CarouselVisibleNeighbors = (mode == "Full") ? 1.5f : 0.0f;

            if (mode == "Full") _draftSettings.LastFullCarouselStyle = styleName;
            else if (mode == "Compact") _draftSettings.LastCompactCarouselStyle = styleName;
            else if (mode == "Minimal") _draftSettings.LastMinimalCarouselStyle = styleName;
        }

        _draftSettings.EnableCarouselAnimation = _chkEnableAnimations?.Checked ?? true;
        _draftSettings.CarouselBackgroundEnabled = _chkCarouselBg?.Checked ?? true;
    }

    private void PushLivePreviewIfActive(bool forceTrigger = false)
    {
        if (_isInitializing) return;
        SyncDraftPersonalizationFields();
        if (forceTrigger || _draftSettings.KeepOverlayOnWhileSettingsOpen)
        {
            _context.TriggerOverlayPreview(_draftSettings);
        }
    }

    private DarkScrollPanel CreateOverlayPage()
    {
        DarkScrollPanel page = new DarkScrollPanel { Dock = DockStyle.Fill };
        int y = 4;

        _previewCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int py = 10;
        AddSectionHeader(_previewCard, "Preview", 14, ref py);

        ModernButton btnShowPreview = CreateModernButton("Test Overlay", 14, py, 155, 28, true);
        btnShowPreview.Click += (s, e) => PushLivePreviewIfActive(forceTrigger: true);
        _previewCard.Controls.Add(btnShowPreview);

        _chkKeepOverlayAlwaysOn = new CheckBox
        {
            Text = "Pin overlay while settings open",
            Checked = _draftSettings.KeepOverlayOnWhileSettingsOpen,
            Left = 185,
            Top = py + 3,
            Width = 265,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkKeepOverlayAlwaysOn.CheckedChanged += (s, e) =>
        {
            _draftSettings.KeepOverlayOnWhileSettingsOpen = _chkKeepOverlayAlwaysOn.Checked;
            MarkDirty();
            _context.NotifySettingsWindowState(true, _draftSettings);
        };
        _previewCard.Controls.Add(_chkKeepOverlayAlwaysOn);

        _previewCard.FitHeightToContent(12);
        page.InnerControls.Add(_previewCard);
        y = _previewCard.Bottom + 10;

        _overlayStyleCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int sy = 10;
        AddSectionHeader(_overlayStyleCard, "Overlay Style", 14, ref sy);

        Label lblIdMode = new Label
        {
            Text = "Header Style:",
            Left = 14,
            Top = sy + 4,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _overlayStyleCard.Controls.Add(lblIdMode);

        _cmbIdentityMode = new ModernComboBox
        {
            Left = 185,
            Top = sy,
            Width = 265,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbIdentityMode.Items.AddRange(new object[] { "Both", "Name", "Icon" });
        _cmbIdentityMode.SelectedItem = _draftSettings.OverlayIdentityMode;
        if (_cmbIdentityMode.SelectedIndex == -1) _cmbIdentityMode.SelectedIndex = 0;
        _cmbIdentityMode.SelectedIndexChanged += (s, e) =>
        {
            _draftSettings.OverlayIdentityMode = _cmbIdentityMode.SelectedItem?.ToString() ?? "Both";
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _overlayStyleCard.Controls.Add(_cmbIdentityMode);
        sy += 32;

        Label lblLvlMode = new Label
        {
            Text = "Level Indicator:",
            Left = 14,
            Top = sy + 4,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _overlayStyleCard.Controls.Add(lblLvlMode);

        _cmbLevelMode = new ModernComboBox
        {
            Left = 185,
            Top = sy,
            Width = 265,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbLevelMode.Items.AddRange(new object[] { "Both", "Bar", "Percentage" });
        _cmbLevelMode.SelectedItem = _draftSettings.OverlayLevelMode;
        if (_cmbLevelMode.SelectedIndex == -1) _cmbLevelMode.SelectedIndex = 0;
        _cmbLevelMode.SelectedIndexChanged += (s, e) =>
        {
            _draftSettings.OverlayLevelMode = _cmbLevelMode.SelectedItem?.ToString() ?? "Both";
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _overlayStyleCard.Controls.Add(_cmbLevelMode);
        sy += 32;

        Label lblOpacity = new Label
        {
            Text = "Overlay Opacity:",
            Left = 14,
            Top = sy + 3,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _overlayStyleCard.Controls.Add(lblOpacity);

        _sldOverlayOpacity = new ModernSlider
        {
            Minimum = 10,
            Maximum = 100,
            Value = Math.Clamp(_draftSettings.OverlayOpacity, 10, 100),
            ValueSuffix = "%",
            Left = 185,
            Top = sy,
            Width = 265
        };
        _sldOverlayOpacity.ValueChanged += (s, e) =>
        {
            if (_isInitializing) return;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _overlayStyleCard.Controls.Add(_sldOverlayOpacity);
        sy += 30;

        _unmutedColorBox = AddColorPickerRow(_overlayStyleCard, "Active Bar Color:", _draftSettings.UnmutedColor, (hex) =>
        {
            _draftSettings.UnmutedColor = hex;
            MarkDirty();
            PushLivePreviewIfActive();
        }, ref sy);

        _mutedColorBox = AddColorPickerRow(_overlayStyleCard, "Muted Bar Color:", _draftSettings.MutedColor, (hex) =>
        {
            _draftSettings.MutedColor = hex;
            MarkDirty();
            PushLivePreviewIfActive();
        }, ref sy);

        _chkUseSourceBgColors = new CheckBox
        {
            Text = "Tint background with source colors",
            Checked = _draftSettings.UseSourceBackgroundColors,
            Left = 14,
            Top = sy + 2,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkUseSourceBgColors.CheckedChanged += (s, e) =>
        {
            _draftSettings.UseSourceBackgroundColors = _chkUseSourceBgColors.Checked;
            if (_chkFadeBgWithVolume != null) _chkFadeBgWithVolume.Enabled = _chkUseSourceBgColors.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _overlayStyleCard.Controls.Add(_chkUseSourceBgColors);
        sy += 26;

        _chkFadeBgWithVolume = new CheckBox
        {
            Text = "Dim background as volume lowers",
            Checked = _draftSettings.FadeBgColorWithVolume,
            Enabled = _draftSettings.UseSourceBackgroundColors,
            Left = 14,
            Top = sy + 2,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkFadeBgWithVolume.CheckedChanged += (s, e) =>
        {
            _draftSettings.FadeBgColorWithVolume = _chkFadeBgWithVolume.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _overlayStyleCard.Controls.Add(_chkFadeBgWithVolume);

        _overlayStyleCard.FitHeightToContent(12);
        page.InnerControls.Add(_overlayStyleCard);
        y = _overlayStyleCard.Bottom + 10;

        _carouselCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int cy = 10;
        AddSectionHeader(_carouselCard, "Pair Switching && Carousel", 14, ref cy);

        Label lblCarouselMode = new Label
        {
            Text = "Carousel Mode:",
            Left = 14,
            Top = cy + 4,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _carouselCard.Controls.Add(lblCarouselMode);

        _cmbCarouselMode = new ModernComboBox
        {
            Left = 185,
            Top = cy,
            Width = 265,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbCarouselMode.Items.AddRange(new object[] { "None", "Full", "Compact", "Minimal" });
        _cmbCarouselMode.SelectedItem = string.IsNullOrWhiteSpace(_draftSettings.CarouselMode) ? "Full" : _draftSettings.CarouselMode;
        if (_cmbCarouselMode.SelectedIndex == -1) _cmbCarouselMode.SelectedIndex = 1;
        _carouselCard.Controls.Add(_cmbCarouselMode);
        cy += 32;

        Label lblCarouselMotion = new Label
        {
            Text = "Switch Style:",
            Left = 14,
            Top = cy + 4,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _carouselCard.Controls.Add(lblCarouselMotion);

        _cmbCarouselStyle = new ModernComboBox
        {
            Left = 185,
            Top = cy,
            Width = 265,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BadgeResolver = null
        };
        _carouselCard.Controls.Add(_cmbCarouselStyle);
        cy += 32;

        Label lblNeighborPreviews = new Label
        {
            Text = "Idle Previews:",
            Left = 14,
            Top = cy + 4,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _carouselCard.Controls.Add(lblNeighborPreviews);

        _cmbNeighborPreviewMode = new ModernComboBox
        {
            Left = 185,
            Top = cy,
            Width = 265,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _carouselCard.Controls.Add(_cmbNeighborPreviewMode);
        cy += 32;

        _chkEnableAnimations = new CheckBox
        {
            Text = "Smooth switch animations",
            Checked = _draftSettings.EnableCarouselAnimation,
            Left = 14,
            Top = cy,
            Width = 170,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _carouselCard.Controls.Add(_chkEnableAnimations);

        _chkCarouselBg = new CheckBox
        {
            Text = "Carousel backdrop",
            Checked = _draftSettings.CarouselBackgroundEnabled,
            Left = 185,
            Top = cy,
            Width = 265,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkCarouselBg.CheckedChanged += (s, e) => { if (!_isInitializing) { MarkDirty(); PushLivePreviewIfActive(); } };
        _carouselCard.Controls.Add(_chkCarouselBg);

        Label lblCarouselOpenSpeed = new Label
        {
            Text = "Carousel Open Speed:",
            Left = 14,
            Top = cy,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _carouselCard.Controls.Add(lblCarouselOpenSpeed);

        _sldCarouselOpenSpeed = new ModernSlider
        {
            Minimum = 5,
            Maximum = 100,
            Value = Math.Clamp(_draftSettings.CarouselOpenSpeed, 5, 100),
            Left = 185,
            Top = cy,
            Width = 265
        };
        _sldCarouselOpenSpeed.ValueChanged += (s, e) => { if (!_isInitializing) { MarkDirty(); PushLivePreviewIfActive(); } };
        _carouselCard.Controls.Add(_sldCarouselOpenSpeed);

        Label lblCarouselSpeed = new Label
        {
            Text = "Carousel Scroll Speed:",
            Left = 14,
            Top = cy,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _carouselCard.Controls.Add(lblCarouselSpeed);

        _sldCarouselSpeed = new ModernSlider
        {
            Minimum = 5,
            Maximum = 100,
            Value = Math.Clamp(_draftSettings.CarouselAnimationSpeed, 5, 100),
            Left = 185,
            Top = cy,
            Width = 265
        };
        _sldCarouselSpeed.ValueChanged += (s, e) => { if (!_isInitializing) { MarkDirty(); PushLivePreviewIfActive(); } };
        _carouselCard.Controls.Add(_sldCarouselSpeed);

        _chkInvertCarouselScroll = new CheckBox
        {
            Text = "Invert carousel scroll direction",
            Checked = _draftSettings.InvertCarouselScroll,
            Left = 14,
            Top = cy,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkInvertCarouselScroll.CheckedChanged += (s, e) => { if (!_isInitializing) { MarkDirty(); PushLivePreviewIfActive(); } };
        _carouselCard.Controls.Add(_chkInvertCarouselScroll);

        Label lblCardOrder = new Label
        {
            Text = "Card Order Direction:",
            Left = 14,
            Top = cy,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _carouselCard.Controls.Add(lblCardOrder);

        _cmbCardOrder = new ModernComboBox
        {
            Left = 185,
            Top = cy,
            Width = 265,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbCardOrder.Items.AddRange(new object[] { "Ascending", "Descending" });
        _cmbCardOrder.SelectedItem = string.IsNullOrWhiteSpace(_draftSettings.CardOrderDirection) ? "Ascending" : _draftSettings.CardOrderDirection;
        if (_cmbCardOrder.SelectedIndex == -1) _cmbCardOrder.SelectedIndex = 0;
        _cmbCardOrder.SelectedIndexChanged += (s, e) => { if (!_isInitializing) { MarkDirty(); PushLivePreviewIfActive(); } };
        _carouselCard.Controls.Add(_cmbCardOrder);

        _cmbNeighborPreviewMode.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            _draftSettings.NeighborPreviewVisibility = _cmbNeighborPreviewMode.SelectedItem?.ToString() ?? "While Switching";
            _draftSettings.CompactShowNeighborPreviews = _draftSettings.NeighborPreviewVisibility != "Off";
            MarkDirty();
            PushLivePreviewIfActive();
        };

        _chkEnableAnimations.CheckedChanged += (s, e) =>
        {
            if (_isInitializing) return;
            string mode = _cmbCarouselMode.SelectedItem?.ToString() ?? "Full";
            var curSt = CarouselStyleStateMachine.ParseState(_cmbCarouselStyle.SelectedItem?.ToString(), mode);
            bool canAnim = _chkEnableAnimations.Checked && !CarouselStyleStateMachine.IsInstantCut(curSt);
            _sldCarouselOpenSpeed.Enabled = canAnim;
            _sldCarouselSpeed.Enabled = canAnim;
            MarkDirty();
            PushLivePreviewIfActive();
        };

        _cmbCarouselStyle.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            string mode = _cmbCarouselMode.SelectedItem?.ToString() ?? "Full";
            var nextState = CarouselStyleStateMachine.ParseState(_cmbCarouselStyle.SelectedItem?.ToString(), mode);
            string styleName = CarouselStyleStateMachine.ToDisplayName(nextState);

            _draftSettings.CarouselMotionStyle = styleName;
            if (mode == "Full") _draftSettings.LastFullCarouselStyle = styleName;
            else if (mode == "Compact") _draftSettings.LastCompactCarouselStyle = styleName;
            else if (mode == "Minimal") _draftSettings.LastMinimalCarouselStyle = styleName;

            bool canAnimate = !CarouselStyleStateMachine.IsInstantCut(nextState);
            _chkEnableAnimations.Enabled = canAnimate;
            _sldCarouselOpenSpeed.Enabled = canAnimate && _chkEnableAnimations.Checked;
            _sldCarouselSpeed.Enabled = canAnimate && _chkEnableAnimations.Checked;

            MarkDirty();
            PushLivePreviewIfActive();
        };

        void ApplyCarouselModeLayout(bool animate)
        {
            string mode = _cmbCarouselMode.SelectedItem?.ToString() ?? "Full";
            bool isNone = (mode == "None");
            bool isFull = (mode == "Full");
            bool isCompact = (mode == "Compact");

            int targetHeight;
            if (isNone)
            {
                targetHeight = _cmbCarouselMode.Bottom + 12;
            }
            else
            {
                bool prevInit = _isInitializing;
                _isInitializing = true;

                _cmbCarouselStyle.Items.Clear();
                if (isFull)
                {
                    _cmbCarouselStyle.Items.AddRange(CarouselStyleStateMachine.FullDisplayNames);
                    _cmbCarouselStyle.SelectedItem = string.IsNullOrWhiteSpace(_draftSettings.LastFullCarouselStyle)
                        ? "3D Floating Pills"
                        : _draftSettings.LastFullCarouselStyle;
                }
                else if (isCompact)
                {
                    _cmbCarouselStyle.Items.AddRange(CarouselStyleStateMachine.CompactDisplayNames);
                    _cmbCarouselStyle.SelectedItem = string.IsNullOrWhiteSpace(_draftSettings.LastCompactCarouselStyle)
                        ? "Top/Bottom Preview Morph"
                        : _draftSettings.LastCompactCarouselStyle;
                }
                else
                {
                    _cmbCarouselStyle.Items.AddRange(CarouselStyleStateMachine.MinimalDisplayNames);
                    _cmbCarouselStyle.SelectedItem = string.IsNullOrWhiteSpace(_draftSettings.LastMinimalCarouselStyle)
                        ? "Side Preview Morph"
                        : _draftSettings.LastMinimalCarouselStyle;
                }
                if (_cmbCarouselStyle.SelectedIndex == -1 && _cmbCarouselStyle.Items.Count > 0)
                    _cmbCarouselStyle.SelectedIndex = 0;

                string curPreviewChoice = _draftSettings.NeighborPreviewVisibility;
                _cmbNeighborPreviewMode.Items.Clear();
                if (isFull)
                {
                    _cmbNeighborPreviewMode.Items.AddRange(new object[] { "While Switching", "Always" });
                    _cmbNeighborPreviewMode.SelectedItem = (curPreviewChoice == "Always") ? "Always" : "While Switching";
                }
                else
                {
                    _cmbNeighborPreviewMode.Items.AddRange(new object[] { "Always", "While Switching", "Off" });
                    _cmbNeighborPreviewMode.SelectedItem = curPreviewChoice;
                    if (_cmbNeighborPreviewMode.SelectedIndex == -1) _cmbNeighborPreviewMode.SelectedIndex = 0;
                }

                var activeSt = CarouselStyleStateMachine.ParseState(_cmbCarouselStyle.SelectedItem?.ToString(), mode);
                string activeStyleName = CarouselStyleStateMachine.ToDisplayName(activeSt);
                _draftSettings.CarouselMotionStyle = activeStyleName;
                if (isFull) _draftSettings.LastFullCarouselStyle = activeStyleName;
                else if (isCompact) _draftSettings.LastCompactCarouselStyle = activeStyleName;
                else _draftSettings.LastMinimalCarouselStyle = activeStyleName;

                bool canAnimate = !CarouselStyleStateMachine.IsInstantCut(activeSt);
                _chkEnableAnimations.Enabled = canAnimate;
                _sldCarouselOpenSpeed.Enabled = canAnimate && _chkEnableAnimations.Checked;
                _sldCarouselSpeed.Enabled = canAnimate && _chkEnableAnimations.Checked;
                _isInitializing = prevInit;

                int rowY = _cmbCarouselStyle.Bottom + 7;

                lblNeighborPreviews.Top = rowY + 4;
                _cmbNeighborPreviewMode.Top = rowY;
                rowY += 32;

                _chkEnableAnimations.Top = rowY;
                _chkCarouselBg.Top = rowY;
                rowY += 28;

                if (isFull)
                {
                    lblCarouselOpenSpeed.Top = rowY + 3;
                    _sldCarouselOpenSpeed.Top = rowY;
                    rowY += 30;
                }

                lblCarouselSpeed.Top = rowY + 3;
                _sldCarouselSpeed.Top = rowY;
                rowY += 30;

                _chkInvertCarouselScroll.Top = rowY;
                rowY += 28;

                lblCardOrder.Top = rowY + 4;
                _cmbCardOrder.Top = rowY;
                rowY += 25;

                targetHeight = rowY + 12;
            }

            void SetTargetControlVisibility()
            {
                lblCarouselMotion.Visible = !isNone;
                _cmbCarouselStyle.Visible = !isNone;
                lblNeighborPreviews.Visible = !isNone;
                _cmbNeighborPreviewMode.Visible = !isNone;
                _chkCarouselBg.Visible = isFull;
                _chkEnableAnimations.Visible = !isNone;
                lblCarouselOpenSpeed.Visible = isFull;
                _sldCarouselOpenSpeed.Visible = isFull;
                lblCarouselSpeed.Visible = !isNone;
                _sldCarouselSpeed.Visible = !isNone;
                _chkInvertCarouselScroll.Visible = !isNone;
                lblCardOrder.Visible = !isNone;
                _cmbCardOrder.Visible = !isNone;
            }

            if (!animate || !this.IsHandleCreated || _isInitializing)
            {
                _carouselCardAnimTimer?.Stop();
                SetTargetControlVisibility();
                _carouselCard.Height = targetHeight;
                ReflowPersonalizationPage();
                return;
            }

            if (!isNone) SetTargetControlVisibility();

            int startHeight = _carouselCard.Height;
            _carouselCardAnimTimer?.Stop();
            _carouselCardAnimTimer?.Dispose();

            const double durationMs = 150.0;
            Stopwatch sw = Stopwatch.StartNew();
            _carouselCardAnimTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _carouselCardAnimTimer.Tick += (s, e) =>
            {
                if (_carouselCard.IsDisposed)
                {
                    _carouselCardAnimTimer.Stop();
                    return;
                }

                double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
                float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

                if (t >= 1.0)
                {
                    _carouselCardAnimTimer.Stop();
                    SetTargetControlVisibility();
                    _carouselCard.Height = targetHeight;
                }
                else
                {
                    _carouselCard.Height = startHeight + (int)Math.Round((targetHeight - startHeight) * ease);
                }

                _pageOverlay?.RecalculateScroll();
            };
            _carouselCardAnimTimer.Start();
        }

        _updateCarouselModeLayout = () => ApplyCarouselModeLayout(animate: false);

        _cmbCarouselMode.SelectedIndexChanged += (s, e) =>
        {
            ApplyCarouselModeLayout(animate: !_isInitializing);
            if (!_isInitializing) { MarkDirty(); PushLivePreviewIfActive(); }
        };

        ApplyCarouselModeLayout(animate: false);
        page.InnerControls.Add(_carouselCard);
        page.RecalculateScroll();
        return page;
    }

    private DarkScrollPanel CreateBehaviorPage()
    {
        DarkScrollPanel page = new DarkScrollPanel { Dock = DockStyle.Fill };
        int y = 4;

        _layoutCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int ay = 10;
        AddSectionHeader(_layoutCard, "Position && Size", 14, ref ay);

        Label lblPos = new Label
        {
            Text = "Screen Position:",
            Left = 14,
            Top = ay + 4,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _layoutCard.Controls.Add(lblPos);

        _cmbPosition = new ModernComboBox
        {
            Left = 185,
            Top = ay,
            Width = 265,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbPosition.Items.AddRange(new object[] { "Bottom Center", "Top Center", "Bottom Right", "Bottom Left", "Top Right", "Top Left" });
        _cmbPosition.SelectedItem = _draftSettings.OverlayPosition;
        if (_cmbPosition.SelectedIndex == -1) _cmbPosition.SelectedIndex = 0;
        _cmbPosition.SelectedIndexChanged += (s, e) =>
        {
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _layoutCard.Controls.Add(_cmbPosition);
        ay += 32;

        CheckBox chkFollowMonitor = new CheckBox
        {
            Text = "Follow active monitor",
            Checked = _draftSettings.FollowActiveMonitor,
            Left = 14,
            Top = ay,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _layoutCard.Controls.Add(chkFollowMonitor);
        ay += 28;

        _monitorRowsPanel = new Panel
        {
            Left = 14,
            Top = ay,
            Width = 436,
            AutoSize = false,
            Enabled = !_draftSettings.FollowActiveMonitor
        };

        Screen[] allScreens = Screen.AllScreens;
        int my = 0;
        for (int i = 0; i < allScreens.Length; i++)
        {
            Screen scr = allScreens[i];
            var existingCfg = _draftSettings.MonitorConfigs.FirstOrDefault(m => m.DeviceName.Equals(scr.DeviceName, StringComparison.OrdinalIgnoreCase));
            if (existingCfg == null)
            {
                existingCfg = new MonitorOverlayConfig
                {
                    DeviceName = scr.DeviceName,
                    Enabled = scr.Primary,
                    Position = _draftSettings.OverlayPosition
                };
                _draftSettings.MonitorConfigs.Add(existingCfg);
            }

            var (physW, physH) = GetPhysicalMonitorResolution(scr);
            string monLabel = $"Monitor {i + 1}{(scr.Primary ? " (Primary)" : "")} [{physW}×{physH}]";
            CheckBox chkMon = new CheckBox
            {
                Text = monLabel,
                Checked = existingCfg.Enabled,
                Left = 0,
                Top = my + 3,
                Width = 230,
                Height = 22,
                ForeColor = Color.FromArgb(215, 215, 215)
            };

            ModernComboBox cmbMonPos = new ModernComboBox
            {
                Left = 236,
                Top = my,
                Width = 200,
                Enabled = existingCfg.Enabled,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbMonPos.Items.AddRange(new object[] { "Bottom Center", "Top Center", "Bottom Right", "Bottom Left", "Top Right", "Top Left" });
            cmbMonPos.SelectedItem = existingCfg.Position;
            if (cmbMonPos.SelectedIndex == -1) cmbMonPos.SelectedIndex = 0;

            chkMon.CheckedChanged += (s, e) =>
            {
                existingCfg.Enabled = chkMon.Checked;
                cmbMonPos.Enabled = chkMon.Checked;
                MarkDirty();
                PushLivePreviewIfActive();
            };
            cmbMonPos.SelectedIndexChanged += (s, e) =>
            {
                existingCfg.Position = cmbMonPos.SelectedItem?.ToString() ?? "Bottom Center";
                if (scr.Primary) _draftSettings.OverlayPosition = existingCfg.Position;
                MarkDirty();
                PushLivePreviewIfActive();
            };

            _monitorRowsPanel.Controls.Add(chkMon);
            _monitorRowsPanel.Controls.Add(cmbMonPos);
            my += 30;
        }
        _monitorRowsPanel.Height = Math.Max(30, my);
        _layoutCard.Controls.Add(_monitorRowsPanel);
        ay += _monitorRowsPanel.Height + 6;

        chkFollowMonitor.CheckedChanged += (s, e) =>
        {
            _draftSettings.FollowActiveMonitor = chkFollowMonitor.Checked;
            _monitorRowsPanel.Enabled = !chkFollowMonitor.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };

        Label lblSize = new Label
        {
            Text = "Dimensions (W × H):",
            Left = 14,
            Top = ay + 3,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _layoutCard.Controls.Add(lblSize);

        _txtWidth = new TextBox
        {
            Text = _draftSettings.OverlayWidth.ToString(),
            Left = 185,
            Top = ay,
            Width = 90,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtWidth.TextChanged += (s, e) =>
        {
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _layoutCard.Controls.Add(_txtWidth);

        Label lblBy = new Label
        {
            Text = "×",
            Left = 280,
            Top = ay + 3,
            Width = 18,
            Height = 20,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(180, 180, 180)
        };
        _layoutCard.Controls.Add(lblBy);

        _txtHeight = new TextBox
        {
            Text = _draftSettings.OverlayHeight.ToString(),
            Left = 302,
            Top = ay,
            Width = 90,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtHeight.TextChanged += (s, e) =>
        {
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _layoutCard.Controls.Add(_txtHeight);
        ay += 30;

        Label lblDur = new Label
        {
            Text = "Hide Delay (ms):",
            Left = 14,
            Top = ay + 3,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _layoutCard.Controls.Add(lblDur);

        _txtPopupDuration = new TextBox
        {
            Text = _draftSettings.PopupDurationMs.ToString(),
            Left = 185,
            Top = ay,
            Width = 90,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtPopupDuration.TextChanged += (s, e) => MarkDirty();
        _layoutCard.Controls.Add(_txtPopupDuration);

        _layoutCard.FitHeightToContent(12);
        page.InnerControls.Add(_layoutCard);
        y = _layoutCard.Bottom + 10;

        _behaviorCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int by = 10;
        AddSectionHeader(_behaviorCard, "Audio && System", 14, ref by);

        Label lblStep = new Label
        {
            Text = "Knob Step Size:",
            Left = 14,
            Top = by + 3,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _behaviorCard.Controls.Add(lblStep);

        _sldStepSize = new ModernSlider
        {
            Minimum = 1,
            Maximum = 50,
            Value = Math.Clamp(_draftSettings.StepSize, 1, 50),
            ValueSuffix = "%",
            Left = 185,
            Top = by,
            Width = 265
        };
        _sldStepSize.ValueChanged += (s, e) => { if (!_isInitializing) MarkDirty(); };
        _behaviorCard.Controls.Add(_sldStepSize);
        by += 30;

        Label lblSpeed = new Label
        {
            Text = "Volume Smoothing Speed:",
            Left = 14,
            Top = by + 3,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _behaviorCard.Controls.Add(lblSpeed);

        _sldInterpSpeed = new ModernSlider
        {
            Minimum = 5,
            Maximum = 100,
            Value = Math.Clamp(_draftSettings.InterpolationSpeed, 5, 100),
            Enabled = _draftSettings.EnableInterpolation,
            Left = 185,
            Top = by,
            Width = 265
        };
        _sldInterpSpeed.ValueChanged += (s, e) => { if (!_isInitializing) MarkDirty(); };
        _behaviorCard.Controls.Add(_sldInterpSpeed);
        by += 30;

        _chkInterpolation = new CheckBox
        {
            Text = "Smooth volume interpolation",
            Checked = _draftSettings.EnableInterpolation,
            Left = 14,
            Top = by,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkInterpolation.CheckedChanged += (s, e) =>
        {
            if (_sldInterpSpeed != null) _sldInterpSpeed.Enabled = _chkInterpolation.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _behaviorCard.Controls.Add(_chkInterpolation);
        by += 26;

        _chkBuffer = new CheckBox
        {
            Text = "Center detent (pause at 50/50)",
            Checked = _draftSettings.EnableEqualizationBuffer,
            Left = 14,
            Top = by,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkBuffer.CheckedChanged += (s, e) => MarkDirty();
        _behaviorCard.Controls.Add(_chkBuffer);
        by += 26;

        _chkStartup = new CheckBox
        {
            Text = "Run at Windows startup",
            Checked = _draftSettings.RunOnStartup,
            Left = 14,
            Top = by,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkStartup.CheckedChanged += (s, e) => MarkDirty();
        _behaviorCard.Controls.Add(_chkStartup);

        _behaviorCard.FitHeightToContent(12);
        page.InnerControls.Add(_behaviorCard);

        page.RecalculateScroll();
        return page;
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
        PopulateCycleListUI();

        _kbKey1.Binding = settings.CrossfadeKeyUp;
        _kbKey2.Binding = settings.CrossfadeKeyDown;
        _kbKey3.Binding = settings.CrossfadeKeyMute;
        _cmbGlobalMuteMode.SelectedItem = settings.GlobalMuteMode;
        if (_cmbGlobalMuteMode.SelectedIndex == -1) _cmbGlobalMuteMode.SelectedIndex = 0;

        _kbKeyCycleNext.Binding = settings.GetEffectiveCycleNextKey();
        _kbKeyCyclePrev.Binding = settings.CyclePrevKey;

        _sldStepSize.Value = Math.Clamp(settings.StepSize, 1, 50);
        _chkBuffer.Checked = settings.EnableEqualizationBuffer;
        _chkStartup.Checked = settings.RunOnStartup;

        _chkKeepOverlayAlwaysOn.Checked = settings.KeepOverlayOnWhileSettingsOpen;
        _cmbIdentityMode.SelectedItem = settings.OverlayIdentityMode;
        _cmbLevelMode.SelectedItem = settings.OverlayLevelMode;

        string currentMode = string.IsNullOrWhiteSpace(settings.CarouselMode) ? "Full" : settings.CarouselMode;
        var loadedStyle = CarouselStyleStateMachine.ParseState(settings.CarouselMotionStyle, currentMode);
        string loadedStyleName = CarouselStyleStateMachine.ToDisplayName(loadedStyle);
        if (CarouselStyleStateMachine.IsWideCarousel(loadedStyle))
            _draftSettings.LastFullCarouselStyle = loadedStyleName;
        else if (CarouselStyleStateMachine.IsCompactCarousel(loadedStyle))
            _draftSettings.LastCompactCarouselStyle = loadedStyleName;
        else
            _draftSettings.LastMinimalCarouselStyle = loadedStyleName;

        _chkUseSourceBgColors.Checked = settings.UseSourceBackgroundColors;
        _chkFadeBgWithVolume.Checked = settings.FadeBgColorWithVolume;
        _chkFadeBgWithVolume.Enabled = settings.UseSourceBackgroundColors;
        _chkEnableAnimations.Checked = settings.EnableCarouselAnimation;
        if (_sldCarouselOpenSpeed != null) _sldCarouselOpenSpeed.Value = Math.Clamp(settings.CarouselOpenSpeed, 5, 100);
        _sldCarouselSpeed.Value = Math.Clamp(settings.CarouselAnimationSpeed, 5, 100);
        _chkCarouselBg.Checked = settings.CarouselBackgroundEnabled;

        if (_cmbCarouselMode != null)
        {
            _cmbCarouselMode.SelectedItem = string.IsNullOrWhiteSpace(settings.CarouselMode) ? "Full" : settings.CarouselMode;
            if (_cmbCarouselMode.SelectedIndex == -1) _cmbCarouselMode.SelectedIndex = 1;
        }
        if (_cmbNeighborPreviewMode != null)
        {
            _cmbNeighborPreviewMode.SelectedItem = string.IsNullOrWhiteSpace(settings.NeighborPreviewVisibility) ? "While Switching" : settings.NeighborPreviewVisibility;
            if (_cmbNeighborPreviewMode.SelectedIndex == -1) _cmbNeighborPreviewMode.SelectedIndex = 0;
        }
        if (_chkInvertCarouselScroll != null) _chkInvertCarouselScroll.Checked = settings.InvertCarouselScroll;
        if (_cmbCardOrder != null)
        {
            _cmbCardOrder.SelectedItem = string.IsNullOrWhiteSpace(settings.CardOrderDirection) ? "Ascending" : settings.CardOrderDirection;
            if (_cmbCardOrder.SelectedIndex == -1) _cmbCardOrder.SelectedIndex = 0;
        }

        _updateCarouselModeLayout?.Invoke();

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

    private void RefreshGroupsTabUI()
    {
        PopulateGroupsFlowContent();
    }

    private void PopulateGroupsFlowContent()
    {
        if (_groupsSlotsHost == null) return;
        _groupsSlotsHost.SuspendLayout();
        _groupsSlotsHost.Controls.Clear();

        int hostW = Math.Max(442, _groupsSlotsHost.Width);
        List<string> exeSuggestions = GetExecutableSuggestions();
        int y = 0;

        foreach (var group in _draftSettings.Groups)
        {
            if (group.Executables.Count == 0)
                group.Executables.Add("");

            Win11CardPanel groupCard = BuildSingleGroupCard(group, exeSuggestions, hostW);
            groupCard.Top = y;
            _groupsSlotsHost.Controls.Add(groupCard);
            y += groupCard.Height + 6;
        }

        _groupsSlotsHost.ResumeLayout();
        ReflowGroupsPage();
    }

    private void AnimateAddGroupCard(ProgramGroup newGroup)
    {
        if (_groupsSlotsHost == null) return;

        var existingOrdered = GetOrderedGroupCards();
        int finalTop = 0;
        foreach (var c in existingOrdered)
        {
            finalTop = Math.Max(finalTop, c.Bottom + 6);
        }

        _draftSettings.Groups.Add(newGroup);
        int hostW = Math.Max(442, _groupsSlotsHost.Width);
        List<string> exeSuggestions = GetExecutableSuggestions();

        Win11CardPanel newCard = BuildSingleGroupCard(newGroup, exeSuggestions, hostW);
        int finalCardH = newCard.Height;

        // Place the new group at its exact bottom slot below all existing groups,
        // and smoothly grow the card & host container downward out of the bottom!
        int startHostH = finalTop;
        int finalHostH = finalTop + finalCardH + 6;

        _groupsSlotsHost.SuspendLayout();
        newCard.Top = finalTop;
        newCard.Height = 0;
        _groupsSlotsHost.Controls.Add(newCard);
        _groupsSlotsHost.Height = Math.Max(1, startHostH);
        _groupsSlotsHost.Visible = true;
        _groupsSlotsHost.ResumeLayout(false);

        MarkDirty();
        ReflowGroupsPage();

        _isGroupListAnimating = true;
        const double durationMs = 190.0;
        Stopwatch sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };

        timer.Tick += (s, e) =>
        {
            if (newCard.IsDisposed || _groupsSlotsHost.IsDisposed)
            {
                _isGroupListAnimating = false;
                timer.Stop();
                timer.Dispose();
                return;
            }

            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

            if (t >= 1.0)
            {
                newCard.Top = finalTop;
                newCard.Height = finalCardH;
                _groupsSlotsHost.Height = finalHostH;
                _isGroupListAnimating = false;
                timer.Stop();
                timer.Dispose();
            }
            else
            {
                newCard.Top = finalTop;
                newCard.Height = Math.Max(1, (int)Math.Round(finalCardH * ease));
                _groupsSlotsHost.Height = startHostH + (int)Math.Round((finalHostH - startHostH) * ease);
            }

            ReflowGroupsPage();
        };
        timer.Start();
    }

    private void AnimateRemoveGroupCard(ProgramGroup groupToRemove)
    {
        if (_groupsSlotsHost == null) return;
        var orderedCards = GetOrderedGroupCards();
        int removeIndex = _draftSettings.Groups.IndexOf(groupToRemove);
        if (removeIndex < 0) return;

        Win11CardPanel? removingCard = orderedCards.FirstOrDefault(c => ReferenceEquals(c.Tag, groupToRemove));
        if (removingCard == null) return;

        int startCardH = removingCard.Height;
        int shiftDelta = startCardH + 6;

        List<(Win11CardPanel ctrl, int startY, int targetY)> trailingCards = new();
        for (int i = removeIndex + 1; i < orderedCards.Count; i++)
        {
            var tc = orderedCards[i];
            trailingCards.Add((tc, tc.Top, tc.Top - shiftDelta));
        }

        int startHostH = _groupsSlotsHost.Height;
        int targetHostH = Math.Max(0, startHostH - shiftDelta);

        _isGroupListAnimating = true;
        const double durationMs = 190.0;
        Stopwatch sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };

        timer.Tick += (s, e) =>
        {
            if (_groupsSlotsHost.IsDisposed)
            {
                _isGroupListAnimating = false;
                timer.Stop();
                timer.Dispose();
                return;
            }

            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

            if (t >= 1.0)
            {
                timer.Stop();
                timer.Dispose();

                _groupsSlotsHost.SuspendLayout();
                _draftSettings.Groups.Remove(groupToRemove);
                _groupsSlotsHost.Controls.Remove(removingCard);
                removingCard.Dispose();

                foreach (var item in trailingCards)
                    item.ctrl.Top = item.targetY;

                _groupsSlotsHost.Height = targetHostH;
                _groupsSlotsHost.ResumeLayout(false);

                _isGroupListAnimating = false;
                ReflowGroupsPage();
                MarkDirty();
                PushLivePreviewIfActive();
                return;
            }

            removingCard.Height = Math.Max(0, (int)Math.Round(startCardH * (1f - ease)));
            foreach (var item in trailingCards)
                item.ctrl.Top = item.startY + (int)Math.Round((item.targetY - item.startY) * ease);

            _groupsSlotsHost.Height = startHostH + (int)Math.Round((targetHostH - startHostH) * ease);
            ReflowGroupsPage();
        };
        timer.Start();
    }

    private void AnimateGroupCardHeightChange(
        Win11CardPanel card,
        int targetCardH,
        Control? slidingRow,
        int rowStartTop,
        int rowTargetTop,
        List<(Control ctrl, int startY, int targetY)> shiftingChildren,
        Action? onFinished = null)
    {
        if (_groupsSlotsHost == null) return;

        int startCardH = card.Height;
        int deltaH = targetCardH - startCardH;

        Control? btnAddExe = card.Controls.Cast<Control>().FirstOrDefault(c => c.Tag as string == "AddExeBtn");
        int btnStartTop = btnAddExe?.Top ?? 0;
        int btnTargetTop = targetCardH - 37;

        var orderedCards = GetOrderedGroupCards();
        int cardIdx = orderedCards.IndexOf(card);
        List<(Win11CardPanel ctrl, int startY, int targetY)> trailingGroupCards = new();
        for (int i = cardIdx + 1; i < orderedCards.Count; i++)
        {
            var tc = orderedCards[i];
            trailingGroupCards.Add((tc, tc.Top, tc.Top + deltaH));
        }

        int startHostH = _groupsSlotsHost.Height;
        int targetHostH = Math.Max(0, startHostH + deltaH);

        _isGroupListAnimating = true;
        const double durationMs = 175.0;
        Stopwatch sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };

        timer.Tick += (s, e) =>
        {
            if (card.IsDisposed || _groupsSlotsHost.IsDisposed)
            {
                _isGroupListAnimating = false;
                timer.Stop();
                timer.Dispose();
                return;
            }

            double t = Math.Clamp(sw.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0));

            if (t >= 1.0)
            {
                timer.Stop();
                timer.Dispose();

                card.Height = targetCardH;
                if (slidingRow != null && !slidingRow.IsDisposed)
                    slidingRow.Top = rowTargetTop;
                foreach (var sc in shiftingChildren)
                    if (!sc.ctrl.IsDisposed) sc.ctrl.Top = sc.targetY;
                if (btnAddExe != null)
                {
                    btnAddExe.Top = btnTargetTop;
                    btnAddExe.BringToFront();
                }
                foreach (var tc in trailingGroupCards)
                    tc.ctrl.Top = tc.targetY;

                _groupsSlotsHost.Height = targetHostH;
                _isGroupListAnimating = false;
                onFinished?.Invoke();
                ReflowGroupsPage();
                return;
            }

            card.Height = startCardH + (int)Math.Round(deltaH * ease);
            if (slidingRow != null && !slidingRow.IsDisposed)
                slidingRow.Top = rowStartTop + (int)Math.Round((rowTargetTop - rowStartTop) * ease);
            foreach (var sc in shiftingChildren)
                if (!sc.ctrl.IsDisposed) sc.ctrl.Top = sc.startY + (int)Math.Round((sc.targetY - sc.startY) * ease);
            if (btnAddExe != null)
            {
                btnAddExe.Top = btnStartTop + (int)Math.Round((btnTargetTop - btnStartTop) * ease);
                btnAddExe.BringToFront();
            }
            foreach (var tc in trailingGroupCards)
                tc.ctrl.Top = tc.startY + (int)Math.Round((tc.targetY - tc.startY) * ease);

            _groupsSlotsHost.Height = startHostH + (int)Math.Round(deltaH * ease);
            ReflowGroupsPage();
        };
        timer.Start();
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

    private BufferedPanel CreateGroupExeRowControl(
        Win11CardPanel card,
        ProgramGroup group,
        int initialIndex,
        List<string> exeSuggestions,
        int hostW)
    {
        int exeComboW = Math.Max(180, hostW - 154);
        int btnBrowseLeft = 10 + exeComboW + 6;
        int btnRemExeLeft = btnBrowseLeft + 94 + 4;

        BufferedPanel exeRow = new BufferedPanel
        {
            Left = 2,
            Top = 60 + initialIndex * 31,
            Width = hostW - 4,
            Height = 27,
            BackColor = Color.FromArgb(48, 48, 48),
            Tag = "GroupExeRow"
        };

        int GetCurrentRowIndex()
        {
            var ordered = card.Controls.Cast<Control>()
                .Where(c => c.Tag as string == "GroupExeRow")
                .OrderBy(c => c.Top)
                .ToList();
            return ordered.IndexOf(exeRow);
        }

        ModernComboBox cmbExe = new ModernComboBox
        {
            Left = 8,
            Top = 1,
            Width = exeComboW,
            DropDownStyle = ComboBoxStyle.DropDown,
            BadgeResolver = ResolveDropdownBadge,
            IconResolver = ResolveDropdownIcon,
            Tag = "ExeCombo"
        };
        cmbExe.Items.AddRange(exeSuggestions.Cast<object>().ToArray());
        string initVal = (initialIndex >= 0 && initialIndex < group.Executables.Count) ? group.Executables[initialIndex] : "";
        cmbExe.SelectedItem = initVal;
        if (cmbExe.SelectedIndex == -1) cmbExe.Text = initVal;

        cmbExe.DropDown += (s, e) =>
        {
            _detectedAudio = AudioProcessDetector.Scan();
            PopulateSingleComboOptions(cmbExe, includeGroups: false);
        };
        cmbExe.SelectedIndexChanged += (s, e) =>
        {
            if (_isInitializing) return;
            int idx = GetCurrentRowIndex();
            if (idx >= 0 && idx < group.Executables.Count)
            {
                group.Executables[idx] = ReadComboText(cmbExe);
                MarkDirty();
            }
        };
        cmbExe.TextChanged += (s, e) =>
        {
            if (_isInitializing) return;
            int idx = GetCurrentRowIndex();
            if (idx >= 0 && idx < group.Executables.Count)
            {
                group.Executables[idx] = ReadComboText(cmbExe);
                MarkDirty();
            }
        };
        exeRow.Controls.Add(cmbExe);

        ModernButton btnBrowseExe = CreateModernButton("Browse", btnBrowseLeft, 1, 94, 25, false);
        btnBrowseExe.Tag = "BrowseExeBtn";
        btnBrowseExe.Click += (s, e) =>
        {
            BrowseForExecutable(cmbExe);
            int idx = GetCurrentRowIndex();
            if (idx >= 0 && idx < group.Executables.Count)
            {
                group.Executables[idx] = ReadComboText(cmbExe);
            }
        };
        exeRow.Controls.Add(btnBrowseExe);

        ModernButton btnRemoveExe = CreateModernButton("✕", btnRemExeLeft, 1, 32, 25, false, isDanger: true);
        btnRemoveExe.Tag = "RemoveExeBtn";
        btnRemoveExe.Enabled = group.Executables.Count > 1;
        btnRemoveExe.Click += (s, e) =>
        {
            if (_isGroupListAnimating) return;
            var orderedRows = card.Controls.Cast<Control>()
                .Where(c => c.Tag as string == "GroupExeRow")
                .OrderBy(c => c.Top)
                .ToList();

            int idx = orderedRows.IndexOf(exeRow);
            if (idx < 0) return;

            if (orderedRows.Count <= 1)
            {
                group.Executables[0] = "";
                cmbExe.Text = "";
                MarkDirty();
                return;
            }

            Control? rowAbove = idx > 0 ? orderedRows[idx - 1] : null;
            if (rowAbove != null) rowAbove.BringToFront();
            else exeRow.SendToBack();

            int startRowTop = exeRow.Top;
            int targetRowTop = rowAbove != null ? rowAbove.Top : 34;

            List<(Control ctrl, int startY, int targetY)> shiftingBelow = new();
            for (int i = idx + 1; i < orderedRows.Count; i++)
            {
                Control rBelow = orderedRows[i];
                shiftingBelow.Add((rBelow, rBelow.Top, rBelow.Top - 31));
            }

            int targetCardH = 100 + ((orderedRows.Count - 1) * 31);
            AnimateGroupCardHeightChange(
                card,
                targetCardH,
                exeRow,
                startRowTop,
                targetRowTop,
                shiftingBelow,
                onFinished: () =>
                {
                    if (idx < group.Executables.Count)
                        group.Executables.RemoveAt(idx);
                    card.Controls.Remove(exeRow);
                    exeRow.Dispose();
                    RebindGroupExeRows(card, group);
                    MarkDirty();
                });
        };
        exeRow.Controls.Add(btnRemoveExe);

        return exeRow;
    }

    private Win11CardPanel BuildSingleGroupCard(ProgramGroup group, List<string> exeSuggestions, int hostW)
    {
        int btnDelW = 104;
        int btnDelGroupLeft = hostW - btnDelW - 8;
        int nameBoxLeft = 106;
        int nameBoxW = Math.Max(130, btnDelGroupLeft - nameBoxLeft - 8);

        int cardH = 100 + (group.Executables.Count * 31);

        Win11CardPanel card = new Win11CardPanel
        {
            Left = 0,
            Top = 0,
            Width = hostW,
            Height = cardH,
            FillColor = Color.FromArgb(48, 48, 48),
            BorderColor = Color.FromArgb(66, 66, 66),
            CornerRadius = 6,
            Padding = new Padding(0),
            Tag = group
        };

        BufferedPanel iconBox = new BufferedPanel
        {
            Left = 10,
            Top = 8,
            Width = 46,
            Height = 46,
            Cursor = Cursors.Hand,
            BackColor = Color.FromArgb(48, 48, 48)
        };
        _toolTip.SetToolTip(iconBox, "Click to choose a group icon (Default presets or Custom icon)");
        iconBox.Paint += (s, e) =>
        {
            try
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                Rectangle r = new Rectangle(0, 0, iconBox.Width - 1, iconBox.Height - 1);
                using var path = Win11CardPanel.CreateRoundRect(r, 8);
                using var bg = new SolidBrush(Color.FromArgb(36, 36, 40));
                using var pen = new Pen(Color.FromArgb(82, 82, 88), 1f);
                e.Graphics.FillPath(bg, path);
                e.Graphics.DrawPath(pen, path);

                Bitmap? grpIco = AppIconCache.GetIconForGroup(group);
                if (grpIco != null)
                {
                    e.Graphics.DrawImage(grpIco, new Rectangle(5, 5, 36, 36));
                }
            }
            catch { }
        };
        iconBox.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            {
                ShowGroupIconPickerMenu(group, iconBox);
            }
        };
        card.Controls.Add(iconBox);

        Label lblGroupName = new Label
        {
            Text = "Group:",
            Left = 62,
            Top = 12,
            Width = 44,
            Height = 18,
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(190, 190, 190)
        };
        card.Controls.Add(lblGroupName);

        TextBox txtGroupName = new TextBox
        {
            Text = group.GroupName,
            Left = nameBoxLeft,
            Top = 8,
            Width = nameBoxW,
            Height = 25,
            BackColor = Color.FromArgb(38, 38, 38),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Tag = "GroupNameTxt"
        };
        txtGroupName.TextChanged += (s, e) =>
        {
            if (_isInitializing) return;
            group.GroupName = txtGroupName.Text.Trim();
            MarkDirty();
        };
        card.Controls.Add(txtGroupName);

        ModernButton btnRemoveGroup = CreateModernButton("Delete Group", btnDelGroupLeft, 8, btnDelW, 25, false, isDanger: true);
        btnRemoveGroup.Tag = "DeleteGroupBtn";
        btnRemoveGroup.Click += (s, e) =>
        {
            if (_isGroupListAnimating) return;
            AnimateRemoveGroupCard(group);
        };
        card.Controls.Add(btnRemoveGroup);

        Label lblAppsHeader = new Label
        {
            Text = "Included Applications:",
            Left = 62,
            Top = 37,
            Width = 220,
            Height = 17,
            Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(155, 155, 155)
        };
        card.Controls.Add(lblAppsHeader);

        for (int i = 0; i < group.Executables.Count; i++)
        {
            BufferedPanel exeRow = CreateGroupExeRowControl(card, group, i, exeSuggestions, hostW);
            card.Controls.Add(exeRow);
        }

        int innerY = 60 + (group.Executables.Count * 31);
        ModernButton btnAddExe = CreateModernButton("+ Add App", 10, innerY + 3, 110, 25, false);
        btnAddExe.Tag = "AddExeBtn";
        btnAddExe.Click += (s, e) =>
        {
            if (_isGroupListAnimating) return;

            var orderedRows = card.Controls.Cast<Control>()
                .Where(c => c.Tag as string == "GroupExeRow")
                .OrderBy(c => c.Top)
                .ToList();

            int newIdx = group.Executables.Count;
            group.Executables.Add("");

            List<string> currentSuggestions = GetExecutableSuggestions();
            BufferedPanel newRow = CreateGroupExeRowControl(card, group, newIdx, currentSuggestions, card.Width);

            Control? prevRow = orderedRows.LastOrDefault();
            int finalRowTop = 60 + (newIdx * 31);
            int startRowTop = prevRow != null ? prevRow.Top : 34;

            newRow.Top = startRowTop;
            card.Controls.Add(newRow);
            if (prevRow != null) prevRow.BringToFront();
            btnAddExe.BringToFront();

            RebindGroupExeRows(card, group);
            MarkDirty();

            int targetCardH = 100 + (group.Executables.Count * 31);
            AnimateGroupCardHeightChange(
                card,
                targetCardH,
                newRow,
                startRowTop,
                finalRowTop,
                new List<(Control ctrl, int startY, int targetY)>());
        };
        card.Controls.Add(btnAddExe);

        return card;
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

            using var dlg = new ModernColorPickerDialog(originalColor, _draftSettings, liveCol =>
            {
                string liveHex = $"#{liveCol.R:X2}{liveCol.G:X2}{liveCol.B:X2}";
                colorBox.SwatchColor = liveCol;
                colorBox.Invalidate();
                onColorChanged(liveHex);
                PushLivePreviewIfActive(forceTrigger: true);
            });

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                colorBox.SwatchColor = dlg.SelectedColor;
                colorBox.Invalidate();
                onColorChanged(dlg.SelectedHex);
                MarkDirty();
                PushLivePreviewIfActive(forceTrigger: true);
            }
            else
            {
                colorBox.SwatchColor = originalColor;
                colorBox.Invalidate();
                onColorChanged(originalHex);
                PushLivePreviewIfActive(forceTrigger: true);
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

    private bool ValidateAndNormalizePairList(List<CrossfadeCyclePair> list, string listDisplayName)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var pair = list[i];
            if (!string.IsNullOrWhiteSpace(pair.Source1) && !pair.Source1.StartsWith("Group: ", StringComparison.OrdinalIgnoreCase))
            {
                pair.Source1 = AppSettings.NormalizeExeName(pair.Source1);
                _draftSettings.RecordProgramHistory(pair.Source1);
            }
            if (!pair.IsSingleMode && !string.IsNullOrWhiteSpace(pair.Source2) && !pair.Source2.StartsWith("Group: ", StringComparison.OrdinalIgnoreCase))
            {
                pair.Source2 = AppSettings.NormalizeExeName(pair.Source2);
                _draftSettings.RecordProgramHistory(pair.Source2);
            }

            if (!pair.IsSingleMode && !string.IsNullOrWhiteSpace(pair.Source1) && !string.IsNullOrWhiteSpace(pair.Source2))
            {
                if (pair.Source1.Equals(pair.Source2, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"{listDisplayName} #{i + 1} has the same source ({pair.Source1}) on both sides.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                var pairOverlaps = _draftSettings.GetOverlappingExecutables(pair.Source1, pair.Source2);
                if (pairOverlaps.Count > 0)
                {
                    string dupList = string.Join("\n• ", pairOverlaps);
                    MessageBox.Show(
                        $"{listDisplayName} #{i + 1} ({pair.Source1} ⇄ {pair.Source2}) contains duplicate software on both sides:\n\n• {dupList}",
                        "Duplicate Software in Source Pair",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }
            }
        }
        return true;
    }

    private void SaveChanges()
    {
        if (_draftSettings.CrossfadeCycleList.Count == 0 && _draftSettings.StandalonePairs.Count == 0)
        {
            MessageBox.Show("You must have at least one source pair configured.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!ValidateAndNormalizePairList(_draftSettings.CrossfadeCycleList, "Carousel Pair")) return;
        if (!ValidateAndNormalizePairList(_draftSettings.StandalonePairs, "Standalone Pair")) return;

        var defaultActivePair = _draftSettings.CrossfadeCycleList.FirstOrDefault() ?? _draftSettings.StandalonePairs.First();
        _draftSettings.EnableCarouselAnimation = _chkEnableAnimations.Checked;
        _draftSettings.CarouselOpenSpeed = _sldCarouselOpenSpeed.Value;
        _draftSettings.CarouselAnimationSpeed = _sldCarouselSpeed.Value;
        _draftSettings.CarouselBackgroundEnabled = _chkCarouselBg.Checked;

        _draftSettings.CycleNextKey = _kbKeyCycleNext.Binding;
        _draftSettings.QuickSwapKey = _kbKeyCycleNext.Binding;
        _draftSettings.CyclePrevKey = _kbKeyCyclePrev.Binding;
        _draftSettings.RunOnStartup = _chkStartup.Checked;
        _draftSettings.GlobalMuteMode = _cmbGlobalMuteMode.SelectedItem?.ToString() ?? "Cycle R → L";
        _draftSettings.CrossfadeKeyUp = _kbKey1.Binding;
        _draftSettings.CrossfadeKeyDown = _kbKey2.Binding;
        _draftSettings.CrossfadeKeyMute = _kbKey3.Binding;
        _draftSettings.KeepOverlayOnWhileSettingsOpen = _chkKeepOverlayAlwaysOn.Checked;
        _draftSettings.OverlayPosition = _cmbPosition.SelectedItem?.ToString() ?? "Bottom Center";
        _draftSettings.PopupDurationMs = Math.Clamp(ParseInt(_txtPopupDuration.Text, 2500), 500, 10000);
        _draftSettings.OverlayOpacity = _sldOverlayOpacity.Value;
        _draftSettings.StepSize = _sldStepSize.Value;
        _draftSettings.OverlayWidth = Math.Clamp(ParseInt(_txtWidth.Text, 360), 120, 2000);
        _draftSettings.OverlayHeight = Math.Clamp(ParseInt(_txtHeight.Text, 80), 50, 1000);
        _draftSettings.EnableInterpolation = _chkInterpolation.Checked;
        _draftSettings.InterpolationSpeed = _sldInterpSpeed.Value;

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

        int ParseInt(string text, int defaultVal) => int.TryParse(text, out int val) ? val : defaultVal;

        string savedMode = _cmbCarouselMode.SelectedItem?.ToString() ?? "Full";
        _draftSettings.CarouselMode = savedMode;
        _draftSettings.InvertCarouselScroll = _chkInvertCarouselScroll.Checked;
        _draftSettings.CardOrderDirection = _cmbCardOrder.SelectedItem?.ToString() ?? "Ascending";
        _draftSettings.NeighborPreviewVisibility = _cmbNeighborPreviewMode.SelectedItem?.ToString() ?? "While Switching";
        _draftSettings.CompactShowNeighborPreviews = _draftSettings.NeighborPreviewVisibility != "Off";

        var savedStyle = CarouselStyleStateMachine.ParseState(_cmbCarouselStyle.SelectedItem?.ToString(), savedMode);
        string savedStyleName = CarouselStyleStateMachine.ToDisplayName(savedStyle);
        _draftSettings.CarouselMotionStyle = savedStyleName;
        _draftSettings.CarouselVisibleNeighbors = (savedMode == "Full") ? 1.5f : 0.0f;

        if (savedMode == "Full") _draftSettings.LastFullCarouselStyle = savedStyleName;
        else if (savedMode == "Compact") _draftSettings.LastCompactCarouselStyle = savedStyleName;
        else if (savedMode == "Minimal") _draftSettings.LastMinimalCarouselStyle = savedStyleName;

        _draftSettings.EnableCarouselAnimation = _chkEnableAnimations.Checked;
        _draftSettings.CarouselBackgroundEnabled = _chkCarouselBg.Checked;

        _draftSettings.CycleNextKey = _kbKeyCycleNext.Binding;
        _draftSettings.QuickSwapKey = _kbKeyCycleNext.Binding;
        _draftSettings.CyclePrevKey = _kbKeyCyclePrev.Binding;
        _draftSettings.RunOnStartup = _chkStartup.Checked;
        _draftSettings.GlobalMuteMode = _cmbGlobalMuteMode.SelectedItem?.ToString() ?? "Cycle R → L";
        _draftSettings.CrossfadeKeyUp = _kbKey1.Binding;
        _draftSettings.CrossfadeKeyDown = _kbKey2.Binding;
        _draftSettings.CrossfadeKeyMute = _kbKey3.Binding;
        _draftSettings.KeepOverlayOnWhileSettingsOpen = _chkKeepOverlayAlwaysOn.Checked;
        _draftSettings.OverlayPosition = _cmbPosition.SelectedItem?.ToString() ?? "Bottom Center";
        _draftSettings.PopupDurationMs = Math.Clamp(ParseInt(_txtPopupDuration.Text, 2500), 500, 10000);
        _draftSettings.OverlayWidth = Math.Clamp(ParseInt(_txtWidth.Text, 360), 120, 2000);
        _draftSettings.OverlayHeight = Math.Clamp(ParseInt(_txtHeight.Text, 80), 50, 1000);
        _draftSettings.EnableInterpolation = _chkInterpolation.Checked;
        _draftSettings.EnableEqualizationBuffer = _chkBuffer.Checked;
        _draftSettings.OverlayIdentityMode = _cmbIdentityMode.SelectedItem?.ToString() ?? "Both";
        _draftSettings.OverlayLevelMode = _cmbLevelMode.SelectedItem?.ToString() ?? "Both";
        _draftSettings.UseSourceBackgroundColors = _chkUseSourceBgColors.Checked;
        _draftSettings.FadeBgColorWithVolume = _chkFadeBgWithVolume.Checked;

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