using System.Drawing.Drawing2D;

namespace ZeusControl;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(9, 11, 18);
    public static readonly Color Sidebar = Color.FromArgb(13, 16, 25);
    public static readonly Color Panel = Color.FromArgb(18, 22, 34);
    public static readonly Color Panel2 = Color.FromArgb(34, 40, 57);
    public static readonly Color Text = Color.FromArgb(244, 246, 255);
    public static readonly Color Muted = Color.FromArgb(126, 135, 156);
    public static readonly Color Purple = Color.FromArgb(139, 92, 246);
    public static readonly Color Green = Color.FromArgb(52, 211, 153);
    public static readonly Color Red = Color.FromArgb(251, 113, 133);
    public static readonly Color Gold = Color.FromArgb(251, 191, 36);
    public static readonly Font Body = new("Segoe UI", 10f, FontStyle.Regular);
    public static readonly Font BodyBold = new("Segoe UI", 10f, FontStyle.Bold);
    public static readonly Font Small = new("Segoe UI", 8.5f, FontStyle.Bold);
    public static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var p = new GraphicsPath(); int d = radius * 2;
        p.AddArc(r.Left, r.Top, d, d, 180, 90); p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
}

internal sealed class CardPanel : Panel
{
    public int Radius { get; set; } = 16;
    public CardPanel() { DoubleBuffered = true; BackColor = Theme.Panel; Padding = new Padding(20); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var p = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), Radius);
        using var b = new SolidBrush(BackColor); e.Graphics.FillPath(b, p);
    }
}

internal sealed class ZeusButton : Button
{
    public Color Accent { get; set; } = Theme.Panel2;
    public ZeusButton()
    {
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Color.Transparent;
        ForeColor = Theme.Text; Font = Theme.BodyBold; Cursor = Cursors.Hand; Height = 42;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var c = MouseButtons == MouseButtons.Left && ClientRectangle.Contains(PointToClient(Cursor.Position)) ? ControlPaint.Light(Accent, .08f) : Accent;
        using var p = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 9); using var b = new SolidBrush(c); e.Graphics.FillPath(b, p);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class ZeusSlider : Control
{
    private int value = 50; private bool dragging;
    public event EventHandler? ValueChanged;
    public int Value { get => value; set { var v = Math.Clamp(value, 0, 100); if (v == this.value) return; this.value = v; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); } }
    public Color Accent { get; set; } = Theme.Purple;
    public ZeusSlider() { DoubleBuffered = true; Height = 30; Cursor = Cursors.Hand; TabStop = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; int cy = Height / 2, x1 = 9, x2 = Width - 9, pos = x1 + (x2 - x1) * value / 100;
        using var idle = new Pen(Color.FromArgb(55, 62, 82), 6) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var active = new Pen(Accent, 6) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawLine(idle, x1, cy, x2, cy); e.Graphics.DrawLine(active, x1, cy, pos, cy);
        using var glow = new SolidBrush(Color.FromArgb(45, Accent)); e.Graphics.FillEllipse(glow, pos - 10, cy - 10, 20, 20);
        using var knob = new SolidBrush(Theme.Text); e.Graphics.FillEllipse(knob, pos - 6, cy - 6, 12, 12);
    }
    private void SetFromMouse(int x) { Value = (int)Math.Round(Math.Clamp((x - 9d) / Math.Max(1, Width - 18), 0, 1) * 100); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { dragging = true; Capture = true; Focus(); SetFromMouse(e.X); } }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) SetFromMouse(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; Capture = false; }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode is Keys.Left or Keys.Down) Value--; if (e.KeyCode is Keys.Right or Keys.Up) Value++; base.OnKeyDown(e); }
}

internal sealed class HeadsetVisual : Control
{
    public Color RgbColor { get; set; } = Theme.Purple;
    public bool MicMuted { get; set; }
    public int Volume { get; set; } = 50;
    public HeadsetVisual() { DoubleBuffered = true; BackColor = Color.Transparent; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; float s = Math.Min(Width / 450f, Height / 360f); g.TranslateTransform((Width - 450 * s) / 2, (Height - 360 * s) / 2); g.ScaleTransform(s, s);
        for (int i = 4; i > 0; i--) { using var glow = new SolidBrush(Color.FromArgb(7 + Volume / 8, RgbColor)); g.FillEllipse(glow, 100 - i * 8, 35 - i * 8, 250 + i * 16, 250 + i * 16); }
        using (var band = new Pen(Color.FromArgb(83, 91, 111), 28) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(band, 95, 42, 260, 260, 188, 164);
        using (var band2 = new Pen(Color.FromArgb(225, 229, 240), 7) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(band2, 102, 50, 246, 246, 188, 164);
        DrawCup(g, 48, 168, "R"); DrawCup(g, 310, 168, "L");
        using var mic = new Pen(Color.FromArgb(124, 132, 153), 7) { StartCap = LineCap.Round, EndCap = LineCap.Round }; g.DrawLines(mic, [new Point(78, 286), new Point(33, 326), new Point(86, 326)]);
        using var led = new SolidBrush(MicMuted ? Theme.Red : Theme.Green); g.FillEllipse(78, 318, 17, 17);
    }
    private void DrawCup(Graphics g, int x, int y, string side)
    {
        using var shell = Theme.Rounded(new Rectangle(x, y, 94, 137), 30); using var sb = new SolidBrush(Color.FromArgb(36, 42, 58)); using var pen = new Pen(RgbColor, 3); g.FillPath(sb, shell); g.DrawPath(pen, shell);
        using var light = new SolidBrush(Color.FromArgb(200, RgbColor)); g.FillEllipse(light, x + 21, y + 41, 52, 52);
        using var f = new Font("Segoe UI", 12, FontStyle.Bold); TextRenderer.DrawText(g, side, f, new Rectangle(x + 21, y + 41, 52, 52), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

internal sealed class EqualizerControl : Control
{
    private readonly int[] values = new int[10]; private int active = -1;
    public int[] Values { get => [.. values]; set { for (int i = 0; i < Math.Min(10, value.Length); i++) values[i] = Math.Clamp(value[i], -12, 12); Invalidate(); } }
    public EqualizerControl() { DoubleBuffered = true; BackColor = Color.Transparent; Cursor = Cursors.Hand; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; string[] f = ["31", "62", "125", "250", "500", "1k", "2k", "4k", "8k", "16k"];
        int top = 45, bottom = Height - 48, step = Width / 10;
        for (int i = 0; i < 10; i++) { int x = step / 2 + i * step, y = bottom - (values[i] + 12) * (bottom - top) / 24; using var idle = new Pen(Color.FromArgb(55, 62, 82), 5); using var on = new Pen(Theme.Purple, 5); e.Graphics.DrawLine(idle, x, top, x, bottom); e.Graphics.DrawLine(on, x, y, x, bottom); using var b = new SolidBrush(Theme.Text); e.Graphics.FillEllipse(b, x - 7, y - 7, 14, 14); TextRenderer.DrawText(e.Graphics, $"{values[i]:+0;-0;0} dB", Theme.Small, new Rectangle(x - 35, 5, 70, 25), Theme.Text, TextFormatFlags.HorizontalCenter); TextRenderer.DrawText(e.Graphics, f[i], Theme.Small, new Rectangle(x - 30, bottom + 12, 60, 22), Theme.Muted, TextFormatFlags.HorizontalCenter); }
    }
    private void UpdateValue(Point p) { int step = Math.Max(1, Width / 10); int i = active >= 0 ? active : Math.Clamp(p.X / step, 0, 9); int top = 45, bottom = Height - 48; values[i] = Math.Clamp(12 - (p.Y - top) * 24 / Math.Max(1, bottom - top), -12, 12); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { active = Math.Clamp(e.X / Math.Max(1, Width / 10), 0, 9); Capture = true; UpdateValue(e.Location); } }
    protected override void OnMouseMove(MouseEventArgs e) { if (active >= 0) UpdateValue(e.Location); }
    protected override void OnMouseUp(MouseEventArgs e) { active = -1; Capture = false; }
}
