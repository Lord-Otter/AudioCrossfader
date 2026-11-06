using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
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
        Win11CardPanel? parentCard = null;
        if (parentBg == Color.Transparent)
        {
            Control? p = this.Parent;
            while (p != null && p.BackColor == Color.Transparent)
            {
                if (p is Win11CardPanel card)
                {
                    parentCard = card;
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

        if (parentCard != null && parentCard.ShowHighlightPill)
        {
            if (_isHovered && !_isPressed)
            {
                using GraphicsPath hoverPath = Win11CardPanel.CreateRoundRect(rect, CornerRadius);
                using SolidBrush hoverBrush = new SolidBrush(Color.FromArgb(46, 46, 46));
                g.FillPath(hoverBrush, hoverPath);
            }

            RectangleF localPill = new RectangleF(
                parentCard.HighlightPillRect.X - this.Left,
                parentCard.HighlightPillRect.Y - this.Top,
                parentCard.HighlightPillRect.Width,
                parentCard.HighlightPillRect.Height);

            if (localPill.Right > 0 && localPill.Left < this.Width)
            {
                using GraphicsPath pillPath = Win11CardPanel.CreateRoundRectF(localPill, CornerRadius);
                using SolidBrush pillBrush = new SolidBrush(parentCard.HighlightPillColor);
                g.FillPath(pillBrush, pillPath);
            }
        }
        else
        {
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

        _viewport = new BufferedPanel
        {
            Left = 0,
            Top = 0,
            Width = MinContentWidth,
            Height = 10,
            BackColor = Color.FromArgb(28, 28, 28)
        };

        _content = new BufferedPanel
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

        bool needsH = this.ClientSize.Width < MinContentWidth;
        int availH = Math.Max(1, this.ClientSize.Height - (needsH ? ScrollBarSize : 0));

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

// Flicker-free Double-Buffered Panel for dynamic card containers and sub-rows
public class BufferedPanel : Panel
{
    public BufferedPanel()
    {
        this.SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
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

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowHighlightPill { get; set; } = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RectangleF HighlightPillRect { get; set; } = RectangleF.Empty;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HighlightPillColor { get; set; } = Color.FromArgb(0, 120, 212);

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
            if (c.Visible && c.Height > 0 && c.Bottom > maxBottom)
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

        if (ShowHighlightPill && HighlightPillRect.Width > 0 && HighlightPillRect.Height > 0)
        {
            using GraphicsPath pillPath = CreateRoundRectF(HighlightPillRect, 6f);
            using SolidBrush pillBrush = new SolidBrush(HighlightPillColor);
            e.Graphics.FillPath(pillBrush, pillPath);
        }

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

public class ModernSlider : Control
{
    private int _minimum = 0;
    private int _maximum = 100;
    private int _value = 50;
    private bool _isHovered = false;
    private bool _isDragging = false;

    public event EventHandler? ValueChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum
    {
        get => _minimum;
        set { _minimum = value; if (_value < _minimum) Value = _minimum; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum
    {
        get => _maximum;
        set { _maximum = Math.Max(_minimum + 1, value); if (_value > _maximum) Value = _maximum; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            int clamped = Math.Clamp(value, _minimum, _maximum);
            if (_value != clamped)
            {
                _value = clamped;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ValueSuffix { get; set; } = "";

    public ModernSlider()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        Height = 25;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
    }

    private int ValueWidth => 44;
    private int TrackLeft => 8;
    private int TrackRight => Math.Max(TrackLeft + 20, Width - ValueWidth - 8);

    private void UpdateFromMouseX(int mouseX)
    {
        float ratio = Math.Clamp((mouseX - TrackLeft) / (float)Math.Max(1, TrackRight - TrackLeft), 0f, 1f);
        Value = _minimum + (int)Math.Round(ratio * (_maximum - _minimum));
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            Capture = true;
            UpdateFromMouseX(e.X);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isDragging) UpdateFromMouseX(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_isDragging)
        {
            _isDragging = false;
            Capture = false;
            Invalidate();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!Enabled) return;
        int step = Math.Max(1, (_maximum - _minimum) / 25);
        Value = Math.Clamp(_value + Math.Sign(e.Delta) * step, _minimum, _maximum);
        if (e is HandledMouseEventArgs hme) hme.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color parentBg = Parent is Win11CardPanel card ? card.FillColor : (Parent?.BackColor ?? Color.FromArgb(39, 39, 39));
        g.Clear(parentBg);

        int cy = Height / 2;
        int trackW = TrackRight - TrackLeft;
        float ratio = (_value - _minimum) / (float)Math.Max(1, _maximum - _minimum);
        int fillW = (int)Math.Round(trackW * ratio);

        Rectangle trackRect = new Rectangle(TrackLeft, cy - 3, trackW, 6);
        using (GraphicsPath trackPath = Win11CardPanel.CreateRoundRect(trackRect, 3))
        using (SolidBrush trackBrush = new SolidBrush(Enabled ? Color.FromArgb(62, 62, 62) : Color.FromArgb(48, 48, 48)))
        {
            g.FillPath(trackBrush, trackPath);
        }

        if (fillW > 2)
        {
            Rectangle fillRect = new Rectangle(TrackLeft, cy - 3, fillW, 6);
            using GraphicsPath fillPath = Win11CardPanel.CreateRoundRect(fillRect, 3);
            using SolidBrush fillBrush = new SolidBrush(Enabled ? Color.FromArgb(0, 120, 212) : Color.FromArgb(85, 85, 85));
            g.FillPath(fillBrush, fillPath);
        }

        float thumbX = TrackLeft + fillW;
        float thumbR = _isDragging ? 7.5f : (_isHovered ? 7.0f : 6.0f);
        RectangleF thumbRect = new RectangleF(thumbX - thumbR, cy - thumbR, thumbR * 2f, thumbR * 2f);
        using (SolidBrush thumbBrush = new SolidBrush(Enabled ? Color.White : Color.FromArgb(130, 130, 130)))
        using (Pen thumbPen = new Pen(Color.FromArgb(30, 30, 30), 1.2f))
        {
            g.FillEllipse(thumbBrush, thumbRect);
            g.DrawEllipse(thumbPen, thumbRect);
        }

        Rectangle valRect = new Rectangle(TrackRight + 6, 0, ValueWidth, Height);
        Color textCol = Enabled ? Color.FromArgb(230, 230, 230) : Color.FromArgb(115, 115, 115);
        TextRenderer.DrawText(
            g,
            $"{_value}{ValueSuffix}",
            Font,
            valRect,
            textCol,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

public class ModernComboBox : ComboBox
{
    private const int WM_SIZE = 0x0005;
    private const int WM_SETFOCUS = 0x0007;
    private const int WM_KILLFOCUS = 0x0008;
    private const int WM_PAINT = 0x000F;
    private const int WM_ERASEBKGND = 0x0014;
    private const int WM_WINDOWPOSCHANGED = 0x0047;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_MOUSELEAVE = 0x02A3;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int CB_SETEDITSEL = 0x0142;
    private const int CB_GETCOMBOBOXINFO = 0x0164;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct COMBOBOXINFO
    {
        public int cbSize;
        public RECT rcItem;
        public RECT rcButton;
        public int stateButton;
        public IntPtr hwndCombo;
        public IntPtr hwndItem;
        public IntPtr hwndList;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetComboBoxInfo(IntPtr hwndCombo, ref COMBOBOXINFO info);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    private bool _isHovered = false;
    private EditBoxHook? _editHook;

    // Hooks the internal Win32 Edit child control on writable dropdowns so moving the cursor
    // into the text area (I-beam transition) never flashes a native white border!
    private sealed class EditBoxHook : NativeWindow
    {
        private readonly ModernComboBox _owner;
        public EditBoxHook(ModernComboBox owner, IntPtr editHandle)
        {
            _owner = owner;
            AssignHandle(editHandle);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT || m.Msg == WM_MOUSEMOVE || m.Msg == WM_MOUSELEAVE ||
                m.Msg == WM_SETFOCUS || m.Msg == WM_KILLFOCUS)
            {
                _owner.UpdateHoverFromCursor();
                _owner.PaintCustomChrome();
            }
        }
    }

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
        this.ItemHeight = 19; // 19 + 6px native Win32 frame = exact 25px control height
        this.Height = 25;
        this.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
    }

    private const int EM_SETMARGINS = 0x00D3;
    private const int EC_LEFTMARGIN = 0x0001;
    private int _appliedEditLeftMargin = -1;

    private void ApplyEditLeftMarginForIcon()
    {
        try
        {
            if (!this.IsHandleCreated || this.IsDisposed || this.DropDownStyle != ComboBoxStyle.DropDown)
                return;

            int desiredMargin = (IconResolver != null) ? 22 : 2;
            if (_appliedEditLeftMargin == desiredMargin)
                return;

            COMBOBOXINFO info = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
            if (GetComboBoxInfo(this.Handle, ref info) && info.hwndItem != IntPtr.Zero)
            {
                _appliedEditLeftMargin = desiredMargin;
                SendMessage(info.hwndItem, EM_SETMARGINS, (IntPtr)EC_LEFTMARGIN, (IntPtr)desiredMargin);
            }
        }
        catch { }
    }

    private void AttachEditHookIfNeeded()
    {
        try
        {
            if (!this.IsHandleCreated || this.DropDownStyle != ComboBoxStyle.DropDown)
                return;

            COMBOBOXINFO info = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
            if (GetComboBoxInfo(this.Handle, ref info) && info.hwndItem != IntPtr.Zero)
            {
                if (_editHook == null)
                {
                    _editHook = new EditBoxHook(this, info.hwndItem);
                }
                ApplyEditLeftMarginForIcon();
            }
        }
        catch { }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        if (this.DropDownStyle == ComboBoxStyle.DropDown && IconResolver != null)
        {
            PaintCustomChrome();
        }
    }

    private void UpdateHoverFromCursor()
    {
        if (!this.IsHandleCreated || this.IsDisposed) return;
        bool nowHovered = false;
        if (GetCursorPos(out Point screenPt))
        {
            Point clientPt = this.PointToClient(screenPt);
            nowHovered = this.ClientRectangle.Contains(clientPt);
        }
        if (nowHovered != _isHovered)
        {
            _isHovered = nowHovered;
            PaintCustomChrome();
        }
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
        AttachEditHookIfNeeded();
        this.BeginInvoke(new Action(ClearNativeEditSelection));
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _editHook?.ReleaseHandle();
        _editHook = null;
        _appliedEditLeftMargin = -1;
        base.OnHandleDestroyed(e);
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        if (!this.Focused)
        {
            this.BeginInvoke(new Action(ClearNativeEditSelection));
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        UpdateHoverFromCursor();
        PaintCustomChrome();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        UpdateHoverFromCursor();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        UpdateHoverFromCursor();
        PaintCustomChrome();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        bool isEditBox = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;
        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        // Never paint the blue selection background inside the closed ComboBox display area
        Color bg = (isSelected && !isEditBox)
            ? Color.FromArgb(0, 103, 192)
            : (isEditBox && _isHovered && this.DropDownStyle == ComboBoxStyle.DropDownList
                ? Color.FromArgb(55, 55, 55)
                : this.BackColor);

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
            using Font badgeFont = new Font("Segoe UI Semibold", 7.5f, FontStyle.Bold);
            Size badgeTextSz = TextRenderer.MeasureText(
                e.Graphics,
                badge.Text,
                badgeFont,
                new Size(400, 30),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

            int pillWidth = badgeTextSz.Width + 12;
            int pillHeight = Math.Max(15, e.Bounds.Height - 4);
            Rectangle pillRect = new Rectangle(
                e.Bounds.Right - pillWidth - 6,
                e.Bounds.Y + (e.Bounds.Height - pillHeight) / 2,
                pillWidth,
                pillHeight);

            Color pillFill = (isSelected && !isEditBox)
                ? Color.FromArgb(85, 0, 0, 0)
                : Color.FromArgb(52, badge.Color);
            Color pillBorder = (isSelected && !isEditBox)
                ? Color.FromArgb(140, 255, 255, 255)
                : Color.FromArgb(115, badge.Color);

            using GraphicsPath pillPath = Win11CardPanel.CreateRoundRect(pillRect, 4);
            using SolidBrush pillBrush = new SolidBrush(pillFill);
            using Pen pillPen = new Pen(pillBorder, 1f);
            e.Graphics.FillPath(pillBrush, pillPath);
            e.Graphics.DrawPath(pillPen, pillPath);

            Color badgeTextColor = (isSelected && !isEditBox) ? Color.White : badge.Color;
            TextRenderer.DrawText(
                e.Graphics,
                badge.Text,
                badgeFont,
                pillRect,
                badgeTextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

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

    private void PaintCustomChrome()
    {
        if (this.IsDisposed || !this.IsHandleCreated) return;
        try
        {
            ClearNativeEditSelection();

            using Graphics g = Graphics.FromHwnd(this.Handle);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle outerRect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            if (outerRect.Width <= 0 || outerRect.Height <= 0) return;

            bool isWritable = (this.DropDownStyle == ComboBoxStyle.DropDown);
            Color buttonBg = (_isHovered && !isWritable) ? Color.FromArgb(55, 55, 55) : this.BackColor;

            // 1. Erase native square 3D border AND the square Win32 dropdown button background on the right
            int btnWidth = SystemInformation.VerticalScrollBarWidth + 2;
            using (SolidBrush btnBgBrush = new SolidBrush(buttonBg))
            {
                g.FillRectangle(btnBgBrush, this.Width - btnWidth - 2, 1, btnWidth + 1, this.Height - 2);
            }
            using (Pen innerPen = new Pen(buttonBg, 2f))
            {
                g.DrawRectangle(innerPen, 1, 1, this.Width - 3, this.Height - 3);
            }

            // 1b. For writable dropdowns with an IconResolver, indent text via EM_SETMARGINS and paint the app/group icon on the left
            if (isWritable && IconResolver != null)
            {
                using (SolidBrush iconBg = new SolidBrush(this.BackColor))
                {
                    g.FillRectangle(iconBg, 2, 2, 21, this.Height - 4);
                }
                string currentTxt = (this.Text ?? "").Trim();
                if (!string.IsNullOrEmpty(currentTxt))
                {
                    Bitmap? bmp = IconResolver.Invoke(currentTxt);
                    if (bmp != null)
                    {
                        int icoSz = 16;
                        int icoY = (this.Height - icoSz) / 2;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(bmp, new Rectangle(5, icoY, icoSz, icoSz));
                    }
                }
            }

            // 2. Draw a clean floating chevron arrow inside the dropdown bar
            float arrowCenterX = this.Width - (btnWidth / 2f) - 1f;
            float arrowCenterY = this.Height / 2f;
            Color arrowColor = this.DroppedDown
                ? Color.FromArgb(96, 205, 255)
                : (_isHovered ? Color.White : Color.FromArgb(195, 195, 195));

            using (Pen arrowPen = new Pen(arrowColor, 1.6f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            })
            {
                if (this.DroppedDown)
                {
                    g.DrawLines(arrowPen, new PointF[]
                    {
                        new PointF(arrowCenterX - 4f, arrowCenterY + 1.5f),
                        new PointF(arrowCenterX, arrowCenterY - 2.5f),
                        new PointF(arrowCenterX + 4f, arrowCenterY + 1.5f)
                    });
                }
                else
                {
                    g.DrawLines(arrowPen, new PointF[]
                    {
                        new PointF(arrowCenterX - 4f, arrowCenterY - 1.5f),
                        new PointF(arrowCenterX, arrowCenterY + 2.5f),
                        new PointF(arrowCenterX + 4f, arrowCenterY - 1.5f)
                    });
                }
            }

            Color borderColor = this.DroppedDown
                ? Color.FromArgb(0, 120, 212)
                : (_isHovered ? Color.FromArgb(115, 115, 115) : Color.FromArgb(72, 72, 72));

            if (isWritable)
            {
                using Pen sharpPen = new Pen(borderColor, 1f);
                g.DrawRectangle(sharpPen, outerRect);
            }
            else
            {
                using GraphicsPath borderPath = Win11CardPanel.CreateRoundRect(outerRect, 5);

                Color parentBg = this.Parent?.BackColor ?? Color.FromArgb(48, 48, 48);
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
                    if (parentBg == Color.Transparent) parentBg = Color.FromArgb(48, 48, 48);
                }

                using (Region cornerMask = new Region(this.ClientRectangle))
                {
                    cornerMask.Exclude(borderPath);
                    using SolidBrush maskBrush = new SolidBrush(parentBg);
                    g.FillRegion(maskBrush, cornerMask);
                }

                using Pen outerPen = new Pen(borderColor, 1f);
                g.DrawPath(outerPen, borderPath);
            }
        }
        catch { }
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

        if (m.Msg == WM_ERASEBKGND)
        {
            m.Result = (IntPtr)1;
            return;
        }

        // Always call base.WndProc so clicking anywhere on the dropdown opens it normally!
        base.WndProc(ref m);

        if (m.Msg == WM_SIZE || m.Msg == WM_WINDOWPOSCHANGED || m.Msg == WM_SETFOCUS)
        {
            ClearNativeEditSelection();
        }

        // Repaint custom chrome immediately after native paint or mouse hover changes so native white borders never show
        if (m.Msg == WM_PAINT || m.Msg == WM_MOUSEMOVE || m.Msg == WM_MOUSELEAVE ||
            m.Msg == WM_SETFOCUS || m.Msg == WM_KILLFOCUS)
        {
            UpdateHoverFromCursor();
            PaintCustomChrome();
        }
    }
}