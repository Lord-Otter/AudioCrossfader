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

public class ModernColorPickerDialog : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // Google Docs 10x8 Palette (Row 0: Black -> White, Row 1: 10 Base Colors, Rows 2..7: Light -> Dark Hues)
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