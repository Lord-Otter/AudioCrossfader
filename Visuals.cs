using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

public readonly struct BadgeInfo
{
    public string Text { get; }
    public Color Color { get; }

    public BadgeInfo(string text, Color color)
    {
        Text = text;
        Color = color;
    }
}

// Custom Dark-Mode Renderer for System Tray ContextMenuStrip
public class DarkContextMenuColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Color.FromArgb(32, 32, 32);
    public override Color ImageMarginGradientBegin => Color.FromArgb(32, 32, 32);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(32, 32, 32);
    public override Color ImageMarginGradientEnd => Color.FromArgb(32, 32, 32);
    public override Color MenuBorder => Color.FromArgb(62, 62, 62);
    public override Color MenuItemBorder => Color.FromArgb(75, 75, 75);
    public override Color MenuItemSelected => Color.FromArgb(52, 52, 52);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(52, 52, 52);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(52, 52, 52);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(45, 45, 45);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(45, 45, 45);
    public override Color SeparatorDark => Color.FromArgb(55, 55, 55);
    public override Color SeparatorLight => Color.FromArgb(55, 55, 55);
    public override Color CheckBackground => Color.FromArgb(0, 120, 212);
    public override Color CheckSelectedBackground => Color.FromArgb(20, 138, 230);
    public override Color CheckPressedBackground => Color.FromArgb(0, 100, 180);
}

public class DarkContextMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkContextMenuRenderer() : base(new DarkContextMenuColorTable())
    {
        this.RoundedEdges = true;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (!e.Item.Enabled)
        {
            e.TextColor = Color.FromArgb(140, 140, 140);
        }
        else if (e.TextColor == SystemColors.ControlText || e.TextColor == Color.Black)
        {
            e.TextColor = Color.FromArgb(240, 240, 240);
        }
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Color.FromArgb(220, 220, 220);
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(e.ImageRectangle.X + 2, e.ImageRectangle.Y + 2, e.ImageRectangle.Width - 4, e.ImageRectangle.Height - 4);
        using SolidBrush b = new SolidBrush(Color.FromArgb(0, 120, 212));
        using GraphicsPath path = Win11CardPanel.CreateRoundRect(r, 3);
        e.Graphics.FillPath(b, path);

        using Pen checkPen = new Pen(Color.White, 1.8f);
        e.Graphics.DrawLines(checkPen, new[]
        {
            new Point(r.Left + 3, r.Top + r.Height / 2),
            new Point(r.Left + r.Width / 2 - 1, r.Bottom - 3),
            new Point(r.Right - 2, r.Top + 3)
        });
    }
}

// Manages System Tray Quick-Actions, Cycle Lists, and Background Worker
public class TrayApplicationContext : ApplicationContext
{
    private NotifyIcon? _trayIcon;
    private readonly ContextMenuStrip _contextMenu;
    private AppSettings _activeSettings;
    private AudioCrossfaderWorker? _crossfaderWorker;
    private SettingsForm? _settingsForm;
    private readonly string _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public TrayApplicationContext()
    {
        _activeSettings = LoadSettings();
        AppIconCache.LoadPathsFromSettings(_activeSettings);

        _contextMenu = new ContextMenuStrip
        {
            Renderer = new DarkContextMenuRenderer(),
            BackColor = Color.FromArgb(32, 32, 32),
            ForeColor = Color.FromArgb(240, 240, 240),
            ShowImageMargin = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Regular)
        };
        _contextMenu.Opening += (s, e) => PopulateTrayMenu();
        PopulateTrayMenu();

        Icon trayIconAsset = SystemIcons.Application;
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                trayIconAsset = new Icon(iconPath);
            }
            else
            {
                trayIconAsset = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            }
        }
        catch { }

        _trayIcon = new NotifyIcon
        {
            Icon = trayIconAsset,
            ContextMenuStrip = _contextMenu,
            Visible = true,
            Text = "Audio Crossfader"
        };
        _trayIcon.DoubleClick += (s, e) => ShowSettings();

        StartWorker();
    }

    private void PopulateTrayMenu()
    {
        _contextMenu.Items.Clear();

        var swapItem = new ToolStripMenuItem($"Swap Sources ({_activeSettings.Source1} ↔ {_activeSettings.Source2})");
        swapItem.Click += (s, e) =>
        {
            var updated = _activeSettings.Clone();
            (updated.Source1, updated.Source2) = (updated.Source2, updated.Source1);
            (updated.Source1Color, updated.Source2Color) = (updated.Source2Color, updated.Source1Color);
            SaveSettings(updated);
        };
        _contextMenu.Items.Add(swapItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        var validPairs = _activeSettings.CrossfadeCycleList
            .Where(p => !string.IsNullOrWhiteSpace(p.Source1) && !string.IsNullOrWhiteSpace(p.Source2))
            .ToList();

        if (validPairs.Count > 0)
        {
            var header = new ToolStripMenuItem("Crossfade Source Pairs") { Enabled = false };
            _contextMenu.Items.Add(header);

            foreach (var pair in validPairs)
            {
                var item = new ToolStripMenuItem($"{pair.Source1}  ⇄  {pair.Source2}")
                {
                    Checked = _activeSettings.Source1.Equals(pair.Source1, StringComparison.OrdinalIgnoreCase) &&
                              _activeSettings.Source2.Equals(pair.Source2, StringComparison.OrdinalIgnoreCase)
                };
                item.Click += (s, e) =>
                {
                    var updated = _activeSettings.Clone();
                    updated.Source1 = pair.Source1;
                    updated.Source1Color = pair.Color1Hex;
                    updated.Source2 = pair.Source2;
                    updated.Source2Color = pair.Color2Hex;
                    SaveSettings(updated);
                };
                _contextMenu.Items.Add(item);
            }
        }
        else
        {
            var emptyItem = new ToolStripMenuItem("No Crossfade Pairs Configured") { Enabled = false };
            _contextMenu.Items.Add(emptyItem);
        }

        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("Settings...", null, (s, e) => ShowSettings());
        _contextMenu.Items.Add("Exit", null, (s, e) => ExitApp());
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                if (loaded.CycleNextKey == 0 && loaded.QuickSwapKey != 0)
                {
                    loaded.CycleNextKey = loaded.QuickSwapKey;
                }
                if (loaded.CrossfadeCycleList.Count > 0)
                {
                    loaded.Source1 = loaded.CrossfadeCycleList[0].Source1;
                    loaded.Source1Color = loaded.CrossfadeCycleList[0].Color1Hex;
                    loaded.Source2 = loaded.CrossfadeCycleList[0].Source2;
                    loaded.Source2Color = loaded.CrossfadeCycleList[0].Color2Hex;
                }
                loaded.RecordProgramHistory(loaded.Source1);
                loaded.RecordProgramHistory(loaded.Source2);
                return loaded;
            }
        }
        catch { }
        return new AppSettings();
    }

    public void SaveSettings(AppSettings newSettings)
    {
        _activeSettings = newSettings.Clone();
        PersistSettingsToDisk(_activeSettings);
        _crossfaderWorker?.UpdateSettings(_activeSettings, false);
        if (_settingsForm != null && !_settingsForm.IsDisposed)
        {
            _crossfaderWorker?.SetSettingsWindowOpen(true, _activeSettings);
        }
    }

    public void TriggerOverlayPreview(AppSettings draftSettings)
    {
        _crossfaderWorker?.PreviewOverlay(draftSettings);
    }

    public void NotifySettingsWindowState(bool isOpen, AppSettings? draftSettings = null)
    {
        _crossfaderWorker?.SetSettingsWindowOpen(isOpen, draftSettings);
    }

    private void PersistSettingsToDisk(AppSettings settingsToSave)
    {
        try
        {
            string json = JsonSerializer.Serialize(settingsToSave, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    private void StartWorker()
    {
        _crossfaderWorker = new AudioCrossfaderWorker(_activeSettings, mutatedSettings =>
        {
            _activeSettings = mutatedSettings;
            PersistSettingsToDisk(_activeSettings);
            if (_settingsForm != null && !_settingsForm.IsDisposed && _settingsForm.IsHandleCreated)
            {
                _settingsForm.BeginInvoke(new Action(() => _settingsForm.SyncActiveSourcesFromCycle(mutatedSettings)));
            }
        });
        _crossfaderWorker.Start();
    }

    private void ShowSettings()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(_activeSettings.Clone(), this);
            _settingsForm.FormClosed += (s, e) => NotifySettingsWindowState(false);
            _settingsForm.Show();
            NotifySettingsWindowState(true, _activeSettings);
        }
        else
        {
            if (_settingsForm.WindowState == FormWindowState.Minimized)
                _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.Activate();
            _settingsForm.BringToFront();
        }
    }

    private void ExitApp()
    {
        _crossfaderWorker?.Stop();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        Application.Exit();
    }
}

// Custom Dark-Mode Scrollable Container
public class DarkScrollPanel : Panel
{
    private readonly Panel _content;
    private int _scrollOffset = 0;
    private int _contentHeight = 0;
    private bool _draggingThumb = false;
    private bool _hoveringThumb = false;
    private int _dragStartY = 0;
    private int _dragStartScroll = 0;
    private const int ScrollBarWidth = 8;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control.ControlCollection InnerControls => _content.Controls;

    public DarkScrollPanel()
    {
        this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        this.BackColor = Color.FromArgb(28, 28, 28);

        _content = new Panel
        {
            Left = 0,
            Top = 0,
            Width = this.ClientSize.Width - ScrollBarWidth - 2,
            Height = 10,
            BackColor = Color.FromArgb(28, 28, 28)
        };
        this.Controls.Add(_content);
    }

    public void RecalculateScroll()
    {
        int maxBottom = 0;
        foreach (Control c in _content.Controls)
        {
            if (c.Visible && c.Bottom > maxBottom)
            {
                maxBottom = c.Bottom;
            }
        }

        _contentHeight = maxBottom + 10;
        _content.Width = Math.Max(50, this.ClientSize.Width - ScrollBarWidth - 4);
        _content.Height = Math.Max(this.ClientSize.Height, _contentHeight);

        ClampAndApplyScroll();
        Invalidate();
    }

    private void ClampAndApplyScroll()
    {
        int maxScroll = Math.Max(0, _contentHeight - this.ClientSize.Height);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);
        _content.Top = -_scrollOffset;
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        if (_content != null)
        {
            RecalculateScroll();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_contentHeight <= this.ClientSize.Height) return;

        int step = 36;
        _scrollOffset -= Math.Sign(e.Delta) * step;
        ClampAndApplyScroll();
        Invalidate();
    }

    private Rectangle GetThumbRect()
    {
        if (_contentHeight <= this.ClientSize.Height || this.ClientSize.Height <= 0)
            return Rectangle.Empty;

        int trackHeight = this.ClientSize.Height - 8;
        int thumbHeight = Math.Max(28, (int)((float)this.ClientSize.Height / _contentHeight * trackHeight));
        int maxScroll = _contentHeight - this.ClientSize.Height;
        int maxThumbTop = trackHeight - thumbHeight;
        int thumbTop = 4 + (maxScroll > 0 ? (int)((float)_scrollOffset / maxScroll * maxThumbTop) : 0);

        return new Rectangle(this.ClientSize.Width - ScrollBarWidth - 2, thumbTop, 6, thumbHeight);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Rectangle thumb = GetThumbRect();
        if (!thumb.IsEmpty && thumb.Contains(e.Location))
        {
            _draggingThumb = true;
            _dragStartY = e.Y;
            _dragStartScroll = _scrollOffset;
            this.Capture = true;
        }
        else if (!thumb.IsEmpty && e.X >= this.ClientSize.Width - ScrollBarWidth - 6)
        {
            _scrollOffset += (e.Y < thumb.Top) ? -120 : 120;
            ClampAndApplyScroll();
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_draggingThumb)
        {
            int trackHeight = this.ClientSize.Height - 8;
            Rectangle thumb = GetThumbRect();
            int maxThumbTravel = Math.Max(1, trackHeight - thumb.Height);
            int maxScroll = Math.Max(0, _contentHeight - this.ClientSize.Height);

            int deltaY = e.Y - _dragStartY;
            _scrollOffset = _dragStartScroll + (int)((float)deltaY / maxThumbTravel * maxScroll);
            ClampAndApplyScroll();
            Invalidate();
        }
        else
        {
            Rectangle thumb = GetThumbRect();
            bool over = !thumb.IsEmpty && thumb.Contains(e.Location);
            if (over != _hoveringThumb)
            {
                _hoveringThumb = over;
                Invalidate();
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_draggingThumb)
        {
            _draggingThumb = false;
            this.Capture = false;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Rectangle thumb = GetThumbRect();
        if (!thumb.IsEmpty)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color thumbColor = (_draggingThumb || _hoveringThumb)
                ? Color.FromArgb(135, 135, 135)
                : Color.FromArgb(85, 85, 85);

            using GraphicsPath path = Win11CardPanel.CreateRoundRect(thumb, 3);
            using SolidBrush brush = new SolidBrush(thumbColor);
            e.Graphics.FillPath(brush, path);
        }
    }
}

// Custom Windows 11 Rounded Surface Card
public class Win11CardPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor { get; set; } = Color.FromArgb(39, 39, 39);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Color.FromArgb(55, 55, 55);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 8;

    public Win11CardPanel()
    {
        this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        this.BackColor = Color.Transparent;
        this.Padding = new Padding(12);
    }

    public void FitHeightToContent(int bottomPadding = 12)
    {
        int maxBottom = 0;
        foreach (Control c in this.Controls)
        {
            if (c.Visible && c.Bottom > maxBottom)
            {
                maxBottom = c.Bottom;
            }
        }
        if (maxBottom > 0)
        {
            this.Height = maxBottom + bottomPadding;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        using GraphicsPath path = CreateRoundRect(rect, CornerRadius);
        using SolidBrush fillBrush = new SolidBrush(FillColor);
        using Pen borderPen = new Pen(BorderColor, 1f);

        e.Graphics.FillPath(fillBrush, path);
        e.Graphics.DrawPath(borderPen, path);
    }

    public static GraphicsPath CreateRoundRect(Rectangle r, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int d = radius * 2;
        if (d <= 0 || r.Width < d || r.Height < d)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static GraphicsPath CreateRoundRectF(RectangleF r, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        float d = radius * 2f;
        if (d <= 0f || r.Width < d || r.Height < d)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

// Custom Borderless Dark-Mode ComboBox (Mouse-wheel events pass through to parent scroll panel)
public class ModernComboBox : ComboBox
{
    private const int WM_PAINT = 0x000F;
    private const int WM_MOUSEWHEEL = 0x020A;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string, BadgeInfo>? BadgeResolver { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string, Bitmap?>? IconResolver { get; set; }

    public ModernComboBox()
    {
        this.BackColor = Color.FromArgb(48, 48, 48);
        this.ForeColor = Color.FromArgb(242, 242, 242);
        this.FlatStyle = FlatStyle.Flat;
        this.DrawMode = DrawMode.OwnerDrawFixed;
        this.ItemHeight = 22;
        this.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        Color bg = isSelected ? Color.FromArgb(0, 103, 192) : Color.FromArgb(45, 45, 45);

        using (SolidBrush bgBrush = new SolidBrush(bg))
        {
            e.Graphics.FillRectangle(bgBrush, e.Bounds);
        }

        string itemText = Items[e.Index]?.ToString() ?? "";
        BadgeInfo badge = BadgeResolver != null ? BadgeResolver(itemText) : new BadgeInfo("", Color.Transparent);
        Bitmap? icon = IconResolver?.Invoke(itemText);

        int textLeft = e.Bounds.X + 6;
        if (icon != null)
        {
            int iconSize = 16;
            int iconY = e.Bounds.Y + (e.Bounds.Height - iconSize) / 2;
            e.Graphics.DrawImage(icon, new Rectangle(textLeft, iconY, iconSize, iconSize));
            textLeft += iconSize + 6;
        }

        Rectangle textRect = new Rectangle(textLeft, e.Bounds.Y, e.Bounds.Right - textLeft - 6, e.Bounds.Height);
        if (!string.IsNullOrEmpty(badge.Text))
        {
            using Font badgeFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            SizeF badgeSize = e.Graphics.MeasureString(badge.Text, badgeFont);
            int pillWidth = (int)badgeSize.Width + 12;
            int pillHeight = Math.Max(14, e.Bounds.Height - 6);
            Rectangle pillRect = new Rectangle(e.Bounds.Right - pillWidth - 6, e.Bounds.Y + (e.Bounds.Height - pillHeight) / 2, pillWidth, pillHeight);

            using GraphicsPath pillPath = Win11CardPanel.CreateRoundRect(pillRect, 4);
            using SolidBrush pillBrush = new SolidBrush(isSelected ? Color.FromArgb(60, 0, 0, 0) : Color.FromArgb(45, badge.Color));
            e.Graphics.FillPath(pillBrush, pillPath);

            using SolidBrush badgeTextBrush = new SolidBrush(isSelected ? Color.White : badge.Color);
            using StringFormat sfBadge = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(badge.Text, badgeFont, badgeTextBrush, pillRect, sfBadge);

            textRect.Width = Math.Max(40, pillRect.Left - textRect.Left - 6);
        }

        TextRenderer.DrawText(
            e.Graphics,
            itemText,
            this.Font,
            textRect,
            Color.White,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEWHEEL)
        {
            Control? parent = this.Parent;
            while (parent != null && !(parent is DarkScrollPanel))
            {
                parent = parent.Parent;
            }
            if (parent != null && parent.IsHandleCreated)
            {
                SendMessage(parent.Handle, m.Msg, m.WParam, m.LParam);
                return;
            }
        }

        base.WndProc(ref m);
        if (m.Msg == WM_PAINT)
        {
            using Graphics g = Graphics.FromHwnd(this.Handle);
            using Pen outerPen = new Pen(this.Focused ? Color.FromArgb(0, 120, 212) : Color.FromArgb(68, 68, 68), 1);
            using Pen innerPen = new Pen(this.BackColor, 1);
            g.DrawRectangle(outerPen, 0, 0, this.Width - 1, this.Height - 1);
            g.DrawRectangle(innerPen, 1, 1, this.Width - 3, this.Height - 3);
        }
    }
}

// Windows 11 Dark Mode Settings Window
public class SettingsForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private Button _navBtnGeneral = null!;
    private Button _navBtnGroups = null!;
    private Button _navBtnPersonalization = null!;
    private DarkScrollPanel _pageGeneral = null!;
    private DarkScrollPanel _pageGroups = null!;
    private DarkScrollPanel _pagePersonalization = null!;

    private Win11CardPanel _keysCard = null!;
    private Win11CardPanel _cycleListCard = null!;
    private Label _lblCycleCardTitle = null!;
    private Button _btnAddCycleEntry = null!;
    private Panel _cycleSlotsHost = null!;
    private Win11CardPanel _behaviorCard = null!;

    private Label _lblKey1 = null!;
    private TextBox _txtKey1 = null!;
    private Button _btnRec1 = null!;

    private Label _lblKey2 = null!;
    private TextBox _txtKey2 = null!;
    private Button _btnRec2 = null!;

    private Label _lblKey3 = null!;
    private TextBox _txtKey3 = null!;
    private Button _btnRec3 = null!;

    private Label _lblKeyCycleNext = null!;
    private TextBox _txtKeyCycleNext = null!;
    private Button _btnRecCycleNext = null!;

    private Label _lblKeyCyclePrev = null!;
    private TextBox _txtKeyCyclePrev = null!;
    private Button _btnRecCyclePrev = null!;

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
    private TextBox _txtStepSize = null!;
    private TextBox _txtWidth = null!;
    private TextBox _txtHeight = null!;
    private CheckBox _chkStartup = null!;
    private CheckBox _chkInterpolation = null!;
    private TextBox _txtInterpSpeed = null!;
    private CheckBox _chkBuffer = null!;

    private Panel _unmutedColorBox = null!;
    private Panel _mutedColorBox = null!;

    private readonly TrayApplicationContext _context;

    private AppSettings _draftSettings;
    private AppSettings _savedSettingsSnapshot;
    private DetectedAudioPrograms _detectedAudio = new DetectedAudioPrograms();
    private Label _lblStatus = null!;

    private bool _isRecording = false;
    private Button? _activeRecordButton = null;
    private TextBox? _activeTargetTextBox = null;
    private Action<int>? _activeKeyCommitCallback = null;
    private string _previousButtonText = "";
    private string _previousTextBoxText = "";
    private KeyCaptureFilter? _mouseFilter;
    private bool _isInitializing = true;

    public SettingsForm(AppSettings initialSettings, TrayApplicationContext context)
    {
        _context = context;
        _draftSettings = initialSettings.Clone();
        _savedSettingsSnapshot = initialSettings.Clone();
        _detectedAudio = AudioProcessDetector.Scan();

        this.Text = "Audio Crossfader Settings";
        this.Size = new Size(520, 780);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
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

        this.KeyDown += SettingsForm_KeyDown;
        this.KeyUp += SettingsForm_KeyUp;

        _isInitializing = true;
        LoadSettingsIntoUI(_draftSettings);
        _isInitializing = false;

        SwitchTab(0);
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

    private Panel BuildTopNavigationBar()
    {
        Panel header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = Color.FromArgb(28, 28, 28)
        };

        Win11CardPanel pillTrack = new Win11CardPanel
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

        _navBtnGeneral = CreateNavTabButton("General & Pairs", 4, 4, 156, () => SwitchTab(0));
        _navBtnGroups = CreateNavTabButton("Program Groups", 162, 4, 150, () => SwitchTab(1));
        _navBtnPersonalization = CreateNavTabButton("Personalization", 314, 4, 150, () => SwitchTab(2));

        pillTrack.Controls.Add(_navBtnGeneral);
        pillTrack.Controls.Add(_navBtnGroups);
        pillTrack.Controls.Add(_navBtnPersonalization);
        header.Controls.Add(pillTrack);

        return header;
    }

    private Button CreateNavTabButton(string text, int left, int top, int width, Action onClick)
    {
        Button btn = new Button
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(200, 200, 200),
            BackColor = Color.FromArgb(37, 37, 37),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 50);
        btn.Click += (s, e) => onClick();
        return btn;
    }

    private void SwitchTab(int index)
    {
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

    private void StyleNavButton(Button btn, bool active)
    {
        btn.BackColor = active ? Color.FromArgb(0, 120, 212) : Color.FromArgb(37, 37, 37);
        btn.ForeColor = active ? Color.White : Color.FromArgb(185, 185, 185);
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

        Button btnRevert = CreateModernButton("Revert Changes", 18, 29, 180, 33, false);
        btnRevert.Click += (s, e) => RevertChanges();
        bottomPanel.Controls.Add(btnRevert);

        Button btnSave = CreateModernButton("Save & Apply", 302, 29, 184, 33, true);
        btnSave.Click += (s, e) => SaveChanges();
        bottomPanel.Controls.Add(btnSave);

        return bottomPanel;
    }

    private Button CreateModernButton(string text, int left, int top, int width, int height, bool isPrimary)
    {
        Button btn = new Button
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            BackColor = isPrimary ? Color.FromArgb(0, 120, 212) : Color.FromArgb(52, 52, 52),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 8.75f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = isPrimary ? Color.FromArgb(30, 144, 255) : Color.FromArgb(70, 70, 70);
        btn.FlatAppearance.MouseOverBackColor = isPrimary ? Color.FromArgb(20, 138, 230) : Color.FromArgb(66, 66, 66);
        return btn;
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

    private Panel CreateCompactColorSwatch(int left, int top, int width, int height, string initialHex, Action<string> onColorPicked)
    {
        Panel swatch = new Panel
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            BackColor = SafeParseHexColor(initialHex, Color.FromArgb(27, 59, 90)),
            Cursor = Cursors.Hand
        };

        swatch.Paint += (s, e) =>
        {
            using Pen borderPen = new Pen(Color.FromArgb(110, 110, 110), 1);
            e.Graphics.DrawRectangle(borderPen, 0, 0, swatch.Width - 1, swatch.Height - 1);
        };

        swatch.Click += (s, e) =>
        {
            using ColorDialog dlg = new ColorDialog { FullOpen = true };
            try { dlg.Color = swatch.BackColor; } catch { }
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                swatch.BackColor = dlg.Color;
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
            Text = "Crossfade Source Pairs (Cycle List)",
            Left = 14,
            Top = 12,
            Width = 290,
            Height = 20,
            Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 205, 255)
        };
        _cycleListCard.Controls.Add(_lblCycleCardTitle);

        _btnAddCycleEntry = CreateModernButton("+ Add Source Pair", 312, 9, 142, 26, true);
        _btnAddCycleEntry.Click += (s, e) =>
        {
            _draftSettings.CrossfadeCycleList.Add(new CrossfadeCyclePair
            {
                Source1 = "",
                Color1Hex = "#1B3B5A",
                Source2 = "",
                Color2Hex = "#4A2040"
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
        AddSectionHeader(_keysCard, "Hotkeys & Key Combinations", 14, ref ky);

        _lblKey1 = AddAlignedLabel(_keysCard, "Fade Up / Right:", 14, ky);
        var r1 = AddKeyBindControls(_keysCard, ref ky, null);
        _txtKey1 = r1.txt;
        _btnRec1 = r1.btn;

        _lblKey2 = AddAlignedLabel(_keysCard, "Fade Down / Left:", 14, ky);
        var r2 = AddKeyBindControls(_keysCard, ref ky, null);
        _txtKey2 = r2.txt;
        _btnRec2 = r2.btn;

        _lblKey3 = AddAlignedLabel(_keysCard, "Cycle Mute:", 14, ky);
        var r3 = AddKeyBindControls(_keysCard, ref ky, null);
        _txtKey3 = r3.txt;
        _btnRec3 = r3.btn;

        _lblKeyCycleNext = AddAlignedLabel(_keysCard, "Cycle Next Pair:", 14, ky);
        var rNext = AddKeyBindControls(_keysCard, ref ky, val =>
        {
            _draftSettings.CycleNextKey = val;
            _draftSettings.QuickSwapKey = val;
        }, allowClear: true);
        _txtKeyCycleNext = rNext.txt;
        _btnRecCycleNext = rNext.btn;

        _lblKeyCyclePrev = AddAlignedLabel(_keysCard, "Cycle Prev Pair:", 14, ky);
        var rPrev = AddKeyBindControls(_keysCard, ref ky, val => _draftSettings.CyclePrevKey = val, allowClear: true);
        _txtKeyCyclePrev = rPrev.txt;
        _btnRecCyclePrev = rPrev.btn;

        _keysCard.FitHeightToContent(12);
        page.InnerControls.Add(_keysCard);
        y = _keysCard.Bottom + 10;

        _behaviorCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int by = 10;
        AddSectionHeader(_behaviorCard, "Step & System Behavior", 14, ref by);

        Label lblStep = new Label
        {
            Text = "Step Size (%, 1 - 100):",
            Left = 14,
            Top = by + 3,
            Width = 150,
            ForeColor = Color.FromArgb(205, 205, 205)
        };
        _behaviorCard.Controls.Add(lblStep);

        _txtStepSize = new TextBox
        {
            Text = _draftSettings.StepSize.ToString(),
            Left = 170,
            Top = by,
            Width = 100,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtStepSize.TextChanged += (s, e) => MarkDirty();
        _behaviorCard.Controls.Add(_txtStepSize);
        by += 30;

        _chkStartup = new CheckBox
        {
            Text = "Run on Windows Startup",
            Checked = _draftSettings.RunOnStartup,
            Left = 14,
            Top = by,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkStartup.CheckedChanged += (s, e) => MarkDirty();
        _behaviorCard.Controls.Add(_chkStartup);
        by += 26;

        _chkBuffer = new CheckBox
        {
            Text = "Enable 100% Equalization Center Detent (Crossfade)",
            Checked = _draftSettings.EnableEqualizationBuffer,
            Left = 14,
            Top = by,
            Width = 430,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkBuffer.CheckedChanged += (s, e) => MarkDirty();
        _behaviorCard.Controls.Add(_chkBuffer);

        _behaviorCard.FitHeightToContent(12);
        page.InnerControls.Add(_behaviorCard);
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

        for (int i = 0; i < _draftSettings.CrossfadeCycleList.Count; i++)
        {
            int index = i;
            var pair = _draftSettings.CrossfadeCycleList[index];
            Win11CardPanel pairCard = new Win11CardPanel
            {
                Left = 0,
                Top = y,
                Width = 442,
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

            Panel swatch1 = CreateCompactColorSwatch(28, 7, 24, 24, pair.Color1Hex, hex => pair.Color1Hex = hex);
            pairCard.Controls.Add(swatch1);

            ModernComboBox cmb1 = new ModernComboBox
            {
                Left = 58,
                Top = 7,
                Width = 222,
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

            Button btnBrowse1 = CreateModernButton("Browse", 286, 6, 74, 25, false);
            btnBrowse1.Click += (s, e) =>
            {
                BrowseForExecutable(cmb1);
                pair.Source1 = ReadComboText(cmb1);
                PushLivePreviewIfActive();
            };
            pairCard.Controls.Add(btnBrowse1);

            Button btnUp = CreateModernButton("▲", 364, 6, 32, 25, false);
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

            Button btnDel = CreateModernButton("✕", 400, 6, 34, 57, false);
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

            Panel swatch2 = CreateCompactColorSwatch(28, 39, 24, 24, pair.Color2Hex, hex => pair.Color2Hex = hex);
            pairCard.Controls.Add(swatch2);

            ModernComboBox cmb2 = new ModernComboBox
            {
                Left = 58,
                Top = 39,
                Width = 222,
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

            Button btnBrowse2 = CreateModernButton("Browse", 286, 38, 74, 25, false);
            btnBrowse2.Click += (s, e) =>
            {
                BrowseForExecutable(cmb2);
                pair.Source2 = ReadComboText(cmb2);
                PushLivePreviewIfActive();
            };
            pairCard.Controls.Add(btnBrowse2);

            Button btnDown = CreateModernButton("▼", 364, 38, 32, 25, false);
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
        if (_keysCard == null || _cycleListCard == null || _behaviorCard == null)
            return;

        int itemCount = _draftSettings.CrossfadeCycleList.Count;

        _cycleSlotsHost.Visible = (itemCount > 0);
        _cycleListCard.Top = 4;
        _cycleListCard.Height = (itemCount == 0) ? 44 : (46 + _cycleSlotsHost.Height);

        _keysCard.Top = _cycleListCard.Bottom + 10;
        _keysCard.FitHeightToContent(12);

        _behaviorCard.Top = _keysCard.Bottom + 10;
        _behaviorCard.FitHeightToContent(12);

        _pageGeneral?.RecalculateScroll();
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

        Win11CardPanel previewCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int py = 10;
        AddSectionHeader(previewCard, "Live Overlay Preview", 14, ref py);

        Button btnShowPreview = CreateModernButton("Show Overlay Preview", 14, py, 195, 29, true);
        btnShowPreview.Click += (s, e) => PushLivePreviewIfActive(forceTrigger: true);
        previewCard.Controls.Add(btnShowPreview);
        py += 36;

        _chkKeepOverlayAlwaysOn = new CheckBox
        {
            Text = "Keep Overlay Always Visible While Settings Window is Open",
            Checked = _draftSettings.KeepOverlayOnWhileSettingsOpen,
            Left = 14,
            Top = py,
            Width = 435,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkKeepOverlayAlwaysOn.CheckedChanged += (s, e) =>
        {
            _draftSettings.KeepOverlayOnWhileSettingsOpen = _chkKeepOverlayAlwaysOn.Checked;
            MarkDirty();
            _context.NotifySettingsWindowState(true, _draftSettings);
        };
        previewCard.Controls.Add(_chkKeepOverlayAlwaysOn);

        previewCard.FitHeightToContent(12);
        page.InnerControls.Add(previewCard);
        y = previewCard.Bottom + 10;

        Win11CardPanel contentStyleCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int cy = 10;
        AddSectionHeader(contentStyleCard, "Overlay Content, Flair & Carousel", 14, ref cy);

        Label lblIdMode = new Label
        {
            Text = "Overlay App Identity:",
            Left = 14,
            Top = cy + 3,
            Width = 165,
            ForeColor = Color.FromArgb(200, 200, 200)
        };
        contentStyleCard.Controls.Add(lblIdMode);

        _cmbIdentityMode = new ModernComboBox
        {
            Left = 185,
            Top = cy,
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
        contentStyleCard.Controls.Add(_cmbIdentityMode);
        cy += 30;

        Label lblLvlMode = new Label
        {
            Text = "Overlay Volume Readout:",
            Left = 14,
            Top = cy + 3,
            Width = 165,
            ForeColor = Color.FromArgb(200, 200, 200)
        };
        contentStyleCard.Controls.Add(lblLvlMode);

        _cmbLevelMode = new ModernComboBox
        {
            Left = 185,
            Top = cy,
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
        contentStyleCard.Controls.Add(_cmbLevelMode);
        cy += 30;

        Label lblCarouselMotion = new Label
        {
            Text = "Carousel Motion Style:",
            Left = 14,
            Top = cy + 3,
            Width = 165,
            ForeColor = Color.FromArgb(200, 200, 200)
        };
        contentStyleCard.Controls.Add(lblCarouselMotion);

        _cmbCarouselStyle = new ModernComboBox
        {
            Left = 185,
            Top = cy,
            Width = 265,
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
        contentStyleCard.Controls.Add(_cmbCarouselStyle);
        cy += 30;

        _chkUseSourceBgColors = new CheckBox
        {
            Text = "Tint Overlay Background with Assigned Source Colors",
            Checked = _draftSettings.UseSourceBackgroundColors,
            Left = 14,
            Top = cy + 2,
            Width = 440,
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
        contentStyleCard.Controls.Add(_chkUseSourceBgColors);
        cy += 26;

        _chkFadeBgWithVolume = new CheckBox
        {
            Text = "Fade Background Color to Dark Grey as Volume Lowers / Mutes",
            Checked = _draftSettings.FadeBgColorWithVolume,
            Enabled = _draftSettings.UseSourceBackgroundColors,
            Left = 14,
            Top = cy + 2,
            Width = 440,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkFadeBgWithVolume.CheckedChanged += (s, e) =>
        {
            _draftSettings.FadeBgColorWithVolume = _chkFadeBgWithVolume.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        contentStyleCard.Controls.Add(_chkFadeBgWithVolume);
        cy += 26;

        _chkShowCarousel = new CheckBox
        {
            Text = "Show Carousel Deck When Cycling Source Pairs",
            Checked = _draftSettings.CarouselVisibleNeighbors > 0.05f,
            Left = 14,
            Top = cy + 2,
            Width = 440,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkShowCarousel.CheckedChanged += (s, e) =>
        {
            _draftSettings.CarouselVisibleNeighbors = _chkShowCarousel.Checked ? 1.5f : 0.0f;
            if (_chkEnableAnimations != null) _chkEnableAnimations.Enabled = _chkShowCarousel.Checked;
            if (_chkCarouselBg != null) _chkCarouselBg.Enabled = _chkShowCarousel.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        contentStyleCard.Controls.Add(_chkShowCarousel);
        cy += 26;

        _chkEnableAnimations = new CheckBox
        {
            Text = "Enable Carousel Animation / Motion",
            Checked = _draftSettings.EnableCarouselAnimation,
            Enabled = _draftSettings.CarouselVisibleNeighbors > 0.05f,
            Left = 14,
            Top = cy + 2,
            Width = 440,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkEnableAnimations.CheckedChanged += (s, e) =>
        {
            _draftSettings.EnableCarouselAnimation = _chkEnableAnimations.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        contentStyleCard.Controls.Add(_chkEnableAnimations);
        cy += 26;

        _chkCarouselBg = new CheckBox
        {
            Text = "Enable Carousel Background Container",
            Checked = _draftSettings.CarouselBackgroundEnabled,
            Enabled = _draftSettings.CarouselVisibleNeighbors > 0.05f,
            Left = 14,
            Top = cy + 2,
            Width = 440,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkCarouselBg.CheckedChanged += (s, e) =>
        {
            _draftSettings.CarouselBackgroundEnabled = _chkCarouselBg.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        contentStyleCard.Controls.Add(_chkCarouselBg);

        contentStyleCard.FitHeightToContent(12);
        page.InnerControls.Add(contentStyleCard);
        y = contentStyleCard.Bottom + 10;

        Win11CardPanel appearanceCard = new Win11CardPanel
        {
            Left = 18,
            Top = y,
            Width = 468
        };

        int ay = 10;
        AddSectionHeader(appearanceCard, "Overlay Colors, Position & Animation", 14, ref ay);

        _unmutedColorBox = AddColorPickerRow(appearanceCard, "Unmuted Bar Color:", _draftSettings.UnmutedColor, (hex) =>
        {
            _draftSettings.UnmutedColor = hex;
            MarkDirty();
            PushLivePreviewIfActive();
        }, ref ay);

        _mutedColorBox = AddColorPickerRow(appearanceCard, "Muted Bar Color:", _draftSettings.MutedColor, (hex) =>
        {
            _draftSettings.MutedColor = hex;
            MarkDirty();
            PushLivePreviewIfActive();
        }, ref ay);

        Label lblPos = new Label
        {
            Text = "Overlay Screen Position:",
            Left = 14,
            Top = ay + 3,
            Width = 165,
            ForeColor = Color.FromArgb(200, 200, 200)
        };
        appearanceCard.Controls.Add(lblPos);

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
        appearanceCard.Controls.Add(_cmbPosition);
        ay += 30;

        Label lblDur = new Label
        {
            Text = "Popup Duration (ms):",
            Left = 14,
            Top = ay + 3,
            Width = 165,
            ForeColor = Color.FromArgb(200, 200, 200)
        };
        appearanceCard.Controls.Add(lblDur);

        _txtPopupDuration = new TextBox
        {
            Text = _draftSettings.PopupDurationMs.ToString(),
            Left = 185,
            Top = ay,
            Width = 110,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtPopupDuration.TextChanged += (s, e) => MarkDirty();
        appearanceCard.Controls.Add(_txtPopupDuration);
        ay += 28;

        Label lblSize = new Label
        {
            Text = "Overlay Size (Width × Height):",
            Left = 14,
            Top = ay + 3,
            Width = 170,
            ForeColor = Color.FromArgb(200, 200, 200)
        };
        appearanceCard.Controls.Add(lblSize);

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
        appearanceCard.Controls.Add(_txtWidth);

        Label lblBy = new Label
        {
            Text = "×",
            Left = 270,
            Top = ay + 3,
            Width = 16,
            ForeColor = Color.FromArgb(180, 180, 180)
        };
        appearanceCard.Controls.Add(lblBy);

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
        appearanceCard.Controls.Add(_txtHeight);
        ay += 30;

        _chkInterpolation = new CheckBox
        {
            Text = "Overlay Bar & Color Interpolation",
            Checked = _draftSettings.EnableInterpolation,
            Left = 14,
            Top = ay + 2,
            Width = 222,
            Height = 22,
            ForeColor = Color.FromArgb(230, 230, 230)
        };
        _chkInterpolation.CheckedChanged += (s, e) =>
        {
            if (_txtInterpSpeed != null) _txtInterpSpeed.Enabled = _chkInterpolation.Checked;
            MarkDirty();
            PushLivePreviewIfActive();
        };
        appearanceCard.Controls.Add(_chkInterpolation);

        Label lblSpeed = new Label
        {
            Text = "Speed (5-100):",
            Left = 240,
            Top = ay + 2,
            Width = 92,
            Height = 20,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.FromArgb(200, 200, 200)
        };
        appearanceCard.Controls.Add(lblSpeed);

        _txtInterpSpeed = new TextBox
        {
            Text = _draftSettings.InterpolationSpeed.ToString(),
            Left = lblSpeed.Right + 4,
            Top = ay + 1,
            Width = 58,
            Enabled = _draftSettings.EnableInterpolation,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtInterpSpeed.TextChanged += (s, e) => MarkDirty();
        appearanceCard.Controls.Add(_txtInterpSpeed);

        appearanceCard.FitHeightToContent(12);
        page.InnerControls.Add(appearanceCard);
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
        _chkEnableAnimations.Checked = settings.EnableCarouselAnimation;
        _chkEnableAnimations.Enabled = _chkShowCarousel.Checked;
        _chkCarouselBg.Checked = settings.CarouselBackgroundEnabled;
        _chkCarouselBg.Enabled = _chkShowCarousel.Checked;

        if (_unmutedColorBox != null)
            _unmutedColorBox.BackColor = SafeParseHexColor(settings.UnmutedColor, Color.FromArgb(0, 168, 255));
        if (_mutedColorBox != null)
            _mutedColorBox.BackColor = SafeParseHexColor(settings.MutedColor, Color.FromArgb(255, 71, 87));

        _cmbPosition.SelectedItem = settings.OverlayPosition;
        _txtPopupDuration.Text = settings.PopupDurationMs.ToString();
        _txtWidth.Text = settings.OverlayWidth.ToString();
        _txtHeight.Text = settings.OverlayHeight.ToString();
        _chkInterpolation.Checked = settings.EnableInterpolation;
        _txtInterpSpeed.Text = settings.InterpolationSpeed.ToString();
        _txtInterpSpeed.Enabled = settings.EnableInterpolation;

        ReflowGeneralPage();
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

        Label lblInfo = new Label
        {
            Text = "Create program groups to control multiple applications together:",
            Left = 18,
            Top = 10,
            Width = 335,
            Height = 22,
            ForeColor = Color.FromArgb(190, 190, 190)
        };
        _pageGroups.InnerControls.Add(lblInfo);

        Button btnAddGroup = CreateModernButton("+ New Group", 360, 6, 126, 28, true);
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
                Width = 468,
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

            TextBox txtGroupName = new TextBox
            {
                Text = group.GroupName,
                Left = 58,
                Top = 11,
                Width = 272,
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

            Button btnRemoveGroup = new Button
            {
                Text = "Delete Group",
                Left = 340,
                Top = 10,
                Width = 114,
                Height = 25,
                BackColor = Color.FromArgb(165, 45, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnRemoveGroup.FlatAppearance.BorderSize = 0;
            btnRemoveGroup.Click += (s, e) =>
            {
                _draftSettings.Groups.Remove(group);
                RefreshGroupsTabUI();
                MarkDirty();
            };
            card.Controls.Add(btnRemoveGroup);

            int innerY = 44;
            for (int i = 0; i < group.Executables.Count; i++)
            {
                int index = i;
                ModernComboBox cmbExe = new ModernComboBox
                {
                    Left = 12,
                    Top = innerY,
                    Width = 314,
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

                Button btnBrowseExe = CreateModernButton("Browse...", 332, innerY - 1, 82, 25, false);
                btnBrowseExe.Click += (s, e) =>
                {
                    BrowseForExecutable(cmbExe);
                    group.Executables[index] = ReadComboText(cmbExe);
                };
                card.Controls.Add(btnBrowseExe);

                Button btnRemoveExe = CreateModernButton("✕", 420, innerY - 1, 34, 25, false);
                btnRemoveExe.Click += (s, e) =>
                {
                    group.Executables.RemoveAt(index);
                    RefreshGroupsTabUI();
                    MarkDirty();
                };
                card.Controls.Add(btnRemoveExe);

                innerY += 30;
            }

            Button btnAddExe = CreateModernButton("+ Add Program to Group", 12, innerY + 2, 185, 25, false);
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

    private (TextBox txt, Button btn) AddKeyBindControls(Control parent, ref int top, Action<int>? onCustomCommit, bool allowClear = false)
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

        Button btnRecord = CreateModernButton("Record Combo", 352, top - 1, 102, 25, false);
        btnRecord.Click += (s, e) => StartRecording(btnRecord, txt, onCustomCommit);
        parent.Controls.Add(btnRecord);

        if (allowClear)
        {
            Button btnClear = CreateModernButton("✕", 314, top - 1, 32, 25, false);
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

    private Panel AddColorPickerRow(Control parent, string labelText, string initialHex, Action<string> onColorChanged, ref int top)
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

        Panel colorBox = new Panel
        {
            Left = 185,
            Top = top,
            Width = 265,
            Height = 22,
            BackColor = SafeParseHexColor(initialHex, Color.FromArgb(0, 168, 255)),
            Cursor = Cursors.Hand
        };

        colorBox.Paint += (s, e) =>
        {
            using Pen p = new Pen(Color.FromArgb(90, 90, 90));
            e.Graphics.DrawRectangle(p, 0, 0, colorBox.Width - 1, colorBox.Height - 1);
        };

        colorBox.Click += (s, e) =>
        {
            using ColorDialog dlg = new ColorDialog { FullOpen = true };
            try { dlg.Color = colorBox.BackColor; } catch { }
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                colorBox.BackColor = dlg.Color;
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

    private void StartRecording(Button? btn, TextBox targetBox, Action<int>? onCommit = null)
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
        }
    }

    private void StopRecording()
    {
        if (!_isRecording) return;
        if (_activeRecordButton != null)
        {
            _activeRecordButton.Text = _previousButtonText;
            _activeRecordButton.BackColor = Color.FromArgb(52, 52, 52);
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
        MarkClean();
    }
}

// Hardware-Accelerated Per-Pixel Alpha Layered Overlay supporting 3D Floating Pills & Fluid Moving Deck with Toggles
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

    

    public CrossfadeOverlayForm(string app1, string app2, AppSettings settings)
    {
        _app1Label = app1;
        _app2Label = app2;
        _rawSource1 = settings.Source1;
        _rawSource2 = settings.Source2;
        _color1Hex = settings.Source1Color;
        _color2Hex = settings.Source2Color;
        _settings = settings.Clone();

        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.ShowInTaskbar = false;
        this.TopMost = true;

        _totalDurationMs = Math.Max(500, _settings.PopupDurationMs);
        SnapInterpolatedColorsImmediate();

        UpdateFixedCanvasGeometry();

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
            // When only 1 pair is configured, add the swapped pair so the user can still preview the carousel
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

            // Pre-warm icons for all cycle items immediately so icons never pop in mid-animation
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

        // Ample symmetric room above and below the hero card so the container's rounded corners are never clipped
        int maxWingPx = (int)Math.Ceiling(baseH * 1.18f) + 12;
        int totalH = baseH + (maxWingPx * 2);
        int canvasW = baseW + 24;

        Screen targetScreen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        try
        {
            IntPtr fgHwnd = GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
            {
                targetScreen = Screen.FromHandle(fgHwnd);
            }
        }
        catch { }

        Rectangle bounds = targetScreen.Bounds;
        int baseX = bounds.Left + (bounds.Width - canvasW) / 2;
        int bottomSafeY = bounds.Bottom - baseH - maxWingPx - 18;
        int topSafeY = bounds.Top + maxWingPx + 18;
        int baseY = bottomSafeY;

        switch (_settings.OverlayPosition)
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
        int clampedWindowTop = Math.Clamp(desiredWindowTop, bounds.Top + 8, Math.Max(bounds.Top + 8, bounds.Bottom - totalH - 8));

        // Always keep the hero card centered inside the window canvas so top and bottom wings have identical space
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
        // Sized to show 1 full neighbor + half of the 2nd neighbor above and below, matching the 3D Floating Pills size
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

            // 1. Draw the background container (if enabled) for BOTH "Fluid Moving Deck" and "3D Floating Pills"
            if (_settings.CarouselBackgroundEnabled)
            {
                int backdropAlpha = (int)Math.Clamp(242 * _wheelExpansion, 0, 242);
                using SolidBrush backdropBrush = new SolidBrush(Color.FromArgb(backdropAlpha, BackdropCharcoal));
                g.FillPath(backdropBrush, containerPath);
            }

            // 2. Clip all moving cards/pills strictly inside the rounded container so top & bottom edges cut off cleanly
            GraphicsState clipState = g.Save();
            g.SetClip(containerPath, CombineMode.Intersect);

            if (style == "Fluid Moving Deck")
            {
                int[] deckOrder = new[] { -2, 2, -1, 1, 0 };
                foreach (int offset in deckOrder)
                {
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

            // 3. Draw the container's outer border on top so clipped cards sit cleanly behind the frame
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
        int iconPx = icon != null ? (int)Math.Clamp(20 * scale, 14, 36) : 0;

        float fontSize = Math.Clamp(11.5f * scale, 6.5f, 28f);
        using Font font = new Font("Segoe UI", fontSize, FontStyle.Bold);

        string namePart = showName ? label : "";
        SizeF nameSize = string.IsNullOrEmpty(namePart) ? SizeF.Empty : g.MeasureString(namePart, font);

        string pctText = isMuted ? "0%" : $"{(int)Math.Round(vol * 100)}%";
        SizeF fixedPctSlotSize = showInlinePercent ? g.MeasureString("100%", font) : SizeF.Empty;

        float iconGap = (icon != null && (!string.IsNullOrEmpty(namePart) || showInlinePercent)) ? (6f * scale) : 0f;
        float pctGap = (!string.IsNullOrEmpty(namePart) && showInlinePercent) ? (6f * scale) : 0f;

        float totalW = iconPx + iconGap + nameSize.Width + pctGap + fixedPctSlotSize.Width;
        float textHeight = Math.Max(nameSize.Height, fixedPctSlotSize.Height);

        float startX = bounds.X + (bounds.Width - totalW) / 2f;
        if (horizontalAlign == StringAlignment.Near) startX = bounds.X;
        else if (horizontalAlign == StringAlignment.Far) startX = bounds.Right - totalW;

        startX = Math.Max(bounds.X, startX);
        float centerY = bounds.Y + bounds.Height / 2f;
        float iconOpacity = textColorOverride.A / 255f;

        using SolidBrush textBrush = new SolidBrush(textColorOverride);

        bool mirrorOrder = (horizontalAlign == StringAlignment.Far);

        if (mirrorOrder)
        {
            if (showInlinePercent)
            {
                RectangleF pctRect = new RectangleF(startX, centerY - textHeight / 2f, fixedPctSlotSize.Width + 4f, textHeight);
                using StringFormat sfPct = new StringFormat
                {
                    Alignment = string.IsNullOrEmpty(namePart) && icon == null ? StringAlignment.Center : StringAlignment.Far,
                    LineAlignment = StringAlignment.Near,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(pctText, font, textBrush, pctRect, sfPct);
                startX += fixedPctSlotSize.Width + pctGap;
            }

            if (!string.IsNullOrEmpty(namePart))
            {
                g.DrawString(namePart, font, textBrush, startX, centerY - textHeight / 2f);
                startX += nameSize.Width + iconGap;
            }

            if (icon != null)
            {
                DrawImageWithAlpha(g, icon, new RectangleF(startX, centerY - iconPx / 2f, iconPx, iconPx), iconOpacity);
            }
        }
        else
        {
            if (icon != null)
            {
                DrawImageWithAlpha(g, icon, new RectangleF(startX, centerY - iconPx / 2f, iconPx, iconPx), iconOpacity);
                startX += iconPx + iconGap;
            }

            if (!string.IsNullOrEmpty(namePart))
            {
                g.DrawString(namePart, font, textBrush, startX, centerY - textHeight / 2f);
                startX += nameSize.Width + pctGap;
            }

            if (showInlinePercent)
            {
                RectangleF pctRect = new RectangleF(startX, centerY - textHeight / 2f, fixedPctSlotSize.Width + 4f, textHeight);
                using StringFormat sfPct = new StringFormat
                {
                    Alignment = string.IsNullOrEmpty(namePart) ? StringAlignment.Center : StringAlignment.Near,
                    LineAlignment = StringAlignment.Near,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(pctText, font, textBrush, pctRect, sfPct);
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
        float activeHudEmphasis)
    {
        int width = (int)activeBounds.Width;
        int height = (int)activeBounds.Height;
        float topY = activeBounds.Y;
        int centerX = (int)(activeBounds.X + width / 2f);

        bool showName = _settings.OverlayIdentityMode == "Name" || _settings.OverlayIdentityMode == "Both";
        bool showIcon = _settings.OverlayIdentityMode == "Icon" || _settings.OverlayIdentityMode == "Both";
        bool showBar = (_settings.OverlayLevelMode == "Bar" || _settings.OverlayLevelMode == "Both") && activeHudEmphasis > 0.05f;
        bool showPct = (_settings.OverlayLevelMode == "Percentage" || _settings.OverlayLevelMode == "Both") && activeHudEmphasis > 0.35f;

        Color mutedColor = ParseOverlayColor(_settings.MutedColor, Color.FromArgb(255, 71, 87));

        int centerLineTop = (int)topY + (int)(10 * scale);
        int centerLineHeight = Math.Max(10, height - (int)(20 * scale));
        using (SolidBrush dividerBrush = new SolidBrush(Color.FromArgb(140, 235, 235, 240)))
        {
            g.FillRectangle(dividerBrush, centerX - 1, centerLineTop, 2, centerLineHeight);
        }

        bool app1Muted = (_muteState == 2);
        bool app2Muted = (_muteState == 1);
        Color text1Color = app1Muted ? mutedColor : Color.White;
        Color text2Color = app2Muted ? mutedColor : Color.White;
        int sideMargin = 18;

        // When a card is inactive in the Fluid Moving Deck, center the header vertically; when active, position it in the upper half above the volume bar
        float headerYOffset = Math.Clamp(activeHudEmphasis, 0f, 1f);
        float headerTop = topY + (6f * scale * headerYOffset) + ((height * 0.26f) * (1f - headerYOffset));

        float halfW = width / 2f;
        RectangleF leftHeader = new RectangleF(activeBounds.X + sideMargin, headerTop, halfW - sideMargin - 10, height * 0.48f);
        RectangleF rightHeader = new RectangleF(centerX + 10, headerTop, halfW - sideMargin - 10, height * 0.48f);

        DrawHeaderBadge(g, leftHeader, label1, rawSource1, vol1, app1Muted, scale, showName, showIcon, showPct && showBar, StringAlignment.Far, text1Color);
        DrawHeaderBadge(g, rightHeader, label2, rawSource2, vol2, app2Muted, scale, showName, showIcon, showPct && showBar, StringAlignment.Near, text2Color);

        if (showBar)
        {
            int barAlpha = (int)Math.Clamp(activeHudEmphasis * 255f, 0f, 255f);
            int trackAlpha = (int)Math.Clamp(activeHudEmphasis * 135f, 0f, 135f);

            int barWidth = Math.Max(20, (int)halfW - sideMargin - 8);
            int barHeight = Math.Clamp((int)(14 * scale), 8, 40);
            int barY = (int)topY + Math.Clamp((int)(height * 0.60f), 10, Math.Max(10, height - barHeight - 6));

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
        else if (showPct)
        {
            int pctAlpha = (int)Math.Clamp(activeHudEmphasis * 255f, 0f, 255f);
            string p1 = app1Muted ? "MUTED" : $"{(int)Math.Round(vol1 * 100)}%";
            string p2 = app2Muted ? "MUTED" : $"{(int)Math.Round(vol2 * 100)}%";

            Color unmutedCol = ParseOverlayColor(_settings.UnmutedColor, Color.FromArgb(0, 168, 255));
            using Font bigFont = new Font("Segoe UI", Math.Clamp(16f * scale, 9f, 40f), FontStyle.Bold);
            using SolidBrush b1 = new SolidBrush(Color.FromArgb(pctAlpha, app1Muted ? mutedColor : unmutedCol));
            using SolidBrush b2 = new SolidBrush(Color.FromArgb(pctAlpha, app2Muted ? mutedColor : unmutedCol));
            using StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            g.DrawString(p1, bigFont, b1, new RectangleF(activeBounds.X + sideMargin, topY + height * 0.44f, halfW - sideMargin - 6, height * 0.50f), sf);
            g.DrawString(p2, bigFont, b2, new RectangleF(centerX + 6, topY + height * 0.44f, halfW - sideMargin - 6, height * 0.50f), sf);
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

        // Smoothly interpolate card height & width between active Hero card (at dist=0) and compact deck cards (at dist>=1)
        float activeBlend = Math.Clamp(1.0f - dist, 0f, 1f);
        float inactiveCardH = Math.Clamp(baseH * 0.54f, 30f, 66f);
        float cardH = inactiveCardH + (heroCardRect.Height - inactiveCardH) * activeBlend;

        float widthRatio = Math.Clamp(1.0f - (dist * 0.06f), 0.82f, 1.0f);
        float cardW = heroCardRect.Width * widthRatio;
        float cardX = heroCardRect.X + (heroCardRect.Width - cardW) / 2f;

        float heroCenterY = heroCardRect.Y + heroCardRect.Height / 2f;
        float stepPitch = (heroCardRect.Height / 2f) + (inactiveCardH / 2f) + (6f * scale);
        float cardCenterY = heroCenterY + (signedVisualOffset * stepPitch * _wheelExpansion);
        RectangleF cardRect = new RectangleF(cardX, cardCenterY - cardH / 2f, cardW, cardH);

        if (cardRect.Bottom < 0 || cardRect.Top > this.Height) return;

        float visibility = isActiveSlot
            ? 1.0f
            : Math.Clamp((1.0f - Math.Max(0f, dist - 1.0f) * 0.55f) * _wheelExpansion, 0f, 1f);
        int cardAlpha = (int)Math.Clamp(visibility * 245f, 0f, 255f);
        if (cardAlpha < 10) return;

        Color bgLeft;
        Color bgRight;

        if (isActiveSlot)
        {
            bgLeft = _renderedBgLeft;
            bgRight = _renderedBgRight;
        }
        else
        {
            Color rawC1 = ParseOverlayColor(item.Color1Hex, Color.FromArgb(27, 59, 90));
            Color rawC2 = ParseOverlayColor(item.Color2Hex, Color.FromArgb(74, 32, 64));
            float tintKeep = Math.Clamp(0.65f - (dist - 1.0f) * 0.20f, 0.30f, 0.70f);
            bgLeft = _settings.UseSourceBackgroundColors ? ColorLerp(DefaultDarkGrey, rawC1, tintKeep) : DefaultDarkGrey;
            bgRight = _settings.UseSourceBackgroundColors ? ColorLerp(DefaultDarkGrey, rawC2, tintKeep) : DefaultDarkGrey;
        }

        Color finalLeft = Color.FromArgb(cardAlpha, bgLeft);
        Color finalRight = Color.FromArgb(cardAlpha, bgRight);

        float radius = GetWin11CornerRadius(scale * widthRatio);
        using GraphicsPath cardPath = Win11CardPanel.CreateRoundRectF(cardRect, radius);
        FillRoundedCardGradient(g, cardPath, cardRect, finalLeft, finalRight);

        if (activeBlend > 0.05f)
        {
            // Only the active card shows the actual crossfade volume bar; as it settles into center, the bar fades in smoothly
            DrawActiveHUDContent(
                g,
                cardRect,
                item.Label1,
                item.Label2,
                item.RawSource1,
                item.RawSource2,
                _app1Vol,
                _app2Vol,
                scale * widthRatio,
                activeBlend);
        }
        else
        {
            // Inactive cards in the Fluid Moving Deck show clean identity headers without any volume sliders
            float centerX = cardRect.X + cardRect.Width / 2f;
            float centerY = cardRect.Y + cardRect.Height / 2f;
            int textAlpha = (int)Math.Clamp(visibility * 240f, 0f, 240f);

            using (Pen divPen = new Pen(Color.FromArgb((int)(textAlpha * 0.45f), 230, 230, 235), 1.5f))
            {
                g.DrawLine(divPen, centerX, cardRect.Y + cardRect.Height * 0.22f, centerX, cardRect.Bottom - cardRect.Height * 0.22f);
            }

            bool showName = _settings.OverlayIdentityMode == "Name" || _settings.OverlayIdentityMode == "Both";
            bool showIcon = _settings.OverlayIdentityMode == "Icon" || _settings.OverlayIdentityMode == "Both";

            RectangleF leftZone = new RectangleF(cardRect.X + 14f * scale, cardRect.Y + 2f, (cardRect.Width / 2f) - 24f * scale, cardRect.Height - 4f);
            RectangleF rightZone = new RectangleF(centerX + 10f * scale, cardRect.Y + 2f, (cardRect.Width / 2f) - 24f * scale, cardRect.Height - 4f);

            DrawPillSideIdentity(g, leftZone, item.Label1, item.RawSource1, textAlpha, scale * 0.88f, showName, showIcon, isRightSide: false);
            DrawPillSideIdentity(g, rightZone, item.Label2, item.RawSource2, textAlpha, scale * 0.88f, showName, showIcon, isRightSide: true);
        }

        int rimAlpha = isActiveSlot ? 220 : (int)Math.Clamp(cardAlpha * 0.55f, 0, 160);
        using Pen rimPen = new Pen(Color.FromArgb(rimAlpha, 255, 255, 255), isActiveSlot ? 1.5f : 1.1f);
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