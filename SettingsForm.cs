using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

// Windows 11 Dark Mode Settings Window
public class SettingsForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private Win11CardPanel _pillTrack = null!;
    private ModernButton _navBtnGeneral = null!;
    private ModernButton _navBtnGroups = null!;
    private ModernButton _navBtnPersonalization = null!;
    private DarkScrollPanel _pageGeneral = null!;
    private DarkScrollPanel _pageGroups = null!;
    private DarkScrollPanel _pagePersonalization = null!;

    private Win11CardPanel _keysCard = null!;
    private Win11CardPanel _cycleListCard = null!;
    private Label _lblCycleCardTitle = null!;
    private ModernButton _btnAddCycleEntry = null!;
    private Panel _cycleSlotsHost = null!;

    private Win11CardPanel _previewCard = null!;
    private Win11CardPanel _overlayStyleCard = null!;
    private Win11CardPanel _carouselCard = null!;
    private Win11CardPanel _layoutCard = null!;
    private Win11CardPanel _behaviorCard = null!;

    private ModernButton _btnRevert = null!;
    private ModernButton _btnSave = null!;

    private Label _lblKey1 = null!;
    private TextBox _txtKey1 = null!;
    private ModernButton _btnRec1 = null!;

    private Label _lblKey2 = null!;
    private TextBox _txtKey2 = null!;
    private ModernButton _btnRec2 = null!;

    private Label _lblKey3 = null!;
    private TextBox _txtKey3 = null!;
    private ModernButton _btnRec3 = null!;

    private Label _lblKeyCycleNext = null!;
    private TextBox _txtKeyCycleNext = null!;
    private ModernButton _btnRecCycleNext = null!;

    private Label _lblKeyCyclePrev = null!;
    private TextBox _txtKeyCyclePrev = null!;
    private ModernButton _btnRecCyclePrev = null!;

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
    private CheckBox _chkPauseMediaOnMute = null!;
    private CheckBox _chkInterpolation = null!;
    private TextBox _txtInterpSpeed = null!;
    private CheckBox _chkBuffer = null!;

    private RoundedColorSwatch _unmutedColorBox = null!;
    private RoundedColorSwatch _mutedColorBox = null!;

    private readonly TrayApplicationContext _context;

    private AppSettings _draftSettings;
    private AppSettings _savedSettingsSnapshot;
    private DetectedAudioPrograms _detectedAudio = new DetectedAudioPrograms();
    private Label _lblStatus = null!;

    private bool _isRecording = false;
    private ModernButton? _activeRecordButton = null;
    private TextBox? _activeTargetTextBox = null;
    private Action<int>? _activeKeyCommitCallback = null;
    private string _previousButtonText = "";
    private string _previousTextBoxText = "";
    private KeyCaptureFilter? _mouseFilter;
    private bool _isInitializing = true;
    private int _activeTabIndex = 0;

    private const int ENUM_CURRENT_SETTINGS = -1;

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

    public SettingsForm(AppSettings initialSettings, TrayApplicationContext context)
    {
        _context = context;
        _draftSettings = initialSettings.Clone();
        _savedSettingsSnapshot = initialSettings.Clone();
        _detectedAudio = AudioProcessDetector.Scan();

        this.Text = "Audio Crossfader Settings";
        this.Size = new Size(540, 780);
        this.MinimumSize = new Size(520, 560);
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

        _mouseFilter = new KeyCaptureFilter(CancelRecording);
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

        int clientW = this.ClientSize.Width;
        int trackW = Math.Max(460, clientW - 36);
        _pillTrack.Width = trackW;

        int tabGap = 4;
        int availW = trackW - 8 - (tabGap * 2);
        int tab1W = availW / 3;
        int tab2W = availW / 3;
        int tab3W = availW - tab1W - tab2W;

        _navBtnGeneral.Left = 4;
        _navBtnGeneral.Width = tab1W;

        _navBtnGroups.Left = _navBtnGeneral.Right + tabGap;
        _navBtnGroups.Width = tab2W;

        _navBtnPersonalization.Left = _navBtnGroups.Right + tabGap;
        _navBtnPersonalization.Width = tab3W;

        if (_btnSave != null && _btnRevert != null && _lblStatus != null)
        {
            _lblStatus.Width = trackW;
            _btnSave.Left = clientW - 18 - _btnSave.Width;
        }

        int cardW = Math.Max(460, (_pageGeneral?.ContentViewportWidth ?? (clientW - 12)) - 28);

        if (_cycleListCard != null)
        {
            _cycleListCard.Width = cardW;
            _btnAddCycleEntry.Left = cardW - 14 - _btnAddCycleEntry.Width;
            _cycleSlotsHost.Width = cardW - 24;
        }
        if (_keysCard != null) _keysCard.Width = cardW;

        if (_previewCard != null) _previewCard.Width = cardW;
        if (_overlayStyleCard != null) _overlayStyleCard.Width = cardW;
        if (_carouselCard != null) _carouselCard.Width = cardW;
        if (_layoutCard != null) _layoutCard.Width = cardW;
        if (_behaviorCard != null) _behaviorCard.Width = cardW;

        if (!_isInitializing)
        {
            PopulateCycleListUI();
            RefreshGroupsTabUI();
            ReflowGeneralPage();
            ReflowPersonalizationPage();
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
            Padding = new Padding(3)
        };

        _navBtnGeneral = CreateNavTabButton("Sources", 4, 4, 152, () => SwitchTab(0));
        _navBtnGroups = CreateNavTabButton("Groups", 160, 4, 152, () => SwitchTab(1));
        _navBtnPersonalization = CreateNavTabButton("Appearance & Behavior", 316, 4, 148, () => SwitchTab(2));

        _pillTrack.Controls.Add(_navBtnGeneral);
        _pillTrack.Controls.Add(_navBtnGroups);
        _pillTrack.Controls.Add(_navBtnPersonalization);
        header.Controls.Add(_pillTrack);

        return header;
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
        _activeTabIndex = index;
        _pageGeneral.Visible = (index == 0);
        _pageGroups.Visible = (index == 1);
        _pagePersonalization.Visible = (index == 2);

        StyleNavButton(_navBtnGeneral, index == 0);
        StyleNavButton(_navBtnGroups, index == 1);
        StyleNavButton(_navBtnPersonalization, index == 2);

        if (index == 0) _pageGeneral.RecalculateScroll();
        else if (index == 1) _pageGroups.RecalculateScroll();
        else if (index == 2) _pagePersonalization.RecalculateScroll();
    }

    private void StyleNavButton(ModernButton btn, bool active)
    {
        btn.BackColor = active ? Color.FromArgb(0, 120, 212) : Color.FromArgb(37, 37, 37);
        btn.HoverBackColor = active ? Color.FromArgb(20, 138, 230) : Color.FromArgb(50, 50, 50);
        btn.PressedBackColor = active ? Color.FromArgb(0, 100, 180) : Color.FromArgb(44, 44, 44);
        btn.ForeColor = active ? Color.White : Color.FromArgb(185, 185, 185);
        btn.Invalidate();
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

        _cycleListCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468,
            Height = 44
        };

        _lblCycleCardTitle = new Label
        {
            Text = "Source Pairs",
            Left = 14,
            Top = 12,
            Width = 220,
            Height = 20,
            Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 205, 255)
        };
        _cycleListCard.Controls.Add(_lblCycleCardTitle);

        _btnAddCycleEntry = CreateModernButton("+ Add Pair", 334, 9, 120, 26, true);
        _btnAddCycleEntry.Click += (s, e) =>
        {
            _draftSettings.CrossfadeCycleList.Add(new CrossfadeCyclePair
            {
                Source1 = "",
                Color1Hex = "#0E0466",
                Source2 = "",
                Color2Hex = "#DA043A"
            });
            PopulateCycleListUI();
            ReflowGeneralPage();
            MarkDirty();
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
        page.InnerControls.Add(_cycleListCard);
        y = _cycleListCard.Bottom + 10;

        _keysCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int ky = 10;
        AddSectionHeader(_keysCard, "Hotkeys", 14, ref ky);

        _lblKey1 = AddAlignedLabel(_keysCard, "Fade Right / Up:", 14, ky);
        var r1 = AddKeyBindControls(_keysCard, ref ky, null);
        _txtKey1 = r1.txt;
        _btnRec1 = r1.btn;

        _lblKey2 = AddAlignedLabel(_keysCard, "Fade Left / Down:", 14, ky);
        var r2 = AddKeyBindControls(_keysCard, ref ky, null);
        _txtKey2 = r2.txt;
        _btnRec2 = r2.btn;

        _lblKey3 = AddAlignedLabel(_keysCard, "Mute Toggle:", 14, ky);
        var r3 = AddKeyBindControls(_keysCard, ref ky, null);
        _txtKey3 = r3.txt;
        _btnRec3 = r3.btn;

        _lblKeyCycleNext = AddAlignedLabel(_keysCard, "Next Pair:", 14, ky);
        var rNext = AddKeyBindControls(_keysCard, ref ky, val =>
        {
            _draftSettings.CycleNextKey = val;
            _draftSettings.QuickSwapKey = val;
        }, allowClear: true);
        _txtKeyCycleNext = rNext.txt;
        _btnRecCycleNext = rNext.btn;

        _lblKeyCyclePrev = AddAlignedLabel(_keysCard, "Previous Pair:", 14, ky);
        var rPrev = AddKeyBindControls(_keysCard, ref ky, val => _draftSettings.CyclePrevKey = val, allowClear: true);
        _txtKeyCyclePrev = rPrev.txt;
        _btnRecCyclePrev = rPrev.btn;

        _keysCard.FitHeightToContent(12);
        page.InnerControls.Add(_keysCard);

        return page;
    }

    private static string ReadComboText(ModernComboBox cmb)
    {
        return (cmb.SelectedItem?.ToString() ?? cmb.Text ?? "").Trim();
    }

    private void PopulateCycleListUI()
    {
        if (_cycleSlotsHost == null) return;
        _cycleSlotsHost.SuspendLayout();
        _cycleSlotsHost.Controls.Clear();

        List<string> options = GetAvailableSourceOptions();
        int y = 0;
        int hostW = Math.Max(442, _cycleSlotsHost.Width);
        int comboW = Math.Max(222, hostW - 220);

        for (int i = 0; i < _draftSettings.CrossfadeCycleList.Count; i++)
        {
            int index = i;
            var pair = _draftSettings.CrossfadeCycleList[index];
            Win11CardPanel pairCard = new Win11CardPanel
            {
                Left = 0,
                Top = y,
                Width = hostW,
                Height = 72,
                FillColor = Color.FromArgb(48, 48, 48),
                BorderColor = Color.FromArgb(66, 66, 66),
                CornerRadius = 6
            };

            Label lblS1 = new Label
            {
                Text = "L:",
                Left = 8,
                Top = 10,
                Width = 18,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(190, 190, 190)
            };
            pairCard.Controls.Add(lblS1);

            RoundedColorSwatch swatch1 = CreateCompactColorSwatch(28, 7, 24, 24, pair.Color1Hex, hex => pair.Color1Hex = hex);
            pairCard.Controls.Add(swatch1);

            ModernComboBox cmb1 = new ModernComboBox
            {
                Left = 58,
                Top = 7,
                Width = comboW,
                DropDownStyle = ComboBoxStyle.DropDown,
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
            pairCard.Controls.Add(cmb1);

            int btnRightStart = 58 + comboW + 6;

            ModernButton btnBrowse1 = CreateModernButton("Browse", btnRightStart, 6, 74, 25, false);
            btnBrowse1.Click += (s, e) =>
            {
                BrowseForExecutable(cmb1);
                pair.Source1 = ReadComboText(cmb1);
                PushLivePreviewIfActive();
            };
            pairCard.Controls.Add(btnBrowse1);

            ModernButton btnUp = CreateModernButton("▲", btnRightStart + 78, 6, 32, 25, false);
            btnUp.Enabled = (index > 0);
            btnUp.Click += (s, e) =>
            {
                if (index <= 0) return;
                _draftSettings.CrossfadeCycleList.RemoveAt(index);
                _draftSettings.CrossfadeCycleList.Insert(index - 1, pair);
                PopulateCycleListUI();
                ReflowGeneralPage();
                MarkDirty();
                PushLivePreviewIfActive();
            };
            pairCard.Controls.Add(btnUp);

            ModernButton btnDel = CreateModernButton("✕", btnRightStart + 114, 6, 34, 57, false, isDanger: true);
            btnDel.Enabled = (_draftSettings.CrossfadeCycleList.Count > 1);
            btnDel.Click += (s, e) =>
            {
                if (_draftSettings.CrossfadeCycleList.Count <= 1)
                {
                    MessageBox.Show("You must keep at least one crossfade pair.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _draftSettings.CrossfadeCycleList.Remove(pair);
                PopulateCycleListUI();
                ReflowGeneralPage();
                MarkDirty();
                PushLivePreviewIfActive();
            };
            pairCard.Controls.Add(btnDel);

            Label lblS2 = new Label
            {
                Text = "R:",
                Left = 8,
                Top = 42,
                Width = 18,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(190, 190, 190)
            };
            pairCard.Controls.Add(lblS2);

            RoundedColorSwatch swatch2 = CreateCompactColorSwatch(28, 39, 24, 24, pair.Color2Hex, hex => pair.Color2Hex = hex);
            pairCard.Controls.Add(swatch2);

            ModernComboBox cmb2 = new ModernComboBox
            {
                Left = 58,
                Top = 39,
                Width = comboW,
                DropDownStyle = ComboBoxStyle.DropDown,
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
            pairCard.Controls.Add(cmb2);

            ModernButton btnBrowse2 = CreateModernButton("Browse", btnRightStart, 38, 74, 25, false);
            btnBrowse2.Click += (s, e) =>
            {
                BrowseForExecutable(cmb2);
                pair.Source2 = ReadComboText(cmb2);
                PushLivePreviewIfActive();
            };
            pairCard.Controls.Add(btnBrowse2);

            ModernButton btnDown = CreateModernButton("▼", btnRightStart + 78, 38, 32, 25, false);
            btnDown.Enabled = (index < _draftSettings.CrossfadeCycleList.Count - 1);
            btnDown.Click += (s, e) =>
            {
                if (index >= _draftSettings.CrossfadeCycleList.Count - 1) return;
                _draftSettings.CrossfadeCycleList.RemoveAt(index);
                _draftSettings.CrossfadeCycleList.Insert(index + 1, pair);
                PopulateCycleListUI();
                ReflowGeneralPage();
                MarkDirty();
                PushLivePreviewIfActive();
            };
            pairCard.Controls.Add(btnDown);

            _cycleSlotsHost.Controls.Add(pairCard);
            y += 78;
        }

        _cycleSlotsHost.Height = y;
        _cycleSlotsHost.ResumeLayout();
    }

    private void ReflowGeneralPage()
    {
        if (_keysCard == null || _cycleListCard == null)
            return;

        int itemCount = _draftSettings.CrossfadeCycleList.Count;

        _cycleSlotsHost.Visible = (itemCount > 0);
        _cycleListCard.Top = 4;
        _cycleListCard.Height = (itemCount == 0) ? 44 : (46 + _cycleSlotsHost.Height);

        _keysCard.Top = _cycleListCard.Bottom + 10;
        _keysCard.FitHeightToContent(12);

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

        ModernButton btnShowPreview = CreateModernButton("Test Overlay", 14, py, 145, 28, true);
        btnShowPreview.Click += (s, e) => PushLivePreviewIfActive(forceTrigger: true);
        _previewCard.Controls.Add(btnShowPreview);

        _chkKeepOverlayAlwaysOn = new CheckBox
        {
            Text = "Pin overlay while settings open",
            Checked = _draftSettings.KeepOverlayOnWhileSettingsOpen,
            Left = 172,
            Top = py + 3,
            Width = 280,
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
            Top = sy + 3,
            Width = 165,
            ForeColor = Color.FromArgb(200, 200, 200)
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
        sy += 30;

        Label lblLvlMode = new Label
        {
            Text = "Volume Display:",
            Left = 14,
            Top = sy + 3,
            Width = 165,
            ForeColor = Color.FromArgb(200, 200, 200)
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
        sy += 30;

        Label lblOpacity = new Label
        {
            Text = "Opacity (10 - 100%):",
            Left = 14,
            Top = sy + 3,
            Width = 165,
            ForeColor = Color.FromArgb(200, 200, 200)
        };
        _overlayStyleCard.Controls.Add(lblOpacity);

        _txtOverlayOpacity = new TextBox
        {
            Text = _draftSettings.OverlayOpacity.ToString(),
            Left = 185,
            Top = sy,
            Width = 80,
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
        cy += 28;

        Label lblCarouselMotion = new Label
        {
            Text = "Carousel Style:",
            Left = 14,
            Top = cy + 3,
            Width = 165,
            ForeColor = Color.FromArgb(200, 200, 200)
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
        _cmbCarouselStyle.Items.AddRange(new object[] { "3D Floating Pills", "Fluid Moving Deck" });
        _cmbCarouselStyle.SelectedItem = _draftSettings.CarouselMotionStyle;
        if (_cmbCarouselStyle.SelectedIndex == -1) _cmbCarouselStyle.SelectedIndex = 0;
        _cmbCarouselStyle.SelectedIndexChanged += (s, e) =>
        {
            _draftSettings.CarouselMotionStyle = _cmbCarouselStyle.SelectedItem?.ToString() ?? "3D Floating Pills";
            MarkDirty();
            PushLivePreviewIfActive();
        };
        _carouselCard.Controls.Add(_cmbCarouselStyle);
        cy += 30;

        _chkEnableAnimations = new CheckBox
        {
            Text = "Carousel animations",
            Checked = _draftSettings.EnableCarouselAnimation,
            Enabled = _draftSettings.CarouselVisibleNeighbors > 0.05f,
            Left = 14,
            Top = cy,
            Width = 210,
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
            Left = 230,
            Top = cy,
            Width = 210,
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
            Top = ay + 3,
            Width = 165,
            ForeColor = Color.FromArgb(200, 200, 200)
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
        ay += 30;

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

        Panel monitorRowsPanel = new Panel
        {
            Left = 14,
            Top = ay,
            Width = 436,
            AutoSize = true,
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
                Width = 198,
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

            monitorRowsPanel.Controls.Add(chkMon);
            monitorRowsPanel.Controls.Add(cmbMonPos);
            my += 30;
        }
        monitorRowsPanel.Height = Math.Max(30, my);
        _layoutCard.Controls.Add(monitorRowsPanel);
        ay += monitorRowsPanel.Height + 6;

        chkFollowMonitor.CheckedChanged += (s, e) =>
        {
            _draftSettings.FollowActiveMonitor = chkFollowMonitor.Checked;
            monitorRowsPanel.Enabled = !chkFollowMonitor.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };

        Label lblSize = new Label
        {
            Text = "Dimensions (W × H):",
            Left = 14,
            Top = ay + 3,
            Width = 165,
            ForeColor = Color.FromArgb(200, 200, 200)
        };
        _layoutCard.Controls.Add(lblSize);

        _txtWidth = new TextBox
        {
            Text = _draftSettings.OverlayWidth.ToString(),
            Left = 185,
            Top = ay,
            Width = 80,
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
            Left = 270,
            Top = ay + 3,
            Width = 16,
            ForeColor = Color.FromArgb(180, 180, 180)
        };
        _layoutCard.Controls.Add(lblBy);

        _txtHeight = new TextBox
        {
            Text = _draftSettings.OverlayHeight.ToString(),
            Left = 288,
            Top = ay,
            Width = 80,
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
            ForeColor = Color.FromArgb(200, 200, 200)
        };
        _layoutCard.Controls.Add(lblDur);

        _txtPopupDuration = new TextBox
        {
            Text = _draftSettings.PopupDurationMs.ToString(),
            Left = 185,
            Top = ay,
            Width = 80,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtPopupDuration.TextChanged += (s, e) => MarkDirty();
        _layoutCard.Controls.Add(_txtPopupDuration);

        _layoutCard.FitHeightToContent(12);
        page.InnerControls.Add(_layoutCard);
        y = _layoutCard.Bottom + 10;

        // 5. Audio & System Behavior Card
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
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _behaviorCard.Controls.Add(lblStep);

        _txtStepSize = new TextBox
        {
            Text = _draftSettings.StepSize.ToString(),
            Left = 185,
            Top = by,
            Width = 80,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtStepSize.TextChanged += (s, e) => MarkDirty();
        _behaviorCard.Controls.Add(_txtStepSize);
        by += 30;

        _chkInterpolation = new CheckBox
        {
            Text = "Smooth animation",
            Checked = _draftSettings.EnableInterpolation,
            Left = 14,
            Top = by + 2,
            Width = 165,
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

        Label lblSpeed = new Label
        {
            Text = "Speed (5-100):",
            Left = 185,
            Top = by + 3,
            Width = 90,
            Height = 20,
            ForeColor = Color.FromArgb(200, 200, 200)
        };
        _behaviorCard.Controls.Add(lblSpeed);

        _txtInterpSpeed = new TextBox
        {
            Text = _draftSettings.InterpolationSpeed.ToString(),
            Left = 280,
            Top = by + 1,
            Width = 65,
            Enabled = _draftSettings.EnableInterpolation,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtInterpSpeed.TextChanged += (s, e) => MarkDirty();
        _behaviorCard.Controls.Add(_txtInterpSpeed);
        by += 28;

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

        int k1 = settings.CrossfadeKeyUp;
        int k2 = settings.CrossfadeKeyDown;
        int k3 = settings.CrossfadeKeyMute;

        _txtKey1.Text = KeybindHelper.FormatKeybind(k1);
        _txtKey1.Tag = k1;

        _txtKey2.Text = KeybindHelper.FormatKeybind(k2);
        _txtKey2.Tag = k2;

        _txtKey3.Text = KeybindHelper.FormatKeybind(k3);
        _txtKey3.Tag = k3;

        int nextKey = settings.GetEffectiveCycleNextKey();
        _txtKeyCycleNext.Text = KeybindHelper.FormatKeybind(nextKey);
        _txtKeyCycleNext.Tag = nextKey;

        _txtKeyCyclePrev.Text = KeybindHelper.FormatKeybind(settings.CyclePrevKey);
        _txtKeyCyclePrev.Tag = settings.CyclePrevKey;

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
            Width = 350,
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

    private (TextBox txt, ModernButton btn) AddKeyBindControls(Control parent, ref int top, Action<int>? onCustomCommit, bool allowClear = false)
    {
        int txtWidth = allowClear ? 162 : 196;
        TextBox txt = new TextBox
        {
            Left = 148,
            Top = top,
            Width = txtWidth,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true,
            TextAlign = HorizontalAlignment.Center
        };
        parent.Controls.Add(txt);

        ModernButton btnRecord = CreateModernButton("Bind Key", 352, top - 1, 102, 25, false);
        btnRecord.Click += (s, e) => StartRecording(btnRecord, txt, onCustomCommit);
        parent.Controls.Add(btnRecord);

        if (allowClear)
        {
            ModernButton btnClear = CreateModernButton("✕", 314, top - 1, 32, 25, false);
            btnClear.Click += (s, e) =>
            {
                txt.Tag = 0;
                txt.Text = "None";
                onCustomCommit?.Invoke(0);
                MarkDirty();
            };
            parent.Controls.Add(btnClear);
        }

        top += 32;
        return (txt, btnRecord);
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
        if (!_isRecording) return;

        if (e.KeyCode == Keys.Escape)
        {
            CancelRecording();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu ||
            e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.RControlKey ||
            e.KeyCode == Keys.LShiftKey || e.KeyCode == Keys.RShiftKey ||
            e.KeyCode == Keys.LMenu || e.KeyCode == Keys.RMenu)
        {
            if (_activeTargetTextBox != null)
            {
                _activeTargetTextBox.Text = KeybindHelper.FormatLiveModifiers(e.Modifiers);
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        int combinedBinding = KeybindHelper.CreateBinding(e.KeyCode, e.Modifiers);
        string comboName = KeybindHelper.FormatKeybind(combinedBinding);

        if (_activeTargetTextBox != null)
        {
            _activeTargetTextBox.Text = comboName;
            _activeTargetTextBox.Tag = combinedBinding;
            _activeKeyCommitCallback?.Invoke(combinedBinding);
            MarkDirty();
        }

        StopRecording();
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void SettingsForm_KeyUp(object? sender, KeyEventArgs e)
    {
        if (!_isRecording || _activeTargetTextBox == null) return;
        _activeTargetTextBox.Text = KeybindHelper.FormatLiveModifiers(e.Modifiers);
    }

    private void StartRecording(ModernButton? btn, TextBox targetBox, Action<int>? onCommit = null)
    {
        if (_isRecording) CancelRecording();
        _isRecording = true;
        _activeRecordButton = btn;
        _activeTargetTextBox = targetBox;
        _activeKeyCommitCallback = onCommit;
        _previousTextBoxText = targetBox.Text;

        targetBox.Text = "Press keys...";
        if (btn != null)
        {
            _previousButtonText = btn.Text;
            btn.Text = "Listening...";
            btn.BackColor = Color.FromArgb(205, 95, 0);
            btn.HoverBackColor = Color.FromArgb(220, 110, 15);
            btn.Invalidate();
        }
    }

    private void StopRecording()
    {
        if (!_isRecording) return;
        if (_activeRecordButton != null)
        {
            _activeRecordButton.Text = _previousButtonText;
            _activeRecordButton.BackColor = Color.FromArgb(52, 52, 52);
            _activeRecordButton.HoverBackColor = Color.FromArgb(66, 66, 66);
            _activeRecordButton.Invalidate();
        }
        _isRecording = false;
        _activeRecordButton = null;
        _activeTargetTextBox = null;
        _activeKeyCommitCallback = null;
    }

    private void CancelRecording()
    {
        if (!_isRecording) return;
        if (_activeTargetTextBox != null)
        {
            _activeTargetTextBox.Text = _previousTextBoxText;
        }
        StopRecording();
    }

    private class KeyCaptureFilter : IMessageFilter
    {
        private readonly Action _onMouseClick;
        public KeyCaptureFilter(Action onMouseClick) => _onMouseClick = onMouseClick;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == 0x0201 || m.Msg == 0x0204) _onMouseClick();
            return false;
        }
    }

    private void SaveChanges()
    {
        if (_draftSettings.CrossfadeCycleList.Count == 0)
        {
            MessageBox.Show("You must have at least one crossfade pair configured.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _draftSettings.Source1 = _draftSettings.CrossfadeCycleList[0].Source1;
        _draftSettings.Source1Color = _draftSettings.CrossfadeCycleList[0].Color1Hex;
        _draftSettings.Source2 = _draftSettings.CrossfadeCycleList[0].Source2;
        _draftSettings.Source2Color = _draftSettings.CrossfadeCycleList[0].Color2Hex;

        int cfUp = (int)(_txtKey1.Tag ?? 0);
        int cfDown = (int)(_txtKey2.Tag ?? 0);
        int cfMute = (int)(_txtKey3.Tag ?? 0);
        int cycleNextVal = (int)(_txtKeyCycleNext.Tag ?? 0);
        int cyclePrevVal = (int)(_txtKeyCyclePrev.Tag ?? 0);

        for (int i = 0; i < _draftSettings.CrossfadeCycleList.Count; i++)
        {
            var pair = _draftSettings.CrossfadeCycleList[i];
            if (!string.IsNullOrWhiteSpace(pair.Source1) && !pair.Source1.StartsWith("Group: ", StringComparison.OrdinalIgnoreCase))
            {
                pair.Source1 = AppSettings.NormalizeExeName(pair.Source1);
                _draftSettings.RecordProgramHistory(pair.Source1);
            }
            if (!string.IsNullOrWhiteSpace(pair.Source2) && !pair.Source2.StartsWith("Group: ", StringComparison.OrdinalIgnoreCase))
            {
                pair.Source2 = AppSettings.NormalizeExeName(pair.Source2);
                _draftSettings.RecordProgramHistory(pair.Source2);
            }

            if (!string.IsNullOrWhiteSpace(pair.Source1) && !string.IsNullOrWhiteSpace(pair.Source2))
            {
                if (pair.Source1.Equals(pair.Source2, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"Crossfade Cycle Pair #{i + 1} has the same source ({pair.Source1}) on both sides.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var pairOverlaps = _draftSettings.GetOverlappingExecutables(pair.Source1, pair.Source2);
                if (pairOverlaps.Count > 0)
                {
                    string dupList = string.Join("\n• ", pairOverlaps);
                    MessageBox.Show(
                        $"Crossfade Cycle Pair #{i + 1} ({pair.Source1} ⇄ {pair.Source2}) contains duplicate software on both sides:\n\n• {dupList}",
                        "Duplicate Software in Cycle Pair",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
        }

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

        _draftSettings.CycleNextKey = cycleNextVal;
        _draftSettings.QuickSwapKey = cycleNextVal;
        _draftSettings.CyclePrevKey = cyclePrevVal;
        _draftSettings.RunOnStartup = _chkStartup.Checked;
        _draftSettings.CrossfadeKeyUp = cfUp;
        _draftSettings.CrossfadeKeyDown = cfDown;
        _draftSettings.CrossfadeKeyMute = cfMute;
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