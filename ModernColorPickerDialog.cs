using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

public class ModernColorPickerDialog : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private static readonly string[,] GoogleDocsPalette = new string[8, 10]
    {
        { "#000000", "#1C1C1C", "#393939", "#555555", "#717171", "#8E8E8E", "#AAAAAA", "#C6C6C6", "#E3E4E3", "#FFFFFF" },
        { "#980000", "#FF0000", "#FF9900", "#FFFF00", "#00FF00", "#00FFFF", "#4A86E8", "#0000FF", "#9900FF", "#FF00FF" },
        { "#E6B8AF", "#F4CCCC", "#FCE5CD", "#FFF2CC", "#D9EAD3", "#D0E0E3", "#C9DAF8", "#CFE2F3", "#D9D2E9", "#EAD1DC" },
        { "#DD7E6B", "#EA9999", "#F9CB9C", "#FFE599", "#B6D7A8", "#A2C4C9", "#A4C2F4", "#9FC5E8", "#B4A7D6", "#D5A6BD" },
        { "#CC4125", "#E06666", "#F6B26B", "#FFD966", "#93C47D", "#76A5AF", "#6D9EEB", "#6FA8DC", "#8E7CC3", "#C27BA0" },
        { "#A61C00", "#CC0000", "#E69138", "#F1C232", "#6AA84F", "#45818E", "#3C78D8", "#3D85C6", "#674EA7", "#A64D79" },
        { "#85200C", "#990000", "#B45F06", "#BF9000", "#38761D", "#134F5C", "#1155CC", "#0B5394", "#351C75", "#741B47" },
        { "#5B0F00", "#660000", "#783F04", "#7F6000", "#274E13", "#0C343D", "#1C4587", "#073763", "#20124D", "#4C1130" }
    };

    private readonly AppSettings _settings;
    private readonly Color _initialColor;
    private readonly Action<Color>? _onLiveColorChanged;

    private float _hue = 200f; // 0..360
    private float _sat = 0.8f; // 0..1
    private float _val = 0.9f; // 0..1
    private Color _currentColor;
    private bool _suppressTextEvents = false;

    private Bitmap? _svBitmap;
    private Bitmap? _hueBitmap;
    private float _cachedSvHue = -1f;

    private readonly BufferedPanel _svBox;
    private readonly BufferedPanel _hueBar;
    private readonly BufferedPanel _rSlider;
    private readonly BufferedPanel _gSlider;
    private readonly BufferedPanel _bSlider;
    private readonly TextBox _txtR;
    private readonly TextBox _txtG;
    private readonly TextBox _txtB;
    private readonly TextBox _txtHex;
    private readonly BufferedPanel _previewPanel;
    private readonly BufferedPanel _paletteGrid;
    private readonly BufferedPanel _historyGrid;
    private readonly ModernButton _btnPinToggle;
    private readonly ToolTip _toolTip = new ToolTip();

    private bool _draggingSv = false;
    private bool _draggingHue = false;
    private int _draggingRgbChannel = -1; // 0=R, 1=G, 2=B

    private Point _hoveredPaletteCell = new Point(-1, -1);
    private int _hoveredHistorySlot = -1;

    public Color SelectedColor => _currentColor;
    public string SelectedHex => $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";

    public ModernColorPickerDialog(Color initialColor, AppSettings settings, Action<Color>? onLiveColorChanged = null)
    {
        _settings = settings;
        _initialColor = initialColor;
        _onLiveColorChanged = onLiveColorChanged;
        _currentColor = Color.FromArgb(255, initialColor.R, initialColor.G, initialColor.B);
        RgbToHsv(_currentColor, out _hue, out _sat, out _val);

        this.Text = "Select Color";
        this.ClientSize = new Size(524, 492);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = Color.FromArgb(28, 28, 28);
        this.ForeColor = Color.FromArgb(242, 242, 242);
        this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

        // --- ONE BIG SINGLE UNIFIED CARD PANEL FOR ALL CONTROLS ---
        Win11CardPanel mainCard = new Win11CardPanel
        {
            Left = 14,
            Top = 14,
            Width = 496,
            Height = 420
        };

        // --- LEFT COLUMN (Swapped): Theme & Standard Colors + History ---
        Label lblPaletteTitle = new Label
        {
            Text = "Theme && Standard Colors",
            Left = 14,
            Top = 12,
            Width = 210,
            Height = 20,
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 205, 255)
        };
        mainCard.Controls.Add(lblPaletteTitle);

        _paletteGrid = new BufferedPanel
        {
            Left = 14,
            Top = 38,
            Width = 220,
            Height = 190,
            Cursor = Cursors.Hand
        };
        _paletteGrid.Paint += PaintGoogleDocsPalette;
        _paletteGrid.MouseMove += OnPaletteMouseMove;
        _paletteGrid.MouseLeave += (s, e) => { _hoveredPaletteCell = new Point(-1, -1); _paletteGrid.Invalidate(); };
        _paletteGrid.MouseDown += OnPaletteMouseDown;
        mainCard.Controls.Add(_paletteGrid);

        Label lblCustomTitle = new Label
        {
            Text = "Custom Colors",
            Left = 14,
            Top = 242,
            Width = 135,
            Height = 18,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 205, 255)
        };
        mainCard.Controls.Add(lblCustomTitle);

        _btnPinToggle = new ModernButton
        {
            Text = "📌 Pin",
            Left = 152,
            Top = 238,
            Width = 82,
            Height = 24,
            CornerRadius = 5,
            BackColor = Color.FromArgb(52, 52, 52),
            HoverBackColor = Color.FromArgb(68, 68, 68),
            PressedBackColor = Color.FromArgb(44, 44, 44),
            BorderColor = Color.FromArgb(80, 80, 80),
            Font = new Font("Segoe UI Semibold", 7.75f, FontStyle.Bold)
        };
        _btnPinToggle.Click += (s, e) => TogglePinForColor(SelectedHex);
        mainCard.Controls.Add(_btnPinToggle);

        Label lblCustomHint = new Label
        {
            Text = "Left-click: Select  •  Right-click: Pin/Unpin",
            Left = 14,
            Top = 264,
            Width = 220,
            Height = 16,
            Font = new Font("Segoe UI", 7.75f, FontStyle.Regular),
            ForeColor = Color.FromArgb(165, 165, 165)
        };
        mainCard.Controls.Add(lblCustomHint);

        _historyGrid = new BufferedPanel
        {
            Left = 14,
            Top = 284,
            Width = 220,
            Height = 52,
            Cursor = Cursors.Hand
        };
        _historyGrid.Paint += PaintHistoryGrid;
        _historyGrid.MouseMove += OnHistoryMouseMove;
        _historyGrid.MouseLeave += (s, e) => { _hoveredHistorySlot = -1; _historyGrid.Invalidate(); };
        _historyGrid.MouseDown += OnHistoryMouseDown;
        mainCard.Controls.Add(_historyGrid);

        // --- RIGHT COLUMN (Swapped): Color Spectrum & RGB Sliders ---
        int rightX = 254;

        Label lblPickerTitle = new Label
        {
            Text = "Color Spectrum && RGB",
            Left = rightX,
            Top = 12,
            Width = 200,
            Height = 20,
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 205, 255)
        };
        mainCard.Controls.Add(lblPickerTitle);

        _svBox = new BufferedPanel
        {
            Left = rightX,
            Top = 38,
            Width = 186,
            Height = 186,
            Cursor = Cursors.Cross
        };
        _svBox.Paint += PaintSvBox;
        _svBox.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _draggingSv = true; UpdateSvFromMouse(e.Location); } };
        _svBox.MouseMove += (s, e) => { if (_draggingSv) UpdateSvFromMouse(e.Location); };
        _svBox.MouseUp += (s, e) => _draggingSv = false;
        mainCard.Controls.Add(_svBox);

        _hueBar = new BufferedPanel
        {
            Left = rightX + 194,
            Top = 38,
            Width = 24,
            Height = 186,
            Cursor = Cursors.Hand
        };
        _hueBar.Paint += PaintHueBar;
        _hueBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _draggingHue = true; UpdateHueFromMouse(e.Y); } };
        _hueBar.MouseMove += (s, e) => { if (_draggingHue) UpdateHueFromMouse(e.Y); };
        _hueBar.MouseUp += (s, e) => _draggingHue = false;
        mainCard.Controls.Add(_hueBar);

        int ry = 236;
        (_rSlider, _txtR) = CreateRgbRow(mainCard, "R", 0, rightX, ref ry);
        (_gSlider, _txtG) = CreateRgbRow(mainCard, "G", 1, rightX, ref ry);
        (_bSlider, _txtB) = CreateRgbRow(mainCard, "B", 2, rightX, ref ry);

        Label lblHex = new Label
        {
            Text = "Hex:",
            Left = rightX,
            Top = ry + 6,
            Width = 34,
            Height = 20,
            Font = new Font("Segoe UI Semibold", 8.75f, FontStyle.Bold),
            ForeColor = Color.FromArgb(200, 200, 200)
        };
        mainCard.Controls.Add(lblHex);

        _txtHex = new TextBox
        {
            Left = rightX + 36,
            Top = ry + 3,
            Width = 76,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9.5f, FontStyle.Bold)
        };
        _txtHex.TextChanged += OnHexTextChanged;
        mainCard.Controls.Add(_txtHex);

        _previewPanel = new BufferedPanel
        {
            Left = rightX + 120,
            Top = ry + 1,
            Width = 98,
            Height = 28
        };
        _previewPanel.Paint += PaintPreviewComparison;
        _toolTip.SetToolTip(_previewPanel, "Left: New Color | Right: Original Color (Click right half to revert)");
        _previewPanel.MouseDown += (s, e) =>
        {
            if (e.X >= _previewPanel.Width / 2)
            {
                SetColorFromRgb(_initialColor, updateHexBox: true);
            }
        };
        mainCard.Controls.Add(_previewPanel);

        this.Controls.Add(mainCard);

        // --- BOTTOM ACTION BUTTONS ---
        ModernButton btnCancel = new ModernButton
        {
            Text = "Cancel",
            Left = 266,
            Top = 446,
            Width = 116,
            Height = 32,
            CornerRadius = 6,
            BackColor = Color.FromArgb(52, 52, 52),
            HoverBackColor = Color.FromArgb(66, 66, 66),
            PressedBackColor = Color.FromArgb(44, 44, 44),
            BorderColor = Color.FromArgb(70, 70, 70)
        };
        btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
        this.Controls.Add(btnCancel);

        ModernButton btnApply = new ModernButton
        {
            Text = "Apply Color",
            Left = 390,
            Top = 446,
            Width = 120,
            Height = 32,
            CornerRadius = 6,
            BackColor = Color.FromArgb(0, 120, 212),
            HoverBackColor = Color.FromArgb(20, 138, 230),
            PressedBackColor = Color.FromArgb(0, 100, 180),
            BorderColor = Color.FromArgb(30, 144, 255)
        };
        btnApply.Click += (s, e) =>
        {
            _settings.RecordCustomColorHistory(SelectedHex);
            this.DialogResult = DialogResult.OK;
            this.Close();
        };
        this.Controls.Add(btnApply);

        BuildHueBitmap();
        SyncUiFromColor(updateHexBox: true);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            int dark = 1;
            DwmSetWindowAttribute(this.Handle, 20, ref dark, sizeof(int));
            int corners = 2;
            DwmSetWindowAttribute(this.Handle, 33, ref corners, sizeof(int));
        }
        catch { }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _svBitmap?.Dispose();
        _hueBitmap?.Dispose();
        base.OnFormClosed(e);
    }

    private (BufferedPanel slider, TextBox box) CreateRgbRow(Control parent, string channelLabel, int channelIndex, int startX, ref int topY)
    {
        Label lbl = new Label
        {
            Text = channelLabel,
            Left = startX,
            Top = topY + 3,
            Width = 16,
            Height = 18,
            Font = new Font("Segoe UI Semibold", 8.75f, FontStyle.Bold),
            ForeColor = channelIndex switch
            {
                0 => Color.FromArgb(255, 115, 115),
                1 => Color.FromArgb(115, 235, 145),
                _ => Color.FromArgb(115, 185, 255)
            }
        };
        parent.Controls.Add(lbl);

        BufferedPanel bar = new BufferedPanel
        {
            Left = startX + 18,
            Top = topY + 4,
            Width = 138,
            Height = 16,
            Cursor = Cursors.Hand
        };

        bar.Paint += (s, e) => PaintRgbSlider(e.Graphics, bar.ClientRectangle, channelIndex);
        bar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _draggingRgbChannel = channelIndex;
                UpdateRgbFromMouse(channelIndex, e.X, bar.Width);
            }
        };
        bar.MouseMove += (s, e) =>
        {
            if (_draggingRgbChannel == channelIndex)
            {
                UpdateRgbFromMouse(channelIndex, e.X, bar.Width);
            }
        };
        bar.MouseUp += (s, e) => _draggingRgbChannel = -1;
        parent.Controls.Add(bar);

        TextBox txt = new TextBox
        {
            Left = startX + 162,
            Top = topY + 1,
            Width = 56,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Center
        };
        txt.TextChanged += (s, e) =>
        {
            if (_suppressTextEvents) return;
            if (int.TryParse(txt.Text, out int v))
            {
                v = Math.Clamp(v, 0, 255);
                int r = channelIndex == 0 ? v : _currentColor.R;
                int g = channelIndex == 1 ? v : _currentColor.G;
                int b = channelIndex == 2 ? v : _currentColor.B;
                SetColorFromRgb(Color.FromArgb(255, r, g, b), updateHexBox: true);
            }
        };
        parent.Controls.Add(txt);

        topY += 32;
        return (bar, txt);
    }

    private void UpdateSvFromMouse(Point pt)
    {
        _sat = Math.Clamp(pt.X / (float)Math.Max(1, _svBox.Width - 1), 0f, 1f);
        _val = Math.Clamp(1f - (pt.Y / (float)Math.Max(1, _svBox.Height - 1)), 0f, 1f);
        _currentColor = HsvToRgb(_hue, _sat, _val);
        SyncUiFromColor(updateHexBox: true);
    }

    private void UpdateHueFromMouse(int mouseY)
    {
        _hue = Math.Clamp((mouseY / (float)Math.Max(1, _hueBar.Height - 1)) * 360f, 0f, 359.9f);
        _currentColor = HsvToRgb(_hue, _sat, _val);
        SyncUiFromColor(updateHexBox: true);
    }

    private void UpdateRgbFromMouse(int channel, int mouseX, int barW)
    {
        int val = (int)Math.Round(Math.Clamp(mouseX / (float)Math.Max(1, barW - 1), 0f, 1f) * 255f);
        int r = channel == 0 ? val : _currentColor.R;
        int g = channel == 1 ? val : _currentColor.G;
        int b = channel == 2 ? val : _currentColor.B;
        SetColorFromRgb(Color.FromArgb(255, r, g, b), updateHexBox: true);
    }

    private void SetColorFromRgb(Color c, bool updateHexBox)
    {
        _currentColor = Color.FromArgb(255, c.R, c.G, c.B);
        RgbToHsv(_currentColor, out _hue, out _sat, out _val);
        SyncUiFromColor(updateHexBox);
    }

    private void OnHexTextChanged(object? sender, EventArgs e)
    {
        if (_suppressTextEvents) return;
        string raw = _txtHex.Text.Trim();
        if (!raw.StartsWith("#")) raw = "#" + raw;
        if (raw.Length == 7)
        {
            try
            {
                Color parsed = ColorTranslator.FromHtml(raw);
                SetColorFromRgb(parsed, updateHexBox: false);
            }
            catch { }
        }
    }

    private void SyncUiFromColor(bool updateHexBox)
    {
        _suppressTextEvents = true;
        _txtR.Text = _currentColor.R.ToString();
        _txtG.Text = _currentColor.G.ToString();
        _txtB.Text = _currentColor.B.ToString();
        if (updateHexBox)
        {
            _txtHex.Text = SelectedHex;
        }
        _suppressTextEvents = false;

        UpdatePinButtonLabel();

        _svBox.Invalidate();
        _hueBar.Invalidate();
        _rSlider.Invalidate();
        _gSlider.Invalidate();
        _bSlider.Invalidate();
        _previewPanel.Invalidate();
        _paletteGrid.Invalidate();
        _historyGrid.Invalidate();

        _onLiveColorChanged?.Invoke(_currentColor);
    }

    private void UpdatePinButtonLabel()
    {
        string hex = SelectedHex;
        bool isPinned = _settings.PinnedCustomColors.Any(c => c.Equals(hex, StringComparison.OrdinalIgnoreCase));
        _btnPinToggle.Text = isPinned ? "📌 Unpin" : "📌 Pin";
        _btnPinToggle.BackColor = isPinned ? Color.FromArgb(0, 105, 185) : Color.FromArgb(52, 52, 52);
        _btnPinToggle.Invalidate();
    }

    private void TogglePinForColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        string norm = hex.Trim().ToUpperInvariant();
        if (!norm.StartsWith("#")) norm = "#" + norm;

        int existingIdx = _settings.PinnedCustomColors.FindIndex(c => c.Equals(norm, StringComparison.OrdinalIgnoreCase));
        if (existingIdx >= 0)
        {
            _settings.PinnedCustomColors.RemoveAt(existingIdx);
            _settings.RecentCustomColors.RemoveAll(c => c.Equals(norm, StringComparison.OrdinalIgnoreCase));
            _settings.RecentCustomColors.Insert(0, norm);
        }
        else
        {
            _settings.RecentCustomColors.RemoveAll(c => c.Equals(norm, StringComparison.OrdinalIgnoreCase));
            if (_settings.PinnedCustomColors.Count >= 20)
            {
                _settings.PinnedCustomColors.RemoveAt(_settings.PinnedCustomColors.Count - 1);
            }
            _settings.PinnedCustomColors.Add(norm);
        }

        int maxRecent = Math.Max(0, 20 - _settings.PinnedCustomColors.Count);
        if (_settings.RecentCustomColors.Count > maxRecent)
        {
            _settings.RecentCustomColors.RemoveRange(maxRecent, _settings.RecentCustomColors.Count - maxRecent);
        }

        UpdatePinButtonLabel();
        _historyGrid.Invalidate();
    }

    private void EnsureSvBitmap()
    {
        int w = Math.Max(1, _svBox.Width);
        int h = Math.Max(1, _svBox.Height);
        if (_svBitmap != null && _svBitmap.Width == w && _svBitmap.Height == h && Math.Abs(_cachedSvHue - _hue) < 0.1f)
            return;

        _svBitmap?.Dispose();
        _svBitmap = new Bitmap(w, h);
        _cachedSvHue = _hue;

        Color pureHue = HsvToRgb(_hue, 1f, 1f);
        using (Graphics g = Graphics.FromImage(_svBitmap))
        {
            using (var horiz = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.White, pureHue, LinearGradientMode.Horizontal))
            {
                g.FillRectangle(horiz, 0, 0, w, h);
            }
            using (var vert = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(0, 0, 0, 0), Color.Black, LinearGradientMode.Vertical))
            {
                g.FillRectangle(vert, 0, 0, w, h);
            }
        }
    }

    private void BuildHueBitmap()
    {
        int w = Math.Max(1, _hueBar.Width);
        int h = Math.Max(1, _hueBar.Height);
        _hueBitmap?.Dispose();
        _hueBitmap = new Bitmap(w, h);

        for (int y = 0; y < h; y++)
        {
            float hue = (y / (float)Math.Max(1, h - 1)) * 360f;
            Color c = HsvToRgb(hue, 1f, 1f);
            for (int x = 0; x < w; x++)
            {
                _hueBitmap.SetPixel(x, y, c);
            }
        }
    }

    private void PaintSvBox(object? sender, PaintEventArgs e)
    {
        EnsureSvBitmap();
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (_svBitmap != null)
        {
            g.DrawImage(_svBitmap, 0, 0);
        }

        using (Pen borderPen = new Pen(Color.FromArgb(85, 85, 85), 1f))
        {
            g.DrawRectangle(borderPen, 0, 0, _svBox.Width - 1, _svBox.Height - 1);
        }

        float cx = _sat * (_svBox.Width - 1);
        float cy = (1f - _val) * (_svBox.Height - 1);
        using Pen outerRing = new Pen(Color.Black, 2.2f);
        using Pen innerRing = new Pen(Color.White, 1.5f);
        g.DrawEllipse(outerRing, cx - 6f, cy - 6f, 12f, 12f);
        g.DrawEllipse(innerRing, cx - 5f, cy - 5f, 10f, 10f);
    }

    private void PaintHueBar(object? sender, PaintEventArgs e)
    {
        if (_hueBitmap == null || _hueBitmap.Height != _hueBar.Height) BuildHueBitmap();
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (_hueBitmap != null)
        {
            g.DrawImage(_hueBitmap, 0, 0);
        }

        using (Pen borderPen = new Pen(Color.FromArgb(85, 85, 85), 1f))
        {
            g.DrawRectangle(borderPen, 0, 0, _hueBar.Width - 1, _hueBar.Height - 1);
        }

        float y = (_hue / 360f) * (_hueBar.Height - 1);
        RectangleF thumbRect = new RectangleF(1f, Math.Clamp(y - 3f, 0f, _hueBar.Height - 7f), _hueBar.Width - 3f, 6f);
        using Pen darkPen = new Pen(Color.Black, 2f);
        using Pen lightPen = new Pen(Color.White, 1.2f);
        g.DrawRectangle(darkPen, thumbRect.X, thumbRect.Y, thumbRect.Width, thumbRect.Height);
        g.DrawRectangle(lightPen, thumbRect.X, thumbRect.Y, thumbRect.Width, thumbRect.Height);
    }

    private void PaintRgbSlider(Graphics g, Rectangle rect, int channel)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (rect.Width <= 2 || rect.Height <= 2) return;

        Color startCol = channel switch
        {
            0 => Color.FromArgb(0, _currentColor.G, _currentColor.B),
            1 => Color.FromArgb(_currentColor.R, 0, _currentColor.B),
            _ => Color.FromArgb(_currentColor.R, _currentColor.G, 0)
        };
        Color endCol = channel switch
        {
            0 => Color.FromArgb(255, _currentColor.G, _currentColor.B),
            1 => Color.FromArgb(_currentColor.R, 255, _currentColor.B),
            _ => Color.FromArgb(_currentColor.R, _currentColor.G, 255)
        };

        Rectangle trackRect = new Rectangle(0, 3, rect.Width - 1, rect.Height - 7);
        using (LinearGradientBrush grad = new LinearGradientBrush(new Point(0, 0), new Point(rect.Width, 0), startCol, endCol))
        using (GraphicsPath path = Win11CardPanel.CreateRoundRect(trackRect, 4))
        using (Pen borderPen = new Pen(Color.FromArgb(80, 80, 80), 1f))
        {
            g.FillPath(grad, path);
            g.DrawPath(borderPen, path);
        }

        int channelVal = channel switch { 0 => _currentColor.R, 1 => _currentColor.G, _ => _currentColor.B };
        float thumbX = (channelVal / 255f) * (rect.Width - 9f);
        RectangleF thumb = new RectangleF(thumbX, 1f, 8f, rect.Height - 3f);
        using SolidBrush thumbBrush = new SolidBrush(Color.White);
        using Pen thumbBorder = new Pen(Color.FromArgb(25, 25, 25), 1.2f);
        g.FillEllipse(thumbBrush, thumb);
        g.DrawEllipse(thumbBorder, thumb);
    }

    private void PaintPreviewComparison(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(0, 0, _previewPanel.Width - 1, _previewPanel.Height - 1);
        using GraphicsPath clip = Win11CardPanel.CreateRoundRect(r, 6);

        GraphicsState st = g.Save();
        g.SetClip(clip);
        int halfW = _previewPanel.Width / 2;
        using (SolidBrush newBrush = new SolidBrush(_currentColor))
        using (SolidBrush oldBrush = new SolidBrush(_initialColor))
        {
            g.FillRectangle(newBrush, 0, 0, halfW + 1, _previewPanel.Height);
            g.FillRectangle(oldBrush, halfW, 0, _previewPanel.Width - halfW, _previewPanel.Height);
        }
        g.Restore(st);

        using Pen borderPen = new Pen(Color.FromArgb(110, 110, 110), 1f);
        g.DrawPath(borderPen, clip);
    }

    private Rectangle GetPaletteCellRect(int row, int col)
    {
        const int cellSize = 19;
        const int gap = 3;
        int x = col * (cellSize + gap);
        int extraGap = row >= 2 ? 8 : (row == 1 ? 4 : 0);
        int y = row * (cellSize + gap) + extraGap;
        return new Rectangle(x, y, cellSize, cellSize);
    }

    private void PaintGoogleDocsPalette(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        string curHex = SelectedHex;

        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 10; c++)
            {
                string hex = GoogleDocsPalette[r, c];
                Color col = ColorTranslator.FromHtml(hex);
                Rectangle cellRect = GetPaletteCellRect(r, c);

                bool isSelected = hex.Equals(curHex, StringComparison.OrdinalIgnoreCase);
                bool isHovered = (_hoveredPaletteCell.X == c && _hoveredPaletteCell.Y == r);

                using (SolidBrush b = new SolidBrush(col))
                using (GraphicsPath p = Win11CardPanel.CreateRoundRect(cellRect, 4))
                {
                    g.FillPath(b, p);
                    Color borderCol = isSelected
                        ? Color.White
                        : (isHovered ? Color.FromArgb(200, 200, 200) : Color.FromArgb(60, 0, 0, 0));
                    using Pen pen = new Pen(borderCol, isSelected ? 2f : 1f);
                    g.DrawPath(pen, p);
                }
            }
        }
    }

    private void OnPaletteMouseMove(object? sender, MouseEventArgs e)
    {
        Point found = new Point(-1, -1);
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 10; c++)
            {
                if (GetPaletteCellRect(r, c).Contains(e.Location))
                {
                    found = new Point(c, r);
                    break;
                }
            }
        }
        if (found != _hoveredPaletteCell)
        {
            _hoveredPaletteCell = found;
            _paletteGrid.Invalidate();
        }
    }

    private void OnPaletteMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 10; c++)
            {
                if (GetPaletteCellRect(r, c).Contains(e.Location))
                {
                    Color picked = ColorTranslator.FromHtml(GoogleDocsPalette[r, c]);
                    SetColorFromRgb(picked, updateHexBox: true);
                    return;
                }
            }
        }
    }

    private List<(string hex, bool isPinned)> GetCombinedHistorySlots()
    {
        var list = new List<(string hex, bool isPinned)>(20);
        foreach (var p in _settings.PinnedCustomColors)
        {
            if (list.Count < 20) list.Add((p, true));
        }
        foreach (var r in _settings.RecentCustomColors)
        {
            if (list.Count < 20 && !list.Any(x => x.hex.Equals(r, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add((r, false));
            }
        }
        return list;
    }

    private Rectangle GetHistorySlotRect(int slotIndex)
    {
        const int cellSize = 19;
        const int gap = 3;
        int row = slotIndex / 10;
        int col = slotIndex % 10;
        return new Rectangle(col * (cellSize + gap), row * (cellSize + 5), cellSize, cellSize);
    }

    private void PaintHistoryGrid(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var slots = GetCombinedHistorySlots();
        string curHex = SelectedHex;

        for (int i = 0; i < 20; i++)
        {
            Rectangle rect = GetHistorySlotRect(i);
            using GraphicsPath path = Win11CardPanel.CreateRoundRect(rect, 4);

            if (i < slots.Count)
            {
                var (hex, isPinned) = slots[i];
                Color c = Color.FromArgb(45, 45, 45);
                try { c = ColorTranslator.FromHtml(hex); } catch { }

                using (SolidBrush fill = new SolidBrush(c))
                {
                    g.FillPath(fill, path);
                }

                if (isPinned)
                {
                    PointF[] pinBadge = new PointF[]
                    {
                        new PointF(rect.Right - 7, rect.Top + 1),
                        new PointF(rect.Right - 1, rect.Top + 1),
                        new PointF(rect.Right - 1, rect.Top + 7)
                    };
                    using SolidBrush goldBrush = new SolidBrush(Color.FromArgb(255, 215, 70));
                    using Pen darkOutline = new Pen(Color.FromArgb(180, 0, 0, 0), 1f);
                    g.FillPolygon(goldBrush, pinBadge);
                    g.DrawPolygon(darkOutline, pinBadge);
                }

                bool isSelected = hex.Equals(curHex, StringComparison.OrdinalIgnoreCase);
                bool isHovered = (_hoveredHistorySlot == i);
                Color borderCol = isSelected
                    ? Color.White
                    : (isPinned
                        ? Color.FromArgb(245, 200, 65)
                        : (isHovered ? Color.FromArgb(210, 210, 210) : Color.FromArgb(80, 80, 80)));

                using Pen borderPen = new Pen(borderCol, isSelected ? 2f : 1f);
                g.DrawPath(borderPen, path);
            }
            else
            {
                using SolidBrush emptyFill = new SolidBrush(Color.FromArgb(33, 33, 33));
                using Pen emptyPen = new Pen(Color.FromArgb(58, 58, 58), 1f);
                g.FillPath(emptyFill, path);
                g.DrawPath(emptyPen, path);
            }
        }
    }

    private void OnHistoryMouseMove(object? sender, MouseEventArgs e)
    {
        int found = -1;
        for (int i = 0; i < 20; i++)
        {
            if (GetHistorySlotRect(i).Contains(e.Location))
            {
                found = i;
                break;
            }
        }
        if (found != _hoveredHistorySlot)
        {
            _hoveredHistorySlot = found;
            _historyGrid.Invalidate();
        }
    }

    private void OnHistoryMouseDown(object? sender, MouseEventArgs e)
    {
        var slots = GetCombinedHistorySlots();
        for (int i = 0; i < slots.Count; i++)
        {
            if (GetHistorySlotRect(i).Contains(e.Location))
            {
                if (e.Button == MouseButtons.Left)
                {
                    try
                    {
                        Color picked = ColorTranslator.FromHtml(slots[i].hex);
                        SetColorFromRgb(picked, updateHexBox: true);
                    }
                    catch { }
                }
                else if (e.Button == MouseButtons.Right)
                {
                    TogglePinForColor(slots[i].hex);
                }
                return;
            }
        }
    }

    private static void RgbToHsv(Color c, out float h, out float s, out float v)
    {
        float r = c.R / 255f;
        float g = c.G / 255f;
        float b = c.B / 255f;

        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float delta = max - min;

        h = 0f;
        if (delta > 0.0001f)
        {
            if (max == r) h = 60f * (((g - b) / delta) % 6f);
            else if (max == g) h = 60f * (((b - r) / delta) + 2f);
            else h = 60f * (((r - g) / delta) + 4f);
            if (h < 0f) h += 360f;
        }

        s = max <= 0.0001f ? 0f : (delta / max);
        v = max;
    }

    private static Color HsvToRgb(float h, float s, float v)
    {
        h = ((h % 360f) + 360f) % 360f;
        s = Math.Clamp(s, 0f, 1f);
        v = Math.Clamp(v, 0f, 1f);

        float c = v * s;
        float x = c * (1f - Math.Abs(((h / 60f) % 2f) - 1f));
        float m = v - c;

        float r1, g1, b1;
        if (h < 60f) (r1, g1, b1) = (c, x, 0f);
        else if (h < 120f) (r1, g1, b1) = (x, c, 0f);
        else if (h < 180f) (r1, g1, b1) = (0f, c, x);
        else if (h < 240f) (r1, g1, b1) = (0f, x, c);
        else if (h < 300f) (r1, g1, b1) = (x, 0f, c);
        else (r1, g1, b1) = (c, 0f, x);

        int r = Math.Clamp((int)Math.Round((r1 + m) * 255f), 0, 255);
        int g = Math.Clamp((int)Math.Round((g1 + m) * 255f), 0, 255);
        int b = Math.Clamp((int)Math.Round((b1 + m) * 255f), 0, 255);
        return Color.FromArgb(255, r, g, b);
    }
}