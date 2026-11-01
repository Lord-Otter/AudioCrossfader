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

    private const int CarouselPairCardHeight = 100;
    private const int StandalonePairCardHeight = 176;

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
    private ModernButton _navBtnPersonalization = null!;
    private DarkScrollPanel _pageGeneral = null!;
    private DarkScrollPanel _pageGroups = null!;
    private DarkScrollPanel _pagePersonalization = null!;

    // Merged Carousel Pairs + Global Hotkeys Box
    private Win11CardPanel _cycleListCard = null!;
    private Label _lblCycleCardTitle = null!;
    private ModernButton _btnAddCycleEntry = null!;
    private Panel _cycleSlotsHost = null!;
    private Panel _globalKeysHost = null!;

    // Standalone (Non-Carousel) Pairs Box
    private Win11CardPanel _standaloneListCard = null!;
    private Label _lblStandaloneCardTitle = null!;
    private ModernButton _btnAddStandaloneEntry = null!;
    private Panel _standaloneSlotsHost = null!;

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
    private CheckBox _chkShowCarousel = null!;
    private CheckBox _chkEnableAnimations = null!;
    private CheckBox _chkCarouselBg = null!;
    private ModernComboBox _cmbPosition = null!;
    private TextBox _txtPopupDuration = null!;
    private TextBox _txtOverlayOpacity = null!;
    private TextBox _txtStepSize = null!;
    private TextBox _txtWidth = null!;
    private TextBox _txtHeight = null!;
    private CheckBox _chkStartup = null!;
    private CheckBox _chkInterpolation = null!;
    private TextBox _txtInterpSpeed = null!;
    private CheckBox _chkBuffer = null!;

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
        _pagePersonalization = CreatePersonalizationPage();

        contentHost.Controls.Add(_pageGeneral);
        contentHost.Controls.Add(_pageGroups);
        contentHost.Controls.Add(_pagePersonalization);
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

    /*private void HandleWindowResize()
    {
        if (_pillTrack == null) return;

        int clientW = this.ClientSize.Width;
        int viewportW = Math.Max(496, clientW - 14);
        int cardW = Math.Max(468, viewportW - 28);

        int effectiveBarW = Math.Max(468, clientW - 36);
        _pillTrack.Width = effectiveBarW;

        int tabGap = 4;
        int availW = effectiveBarW - 8 - (tabGap * 2);
        int tab1W = availW / 3;
        int tab2W = availW / 3;
        int tab3W = availW - tab1W - tab2W;

        _navBtnGeneral.Left = 4; _navBtnGeneral.Width = tab1W;
        _navBtnGroups.Left = _navBtnGeneral.Right + tabGap; _navBtnGroups.Width = tab2W;
        _navBtnPersonalization.Left = _navBtnGroups.Right + tabGap; _navBtnPersonalization.Width = tab3W;

        if (_btnSave != null && _btnRevert != null && _lblStatus != null)
        {
            _lblStatus.Width = effectiveBarW;
            _btnSave.Left = Math.Max(_btnRevert.Right + 12, clientW - 18 - _btnSave.Width);
        }

        if (_cycleListCard != null)
        {
            _cycleListCard.Width = cardW;
            _btnAddCycleEntry.Left = cardW - 14 - _btnAddCycleEntry.Width;
            _cycleSlotsHost.Width = cardW - 24;

            if (_globalKeysHost != null)
            {
                _globalKeysHost.Width = cardW - 24;
                int kbWidth = Math.Max(220, _globalKeysHost.Width - 142);
                if (_kbKey1 != null) _kbKey1.Width = kbWidth;
                if (_kbKey2 != null) _kbKey2.Width = kbWidth;
                if (_kbKey3 != null) _kbKey3.Width = kbWidth;
                if (_cmbGlobalMuteMode != null) _cmbGlobalMuteMode.Width = kbWidth;
                if (_kbKeyCycleNext != null) _kbKeyCycleNext.Width = kbWidth;
                if (_kbKeyCyclePrev != null) _kbKeyCyclePrev.Width = kbWidth;
            }
        }

        if (_standaloneListCard != null)
        {
            _standaloneListCard.Width = cardW;
            _btnAddStandaloneEntry.Left = cardW - 14 - _btnAddStandaloneEntry.Width;
            _standaloneSlotsHost.Width = cardW - 24;
        }

        if (!_isInitializing)
        {
            ResizeExistingPairCards(_cycleSlotsHost);
            ResizeExistingPairCards(_standaloneSlotsHost);
            ResizeExistingGroupCards(cardW);
            StretchPersonalizationControls(cardW);

            ReflowGeneralPage();
            ReflowPersonalizationPage();
            _pageGroups?.RecalculateScroll();
        }
    }*/

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
            // Both top navigation bar and main cards use clientW - 36 (18px left, 18px right)
            // so the right edge of the cards aligns with the top tabs box.
            int effectiveBarW = Math.Max(468, clientW - 36);
            int cardW = effectiveBarW;

            _pillTrack.Width = effectiveBarW;

            int tabGap = 4;
            int availW = effectiveBarW - 8 - (tabGap * 2);
            int tab1W = availW / 3;
            int tab2W = availW / 3;
            int tab3W = availW - tab1W - tab2W;

            _navBtnGeneral.Left = 4;
            _navBtnGeneral.Width = tab1W;

            _navBtnGroups.Left = _navBtnGeneral.Right + tabGap;
            _navBtnGroups.Width = tab2W;

            _navBtnPersonalization.Left = _navBtnGroups.Right + tabGap;
            _navBtnPersonalization.Width = tab3W;

            ModernButton activeNavBtn = _activeTabIndex switch
            {
                1 => _navBtnGroups,
                2 => _navBtnPersonalization,
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

            // During interactive drag, only resize the currently visible tab to keep the catch-up snappy
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

            if (!_isResizing || _activeTabIndex == 2)
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
        _unmutedColorBox.Width = rightStretchW;
        _mutedColorBox.Width = rightStretchW;
        _cmbCarouselStyle.Width = rightStretchW;
        _cmbPosition.Width = rightStretchW;

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
        if (_pageGroups == null) return;

        int nameBoxW = Math.Max(200, cardW - 196);
        int exeComboW = Math.Max(240, cardW - 154);

        foreach (Control c in _pageGroups.InnerControls)
        {
            if (c is ModernButton btn && btn.Text == "+ New Group")
            {
                btn.Left = 18 + cardW - 126;
            }
            else if (c is Win11CardPanel card)
            {
                card.Width = cardW;
                foreach (Control child in card.Controls)
                {
                    if (child is TextBox txt)
                    {
                        txt.Width = nameBoxW;
                    }
                    else if (child is ModernComboBox cmb)
                    {
                        cmb.Width = exeComboW;
                    }
                    else if (child is ModernButton childBtn)
                    {
                        if (childBtn.Text == "Delete Group")
                            childBtn.Left = cardW - 126;
                        else if (childBtn.Text == "Browse...")
                            childBtn.Left = 12 + exeComboW + 6;
                        else if (childBtn.Text == "✕")
                            childBtn.Left = 12 + exeComboW + 94;
                    }
                }
            }
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
            HighlightPillRect = new RectangleF(4, 4, 152, 30)
        };

        _navBtnGeneral = CreateNavTabButton("Sources", 4, 4, 152, () => SwitchTab(0));
        _navBtnGroups = CreateNavTabButton("Groups", 160, 4, 152, () => SwitchTab(1));
        _navBtnPersonalization = CreateNavTabButton("Appearance && Behavior", 316, 4, 148, () => SwitchTab(2));

        _pillTrack.Controls.Add(_navBtnGeneral);
        _pillTrack.Controls.Add(_navBtnGroups);
        _pillTrack.Controls.Add(_navBtnPersonalization);
        header.Controls.Add(_pillTrack);

        return header;
    }

    private void InitializeTabStateMachine()
    {
        if (_tabStateMachine != null) return;

        _tabStateMachine = new StateMachine<SettingsTabState>(SettingsTabState.Sources);

        _tabStateMachine.ConfigureState(
            SettingsTabState.Sources,
            onEnter: () =>
            {
                _activeTabIndex = 0;
                _pageGeneral.Visible = true;
                StyleNavButton(_navBtnGeneral, true);
                AnimateNavPillTo(_navBtnGeneral);
                ResizeExistingPairCards(_cycleSlotsHost);
                ResizeExistingPairCards(_standaloneSlotsHost);
                ReflowGeneralPage();
            },
            onExit: () =>
            {
                _pageGeneral.Visible = false;
                StyleNavButton(_navBtnGeneral, false);
            });

        _tabStateMachine.ConfigureState(
            SettingsTabState.Groups,
            onEnter: () =>
            {
                _activeTabIndex = 1;
                _pageGroups.Visible = true;
                StyleNavButton(_navBtnGroups, true);
                AnimateNavPillTo(_navBtnGroups);
                int cardW = Math.Max(468, ClientSize.Width - 36);
                ResizeExistingGroupCards(cardW);
                _pageGroups.RecalculateScroll();
            },
            onExit: () =>
            {
                _pageGroups.Visible = false;
                StyleNavButton(_navBtnGroups, false);
            });

        _tabStateMachine.ConfigureState(
            SettingsTabState.AppearanceAndBehavior,
            onEnter: () =>
            {
                _activeTabIndex = 2;
                _pagePersonalization.Visible = true;
                StyleNavButton(_navBtnPersonalization, true);
                AnimateNavPillTo(_navBtnPersonalization);
                int cardW = Math.Max(468, ClientSize.Width - 36);
                StretchPersonalizationControls(cardW);
                ReflowPersonalizationPage();
            },
            onExit: () =>
            {
                _pagePersonalization.Visible = false;
                StyleNavButton(_navBtnPersonalization, false);
            });
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
        InitializeTabStateMachine();
        CancelRecording();
        this.ActiveControl = null;

        SettingsTabState targetState = index switch
        {
            1 => SettingsTabState.Groups,
            2 => SettingsTabState.AppearanceAndBehavior,
            _ => SettingsTabState.Sources
        };

        _tabStateMachine.TransitionTo(targetState, force: _isInitializing);
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

        const double durationMs = 220.0;
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
                // Cubic Ease-Out damper: fast start, smooth deceleration, exact snap at t == 1.0
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
        _navBtnPersonalization?.Invalidate();
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

        _btnSave = CreateModernButton("Save & Apply", 302, 29, 184, 33, true);
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

    private RoundedColorSwatch CreateCompactColorSwatch(int left, int top, int width, int height, string initialHex, Action<string> onColorPicked)
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
            using ColorDialog dlg = new ColorDialog { FullOpen = true };
            try { dlg.Color = swatch.SwatchColor; } catch { }
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                swatch.SwatchColor = dlg.Color;
                swatch.Invalidate();
                string hex = ColorTranslator.ToHtml(dlg.Color);
                onColorPicked(hex);
                MarkDirty();
                PushLivePreviewIfActive();
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

        // 1. Merged Carousel Source Pairs & Global Hotkeys Box
        _cycleListCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468,
            Height = 44
        };

        _lblCycleCardTitle = new Label
        {
            Text = "Carousel Pairs & Global Hotkeys",
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

        _cycleSlotsHost = new Panel
        {
            Left = 12,
            Top = 42,
            Width = 444,
            Height = 0,
            BackColor = Color.Transparent
        };
        _cycleListCard.Controls.Add(_cycleSlotsHost);

        // Global Hotkeys section embedded inside the Carousel Pairs card
        _globalKeysHost = new Panel
        {
            Left = 12,
            Top = 42,
            Width = 444,
            Height = 212,
            BackColor = Color.Transparent
        };
        _globalKeysHost.Paint += (s, e) =>
        {
            using Pen sepPen = new Pen(Color.FromArgb(62, 62, 62), 1);
            e.Graphics.DrawLine(sepPen, 2, 2, _globalKeysHost.Width - 4, 2);
        };

        int ky = 10;
        AddSectionHeader(_globalKeysHost, "Global Hotkeys (Click to bind, Esc to clear)", 2, ref ky);

        AddAlignedLabel(_globalKeysHost, "Fade Left / Down:", 2, ky);
        _kbKey2 = AddKeybindBoxControl(_globalKeysHost, 136, 304, ref ky, val => _draftSettings.CrossfadeKeyDown = val);

        AddAlignedLabel(_globalKeysHost, "Fade Right / Up:", 2, ky);
        _kbKey1 = AddKeybindBoxControl(_globalKeysHost, 136, 304, ref ky, val => _draftSettings.CrossfadeKeyUp = val);

        AddAlignedLabel(_globalKeysHost, "Mute Toggle:", 2, ky);
        _kbKey3 = AddKeybindBoxControl(_globalKeysHost, 136, 304, ref ky, val => _draftSettings.CrossfadeKeyMute = val);

        AddAlignedLabel(_globalKeysHost, "Mute Behavior:", 2, ky);
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
        _globalKeysHost.Controls.Add(_cmbGlobalMuteMode);
        ky += 32;

        AddAlignedLabel(_globalKeysHost, "Next Pair:", 2, ky);
        _kbKeyCycleNext = AddKeybindBoxControl(_globalKeysHost, 136, 304, ref ky, val =>
        {
            _draftSettings.CycleNextKey = val;
            _draftSettings.QuickSwapKey = val;
        });

        AddAlignedLabel(_globalKeysHost, "Previous Pair:", 2, ky);
        _kbKeyCyclePrev = AddKeybindBoxControl(_globalKeysHost, 136, 304, ref ky, val => _draftSettings.CyclePrevKey = val);

        _globalKeysHost.Height = ky + 2;
        _cycleListCard.Controls.Add(_globalKeysHost);

        page.InnerControls.Add(_cycleListCard);
        y = _cycleListCard.Bottom + 10;

        // 2. Standalone Source Pairs Box (Excluded from Carousel, always has dedicated keybinds)
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

        _standaloneSlotsHost = new Panel
        {
            Left = 12,
            Top = 42,
            Width = 444,
            Height = 0,
            BackColor = Color.Transparent
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

        int comboW = Math.Max(140, hostW - 234);
        int btnActionLeft = 56 + comboW + 6; // Move & Browse buttons (Width = 94)
        int btnArrowLeft = btnActionLeft + 94 + 4; // Up & Down arrows (Width = 30)
        int btnDelLeft = btnArrowLeft + 30 + 4; // 2-Row 'X' button (Width = 32)

        for (int i = 0; i < targetList.Count; i++)
        {
            int index = i;
            var pair = targetList[index];
            bool isSingle = pair.IsSingleMode;

            int cardH = isCarouselList
                ? (isSingle ? 68 : CarouselPairCardHeight)
                : (isSingle ? 144 : StandalonePairCardHeight);

            Win11CardPanel pairCard = new Win11CardPanel
            {
                Left = 0,
                Top = y,
                Width = hostW,
                Height = cardH,
                FillColor = Color.FromArgb(48, 48, 48),
                BorderColor = Color.FromArgb(66, 66, 66),
                CornerRadius = 6,
                Padding = new Padding(0)
            };

            // --- TOP BLOCK PANEL (Holds Row 1 [Mode] + Row 2 [Source 1] + 2-row tall X button) ---
            Panel topBlockPanel = new Panel
            {
                Left = 2,
                Top = 4,
                Width = hostW - 4,
                Height = 62,
                BackColor = Color.FromArgb(48, 48, 48),
                Tag = "TopBlockPanel"
            };

            // Row 1: Mode Dropdown + Invert Checkbox + Move List Button + Up Arrow
            Label lblMode = new Label
            {
                Text = "Mode:",
                Left = 6,
                Top = 6,
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
                Top = 2,
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
                Top = 4,
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

            ModernButton btnMoveList = CreateModernButton(isCarouselList ? "To Standalone" : "To Carousel", btnActionLeft, 2, 94, 25, false);
            btnMoveList.Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);
            btnMoveList.Tag = "MoveListBtn";
            btnMoveList.Click += (s, e) =>
            {
                if (_isPairListAnimating) return;
                AnimateRemovePairCard(host, targetList, index, onComplete: () =>
                {
                    pair.UseCustomKeybinds = isCarouselList;
                    Panel? destHost = isCarouselList ? _standaloneSlotsHost : _cycleSlotsHost;
                    AnimateAddPairCard(destHost, oppositeList, pair, isCarouselList: !isCarouselList);
                });
            };
            topBlockPanel.Controls.Add(btnMoveList);

            ModernButton btnUp = CreateModernButton("▲", btnArrowLeft, 2, 30, 25, false);
            btnUp.Tag = "UpBtn";
            btnUp.Enabled = (index > 0);
            btnUp.Click += (s, e) =>
            {
                if (_isPairListAnimating || index <= 0) return;
                AnimateSwapPairCards(host, targetList, index, index - 1);
            };
            topBlockPanel.Controls.Add(btnUp);

            // Row 2 (y = 34 inside topBlockPanel): Source 1 + Browse + Down Arrow
            Label lblS1 = new Label
            {
                Text = isSingle ? "S:" : "L:",
                Left = 6,
                Top = 38,
                Width = 18,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(190, 190, 190)
            };
            topBlockPanel.Controls.Add(lblS1);

            RoundedColorSwatch swatch1 = CreateCompactColorSwatch(26, 35, 24, 24, pair.Color1Hex, hex => pair.Color1Hex = hex);
            topBlockPanel.Controls.Add(swatch1);

            ModernComboBox cmb1 = new ModernComboBox
            {
                Left = 56,
                Top = 35,
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

            ModernButton btnBrowse1 = CreateModernButton("Browse", btnActionLeft, 34, 94, 25, false);
            btnBrowse1.Tag = "Browse1Btn";
            btnBrowse1.Click += (s, e) =>
            {
                BrowseForExecutable(cmb1);
                pair.Source1 = ReadComboText(cmb1);
                PushLivePreviewIfActive();
            };
            topBlockPanel.Controls.Add(btnBrowse1);

            ModernButton btnDown = CreateModernButton("▼", btnArrowLeft, 34, 30, 25, false);
            btnDown.Tag = "DownBtn";
            btnDown.Enabled = (index < targetList.Count - 1);
            btnDown.Click += (s, e) =>
            {
                if (_isPairListAnimating || index >= targetList.Count - 1) return;
                AnimateSwapPairCards(host, targetList, index, index + 1);
            };
            topBlockPanel.Controls.Add(btnDown);

            // Tall 2-Row Delete ('X') Button spanning Row 1 and Row 2
            int totalPairsCount = _draftSettings.CrossfadeCycleList.Count + _draftSettings.StandalonePairs.Count;
            ModernButton btnDel = CreateModernButton("✕", btnDelLeft, 2, 32, 57, false, isDanger: true);
            btnDel.Tag = "DeleteBtn";
            btnDel.Enabled = (totalPairsCount > 1);
            btnDel.Click += (s, e) =>
            {
                if (_isPairListAnimating) return;
                if (_draftSettings.CrossfadeCycleList.Count + _draftSettings.StandalonePairs.Count <= 1)
                {
                    MessageBox.Show("You must keep at least one source pair.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                AnimateRemovePairCard(host, targetList, index);
            };
            topBlockPanel.Controls.Add(btnDel);

            // --- ROW 3 PANEL (Source 2 + Browse ONLY - slides behind topBlockPanel when in Single Mode) ---
            Panel row2Panel = new Panel
            {
                Left = 2,
                Top = isSingle ? 34 : 66,
                Width = hostW - 4,
                Height = 30,
                Visible = !isSingle,
                BackColor = Color.FromArgb(48, 48, 48),
                Tag = "Row2Panel"
            };

            Label lblS2 = new Label
            {
                Text = "R:",
                Left = 6,
                Top = 5,
                Width = 18,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(190, 190, 190)
            };
            row2Panel.Controls.Add(lblS2);

            RoundedColorSwatch swatch2 = CreateCompactColorSwatch(26, 2, 24, 24, pair.Color2Hex, hex => pair.Color2Hex = hex);
            row2Panel.Controls.Add(swatch2);

            ModernComboBox cmb2 = new ModernComboBox
            {
                Left = 56,
                Top = 2,
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

            ModernButton btnBrowse2 = CreateModernButton("Browse", btnActionLeft, 1, 94, 25, false);
            btnBrowse2.Tag = "Browse2Btn";
            btnBrowse2.Click += (s, e) =>
            {
                BrowseForExecutable(cmb2);
                pair.Source2 = ReadComboText(cmb2);
                PushLivePreviewIfActive();
            };
            row2Panel.Controls.Add(btnBrowse2);

            // --- STANDALONE KEYBINDS PANEL (Only used for Standalone Pairs) ---
            Panel? bottomPanel = null;
            if (!isCarouselList)
            {
                pair.UseCustomKeybinds = true;

                bottomPanel = new Panel
                {
                    Left = 2,
                    Top = isSingle ? 66 : 98,
                    Width = hostW - 4,
                    Height = 74,
                    BackColor = Color.FromArgb(48, 48, 48),
                    Tag = "BottomPanel"
                };

                Label lblMuteMode = new Label
                {
                    Text = "Mute:",
                    Left = 6,
                    Top = 4,
                    Width = 46,
                    Height = 18,
                    Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(190, 190, 190)
                };
                bottomPanel.Controls.Add(lblMuteMode);

                ModernComboBox cmbPairMute = new ModernComboBox
                {
                    Left = 56,
                    Top = 0,
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

                // Volume Down before Volume Up
                AddSideBySideKeybindColumn(bottomPanel, "Fade Left / Down", rowLeft, colW, 28, pair.KeyDown, val => pair.KeyDown = val, 0);
                AddSideBySideKeybindColumn(bottomPanel, "Fade Right / Up", rowLeft + colW + colGap, colW, 28, pair.KeyUp, val => pair.KeyUp = val, 1);
                AddSideBySideKeybindColumn(bottomPanel, "Mute Toggle", rowLeft + (colW + colGap) * 2, totalRowW - (colW * 2) - (colGap * 2), 28, pair.KeyMute, val => pair.KeyMute = val, 2);

                pairCard.Controls.Add(bottomPanel);
            }
            else
            {
                pair.UseCustomKeybinds = false;
            }

            // Per-Card Finite State Machine for ControlModeState (Volume / Balance / Crossfade)
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
            topBlockPanel.BringToFront(); // Keep topBlockPanel in front so row2Panel slides behind it

            host.Controls.Add(pairCard);
            y += cardH + 6;
        }

        host.Height = y;
        host.ResumeLayout();
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
        topBlockPanel.BringToFront(); // Ensures row2Panel slides neatly BEHIND topBlockPanel

        int startCardH = pairCard.Height;
        int targetCardH = isCarouselList
            ? (toSingleMode ? 68 : CarouselPairCardHeight)
            : (toSingleMode ? 144 : StandalonePairCardHeight);

        int startRow2Top = row2Panel.Top;
        int targetRow2Top = toSingleMode ? 34 : 66;

        int startBottomTop = bottomSection?.Top ?? 0;
        int targetBottomTop = toSingleMode ? 66 : 98;

        row2Panel.Visible = true;

        const double durationMs = 220.0;
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
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0)); // Cubic Ease-Out damper

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
                foreach (Control sibling in pairCard.Parent.Controls)
                {
                    sibling.Top = curY;
                    curY += sibling.Height + 6;
                }
                pairCard.Parent.Height = curY;
            }
            ReflowGeneralPage();
        };
        animTimer.Start();
    }

    private void AnimateAddPairCard(
        Panel? host,
        List<CrossfadeCyclePair> targetList,
        CrossfadeCyclePair newPair,
        bool isCarouselList)
    {
        if (host == null) return;

        targetList.Add(newPair);
        PopulateCycleListUI();
        MarkDirty();
        PushLivePreviewIfActive();

        if (host.Controls.Count == 0)
        {
            ReflowGeneralPage();
            return;
        }

        int lastIdx = host.Controls.Count - 1;
        Control newCard = host.Controls[lastIdx];
        Control? prevCard = lastIdx > 0 ? host.Controls[lastIdx - 1] : null;

        int finalTop = newCard.Top;
        int finalCardH = newCard.Height;
        int startTop = prevCard != null ? prevCard.Top : -finalCardH;
        int startHostH = Math.Max(0, finalTop);
        int finalHostH = finalTop + finalCardH + 6;

        // Place the new card behind the card above it so it slides out from underneath
        if (prevCard != null)
        {
            prevCard.BringToFront();
        }
        newCard.Top = startTop;
        host.Height = startHostH;
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
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0)); // Cubic Ease-Out damper

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
        if (host == null || removeIndex < 0 || removeIndex >= host.Controls.Count || removeIndex >= targetList.Count)
            return;

        Control removingCard = host.Controls[removeIndex];
        Control? cardAbove = removeIndex > 0 ? host.Controls[removeIndex - 1] : null;

        // Ensure the card above stays visually on top while the removed card slides up behind it
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
        for (int i = removeIndex + 1; i < host.Controls.Count; i++)
        {
            Control c = host.Controls[i];
            trailingCards.Add((c, c.Top, c.Top - shiftDelta));
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
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0)); // Cubic Ease-Out damper

            if (t >= 1.0)
            {
                timer.Stop();
                timer.Dispose();

                targetList.RemoveAt(removeIndex);
                _isPairListAnimating = false;
                PopulateCycleListUI();
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
            indexA >= host.Controls.Count || indexB >= host.Controls.Count ||
            indexA == indexB)
        {
            return;
        }

        int upperIdx = Math.Min(indexA, indexB);
        int lowerIdx = Math.Max(indexA, indexB);

        Control upperCard = host.Controls[upperIdx];
        Control lowerCard = host.Controls[lowerIdx];
        Control initiatedCard = host.Controls[indexA];
        initiatedCard.BringToFront();

        int upperStartTop = upperCard.Top;
        int lowerStartTop = lowerCard.Top;

        // Calculate exact final Y positions (accounting for cards that may have different heights)
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
            float ease = (float)(1.0 - Math.Pow(1.0 - t, 3.0)); // Cubic Ease-Out damper

            if (t >= 1.0)
            {
                // Snap to exact final positions and commit the swap in the data model
                upperCard.Top = upperTargetTop;
                lowerCard.Top = lowerTargetTop;
                timer.Stop();
                timer.Dispose();

                (targetList[indexA], targetList[indexB]) = (targetList[indexB], targetList[indexA]);
                _isPairListAnimating = false;
                PopulateCycleListUI();
                ReflowGeneralPage();
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

    private void ReflowGeneralPage()
    {
        if (_cycleListCard == null || _standaloneListCard == null || _globalKeysHost == null)
            return;

        int cycleCount = _draftSettings.CrossfadeCycleList.Count;
        _cycleSlotsHost.Visible = (cycleCount > 0);
        _cycleListCard.Top = 4;

        int keysTop = (cycleCount == 0) ? 42 : (44 + _cycleSlotsHost.Height);
        _globalKeysHost.Top = keysTop;
        _cycleListCard.Height = _globalKeysHost.Bottom + 10;

        int standaloneCount = _draftSettings.StandalonePairs.Count;
        _standaloneSlotsHost.Visible = (standaloneCount > 0);
        _standaloneListCard.Top = _cycleListCard.Bottom + 10;
        _standaloneListCard.Height = (standaloneCount == 0) ? 44 : (46 + _standaloneSlotsHost.Height);

        _pageGeneral?.RecalculateScroll();
    }

    private void ReflowPersonalizationPage()
    {
        if (_previewCard == null || _overlayStyleCard == null || _carouselCard == null || _layoutCard == null || _behaviorCard == null)
            return;

        _previewCard.Top = 4;
        _previewCard.FitHeightToContent(12);

        _overlayStyleCard.Top = _previewCard.Bottom + 10;
        _overlayStyleCard.FitHeightToContent(12);

        _carouselCard.Top = _overlayStyleCard.Bottom + 10;
        _carouselCard.FitHeightToContent(12);

        _layoutCard.Top = _carouselCard.Bottom + 10;
        _layoutCard.FitHeightToContent(12);

        _behaviorCard.Top = _layoutCard.Bottom + 10;
        _behaviorCard.FitHeightToContent(12);

        _pagePersonalization?.RecalculateScroll();
    }

    private DarkScrollPanel CreateGroupsPage()
    {
        DarkScrollPanel page = new DarkScrollPanel { Dock = DockStyle.Fill };
        return page;
    }

    private void SyncDraftPersonalizationFields()
    {
        if (_isInitializing) return;
        int ParseInt(string text, int defaultVal) => int.TryParse(text, out int val) ? val : defaultVal;

        _draftSettings.OverlayPosition = _cmbPosition?.SelectedItem?.ToString() ?? _draftSettings.OverlayPosition;
        _draftSettings.PopupDurationMs = Math.Clamp(ParseInt(_txtPopupDuration?.Text ?? "2500", 2500), 500, 10000);
        _draftSettings.OverlayOpacity = Math.Clamp(ParseInt(_txtOverlayOpacity?.Text ?? "100", 100), 10, 100);
        _draftSettings.OverlayWidth = Math.Clamp(ParseInt(_txtWidth?.Text ?? "360", 360), 120, 2000);
        _draftSettings.OverlayHeight = Math.Clamp(ParseInt(_txtHeight?.Text ?? "80", 80), 50, 1000);
        _draftSettings.EnableInterpolation = _chkInterpolation?.Checked ?? true;
        _draftSettings.InterpolationSpeed = Math.Clamp(ParseInt(_txtInterpSpeed?.Text ?? "45", 45), 5, 100);
        _draftSettings.CarouselVisibleNeighbors = (_chkShowCarousel?.Checked ?? true) ? 1.5f : 0.0f;
        _draftSettings.CarouselMotionStyle = _cmbCarouselStyle?.SelectedItem?.ToString() ?? "3D Floating Pills";
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

    private DarkScrollPanel CreatePersonalizationPage()
    {
        DarkScrollPanel page = new DarkScrollPanel { Dock = DockStyle.Fill };
        int y = 4;

        // 1. Live Preview Card
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

        // 2. Overlay Style & Colors Card
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
            Text = "App Display:",
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
            Text = "Volume Display:",
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
            Text = "Opacity (10 - 100%):",
            Left = 14,
            Top = sy + 3,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _overlayStyleCard.Controls.Add(lblOpacity);

        _txtOverlayOpacity = new TextBox
        {
            Text = _draftSettings.OverlayOpacity.ToString(),
            Left = 185,
            Top = sy,
            Width = 90,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtOverlayOpacity.TextChanged += (s, e) =>
        {
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _overlayStyleCard.Controls.Add(_txtOverlayOpacity);
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

        // 3. Carousel Card
        _carouselCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int cy = 10;
        AddSectionHeader(_carouselCard, "Carousel", 14, ref cy);

        Label lblCarouselMotion = new Label
        {
            Text = "Carousel Style:",
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
            Enabled = _draftSettings.CarouselVisibleNeighbors > 0.05f,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbCarouselStyle.Items.AddRange(new object[]
        {
            "3D Floating Pills",
            "Fluid Moving Deck",
            "3D Coverflow Arc",
            "Stacked Rolodex"
        });
        _cmbCarouselStyle.SelectedItem = _draftSettings.CarouselMotionStyle;
        if (_cmbCarouselStyle.SelectedIndex == -1) _cmbCarouselStyle.SelectedIndex = 0;
        _cmbCarouselStyle.SelectedIndexChanged += (s, e) =>
        {
            _draftSettings.CarouselMotionStyle = _cmbCarouselStyle.SelectedItem?.ToString() ?? "3D Floating Pills";
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _carouselCard.Controls.Add(_cmbCarouselStyle);
        cy += 32;

        _chkShowCarousel = new CheckBox
        {
            Text = "Show carousel when cycling",
            Checked = _draftSettings.CarouselVisibleNeighbors > 0.05f,
            Left = 14,
            Top = cy,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkShowCarousel.CheckedChanged += (s, e) =>
        {
            _draftSettings.CarouselVisibleNeighbors = _chkShowCarousel.Checked ? 1.5f : 0.0f;
            if (_chkEnableAnimations != null) _chkEnableAnimations.Enabled = _chkShowCarousel.Checked;
            if (_chkCarouselBg != null) _chkCarouselBg.Enabled = _chkShowCarousel.Checked;
            if (_cmbCarouselStyle != null) _cmbCarouselStyle.Enabled = _chkShowCarousel.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _carouselCard.Controls.Add(_chkShowCarousel);
        cy += 26;

        _chkEnableAnimations = new CheckBox
        {
            Text = "Carousel animations",
            Checked = _draftSettings.EnableCarouselAnimation,
            Enabled = _draftSettings.CarouselVisibleNeighbors > 0.05f,
            Left = 14,
            Top = cy,
            Width = 165,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkEnableAnimations.CheckedChanged += (s, e) =>
        {
            _draftSettings.EnableCarouselAnimation = _chkEnableAnimations.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _carouselCard.Controls.Add(_chkEnableAnimations);

        _chkCarouselBg = new CheckBox
        {
            Text = "Carousel backdrop",
            Checked = _draftSettings.CarouselBackgroundEnabled,
            Enabled = _draftSettings.CarouselVisibleNeighbors > 0.05f,
            Left = 185,
            Top = cy,
            Width = 265,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkCarouselBg.CheckedChanged += (s, e) =>
        {
            _draftSettings.CarouselBackgroundEnabled = _chkCarouselBg.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _carouselCard.Controls.Add(_chkCarouselBg);

        _carouselCard.FitHeightToContent(12);
        page.InnerControls.Add(_carouselCard);
        y = _carouselCard.Bottom + 10;

        // 4. Position & Size Card
        _layoutCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int ay = 10;
        AddSectionHeader(_layoutCard, "Position & Size", 14, ref ay);

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

        // 5. Audio & System Behavior Card (Cleanly aligned 2-column grid)
        _behaviorCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int by = 10;
        AddSectionHeader(_behaviorCard, "Audio & System", 14, ref by);

        Label lblStep = new Label
        {
            Text = "Fade Step (%):",
            Left = 14,
            Top = by + 3,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _behaviorCard.Controls.Add(lblStep);

        _txtStepSize = new TextBox
        {
            Text = _draftSettings.StepSize.ToString(),
            Left = 185,
            Top = by,
            Width = 90,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtStepSize.TextChanged += (s, e) => MarkDirty();
        _behaviorCard.Controls.Add(_txtStepSize);
        by += 30;

        Label lblSpeed = new Label
        {
            Text = "Animation Speed (5 - 100):",
            Left = 14,
            Top = by + 3,
            Width = 165,
            Height = 20,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _behaviorCard.Controls.Add(lblSpeed);

        _txtInterpSpeed = new TextBox
        {
            Text = _draftSettings.InterpolationSpeed.ToString(),
            Left = 185,
            Top = by,
            Width = 90,
            Enabled = _draftSettings.EnableInterpolation,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtInterpSpeed.TextChanged += (s, e) => MarkDirty();
        _behaviorCard.Controls.Add(_txtInterpSpeed);
        by += 30;

        _chkInterpolation = new CheckBox
        {
            Text = "Smooth animation",
            Checked = _draftSettings.EnableInterpolation,
            Left = 14,
            Top = by,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkInterpolation.CheckedChanged += (s, e) =>
        {
            if (_txtInterpSpeed != null) _txtInterpSpeed.Enabled = _chkInterpolation.Checked;
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

        _txtStepSize.Text = settings.StepSize.ToString();
        _chkBuffer.Checked = settings.EnableEqualizationBuffer;
        _chkStartup.Checked = settings.RunOnStartup;

        _chkKeepOverlayAlwaysOn.Checked = settings.KeepOverlayOnWhileSettingsOpen;
        _cmbIdentityMode.SelectedItem = settings.OverlayIdentityMode;
        _cmbLevelMode.SelectedItem = settings.OverlayLevelMode;
        _cmbCarouselStyle.SelectedItem = settings.CarouselMotionStyle;
        if (_cmbCarouselStyle.SelectedIndex == -1) _cmbCarouselStyle.SelectedIndex = 0;

        _chkUseSourceBgColors.Checked = settings.UseSourceBackgroundColors;
        _chkFadeBgWithVolume.Checked = settings.FadeBgColorWithVolume;
        _chkFadeBgWithVolume.Enabled = settings.UseSourceBackgroundColors;
        _chkShowCarousel.Checked = settings.CarouselVisibleNeighbors > 0.05f;
        _cmbCarouselStyle.Enabled = _chkShowCarousel.Checked;
        _chkEnableAnimations.Checked = settings.EnableCarouselAnimation;
        _chkEnableAnimations.Enabled = _chkShowCarousel.Checked;
        _chkCarouselBg.Checked = settings.CarouselBackgroundEnabled;
        _chkCarouselBg.Enabled = _chkShowCarousel.Checked;

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
        _txtOverlayOpacity.Text = settings.OverlayOpacity.ToString();
        _txtWidth.Text = settings.OverlayWidth.ToString();
        _txtHeight.Text = settings.OverlayHeight.ToString();
        _chkInterpolation.Checked = settings.EnableInterpolation;
        _txtInterpSpeed.Text = settings.InterpolationSpeed.ToString();
        _txtInterpSpeed.Enabled = settings.EnableInterpolation;

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
        if (_pageGroups == null) return;
        _pageGroups.SuspendLayout();
        _pageGroups.InnerControls.Clear();

        int cardW = Math.Max(468, _pageGroups.ContentViewportWidth - 28);

        Label lblInfo = new Label
        {
            Text = "Bundle multiple apps to control them as a single source:",
            Left = 18,
            Top = 10,
            Width = 335,
            Height = 22,
            ForeColor = Color.FromArgb(190, 190, 190)
        };
        _pageGroups.InnerControls.Add(lblInfo);

        ModernButton btnAddGroup = CreateModernButton("+ New Group", 18 + cardW - 126, 6, 126, 28, true);
        btnAddGroup.Click += (s, e) =>
        {
            _draftSettings.Groups.Add(new ProgramGroup
            {
                GroupName = $"Group{_draftSettings.Groups.Count + 1}",
                Executables = new List<string> { "" }
            });
            RefreshGroupsTabUI();
            MarkDirty();
        };
        _pageGroups.InnerControls.Add(btnAddGroup);

        List<string> exeSuggestions = GetExecutableSuggestions();
        int cardY = 42;

        foreach (var group in _draftSettings.Groups)
        {
            int cardHeight = 82 + (group.Executables.Count * 30);
            Win11CardPanel card = new Win11CardPanel
            {
                Left = 18,
                Top = cardY,
                Width = cardW,
                Height = cardHeight
            };

            Label lblGroupName = new Label
            {
                Text = "Name:",
                Left = 12,
                Top = 14,
                Width = 44,
                ForeColor = Color.FromArgb(195, 195, 195)
            };
            card.Controls.Add(lblGroupName);

            int nameBoxW = Math.Max(260, cardW - 196);
            TextBox txtGroupName = new TextBox
            {
                Text = group.GroupName,
                Left = 58,
                Top = 11,
                Width = nameBoxW,
                BackColor = Color.FromArgb(52, 52, 52),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtGroupName.TextChanged += (s, e) =>
            {
                group.GroupName = txtGroupName.Text.Trim();
                MarkDirty();
            };
            card.Controls.Add(txtGroupName);

            ModernButton btnRemoveGroup = CreateModernButton("Delete Group", cardW - 128, 10, 114, 25, false, isDanger: true);
            btnRemoveGroup.Click += (s, e) =>
            {
                _draftSettings.Groups.Remove(group);
                RefreshGroupsTabUI();
                MarkDirty();
            };
            card.Controls.Add(btnRemoveGroup);

            int innerY = 44;
            int exeComboW = Math.Max(314, cardW - 154);

            for (int i = 0; i < group.Executables.Count; i++)
            {
                int index = i;
                ModernComboBox cmbExe = new ModernComboBox
                {
                    Left = 12,
                    Top = innerY,
                    Width = exeComboW,
                    DropDownStyle = ComboBoxStyle.DropDown,
                    BadgeResolver = ResolveDropdownBadge,
                    IconResolver = ResolveDropdownIcon
                };
                cmbExe.Items.AddRange(exeSuggestions.Cast<object>().ToArray());
                cmbExe.SelectedItem = group.Executables[index];
                if (cmbExe.SelectedIndex == -1) cmbExe.Text = group.Executables[index];
                cmbExe.DropDown += (s, e) =>
                {
                    _detectedAudio = AudioProcessDetector.Scan();
                    PopulateSingleComboOptions(cmbExe, includeGroups: false);
                };
                cmbExe.SelectedIndexChanged += (s, e) =>
                {
                    if (_isInitializing) return;
                    group.Executables[index] = ReadComboText(cmbExe);
                    MarkDirty();
                };
                cmbExe.TextChanged += (s, e) =>
                {
                    if (_isInitializing) return;
                    group.Executables[index] = ReadComboText(cmbExe);
                    MarkDirty();
                };
                card.Controls.Add(cmbExe);

                ModernButton btnBrowseExe = CreateModernButton("Browse...", 12 + exeComboW + 6, innerY - 1, 82, 25, false);
                btnBrowseExe.Click += (s, e) =>
                {
                    BrowseForExecutable(cmbExe);
                    group.Executables[index] = ReadComboText(cmbExe);
                };
                card.Controls.Add(btnBrowseExe);

                ModernButton btnRemoveExe = CreateModernButton("✕", 12 + exeComboW + 94, innerY - 1, 34, 25, false, isDanger: true);
                btnRemoveExe.Click += (s, e) =>
                {
                    group.Executables.RemoveAt(index);
                    RefreshGroupsTabUI();
                    MarkDirty();
                };
                card.Controls.Add(btnRemoveExe);

                innerY += 30;
            }

            ModernButton btnAddExe = CreateModernButton("+ Add App", 12, innerY + 2, 145, 25, false);
            btnAddExe.Click += (s, e) =>
            {
                group.Executables.Add("");
                RefreshGroupsTabUI();
                MarkDirty();
            };
            card.Controls.Add(btnAddExe);

            _pageGroups.InnerControls.Add(card);
            cardY += cardHeight + 10;
        }

        _pageGroups.ResumeLayout();
        _pageGroups.RecalculateScroll();
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
            using ColorDialog dlg = new ColorDialog { FullOpen = true };
            try { dlg.Color = colorBox.SwatchColor; } catch { }
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                colorBox.SwatchColor = dlg.Color;
                colorBox.Invalidate();
                string hex = ColorTranslator.ToHtml(dlg.Color);
                onColorChanged(hex);
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
        _draftSettings.Source1 = defaultActivePair.Source1;
        _draftSettings.Source1Color = defaultActivePair.Color1Hex;
        _draftSettings.Source2 = defaultActivePair.IsSingleMode ? "" : defaultActivePair.Source2;
        _draftSettings.Source2Color = defaultActivePair.Color2Hex;
        _draftSettings.ActiveControlMode = defaultActivePair.ControlMode;

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
        _draftSettings.OverlayOpacity = Math.Clamp(ParseInt(_txtOverlayOpacity.Text, 100), 10, 100);
        _draftSettings.StepSize = Math.Clamp(ParseInt(_txtStepSize.Text, 10), 1, 100);
        _draftSettings.OverlayWidth = Math.Clamp(ParseInt(_txtWidth.Text, 360), 120, 2000);
        _draftSettings.OverlayHeight = Math.Clamp(ParseInt(_txtHeight.Text, 80), 50, 1000);
        _draftSettings.EnableInterpolation = _chkInterpolation.Checked;
        _draftSettings.InterpolationSpeed = Math.Clamp(ParseInt(_txtInterpSpeed.Text, 45), 5, 100);
        _draftSettings.EnableEqualizationBuffer = _chkBuffer.Checked;
        _draftSettings.OverlayIdentityMode = _cmbIdentityMode.SelectedItem?.ToString() ?? "Both";
        _draftSettings.OverlayLevelMode = _cmbLevelMode.SelectedItem?.ToString() ?? "Both";
        _draftSettings.UseSourceBackgroundColors = _chkUseSourceBgColors.Checked;
        _draftSettings.FadeBgColorWithVolume = _chkFadeBgWithVolume.Checked;
        _draftSettings.CarouselVisibleNeighbors = _chkShowCarousel.Checked ? 1.5f : 0.0f;
        _draftSettings.CarouselMotionStyle = _cmbCarouselStyle.SelectedItem?.ToString() ?? "3D Floating Pills";
        _draftSettings.EnableCarouselAnimation = _chkEnableAnimations.Checked;
        _draftSettings.CarouselBackgroundEnabled = _chkCarouselBg.Checked;

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