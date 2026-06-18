using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace KeyboardWipeLock;

/// <summary>
/// Centralized type scale so fonts stay consistent across the whole app.
/// All built from a single family with a small, deliberate set of sizes.
/// (Shared instances — never dispose these.)
/// </summary>
internal static class Fonts
{
    private const string Family = "Segoe UI";
    private const string Semibold = "Segoe UI Semibold";

    // --- Main window ---
    public static readonly Font Title       = new(Semibold, 15f);
    public static readonly Font Button      = new(Semibold, 15f);
    public static readonly Font StatusText  = new(Semibold, 12f);
    public static readonly Font OptionTitle = new(Family, 10f);
    public static readonly Font Caption     = new(Family, 9f);
    public static readonly Font StatusDot   = new(Family, 14f);

    // --- Stepper ---
    public static readonly Font StepperValue  = new(Semibold, 12f);
    public static readonly Font StepperSymbol = new(Family, 14f, FontStyle.Bold);

    // --- Lock overlay ---
    public static readonly Font EnableButton  = new(Semibold, 13f);
    public static readonly Font OverlayHeader = new(Family, 14f);
    public static readonly Font Countdown     = new(Family, 72f, FontStyle.Bold);
    public static readonly Font CountdownUnit = new(Family, 12f);
    public static readonly Font OverlayHint   = new(Family, 12f);
    public static readonly Font LockedWord    = new(Family, 38f, FontStyle.Bold);
}

internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(24, 26, 33);
    public static readonly Color Panel = Color.FromArgb(33, 36, 46);
    public static readonly Color PanelHi = Color.FromArgb(43, 47, 60);
    public static readonly Color Text = Color.FromArgb(235, 238, 245);
    public static readonly Color SubText = Color.FromArgb(150, 156, 170);
    public static readonly Color Accent = Color.FromArgb(86, 130, 255);
    public static readonly Color Danger = Color.FromArgb(235, 72, 86);
    public static readonly Color Success = Color.FromArgb(58, 196, 122);

    public static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(r);
            path.CloseFigure();
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // Windows 11 niceties: dark title bar + rounded window corners.
    public static void ApplyWindowChrome(IntPtr handle)
    {
        try
        {
            int useDark = 1;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            int pref = DWMWCP_ROUND;
            DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        }
        catch
        {
            // Older Windows: silently ignore.
        }
    }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}

/// <summary>A flat, rounded, hover-animated button.</summary>
internal sealed class RoundButton : Button
{
    private bool _hover;
    private bool _down;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 14;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color BaseColor { get; set; } = Theme.Accent;

    public RoundButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        ForeColor = Color.White;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

    private static Color Shift(Color c, int amount)
    {
        int R = Math.Clamp(c.R + amount, 0, 255);
        int G = Math.Clamp(c.G + amount, 0, 255);
        int B = Math.Clamp(c.B + amount, 0, 255);
        return Color.FromArgb(c.A, R, G, B);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        // Paint the parent's background so the rounded corners blend in (no black corners).
        g.Clear(Parent?.BackColor ?? BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        Color fill = BaseColor;
        if (!Enabled) fill = Color.FromArgb(70, 74, 86);
        else if (_down) fill = Shift(BaseColor, -22);
        else if (_hover) fill = Shift(BaseColor, 18);

        using var path = Theme.RoundedRect(rect, Radius);
        using (var b = new SolidBrush(fill))
            g.FillPath(b, path);

        var fg = Enabled ? ForeColor : Theme.SubText;
        TextRenderer.DrawText(g, Text, Font, rect, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
    }
}

/// <summary>A modern toggle switch.</summary>
internal sealed class ToggleSwitch : CheckBox
{
    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
        AutoSize = false;
        Size = new Size(46, 24);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        // Fill with the parent's colour so the rounded track blends in (no ghost halo).
        g.Clear(Parent?.BackColor ?? BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var track = new Rectangle(0, 2, 44, 20);
        Color on = Enabled ? Theme.Accent : Color.FromArgb(70, 74, 86);
        Color off = Color.FromArgb(64, 68, 82);
        using (var b = new SolidBrush(Checked ? on : off))
        using (var p = Theme.RoundedRect(track, 10))
            g.FillPath(b, p);

        int knob = 16;
        int x = Checked ? track.Right - knob - 2 : track.X + 2;
        using var kb = new SolidBrush(Enabled ? Color.White : Color.FromArgb(150, 150, 160));
        g.FillEllipse(kb, x, track.Y + 2, knob, knob);
    }
}

/// <summary>A compact, theme-styled "−  value  +" stepper pill.</summary>
internal sealed class NumberStepper : Control
{
    private int _value = 30;
    private int _hover; // 0 none, 1 minus, 2 plus

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Min { get; set; } = 5;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Max { get; set; } = 600;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Step { get; set; } = 5;

    public event EventHandler? ValueChanged;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            int v = Math.Clamp(value, Min, Max);
            if (v == _value) return;
            _value = v;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public NumberStepper()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer, true);
        Size = new Size(116, 36);
        Cursor = Cursors.Hand;
    }

    private Rectangle MinusRect => new(0, 0, 38, Height);
    private Rectangle PlusRect => new(Width - 38, 0, 38, Height);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = MinusRect.Contains(e.Location) ? 1 : PlusRect.Contains(e.Location) ? 2 : 0;
        if (h != _hover) { _hover = h; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hover != 0) { _hover = 0; Invalidate(); }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!Enabled) return;
        if (MinusRect.Contains(e.Location)) Value -= Step;
        else if (PlusRect.Contains(e.Location)) Value += Step;
        base.OnMouseDown(e);
    }

    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        Color pill = Enabled ? Theme.PanelHi : Color.FromArgb(40, 43, 54);
        using (var b = new SolidBrush(pill))
        using (var p = Theme.RoundedRect(rect, Height / 2))
            g.FillPath(b, p);

        // Hover highlight on the active half (rounded ends).
        if (Enabled && _hover != 0)
        {
            var hr = _hover == 1 ? MinusRect : PlusRect;
            using var hb = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
            var clip = g.Clip;
            g.SetClip(Theme.RoundedRect(rect, Height / 2));
            g.FillRectangle(hb, hr);
            g.Clip = clip;
        }

        Color glyph = Enabled ? Theme.Text : Theme.SubText;
        using var gb = new SolidBrush(glyph);
        using var vb = new SolidBrush(Enabled ? Theme.Text : Theme.SubText);
        var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        g.DrawString("\u2212", Fonts.StepperSymbol, gb, MinusRect, center);   // minus
        g.DrawString("+", Fonts.StepperSymbol, gb, PlusRect, center);         // plus
        var midRect = new Rectangle(MinusRect.Right, 0, PlusRect.Left - MinusRect.Right, Height);
        g.DrawString(_value.ToString(), Fonts.StepperValue, vb, midRect, center); // value
    }
}
