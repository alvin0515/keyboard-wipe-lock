using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace KeyboardWipeLock;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private RoundButton _toggleButton = null!;
    private Label _statusDot = null!;
    private Label _statusText = null!;
    private ToggleSwitch _blockMouse = null!;
    private ToggleSwitch _autoEnable = null!;
    private NumberStepper _autoSeconds = null!;
    private Label _autoHint = null!;
    private OverlayForm? _overlay;

    private readonly System.Windows.Forms.Timer _tick;
    private readonly NotifyIcon _tray;

    private readonly LowLevelProc _keyboardProc;
    private readonly LowLevelProc _mouseProc;
    private IntPtr _keyboardHookId = IntPtr.Zero;
    private IntPtr _mouseHookId = IntPtr.Zero;
    private bool _locked;
    private bool _mouseLocked;
    private bool _hasTimer;
    private DateTime _lockStart;
    private int _totalSeconds;

    public MainForm()
    {
        _keyboardProc = KeyboardCallback;
        _mouseProc = MouseCallback;

        Text = "Keyboard Wipe Lock";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 564);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Fonts.OptionTitle;

        BuildUi();

        _tick = new System.Windows.Forms.Timer { Interval = 50 };
        _tick.Tick += OnTick;

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Show", null, (_, _) => RestoreWindow());
        trayMenu.Items.Add("Exit", null, (_, _) => Close());
        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Keyboard Wipe Lock",
            Visible = true,
            ContextMenuStrip = trayMenu
        };
        _tray.DoubleClick += (_, _) => RestoreWindow();

        Load += (_, _) => Theme.ApplyWindowChrome(Handle);
        FormClosing += OnClosing;
        UpdateMouseModeUi();
    }

    private void BuildUi()
    {
        // ---- Header ----
        var header = new Label
        {
            Text = "Keyboard Wipe Lock",
            AutoSize = true,
            Location = new Point(22, 18),
            Font = Fonts.Title,
            ForeColor = Theme.Text,
            BackColor = Color.Transparent
        };
        var headerSub = new Label
        {
            Text = "Safely clean your keyboard without misclicks",
            ForeColor = Theme.SubText,
            AutoSize = true,
            Location = new Point(24, 52),
            Font = Fonts.Caption,
            BackColor = Color.Transparent
        };

        // ---- Status card ----
        var statusCard = MakeCard(new Rectangle(20, 88, 380, 60));
        _statusDot = new Label
        {
            Text = "\u25CF",
            ForeColor = Theme.Success,
            Font = Fonts.StatusDot,
            AutoSize = true,
            Location = new Point(16, 18),
            BackColor = Color.Transparent
        };
        _statusText = new Label
        {
            Text = "Input is ENABLED",
            ForeColor = Theme.Text,
            Font = Fonts.StatusText,
            AutoSize = true,
            Location = new Point(44, 20),
            BackColor = Color.Transparent
        };
        statusCard.Controls.Add(_statusDot);
        statusCard.Controls.Add(_statusText);

        // ---- Main action button ----
        _toggleButton = new RoundButton
        {
            Text = "Disable Keyboard",
            BaseColor = Theme.Danger,
            Location = new Point(20, 166),
            Size = new Size(380, 72),
            Font = Fonts.Button,
            Radius = 18
        };
        _toggleButton.Click += (_, _) => Toggle();

        // ---- Options card ----
        var optCard = MakeCard(new Rectangle(20, 258, 380, 202));

        _blockMouse = new ToggleSwitch { Location = new Point(326, 24), Checked = false };
        _blockMouse.CheckedChanged += (_, _) => UpdateMouseModeUi();
        var blockLbl = new Label
        {
            Text = "Also block mouse",
            ForeColor = Theme.Text,
            Font = Fonts.OptionTitle,
            AutoSize = true,
            Location = new Point(18, 20),
            BackColor = Color.Transparent
        };
        var blockSub = new Label
        {
            Text = "Wipe the whole device — timer required",
            ForeColor = Theme.SubText,
            AutoSize = true,
            Location = new Point(18, 44),
            Font = Fonts.Caption,
            BackColor = Color.Transparent
        };

        var sep = new Panel
        {
            BackColor = Theme.PanelHi,
            Location = new Point(18, 92),
            Size = new Size(344, 1)
        };

        _autoEnable = new ToggleSwitch { Location = new Point(326, 116), Checked = true };
        var autoLbl = new Label
        {
            Text = "Auto re-enable",
            ForeColor = Theme.Text,
            Font = Fonts.OptionTitle,
            AutoSize = true,
            Location = new Point(18, 114),
            BackColor = Color.Transparent
        };
        _autoSeconds = new NumberStepper
        {
            Min = 5,
            Max = 600,
            Value = 30,
            Location = new Point(60, 148)
        };
        var afterLbl = new Label
        {
            Text = "after",
            ForeColor = Theme.SubText,
            AutoSize = true,
            Location = new Point(18, 157),
            Font = Fonts.Caption,
            BackColor = Color.Transparent
        };
        var secLbl = new Label
        {
            Text = "seconds",
            ForeColor = Theme.SubText,
            AutoSize = true,
            Location = new Point(184, 157),
            Font = Fonts.Caption,
            BackColor = Color.Transparent
        };
        optCard.Controls.AddRange(new Control[]
        {
            blockLbl, blockSub, _blockMouse, sep, autoLbl, _autoEnable, afterLbl, _autoSeconds, secLbl
        });

        // ---- Footer hint ----
        _autoHint = new Label
        {
            Text = "Use your mouse to click the button again to re-enable.",
            ForeColor = Theme.SubText,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(20, 474),
            Size = new Size(380, 64),
            Font = Fonts.Caption,
            BackColor = Color.Transparent
        };

        Controls.Add(_autoHint);
        Controls.Add(optCard);
        Controls.Add(_toggleButton);
        Controls.Add(statusCard);
        Controls.Add(headerSub);
        Controls.Add(header);
    }

    private static Panel MakeCard(Rectangle bounds)
    {
        var card = new Panel { Bounds = bounds, BackColor = Theme.Panel };
        card.Paint += (s, e) =>
        {
            var p = (Panel)s!;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Theme.RoundedRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 14);
            using var b = new SolidBrush(Theme.Panel);
            e.Graphics.Clear(Theme.Bg);
            e.Graphics.FillPath(b, path);
        };
        return card;
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void UpdateMouseModeUi()
    {
        if (_locked) return;

        if (_blockMouse.Checked)
        {
            _autoEnable.Checked = true;
            _autoEnable.Enabled = false;
            _autoHint.Text = "Mouse will be blocked too — the countdown timer is the ONLY way "
                           + "to restore input, so it is required in this mode.";
        }
        else
        {
            _autoEnable.Enabled = true;
            _autoHint.Text = "Use your mouse to click the button again to re-enable.";
        }
    }

    private void Toggle()
    {
        if (_locked) DisableLock();
        else EnableLock();
    }

    private void EnableLock()
    {
        bool blockMouse = _blockMouse.Checked;

        _keyboardHookId = SetHook(WH_KEYBOARD_LL, _keyboardProc);
        if (_keyboardHookId == IntPtr.Zero) { ShowHookError(); return; }

        if (blockMouse)
        {
            _mouseHookId = SetHook(WH_MOUSE_LL, _mouseProc);
            if (_mouseHookId == IntPtr.Zero) { Unhook(); ShowHookError(); return; }
            _mouseLocked = true;
        }

        _locked = true;
        _blockMouse.Enabled = false;
        _autoSeconds.Enabled = false;

        _statusDot.ForeColor = Theme.Danger;
        _statusText.Text = _mouseLocked ? "Keyboard + Mouse DISABLED" : "Keyboard is DISABLED";
        _toggleButton.Text = _mouseLocked ? "Locked — wait for timer" : "Enable Keyboard";
        _toggleButton.BaseColor = Theme.Success;
        _toggleButton.Enabled = !_mouseLocked;

        _hasTimer = _mouseLocked || _autoEnable.Checked;

        _overlay = new OverlayForm();
        _overlay.EnableRequested += (_, _) => DisableLock();
        _overlay.Configure(_hasTimer, _mouseLocked);
        _overlay.Show();
        _overlay.BringToFront();

        if (_hasTimer)
        {
            _totalSeconds = (int)_autoSeconds.Value;
            _lockStart = DateTime.Now;
            _overlay.SetCountdown(_totalSeconds, 1f);
            _tick.Start();
        }
        else
        {
            _overlay.SetCountdown(0, 0f);
        }
    }

    private void DisableLock()
    {
        _tick.Stop();
        Unhook();
        _locked = false;
        _mouseLocked = false;

        if (_overlay != null)
        {
            _overlay.Close();
            _overlay.Dispose();
            _overlay = null;
        }

        _blockMouse.Enabled = true;
        _autoSeconds.Enabled = true;
        _toggleButton.Enabled = true;
        _statusDot.ForeColor = Theme.Success;
        _statusText.Text = "Input is ENABLED";
        _toggleButton.Text = "Disable Keyboard";
        _toggleButton.BaseColor = Theme.Danger;
        UpdateMouseModeUi();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        double elapsed = (DateTime.Now - _lockStart).TotalSeconds;
        double remaining = _totalSeconds - elapsed;
        if (remaining <= 0)
        {
            DisableLock();
            return;
        }
        float fraction = (float)(remaining / _totalSeconds);
        _overlay?.SetCountdown((int)Math.Ceiling(remaining), fraction);
    }

    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        Unhook();
        _overlay?.Close();
        _tray.Visible = false;
        _tray.Dispose();
    }

    private void Unhook()
    {
        if (_keyboardHookId != IntPtr.Zero) { UnhookWindowsHookEx(_keyboardHookId); _keyboardHookId = IntPtr.Zero; }
        if (_mouseHookId != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHookId); _mouseHookId = IntPtr.Zero; }
    }

    private void ShowHookError()
    {
        MessageBox.Show(this,
            "Failed to install the input hook:\n" + new Win32Exception(Marshal.GetLastWin32Error()).Message,
            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static IntPtr SetHook(int hookType, LowLevelProc proc)
        => SetWindowsHookEx(hookType, proc, GetModuleHandle(null), 0);

    private IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == WM_KEYDOWN || msg == WM_KEYUP || msg == WM_SYSKEYDOWN || msg == WM_SYSKEYUP)
                return (IntPtr)1;
        }
        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0) return (IntPtr)1;
        return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
