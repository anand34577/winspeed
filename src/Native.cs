using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WinSpeed
{
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int cx, cy; public SIZE(int w, int h) { cx = w; cy = h; } }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        public const int WS_EX_LAYERED = 0x80000, WS_EX_TOOLWINDOW = 0x80, WS_EX_TOPMOST = 0x8,
                         WS_EX_NOACTIVATE = 0x08000000, WS_EX_TRANSPARENT = 0x20;
        public const int GWL_EXSTYLE = -20;
        public const int ULW_ALPHA = 2;
        public const byte AC_SRC_OVER = 0, AC_SRC_ALPHA = 1;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 0x10, SWP_NOOWNERZORDER = 0x200;
        public const int WM_HOTKEY = 0x312, WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string name);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int index);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int index, int value);
        [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);

        [DllImport("kernel32.dll")] public static extern uint GetCurrentProcessId();
        public delegate bool EnumProc(IntPtr h, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);
        public const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4;
        public const uint SWP_NOZORDER = 4;

        static bool IsCloaked(IntPtr h)
        {
            try { return DwmGetWindowAttribute(h, 14 /* DWMWA_CLOAKED */, out int c, 4) == 0 && c != 0; }
            catch { return false; }
        }

        static bool IgnoredClass(string cls)
        {
            switch (cls)
            {
                case "Shell_TrayWnd": case "Shell_SecondaryTrayWnd": case "TrayNotifyWnd": case "ReBarWindow32":
                case "MSTaskSwWClass": case "MSTaskListWClass": case "Progman": case "WorkerW":
                case "NotifyIconOverflowWindow": case "TopLevelWindowForOverflowXamlIsland":
                case "XamlExplorerHostIslandWindow": case "Windows.UI.Core.CoreWindow": case "TrayDummySearchControl":
                case "TaskListThumbnailWnd": case "Xaml_WindowedPopupClass": case "tooltips_class32":
                case "ForegroundStaging": case "MultitaskingViewFrame": case "EdgeUiInputTopWndClass":
                    return true;
                default: return false;
            }
        }

        /// <summary>A small window lying on the taskbar: a widget or tray extra from another app.</summary>
        static bool IsTaskbarWidget(Rectangle r, Rectangle bounds, bool horizontal)
        {
            if (r.Width < 8 || r.Height < 8) return false;
            var inter = Rectangle.Intersect(r, bounds);
            if (inter.Width * inter.Height < r.Width * r.Height * 0.6) return false;
            return horizontal ? r.Width <= bounds.Width / 3 && r.Height <= bounds.Height + 4
                              : r.Height <= bounds.Height / 3 && r.Width <= bounds.Width + 4;
        }

        /// <summary>
        /// Rectangles of other apps' small windows (widgets, tray extras) that sit on the taskbar, so the meter can step aside.
        /// </summary>
        public static System.Collections.Generic.List<Rectangle> ForeignTaskbarWindows(IntPtr taskbar, Rectangle bounds, IntPtr self)
        {
            var list = new System.Collections.Generic.List<Rectangle>();
            uint me = GetCurrentProcessId();
            bool horizontal = bounds.Width >= bounds.Height;
            void Consider(IntPtr h)
            {
                if (h == self || h == taskbar || !IsWindowVisible(h)) return;
                GetWindowThreadProcessId(h, out uint pid);
                if (pid == me || IgnoredClass(ClassOf(h)) || !GetWindowRect(h, out var wr)) return;
                var r = wr.ToRectangle();
                if (IsTaskbarWidget(r, bounds, horizontal)) list.Add(r);
            }
            EnumWindows((h, _) => { Consider(h); return true; }, IntPtr.Zero);
            // direct children only (EnumChildWindows is recursive and would also report the tray's internals)
            EnumChildWindows(taskbar, (h, _) => { if (GetAncestor(h, 1 /* GA_PARENT */) == taskbar) Consider(h); return true; }, IntPtr.Zero);
            return list;
        }

        /// <summary>
        /// True if a real window (an app dragged or raised over the taskbar, a topmost tool) is stacked above the
        /// taskbar and covers part of <paramref name="area"/>. Taskbar widgets, shell flyouts, tooltips,
        /// click-through overlays and cloaked windows don't count.
        /// </summary>
        public static bool IsCoveredByWindow(IntPtr taskbar, Rectangle taskbarBounds, Rectangle area, IntPtr self)
        {
            uint me = GetCurrentProcessId();
            bool horizontal = taskbarBounds.Width >= taskbarBounds.Height;
            bool covered = false;
            EnumWindows((h, _) =>
            {
                if (h == taskbar) return false;           // everything below the taskbar can't cover it
                if (h == self || !IsWindowVisible(h)) return true;
                GetWindowThreadProcessId(h, out uint pid);
                if (pid == me) return true;
                if ((GetWindowLong(h, GWL_EXSTYLE) & WS_EX_TRANSPARENT) != 0) return true;
                if (!GetWindowRect(h, out var wr)) return true;
                var r = wr.ToRectangle();
                if (!r.IntersectsWith(area) || IgnoredClass(ClassOf(h)) || IsCloaked(h)) return true;
                if (IsTaskbarWidget(r, taskbarBounds, horizontal)) return true;
                covered = true;
                return false;
            }, IntPtr.Zero);
            return covered;
        }

        public delegate void WinEventProc(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
        public const uint EVENT_SYSTEM_FOREGROUND = 3, WINEVENT_OUTOFCONTEXT = 0, WINEVENT_SKIPOWNPROCESS = 2;
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);

        /// <summary>Taskbar, tray overflow, Start, search and other shell surfaces that raise the taskbar above us.</summary>
        public static bool IsShellSurface(IntPtr h)
        {
            if (h == IntPtr.Zero) return false;
            switch (ClassOf(h))
            {
                case "Shell_TrayWnd": case "Shell_SecondaryTrayWnd": case "NotifyIconOverflowWindow":
                case "TopLevelWindowForOverflowXamlIsland": case "XamlExplorerHostIslandWindow":
                case "Windows.UI.Core.CoreWindow": case "TaskListThumbnailWnd": case "Xaml_WindowedPopupClass":
                    return true;
                default: return false;
            }
        }

        public static string ClassOf(IntPtr h)
        {
            var sb = new StringBuilder(256);
            GetClassName(h, sb, sb.Capacity);
            return sb.ToString();
        }

        /// <summary>True if the foreground window covers the whole screen (game, video, presentation).</summary>
        public static bool IsFullscreenAppActive(Screen onScreen)
        {
            var fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == GetShellWindow()) return false;
            var cls = ClassOf(fg);
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd"
                || cls == "XamlExplorerHostIslandWindow" || cls == "Windows.UI.Core.CoreWindow") return false;
            if (!GetWindowRect(fg, out var r)) return false;
            var rect = r.ToRectangle();
            var scr = Screen.FromHandle(fg);
            if (onScreen != null && !scr.Bounds.Equals(onScreen.Bounds)) return false;
            var b = scr.Bounds;
            return rect.Left <= b.Left && rect.Top <= b.Top && rect.Right >= b.Right && rect.Bottom >= b.Bottom;
        }
    }

    static class Dpi
    {
        static float? scale;
        public static float Scale
        {
            get
            {
                if (scale == null)
                    using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
                return scale.Value;
            }
        }
        public static float Value => Scale * 96f;
        public static int S(int v) => (int)Math.Round(v * Scale);
    }

    static class Theme
    {
        static bool? light;
        public static bool TaskbarLight
        {
            get
            {
                if (light == null)
                {
                    try
                    {
                        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                            light = k?.GetValue("SystemUsesLightTheme") is int v && v == 1;
                    }
                    catch { light = false; }
                }
                return light.Value;
            }
        }
        public static void Invalidate() => light = null;
        public static Color TaskbarForeground => TaskbarLight ? Color.FromArgb(24, 24, 24) : Color.FromArgb(245, 245, 245);
        public static bool IsDark(Color c) => (c.R * 299 + c.G * 587 + c.B * 114) / 1000 < 128;
    }

    struct TaskbarInfo
    {
        public bool Found, Horizontal, Visible;
        public IntPtr Handle;
        public Rectangle Bounds, Tray;
        public Screen Screen;

        public static TaskbarInfo Get()
        {
            var t = new TaskbarInfo();
            var h = Native.FindWindow("Shell_TrayWnd", null);
            if (h == IntPtr.Zero || !Native.GetWindowRect(h, out var r)) return t;
            t.Found = true;
            t.Handle = h;
            t.Bounds = r.ToRectangle();
            t.Horizontal = t.Bounds.Width >= t.Bounds.Height;
            t.Screen = Screen.FromRectangle(t.Bounds);
            var inter = Rectangle.Intersect(t.Bounds, t.Screen.Bounds);
            t.Visible = (t.Horizontal ? inter.Height : inter.Width) > Dpi.S(8);

            var n = Native.FindWindowEx(h, IntPtr.Zero, "TrayNotifyWnd", null);
            if (n != IntPtr.Zero && Native.GetWindowRect(n, out var nr) && nr.Right > nr.Left)
                t.Tray = nr.ToRectangle();
            else if (t.Horizontal)
                t.Tray = Rectangle.FromLTRB(t.Bounds.Right - Dpi.S(260), t.Bounds.Top, t.Bounds.Right, t.Bounds.Bottom);
            else
                t.Tray = Rectangle.FromLTRB(t.Bounds.Left, t.Bounds.Bottom - Dpi.S(200), t.Bounds.Right, t.Bounds.Bottom);
            return t;
        }
    }
}
