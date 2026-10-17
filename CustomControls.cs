using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

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