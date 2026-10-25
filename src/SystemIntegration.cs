using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WinSpeed
{
    /// <summary>"Start with Windows" via HKCU\...\Run (no admin rights needed).</summary>
    static class StartupManager
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        static string Command => "\"" + Application.ExecutablePath + "\" --startup";

        public static bool Enabled
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                        return k?.GetValue(Program.AppName) is string v && v.IndexOf(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                catch { return false; }
            }
            set
            {
                try
                {
                    using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (value) k.SetValue(Program.AppName, Command);
                        else if (k.GetValue(Program.AppName) != null) k.DeleteValue(Program.AppName, false);
                    }
                }
                catch (Exception ex) { Program.Log(ex); }
            }
        }
    }

    /// <summary>Global hotkey support ("Ctrl+Alt+N" style strings).</summary>
    sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        const int Id = 0x5753;
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;
        bool registered;
        public event Action Pressed;

        public HotkeyWindow() { CreateHandle(new CreateParams()); }

        public bool Register(string text)
        {
            Unregister();
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (!TryParse(text, out uint mods, out Keys key)) return false;
            registered = Native.RegisterHotKey(Handle, Id, mods | MOD_NOREPEAT, (uint)key);
            return registered;
        }

        public void Unregister()
        {
            if (registered) Native.UnregisterHotKey(Handle, Id);
            registered = false;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && (int)m.WParam == Id) Pressed?.Invoke();
            base.WndProc(ref m);
        }

        public static bool TryParse(string text, out uint mods, out Keys key)
        {
            mods = 0; key = Keys.None;
            foreach (var raw in text.Split('+'))
            {
                var part = raw.Trim();
                if (part.Length == 0) continue;
                switch (part.ToLowerInvariant())
                {
                    case "ctrl": case "control": mods |= MOD_CONTROL; break;
                    case "alt": mods |= MOD_ALT; break;
                    case "shift": mods |= MOD_SHIFT; break;
                    case "win": case "windows": mods |= MOD_WIN; break;
                    default:
                        if (char.IsDigit(part[0])) return false; // "5" would parse as a raw key code; digits are named D0-D9
                        try { key = (Keys)Enum.Parse(typeof(Keys), part, true); }
                        catch { return false; }
                        break;
                }
            }
            return key != Keys.None && mods != 0;
        }

        public static string Format(Keys keyData)
        {
            var keyCode = keyData & Keys.KeyCode;
            if (keyCode == Keys.ControlKey || keyCode == Keys.ShiftKey || keyCode == Keys.Menu || keyCode == Keys.LWin || keyCode == Keys.RWin || keyCode == Keys.None)
                return null;
            var parts = new List<string>();
            if ((keyData & Keys.Control) != 0) parts.Add("Ctrl");
            if ((keyData & Keys.Alt) != 0) parts.Add("Alt");
            if ((keyData & Keys.Shift) != 0) parts.Add("Shift");
            if (parts.Count == 0) return null;
            parts.Add(keyCode.ToString());
            return string.Join("+", parts);
        }

        public void Dispose()
        {
            Unregister();
            DestroyHandle();
        }
    }
}
