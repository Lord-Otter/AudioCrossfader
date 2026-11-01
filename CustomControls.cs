using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

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

// Custom Windows 11 Rounded Button (Used for Nav Tabs, Primary/Secondary Buttons, and Danger Buttons)
public class ModernButton : Button
{
    private bool _isHovered = false;
    private bool _isPressed = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 6;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Color.FromArgb(70, 70, 70);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverBackColor { get; set; } = Color.FromArgb(66, 66, 66);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color PressedBackColor { get; set; } = Color.FromArgb(44, 44, 44);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowBorder { get; set; } = true;

    public ModernButton()
    {
        this.SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        this.FlatStyle = FlatStyle.Flat;
        this.FlatAppearance.BorderSize = 0;
        this.BackColor = Color.FromArgb(52, 52, 52);
        this.ForeColor = Color.White;
        this.Font = new Font("Segoe UI Semibold", 8.75f, FontStyle.Bold);
        this.Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        _isPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        if (mevent.Button == MouseButtons.Left)
        {
            _isPressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _isPressed = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        Graphics g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color parentBg = this.Parent?.BackColor ?? Color.FromArgb(39, 39, 39);
        if (parentBg == Color.Transparent)
        {
            Control? p = this.Parent;
            while (p != null && p.BackColor == Color.Transparent)
            {
                if (p is Win11CardPanel card)
                {
                    parentBg = card.FillColor;
                    break;
                }
                p = p.Parent;
            }
            if (parentBg == Color.Transparent) parentBg = Color.FromArgb(39, 39, 39);
        }
        g.Clear(parentBg);

        Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        Color fill = this.BackColor;
        if (!this.Enabled)
        {
            fill = Color.FromArgb(42, 42, 42);
        }
        else if (_isPressed)
        {
            fill = PressedBackColor;
        }
        else if (_isHovered)
        {
            fill = HoverBackColor;
        }

        using GraphicsPath path = Win11CardPanel.CreateRoundRect(rect, CornerRadius);
        using SolidBrush brush = new SolidBrush(fill);
        g.FillPath(brush, path);

        if (ShowBorder)
        {
            Color bCol = this.Enabled ? BorderColor : Color.FromArgb(55, 55, 55);
            using Pen pen = new Pen(bCol, 1f);
            g.DrawPath(pen, path);
        }

        Color textCol = this.Enabled ? this.ForeColor : Color.FromArgb(115, 115, 115);
        TextRenderer.DrawText(
            g,
            this.Text,
            this.Font,
            this.ClientRectangle,
            textCol,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

// Custom Rounded Color Swatch Control
public class RoundedColorSwatch : Panel
{
    private bool _isHovered = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SwatchColor { get; set; } = Color.FromArgb(0, 168, 255);

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 6;

    public RoundedColorSwatch()
    {
        this.SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        this.Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color parentBg = this.Parent?.BackColor ?? Color.FromArgb(39, 39, 39);
        if (parentBg == Color.Transparent && this.Parent is Win11CardPanel card)
        {
            parentBg = card.FillColor;
        }
        g.Clear(parentBg);

        Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        using GraphicsPath path = Win11CardPanel.CreateRoundRect(rect, CornerRadius);
        using SolidBrush fillBrush = new SolidBrush(SwatchColor);
        g.FillPath(fillBrush, path);

        Color borderCol = _isHovered ? Color.FromArgb(185, 185, 185) : Color.FromArgb(105, 105, 105);
        using Pen borderPen = new Pen(borderCol, 1f);
        g.DrawPath(borderPen, path);
    }
}

// Interactive Single-Click Rounded Keybind Control (Click to bind, Esc to clear)
public class KeybindBox : Control
{
    private bool _isHovered = false;
    private bool _isRecording = false;
    private int _binding = 0;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 6;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Binding
    {
        get => _binding;
        set
        {
            _binding = value;
            if (!_isRecording)
            {
                this.Text = KeybindHelper.FormatKeybind(_binding);
            }
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsRecording
    {
        get => _isRecording;
        set
        {
            _isRecording = value;
            if (!_isRecording)
            {
                this.Text = KeybindHelper.FormatKeybind(_binding);
            }
            Invalidate();
        }
    }

    public KeybindBox()
    {
        this.SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        this.Cursor = Cursors.Hand;
        this.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
        this.ForeColor = Color.White;
        this.BackColor = Color.FromArgb(38, 38, 38);
        this.Height = 25;
        this.Text = "None";
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color parentBg = this.Parent?.BackColor ?? Color.FromArgb(39, 39, 39);
        if (parentBg == Color.Transparent)
        {
            Control? p = this.Parent;
            while (p != null && p.BackColor == Color.Transparent)
            {
                if (p is Win11CardPanel card)
                {
                    parentBg = card.FillColor;
                    break;
                }
                p = p.Parent;
            }
            if (parentBg == Color.Transparent) parentBg = Color.FromArgb(39, 39, 39);
        }
        g.Clear(parentBg);

        Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        Color fill = _isRecording
            ? Color.FromArgb(205, 95, 0)
            : (_isHovered ? Color.FromArgb(56, 56, 56) : this.BackColor);

        Color border = _isRecording
            ? Color.FromArgb(255, 145, 45)
            : (_isHovered ? Color.FromArgb(110, 110, 110) : Color.FromArgb(72, 72, 72));

        using GraphicsPath path = Win11CardPanel.CreateRoundRect(rect, CornerRadius);
        using SolidBrush brush = new SolidBrush(fill);
        using Pen pen = new Pen(border, 1f);

        g.FillPath(brush, path);
        g.DrawPath(pen, path);

        Color textCol = (!_isRecording && _binding == 0)
            ? Color.FromArgb(145, 145, 145)
            : Color.White;

        TextRenderer.DrawText(
            g,
            this.Text,
            this.Font,
            this.ClientRectangle,
            textCol,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

// In CustomControls.cs -> Replace DarkScrollPanel with this updated version
public class DarkScrollPanel : Panel
{
    private readonly Panel _viewport;
    private readonly Panel _content;
    private int _scrollOffsetY = 0;
    private int _scrollOffsetX = 0;
    private int _contentHeight = 0;
    private int _contentWidth = 0;

    private bool _draggingThumbY = false;
    private bool _draggingThumbX = false;
    private bool _hoveringThumbY = false;
    private bool _hoveringThumbX = false;
    private int _dragStartMouse = 0;
    private int _dragStartScroll = 0;
    private const int ScrollBarSize = 10;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int MinContentWidth { get; set; } = 496;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control.ControlCollection InnerControls => _content.Controls;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ContentViewportWidth => Math.Max(MinContentWidth, this.ClientSize.Width - ScrollBarSize - 4);

    public DarkScrollPanel()
    {
        this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                      ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        this.BackColor = Color.FromArgb(28, 28, 28);

        _viewport = new Panel
        {
            Left = 0,
            Top = 0,
            Width = MinContentWidth,
            Height = 10,
            BackColor = Color.FromArgb(28, 28, 28)
        };

        _content = new Panel
        {
            Left = 0,
            Top = 0,
            Width = MinContentWidth,
            Height = 10,
            BackColor = Color.FromArgb(28, 28, 28)
        };

        _viewport.Controls.Add(_content);
        this.Controls.Add(_viewport);
    }

    public void RecalculateScroll()
    {
        int maxBottom = 0;
        foreach (Control c in _content.Controls)
        {
            if (c.Height > 0 && c.Bottom > maxBottom) maxBottom = c.Bottom;
        }

        // Horizontal scrollbar ONLY appears when the window is narrower than MinContentWidth (496)
        bool needsH = this.ClientSize.Width < MinContentWidth;
        int availH = Math.Max(1, this.ClientSize.Height - (needsH ? ScrollBarSize : 0));

        // Vertical scrollbar ONLY appears when the cards + bottom margin exceed the available window height
        int rawContentH = maxBottom + 10;
        bool needsV = rawContentH > availH;
        int availW = Math.Max(1, this.ClientSize.Width - (needsV ? ScrollBarSize : 0));

        _viewport.Width = availW;
        _viewport.Height = availH;

        _contentWidth = needsH ? MinContentWidth : availW;
        _contentHeight = needsV ? rawContentH : availH;

        _content.Width = _contentWidth;
        _content.Height = _contentHeight;

        ClampAndApplyScroll();
        Invalidate();
    }

    private void ClampAndApplyScroll()
    {
        int maxScrollY = Math.Max(0, _contentHeight - Math.Max(1, _viewport.Height));
        int maxScrollX = Math.Max(0, _contentWidth - Math.Max(1, _viewport.Width));

        _scrollOffsetY = Math.Clamp(_scrollOffsetY, 0, maxScrollY);
        _scrollOffsetX = Math.Clamp(_scrollOffsetX, 0, maxScrollX);

        _content.Top = -_scrollOffsetY;
        _content.Left = -_scrollOffsetX;
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        if (_content != null) RecalculateScroll();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (ModifierKeys == Keys.Shift && _contentWidth > _viewport.Width)
        {
            _scrollOffsetX -= Math.Sign(e.Delta) * 36;
        }
        else if (_contentHeight > _viewport.Height)
        {
            _scrollOffsetY -= Math.Sign(e.Delta) * 36;
        }
        ClampAndApplyScroll();
        Invalidate();
    }

    private Rectangle GetVerticalThumbRect()
    {
        if (_contentHeight <= _viewport.Height || _viewport.Height <= 0) return Rectangle.Empty;
        int trackH = _viewport.Height - 8;
        int thumbH = Math.Max(28, (int)((float)_viewport.Height / _contentHeight * trackH));
        int maxScroll = _contentHeight - _viewport.Height;
        int thumbTop = 4 + (maxScroll > 0 ? (int)((float)_scrollOffsetY / maxScroll * (trackH - thumbH)) : 0);
        return new Rectangle(this.ClientSize.Width - ScrollBarSize + 2, thumbTop, 6, thumbH);
    }

    private Rectangle GetHorizontalThumbRect()
    {
        if (_contentWidth <= _viewport.Width || _viewport.Width <= 0) return Rectangle.Empty;
        int trackW = _viewport.Width - 8;
        int thumbW = Math.Max(28, (int)((float)_viewport.Width / _contentWidth * trackW));
        int maxScroll = _contentWidth - _viewport.Width;
        int thumbLeft = 4 + (maxScroll > 0 ? (int)((float)_scrollOffsetX / maxScroll * (trackW - thumbW)) : 0);
        return new Rectangle(thumbLeft, this.ClientSize.Height - ScrollBarSize + 2, thumbW, 6);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Rectangle vThumb = GetVerticalThumbRect();
        Rectangle hThumb = GetHorizontalThumbRect();

        if (!vThumb.IsEmpty && vThumb.Contains(e.Location))
        {
            _draggingThumbY = true;
            _dragStartMouse = e.Y;
            _dragStartScroll = _scrollOffsetY;
            this.Capture = true;
        }
        else if (!hThumb.IsEmpty && hThumb.Contains(e.Location))
        {
            _draggingThumbX = true;
            _dragStartMouse = e.X;
            _dragStartScroll = _scrollOffsetX;
            this.Capture = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_draggingThumbY)
        {
            Rectangle vThumb = GetVerticalThumbRect();
            int maxTravel = Math.Max(1, (_viewport.Height - 8) - vThumb.Height);
            int maxScroll = Math.Max(0, _contentHeight - _viewport.Height);
            _scrollOffsetY = _dragStartScroll + (int)((float)(e.Y - _dragStartMouse) / maxTravel * maxScroll);
            ClampAndApplyScroll();
            Invalidate();
        }
        else if (_draggingThumbX)
        {
            Rectangle hThumb = GetHorizontalThumbRect();
            int maxTravel = Math.Max(1, (_viewport.Width - 8) - hThumb.Width);
            int maxScroll = Math.Max(0, _contentWidth - _viewport.Width);
            _scrollOffsetX = _dragStartScroll + (int)((float)(e.X - _dragStartMouse) / maxTravel * maxScroll);
            ClampAndApplyScroll();
            Invalidate();
        }
        else
        {
            bool overY = GetVerticalThumbRect().Contains(e.Location);
            bool overX = GetHorizontalThumbRect().Contains(e.Location);
            if (overY != _hoveringThumbY || overX != _hoveringThumbX)
            {
                _hoveringThumbY = overY;
                _hoveringThumbX = overX;
                Invalidate();
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_draggingThumbY || _draggingThumbX)
        {
            _draggingThumbY = _draggingThumbX = false;
            this.Capture = false;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle vThumb = GetVerticalThumbRect();
        if (!vThumb.IsEmpty)
        {
            Color col = (_draggingThumbY || _hoveringThumbY) ? Color.FromArgb(145, 145, 145) : Color.FromArgb(90, 90, 90);
            using GraphicsPath path = Win11CardPanel.CreateRoundRect(vThumb, 3);
            using SolidBrush brush = new SolidBrush(col);
            e.Graphics.FillPath(brush, path);
        }

        Rectangle hThumb = GetHorizontalThumbRect();
        if (!hThumb.IsEmpty)
        {
            Color col = (_draggingThumbX || _hoveringThumbX) ? Color.FromArgb(145, 145, 145) : Color.FromArgb(90, 90, 90);
            using GraphicsPath path = Win11CardPanel.CreateRoundRect(hThumb, 3);
            using SolidBrush brush = new SolidBrush(col);
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
            if (c.Height > 0 && c.Bottom > maxBottom)
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

public class ModernComboBox : ComboBox
{
    private const int WM_SIZE = 0x0005;
    private const int WM_SETFOCUS = 0x0007;
    private const int WM_PAINT = 0x000F;
    private const int WM_WINDOWPOSCHANGED = 0x0047;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int CB_SETEDITSEL = 0x0142;

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

    private void ClearNativeEditSelection()
    {
        if (!this.IsDisposed && this.IsHandleCreated && this.DropDownStyle == ComboBoxStyle.DropDown && !this.Focused)
        {
            SendMessage(this.Handle, CB_SETEDITSEL, IntPtr.Zero, (IntPtr)(-1));
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        this.BeginInvoke(new Action(ClearNativeEditSelection));
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        if (!this.Focused)
        {
            this.BeginInvoke(new Action(ClearNativeEditSelection));
        }
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        bool isEditBox = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;
        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        // Never paint the blue selection background inside the closed ComboBox display area
        Color bg = (isSelected && !isEditBox) ? Color.FromArgb(0, 103, 192) : this.BackColor;

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
            using SolidBrush pillBrush = new SolidBrush((isSelected && !isEditBox) ? Color.FromArgb(60, 0, 0, 0) : Color.FromArgb(45, badge.Color));
            e.Graphics.FillPath(pillBrush, pillPath);

            using SolidBrush badgeTextBrush = new SolidBrush((isSelected && !isEditBox) ? Color.White : badge.Color);
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
        if (m.Msg == WM_MOUSEWHEEL && !this.DroppedDown)
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

        // Immediately clear any auto-selection triggered by Win32 during resize or layout changes
        if (m.Msg == WM_SIZE || m.Msg == WM_WINDOWPOSCHANGED || m.Msg == WM_SETFOCUS)
        {
            ClearNativeEditSelection();
        }

        if (m.Msg == WM_PAINT)
        {
            ClearNativeEditSelection();
            using Graphics g = Graphics.FromHwnd(this.Handle);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using Pen innerPen = new Pen(this.BackColor, 2f);
            g.DrawRectangle(innerPen, 0, 0, this.Width - 1, this.Height - 1);

            using GraphicsPath borderPath = Win11CardPanel.CreateRoundRect(new Rectangle(0, 0, this.Width - 1, this.Height - 1), 5);
            using Pen outerPen = new Pen(this.DroppedDown ? Color.FromArgb(0, 120, 212) : Color.FromArgb(68, 68, 68), 1f);
            g.DrawPath(outerPen, borderPath);
        }
    }
}