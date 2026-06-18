# Keyboard Wipe Lock

A small Windows desktop app that **temporarily disables your keyboard (and optionally your mouse)** so you can wipe and clean your device **without triggering any accidental keystrokes or misclicks**.

When you're done cleaning, input is restored — either with a mouse click or automatically via a built-in countdown timer.

---

## What it's for

Cleaning a keyboard normally means pressing dozens of keys, which can:

- Open random apps or menus
- Type garbage into whatever window is focused
- Trigger destructive shortcuts (e.g. `Ctrl`+`A`, `Delete`)

**Keyboard Wipe Lock** blocks all of that. Turn on *Wipe Mode*, scrub your keys/trackpad freely, and nothing you press registers. A safety timer guarantees you can never get permanently locked out.

---

## Features

- 🔒 **One-click keyboard lock** — blocks every keystroke instantly.
- 🖱️ **Optional mouse lock** — also block mouse clicks, movement, and scroll to clean the whole device.
- ⏱️ **Auto re-enable timer** — restores input automatically after a set time (5–600 seconds). **Required** when the mouse is blocked, so you always have a way out.
- 🎯 **Always-on-top window** — the re-enable button is always reachable.
- 📊 **Full-screen lock overlay** — a large animated countdown ring shows exactly when input returns.
- 🔔 **System tray icon** — double-click to show the window; right-click → **Exit** to quit.

---

## How to use

1. **Launch the app** (double-click `KeyboardWipeLock.exe` or the *Keyboard Wipe Lock* desktop shortcut).
2. **(Optional) Toggle "Also block mouse"** if you want to wipe the mouse/trackpad too.
   - When this is on, the **auto re-enable timer is required** (the mouse can't click the button to unlock).
3. **(Optional) Set the timer** with the `−  30  +` stepper (5 to 600 seconds).
4. **Click "Disable Keyboard."**
   - The screen dims and a countdown ring appears. Your keyboard (and mouse, if selected) is now dead.
5. **Wipe your keyboard.** Press anything — nothing registers.
6. **Re-enable input:**
   - **Keyboard-only mode:** click **"Re-enable now"** with your mouse, *or* wait for the timer.
   - **Mouse-blocked mode:** just wait for the countdown to reach zero — input restores automatically.

### Emergency exit
If anything ever seems stuck, **holding the physical power button** to restart the PC always works — the locks exist only while the app is running and disappear the moment it closes.

---

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) (or newer) on Windows.

```powershell
# Build
dotnet build -c Release

# Run
dotnet run -c Release
```

The compiled executable is produced at:

```
bin\Release\net10.0-windows\KeyboardWipeLock.exe
```

### Create a desktop shortcut (optional)

```powershell
$exe = "$PWD\bin\Release\net10.0-windows\KeyboardWipeLock.exe"
$lnk = [Environment]::GetFolderPath('Desktop') + "\Keyboard Wipe Lock.lnk"
$w = New-Object -ComObject WScript.Shell
$s = $w.CreateShortcut($lnk)
$s.TargetPath = $exe
$s.WorkingDirectory = Split-Path $exe
$s.IconLocation = "$exe,0"
$s.Save()
```

---

## Notes & permissions

- **No administrator rights required** for normal use.
- To block input even while an **elevated** (admin) window is focused, change `asInvoker` to `requireAdministrator` in [`app.manifest`](app.manifest) and rebuild.
- Windows only (uses Win32 low-level keyboard/mouse hooks and WinForms).

---

## How it works (technical)

The app installs Windows low-level hooks (`WH_KEYBOARD_LL` and, when enabled, `WH_MOUSE_LL`) via `SetWindowsHookEx`. While locked, the hook callbacks return a non-zero value to **swallow** input events before they reach any application. Removing the hooks (on re-enable, timer expiry, or app exit) instantly restores normal input.

---

## Project structure

| File | Purpose |
| --- | --- |
| `Program.cs` | App entry point, main window, lock/unlock logic, hooks, tray icon. |
| `OverlayForm.cs` | Full-screen countdown overlay shown while input is locked. |
| `Theme.cs` | Colors, shared font scale, and custom controls (rounded button, toggle switch, number stepper). |
| `app.manifest` | Execution level (privileges) and DPI settings. |
| `KeyboardWipeLock.csproj` | .NET project configuration. |
