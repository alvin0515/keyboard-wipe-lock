using System.Drawing.Drawing2D;

namespace KeyboardWipeLock;

/// <summary>
/// Full-screen dim overlay shown while input is locked. Displays an animated
/// countdown ring. In keyboard-only mode it also shows a mouse-clickable
/// "Re-enable now" button.
/// </summary>
internal sealed class OverlayForm : Form
{
    private readonly RoundButton _enableNow;
    private float _fraction = 1f;   // remaining / total  (1 -> full ring)
    private int _remaining;
    private bool _hasTimer;
    private bool _mouseBlocked;

    public event EventHandler? EnableRequested;

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen!.Bounds;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(10, 11, 15);
        Opacity = 0.92;
        DoubleBuffered = true;

        _enableNow = new RoundButton
        {
            Text = "Re-enable now",
            BaseColor = Theme.Success,
            Size = new Size(220, 56),
            Font = Fonts.EnableButton,
            Radius = 16,
            Visible = false
        };
        _enableNow.Click += (_, _) => EnableRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(_enableNow);
        Resize += (_, _) => LayoutButton();
    }

    public void Configure(bool hasTimer, bool mouseBlocked)
    {
        _hasTimer = hasTimer;
        _mouseBlocked = mouseBlocked;
        _enableNow.Visible = !mouseBlocked; // can't click when mouse is blocked
        LayoutButton();
        Invalidate();
    }

    public void SetCountdown(int remaining, float fraction)
    {
        _remaining = remaining;
        _fraction = Math.Clamp(fraction, 0f, 1f);
        Invalidate();
    }

    private void LayoutButton()
    {
        // Place well below the ring + footer hint so nothing overlaps.
        int cy = Height / 2 - 40;
        int ringBottom = cy + 150;          // ringSize 300 -> radius 150
        _enableNow.Location = new Point((Width - _enableNow.Width) / 2, ringBottom + 96);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using var center = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        int cx = Width / 2;
        int cy = Height / 2 - 40;
        int ringSize = 300;
        var ring = new Rectangle(cx - ringSize / 2, cy - ringSize / 2, ringSize, ringSize);

        // ---- Header (letter-spaced) ----
        using (var sub = new SolidBrush(Theme.SubText))
        {
            string header = Spaced(_mouseBlocked ? "KEYBOARD + MOUSE LOCKED" : "KEYBOARD LOCKED");
            var headerRect = new RectangleF(0, ring.Y - 92, Width, 40);
            g.DrawString(header, Fonts.OverlayHeader, sub, headerRect, center);
        }

        // ---- Track ring ----
        using (var trackPen = new Pen(Color.FromArgb(55, 255, 255, 255), 16f))
            g.DrawEllipse(trackPen, ring);

        if (_hasTimer)
        {
            // Progress arc (depletes clockwise)
            float sweep = 360f * _fraction;
            Color arcColor = _mouseBlocked ? Theme.Danger : Theme.Accent;
            using (var arcPen = new Pen(arcColor, 16f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(arcPen, ring, -90f, -sweep);

            // Big number occupies the UPPER portion of the ring...
            using var tb = new SolidBrush(Theme.Text);
            var numRect = new RectangleF(ring.X, ring.Y + 24, ring.Width, ring.Height - 88);
            g.DrawString(_remaining.ToString(), Fonts.Countdown, tb, numRect, center);

            // ...and the unit label sits in the LOWER portion (no overlap).
            using var lbr = new SolidBrush(Theme.SubText);
            var lblRect = new RectangleF(ring.X, ring.Bottom - 70, ring.Width, 30);
            g.DrawString(Spaced("SECONDS"), Fonts.CountdownUnit, lbr, lblRect, center);
        }
        else
        {
            using var tb = new SolidBrush(Theme.Text);
            g.DrawString("LOCKED", Fonts.LockedWord, tb, new RectangleF(ring.X, ring.Y, ring.Width, ring.Height), center);
        }

        // ---- Footer hint ----
        using (var hb = new SolidBrush(Theme.SubText))
        {
            string hint = _mouseBlocked
                ? "Wipe away — input restores automatically when the timer ends."
                : "Click \u201cRe-enable now\u201d with your mouse, or wait for the timer.";
            var footRect = new RectangleF(0, ring.Bottom + 28, Width, 36);
            g.DrawString(hint, Fonts.OverlayHint, hb, footRect, center);
        }
    }

    // Adds thin spacing between characters for a premium, uncramped header look.
    private static string Spaced(string s) => string.Join("\u2009", s.ToCharArray());
}
