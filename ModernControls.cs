using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DesktopHide;

internal sealed class ModernCard : Panel
{
    public Color SurfaceColor { get; set; } = Color.FromArgb(22, 29, 42);
    public Color StrokeColor { get; set; } = Color.FromArgb(42, 54, 74);
    public int CornerRadius { get; set; } = 20;

    public ModernCard()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(FindSurfaceColor(this));
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Shape(0.5f);
        using var brush = new SolidBrush(SurfaceColor);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Shape(0.8f);
        using var pen = new Pen(StrokeColor, 1f);
        e.Graphics.DrawPath(pen, path);
    }

    private GraphicsPath Shape(float inset)
    {
        float x = inset, y = inset, w = Math.Max(1, Width - inset * 2), h = Math.Max(1, Height - inset * 2);
        float d = Math.Min(CornerRadius * 2, Math.Min(w, h));
        var path = new GraphicsPath();
        path.AddArc(x, y, d, d, 180, 90); path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90); path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static Color FindSurfaceColor(Control control)
    {
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
            if (parent is ModernCard card) return card.SurfaceColor;
        return control.Parent?.BackColor is { A: 255 } color ? color : Color.FromArgb(12, 17, 27);
    }
}

internal sealed class ModernButton : Button
{
    public Color FillColor { get; set; } = Color.FromArgb(35, 45, 63);
    public Color AccentColor { get; set; } = Color.FromArgb(91, 126, 255);
    public Color OutlineColor { get; set; } = Color.FromArgb(57, 70, 92);
    public bool Primary { get; set; }
    public int CornerRadius { get; set; } = 14;
    private bool hovered, pressed;

    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        TextAlign = ContentAlignment.MiddleCenter;
        ForeColor = Color.FromArgb(235, 240, 250);
    }

    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(ModernCard.FindSurfaceColor(this));
        var rect = new RectangleF(0.7f, 0.7f, Math.Max(1, Width - 1.4f), Math.Max(1, Height - 1.4f));
        using var path = Rounded(rect, CornerRadius);
        var fill = Primary ? AccentColor : FillColor;
        if (!Enabled) fill = Color.FromArgb(42, fill);
        else if (pressed) fill = ControlPaint.Dark(fill, 0.08f);
        else if (hovered) fill = ControlPaint.Light(fill, 0.06f);
        using Brush brush = Primary
            ? new LinearGradientBrush(rect, Color.FromArgb(116, 146, 255), fill, 90f)
            : new SolidBrush(fill);
        e.Graphics.FillPath(brush, path);
        if (!Primary)
        {
            using var pen = new Pen(OutlineColor, 1f);
            e.Graphics.DrawPath(pen, path);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, Rectangle.Round(rect), Enabled ? ForeColor : Color.FromArgb(130, 139, 154), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }

    internal static GraphicsPath Rounded(RectangleF rect, int radius)
    {
        float d = Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height));
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ModernToggle : Control
{
    private bool isChecked;
    private bool hovered;
    public bool Checked
    {
        get => isChecked;
        set { if (isChecked == value) return; isChecked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
    }
    public event EventHandler? CheckedChanged;

    public ModernToggle()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        ForeColor = Color.FromArgb(220, 228, 241);
        Font = new Font("Segoe UI", 10.5f);
        Height = 36;
        AccessibleRole = AccessibleRole.CheckButton;
    }

    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; } base.OnKeyDown(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(ModernCard.FindSurfaceColor(this));
        const int sw = 44, sh = 25;
        var toggle = new Rectangle(0, (Height - sh) / 2, sw, sh);
        using (var track = new SolidBrush(Checked ? Color.FromArgb(74, 130, 255) : Color.FromArgb(63, 72, 90)))
        using (var trackPath = ModernButton.Rounded(toggle, sh / 2)) e.Graphics.FillPath(track, trackPath);
        int thumbX = Checked ? toggle.Right - sh : toggle.X;
        if (hovered) thumbX += Checked ? -1 : 1;
        var thumb = new Rectangle(thumbX + 2, toggle.Y + 2, sh - 4, sh - 4);
        using (var thumbBrush = new SolidBrush(Color.White)) e.Graphics.FillEllipse(thumbBrush, thumb);
        var textRect = new Rectangle(sw + 13, 0, Math.Max(0, Width - sw - 13), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textRect, ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
}

internal sealed class ModernSlider : Control
{
    private int value;
    private bool dragging;
    public int Minimum { get; set; } = 0;
    public int Maximum { get; set; } = 100;
    public int Value
    {
        get => value;
        set { int next = Math.Clamp(value, Minimum, Maximum); if (this.value == next) return; this.value = next; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); }
    }
    public event EventHandler? ValueChanged;

    public ModernSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Height = 38;
        AccessibleRole = AccessibleRole.Slider;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(ModernCard.FindSurfaceColor(this));
        int pad = 11, cy = Height / 2, trackWidth = Math.Max(1, Width - pad * 2);
        float pct = Maximum == Minimum ? 0f : (Value - Minimum) / (float)(Maximum - Minimum);
        int filled = (int)(trackWidth * pct);
        using (var inactive = new SolidBrush(Color.FromArgb(59, 69, 87))) e.Graphics.FillRoundedRectangle(inactive, new Rectangle(pad, cy - 3, trackWidth, 6), 3);
        if (filled > 0) using (var active = new SolidBrush(Color.FromArgb(92, 132, 255))) e.Graphics.FillRoundedRectangle(active, new Rectangle(pad, cy - 3, filled, 6), 3);
        int thumbX = pad + filled;
        var thumb = new Rectangle(thumbX - 9, cy - 9, 18, 18);
        using (var shadow = new SolidBrush(Color.FromArgb(55, 0, 0, 0))) e.Graphics.FillEllipse(shadow, thumb.X, thumb.Y + 1, thumb.Width, thumb.Height);
        using (var knob = new SolidBrush(Color.White)) e.Graphics.FillEllipse(knob, thumb);
        using var outline = new Pen(Color.FromArgb(215, 224, 243), 1f);
        e.Graphics.DrawEllipse(outline, thumb);
    }

    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { dragging = true; SetFromX(e.X); Capture = true; } base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (dragging) SetFromX(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { dragging = false; Capture = false; } base.OnMouseUp(e); }
    private void SetFromX(int x) { int pad = 11, width = Math.Max(1, Width - pad * 2); Value = Minimum + (int)Math.Round(Math.Clamp((x - pad) / (double)width, 0, 1) * (Maximum - Minimum)); }

}

internal sealed class ModernComboBox : Control
{
    private readonly List<string> items = new();
    private int selectedIndex = -1;
    public List<string> Items => items;
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            int next = value >= 0 && value < items.Count ? value : -1;
            if (selectedIndex == next) return;
            selectedIndex = next;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler? SelectedIndexChanged;

    public ModernComboBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        Height = 36;
        Width = 205;
        ForeColor = Color.FromArgb(224, 232, 245);
        AccessibleRole = AccessibleRole.ComboBox;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var fill = new SolidBrush(ModernCard.FindSurfaceColor(this))) e.Graphics.FillRoundedRectangle(fill, new Rectangle(0, 0, Width - 1, Height - 1), 8);
        using (var outline = new Pen(Color.FromArgb(51, 66, 89), 1))
        using (var shape = ModernButton.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 8)) e.Graphics.DrawPath(outline, shape);
        var text = selectedIndex >= 0 ? items[selectedIndex] : "Vyberte…";
        TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(12, 0, Math.Max(1, Width - 42), Height), ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        using var arrow = new Pen(Color.FromArgb(169, 190, 221), 1.8f);
        int cx = Width - 18, cy = Height / 2;
        e.Graphics.DrawLines(arrow, new[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        var menu = new ContextMenuStrip { BackColor = Color.FromArgb(30, 39, 54), ForeColor = ForeColor, ShowImageMargin = false };
        for (int i = 0; i < items.Count; i++)
        {
            int itemIndex = i;
            var option = new ToolStripMenuItem(items[i]) { Checked = i == selectedIndex, CheckOnClick = false };
            option.Click += (_, _) => SelectedIndex = itemIndex;
            menu.Items.Add(option);
        }
        menu.Closed += (_, _) => menu.Dispose();
        menu.Show(this, new Point(0, Height));
    }
}

internal sealed class StatusIndicator : Control
{
    public StatusIndicator()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int diameter = Math.Min(Width, Height) - 2;
        var circle = new Rectangle((Width - diameter) / 2, (Height - diameter) / 2, diameter, diameter);
        using (var green = new SolidBrush(Color.FromArgb(20, 165, 106))) e.Graphics.FillEllipse(green, circle);
        using var pen = new Pen(Color.FromArgb(231, 255, 243), Math.Max(2, diameter / 18f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var screen = new Rectangle(circle.X + diameter / 4, circle.Y + diameter / 4, diameter / 2, diameter / 3);
        e.Graphics.DrawRoundedRectangle(pen, screen, 3);
        int mid = circle.X + diameter / 2, baseY = screen.Bottom + diameter / 9;
        e.Graphics.DrawLine(pen, mid, screen.Bottom, mid, baseY);
        e.Graphics.DrawLine(pen, mid - diameter / 6, baseY, mid + diameter / 6, baseY);
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle rectangle, int radius)
    {
        using var path = ModernButton.Rounded(rectangle, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle rectangle, int radius)
    {
        using var path = ModernButton.Rounded(rectangle, radius);
        graphics.DrawPath(pen, path);
    }
}

