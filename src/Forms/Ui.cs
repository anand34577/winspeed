using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WinSpeed
{
    /// <summary>Light / dark colour tokens for the app windows, plus a recursive themer.</summary>
    static class UiTheme
    {
        public static bool Dark { get; private set; }

        public static Color Page, Surface, Surface2, Border, Text, Muted, Accent, AccentHover, AccentText, Nav, NavHover, NavSelected, Input, Danger, Success, Warning;

        static UiTheme() { Resolve(AppTheme.System); }

        public static bool SystemAppsDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
            }
            catch { return false; }
        }

        public static void Resolve(AppTheme t)
        {
            Dark = t == AppTheme.Dark || (t == AppTheme.System && SystemAppsDark());
            if (Dark)
            {
                Page = C(0x16, 0x18, 0x1D); Surface = C(0x20, 0x23, 0x2A); Surface2 = C(0x2A, 0x2E, 0x37); Border = C(0x34, 0x39, 0x44);
                Text = C(0xEC, 0xEE, 0xF2); Muted = C(0x9A, 0xA3, 0xB2); Accent = C(0x4F, 0x9D, 0xFF); AccentHover = C(0x6B, 0xAE, 0xFF);
                AccentText = C(0x0B, 0x12, 0x20); Nav = C(0x12, 0x14, 0x18); NavHover = C(0x22, 0x26, 0x2E); NavSelected = C(0x26, 0x2B, 0x35);
                Input = C(0x1A, 0x1D, 0x23); Danger = C(0xFF, 0x6B, 0x6B); Success = C(0x3D, 0xD6, 0x8C); Warning = C(0xFF, 0xB0, 0x3B);
            }
            else
            {
                Page = C(0xF4, 0xF6, 0xF9); Surface = Color.White; Surface2 = C(0xEE, 0xF1, 0xF5); Border = C(0xE1, 0xE5, 0xEB);
                Text = C(0x14, 0x1A, 0x24); Muted = C(0x64, 0x6E, 0x7D); Accent = C(0x1F, 0x6F, 0xEB); AccentHover = C(0x18, 0x5E, 0xCC);
                AccentText = Color.White; Nav = C(0xFA, 0xFB, 0xFC); NavHover = C(0xEE, 0xF1, 0xF5); NavSelected = C(0xE6, 0xEE, 0xFD);
                Input = Color.White; Danger = C(0xD9, 0x30, 0x25); Success = C(0x12, 0x9E, 0x5B); Warning = C(0xD9, 0x7A, 0x00);
            }
        }

        static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        public static void TitleBar(Form f)
        {
            if (!f.IsHandleCreated) return;
            int v = Dark ? 1 : 0;
            try
            {
                if (DwmSetWindowAttribute(f.Handle, 20, ref v, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref v, 4);
                int corner = 2; // rounded corners (Windows 11)
                DwmSetWindowAttribute(f.Handle, 33, ref corner, 4);
            }
            catch { }
        }

        static void Themed(Control c, string darkTheme)
        {
            try { SetWindowTheme(c.Handle, Dark ? darkTheme : null, null); } catch { }
        }

        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        static void ListTheme(ListView lv)
        {
            try
            {
                SetWindowTheme(lv.Handle, Dark ? "DarkMode_Explorer" : "Explorer", null);
                var header = SendMessage(lv.Handle, 0x101F /* LVM_GETHEADER */, IntPtr.Zero, IntPtr.Zero);
                if (header != IntPtr.Zero) SetWindowTheme(header, Dark ? "DarkMode_ItemsView" : "ItemsView", null);
            }
            catch { }
        }

        public static void Scrollbars(Control c)
        {
            if (!c.IsHandleCreated) return;
            try { SetWindowTheme(c.Handle, Dark ? "DarkMode_Explorer" : "Explorer", null); } catch { }
        }

        /// <summary>Applies colours to a control tree. Tags: "muted", "accent", "primary", "card", "title", "nav".</summary>
        public static void Apply(Control root)
        {
            if (root is Form f) { f.BackColor = Page; f.ForeColor = Text; TitleBar(f); }
            foreach (Control c in root.Controls) ApplyOne(c);
        }

        static void ApplyOne(Control c)
        {
            var tag = c.Tag as string;
            switch (c)
            {
                case Card card: card.BackColor = Surface; break;
                case Button b:
                    b.FlatStyle = FlatStyle.Flat;
                    b.UseVisualStyleBackColor = false;
                    b.Cursor = Cursors.Hand;
                    if (tag == "primary")
                    {
                        b.BackColor = Accent; b.ForeColor = AccentText;
                        b.FlatAppearance.BorderColor = Accent; b.FlatAppearance.MouseOverBackColor = AccentHover;
                        b.FlatAppearance.MouseDownBackColor = AccentHover;
                    }
                    else
                    {
                        b.BackColor = Surface2; b.ForeColor = Text;
                        b.FlatAppearance.BorderColor = Border;
                        b.FlatAppearance.MouseOverBackColor = Dark ? C(0x34, 0x39, 0x44) : C(0xE3, 0xE8, 0xEF);
                        b.FlatAppearance.MouseDownBackColor = b.FlatAppearance.MouseOverBackColor;
                    }
                    break;
                case TextBox t: t.BackColor = Input; t.ForeColor = Text; t.BorderStyle = BorderStyle.FixedSingle; break;
                case NumericUpDown n:
                    n.BackColor = Input; n.ForeColor = Text; n.BorderStyle = BorderStyle.FixedSingle;
                    foreach (Control part in n.Controls) { if (part.IsHandleCreated) Themed(part, "DarkMode_Explorer"); else part.HandleCreated += (s, e) => Themed(part, "DarkMode_Explorer"); }
                    break;
                case ComboBox cb:
                    cb.BackColor = Input; cb.ForeColor = Text; cb.FlatStyle = FlatStyle.Standard;
                    if (cb.IsHandleCreated) Themed(cb, "DarkMode_CFD"); else cb.HandleCreated += (s, e) => Themed(cb, "DarkMode_CFD");
                    break;
                case ListBox lb: lb.BackColor = Input; lb.ForeColor = Text; lb.BorderStyle = BorderStyle.FixedSingle; break;
                case ListView lv:
                    lv.BackColor = Surface; lv.ForeColor = Text; lv.BorderStyle = BorderStyle.None;
                    if (lv.IsHandleCreated) ListTheme(lv); else lv.HandleCreated += (s, e) => ListTheme(lv);
                    break;
                case CheckBox cx: cx.ForeColor = Text; cx.BackColor = Color.Transparent; break;
                case RadioButton rb: rb.ForeColor = Text; rb.BackColor = Color.Transparent; break;
                case LinkLabel ll: ll.LinkColor = Accent; ll.ActiveLinkColor = AccentHover; ll.BackColor = Color.Transparent; break;
                case Label l:
                    l.BackColor = Color.Transparent;
                    l.ForeColor = tag == "muted" ? Muted : tag == "accent" ? Accent : tag == "danger" ? Danger : Text;
                    break;
                case ScrollableControl sc when tag == "card": sc.BackColor = Surface; break;
                case ScrollableControl sc2:
                    if (sc2.AutoScroll) { if (sc2.IsHandleCreated) Scrollbars(sc2); else sc2.HandleCreated += (s, e) => Scrollbars(sc2); }
                    break;
            }
            if (c is ThemedControl tc) tc.OnThemeChanged();
            foreach (Control child in c.Controls) ApplyOne(child);
        }

        // ------------------------------------------------------------------ glyph icons

        static FontFamily iconFamily;
        public static FontFamily IconFamily
        {
            get
            {
                if (iconFamily != null) return iconFamily;
                using (var fonts = new InstalledFontCollection())
                    foreach (var f in fonts.Families)
                        if (f.Name == "Segoe Fluent Icons") return iconFamily = f;
                try { return iconFamily = new FontFamily("Segoe MDL2 Assets"); }
                catch { return iconFamily = FontFamily.GenericSansSerif; }
            }
        }

        public static Font IconFont(float pt) => new Font(IconFamily, pt, FontStyle.Regular, GraphicsUnit.Point);

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>Segoe Fluent / MDL2 glyph code points.</summary>
    static class Glyph
    {
        public const string Home = "", Speed = "", Chart = "", Network = "", Ethernet = "",
            Settings = "", Info = "", Brush = "", Download = "", Upload = "", Refresh = "",
            Play = "", Stop = "", Globe = "", Heart = "", Warning = "", Check = "",
            Clock = "", Open = "", Wifi = "", Export = "", History = "", Delete = "";
    }

    interface ThemedControl { void OnThemeChanged(); }

    /// <summary>Rounded "card" surface.</summary>
    class Card : Panel, ThemedControl
    {
        public int Radius = 8;
        public Card()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Padding = new Padding(Dpi.S(16), Dpi.S(14), Dpi.S(16), Dpi.S(14));
            BackColor = UiTheme.Surface;
            Tag = "card";
        }
        public void OnThemeChanged() { BackColor = UiTheme.Surface; Invalidate(); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? UiTheme.Page);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = UiTheme.Rounded(r, Dpi.S(Radius)))
            using (var b = new SolidBrush(BackColor))
            using (var pen = new Pen(UiTheme.Border))
            {
                g.FillPath(b, path);
                g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>Thin rounded progress bar.</summary>
    sealed class Meter : Control, ThemedControl
    {
        double value;
        public Color BarColor = UiTheme.Accent;
        public double Value { get => value; set { this.value = Math.Max(0, Math.Min(1, value)); Invalidate(); } }
        public Meter()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Height = Dpi.S(6);
        }
        public void OnThemeChanged() => Invalidate();
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? UiTheme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var p = UiTheme.Rounded(r, Height / 2f)) using (var b = new SolidBrush(UiTheme.Surface2)) g.FillPath(b, p);
            if (value > 0)
            {
                var fr = new RectangleF(0, 0, Math.Max(Height, (float)((Width - 1) * value)), Height - 1);
                using (var p = UiTheme.Rounded(fr, Height / 2f)) using (var b = new SolidBrush(BarColor)) g.FillPath(b, p);
            }
        }
    }

    /// <summary>Left navigation rail with icon + text entries.</summary>
    sealed class NavRail : Control, ThemedControl
    {
        public sealed class Entry { public string Text, Glyph; public bool Separator; }
        public readonly List<Entry> Entries = new List<Entry>();
        int selected, hover = -1;
        public event Action<int> SelectionChanged;
        public string Footer = "";

        public NavRail()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Width = Dpi.S(208);
            Dock = DockStyle.Left;
            Cursor = Cursors.Hand;
        }

        public void OnThemeChanged() => Invalidate();

        public int Selected
        {
            get => selected;
            set { if (value == selected) return; selected = value; Invalidate(); SelectionChanged?.Invoke(value); }
        }

        int Top0 => Dpi.S(78);
        int ItemH => Dpi.S(40);

        Rectangle ItemRect(int i)
        {
            int y = Top0;
            for (int k = 0; k < i; k++) y += Entries[k].Separator ? Dpi.S(17) : ItemH;
            return new Rectangle(Dpi.S(10), y, Width - Dpi.S(20), Entries[i].Separator ? Dpi.S(17) : ItemH - Dpi.S(4));
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < Entries.Count; i++)
                if (!Entries[i].Separator && ItemRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e) { int h = HitTest(e.Location); if (h != hover) { hover = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e) { int h = HitTest(e.Location); if (h >= 0) Selected = h; base.OnMouseClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(UiTheme.Nav);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // brand
            using (var icon = TrayIcons.LoadAppIcon(new Size(Dpi.S(32), Dpi.S(32))))
                g.DrawIcon(icon, new Rectangle(Dpi.S(20), Dpi.S(22), Dpi.S(30), Dpi.S(30)));
            using (var f = new Font(Font.FontFamily, 13f, FontStyle.Bold))
            using (var b = new SolidBrush(UiTheme.Text))
                g.DrawString("WinSpeed", f, b, Dpi.S(58), Dpi.S(21));
            using (var b = new SolidBrush(UiTheme.Muted))
                g.DrawString("Internet speed meter", Font, b, Dpi.S(60), Dpi.S(44));

            using (var iconFont = UiTheme.IconFont(11.5f))
            using (var sfV = new StringFormat { LineAlignment = StringAlignment.Center })
            {
                for (int i = 0; i < Entries.Count; i++)
                {
                    var r = ItemRect(i);
                    var en = Entries[i];
                    if (en.Separator)
                    {
                        using (var pen = new Pen(UiTheme.Border)) g.DrawLine(pen, r.Left + Dpi.S(8), r.Top + r.Height / 2, r.Right - Dpi.S(8), r.Top + r.Height / 2);
                        continue;
                    }
                    bool sel = i == selected;
                    if (sel || i == hover)
                        using (var path = UiTheme.Rounded(r, Dpi.S(6)))
                        using (var b = new SolidBrush(sel ? UiTheme.NavSelected : UiTheme.NavHover))
                            g.FillPath(b, path);
                    if (sel)
                        using (var path = UiTheme.Rounded(new RectangleF(r.Left, r.Top + r.Height * 0.25f, Dpi.S(3), r.Height * 0.5f), Dpi.S(2)))
                        using (var b = new SolidBrush(UiTheme.Accent))
                            g.FillPath(b, path);
                    using (var b = new SolidBrush(sel ? UiTheme.Accent : UiTheme.Muted))
                        g.DrawString(en.Glyph, iconFont, b, new RectangleF(r.Left + Dpi.S(12), r.Top, Dpi.S(26), r.Height), sfV);
                    using (var f = new Font(Font, sel ? FontStyle.Bold : FontStyle.Regular))
                    using (var b = new SolidBrush(UiTheme.Text))
                        g.DrawString(en.Text, f, b, new RectangleF(r.Left + Dpi.S(44), r.Top, r.Width - Dpi.S(44), r.Height), sfV);
                }
            }

            if (Footer.Length > 0)
                using (var b = new SolidBrush(UiTheme.Muted))
                    g.DrawString(Footer, Font, b, Dpi.S(20), Height - Dpi.S(30));
            using (var pen = new Pen(UiTheme.Border)) g.DrawLine(pen, Width - 1, 0, Width - 1, Height);
        }
    }

    /// <summary>Page title + subtitle header used at the top of every page.</summary>
    sealed class PageHeader : Control, ThemedControl
    {
        public string Title = "", Subtitle = "";
        public PageHeader()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top;
            Height = Dpi.S(64);
        }
        public void OnThemeChanged() => Invalidate();
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(UiTheme.Page);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (var f = new Font(Font.FontFamily, 17f, FontStyle.Bold))
            using (var b = new SolidBrush(UiTheme.Text))
                g.DrawString(Title, f, b, -Dpi.S(2), Dpi.S(2));
            using (var b = new SolidBrush(UiTheme.Muted))
                g.DrawString(Subtitle, Font, b, 0, Dpi.S(38));
        }
    }

    /// <summary>ListView with flat themed column headers.</summary>
    sealed class DbListView : ListView
    {
        /// <summary>Scale columns proportionally so they always fill the width (no horizontal scrollbar).</summary>
        public bool FitColumns = true;
        int[] baseWidths;
        bool fitting;

        protected override void OnResize(EventArgs e) { base.OnResize(e); Fit(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Fit(); }

        void Fit()
        {
            if (!FitColumns || fitting || Columns.Count == 0 || ClientSize.Width <= 0) return;
            fitting = true;
            try
            {
                if (baseWidths == null || baseWidths.Length != Columns.Count)
                {
                    baseWidths = new int[Columns.Count];
                    for (int i = 0; i < Columns.Count; i++) baseWidths[i] = Math.Max(20, Columns[i].Width);
                }
                int total = 0;
                foreach (var w in baseWidths) total += w;
                int avail = ClientSize.Width - 1;
                if (avail < total * 0.6) avail = (int)(total * 0.6); // too narrow: allow scrolling
                int used = 0;
                for (int i = 0; i < Columns.Count; i++)
                {
                    int w = i == Columns.Count - 1 ? Math.Max(20, avail - used) : baseWidths[i] * avail / total;
                    Columns[i].Width = w;
                    used += w;
                }
            }
            finally { fitting = false; }
        }

        public DbListView()
        {
            DoubleBuffered = true;
            View = View.Details;
            FullRowSelect = true;
            GridLines = false;
            HideSelection = false;
            OwnerDraw = true;
            BorderStyle = BorderStyle.None;
        }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(UiTheme.Surface)) g.FillRectangle(b, e.Bounds);
            using (var pen = new Pen(UiTheme.Border)) g.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine |
                        (e.Header.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);
            var r = new Rectangle(e.Bounds.X + Dpi.S(6), e.Bounds.Y, e.Bounds.Width - Dpi.S(12), e.Bounds.Height);
            using (var f = new Font(Font, FontStyle.Bold))
                TextRenderer.DrawText(g, e.Header.Text, f, r, UiTheme.Muted, flags);
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs e) { e.DrawDefault = false; }

        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out Native.RECT r);

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x000F /* WM_PAINT */) return;
            // the themed list view draws column separators in the empty area below the last row; paint them over
            int top = 0;
            if (Items.Count > 0) top = Items[Items.Count - 1].Bounds.Bottom;
            else
            {
                var header = SendMessage(Handle, 0x101F, IntPtr.Zero, IntPtr.Zero);
                if (header != IntPtr.Zero && GetWindowRect(header, out var hr)) top = hr.Bottom - hr.Top;
            }
            if (top < 0 || top >= ClientSize.Height) return;
            using (var g = Graphics.FromHwnd(Handle))
            using (var b = new SolidBrush(UiTheme.Surface))
                g.FillRectangle(b, 0, top, ClientSize.Width, ClientSize.Height - top);
        }

        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            var g = e.Graphics;
            bool sel = e.Item.Selected;
            var bg = sel ? UiTheme.NavSelected : (e.ItemIndex % 2 == 1 ? Blend(UiTheme.Surface, UiTheme.Surface2, 0.45) : UiTheme.Surface);
            using (var b = new SolidBrush(bg)) g.FillRectangle(b, e.Bounds);
            var col = e.Header;
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                        (col.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);
            var r = new Rectangle(e.Bounds.X + Dpi.S(6), e.Bounds.Y, e.Bounds.Width - Dpi.S(12), e.Bounds.Height);
            var fore = e.Item.ForeColor == SystemColors.WindowText || e.Item.ForeColor == Color.Empty ? UiTheme.Text : e.Item.ForeColor;
            TextRenderer.DrawText(g, e.SubItem.Text, Font, r, fore, flags);
        }

        static Color Blend(Color a, Color b, double t) =>
            Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    static class Ui
    {
        /// <summary>Privacy mode: hides the digits of an IP / MAC address but keeps its shape.</summary>
        public static string Mask(string address, bool enabled) =>
            enabled && !string.IsNullOrEmpty(address) ? System.Text.RegularExpressions.Regex.Replace(address, "[0-9A-Fa-f]", "•") : address;

        public static Label Label(string text, string tag = null, float size = 0, FontStyle style = FontStyle.Regular)
        {
            var l = new Label { Text = text, AutoSize = true, Tag = tag, BackColor = Color.Transparent, UseMnemonic = false };
            if (size > 0 || style != FontStyle.Regular)
                l.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, size > 0 ? size : SystemFonts.MessageBoxFont.SizeInPoints, style);
            return l;
        }

        public static Button Button(string text, EventHandler click, bool primary = false, string glyph = null)
        {
            var b = new Button
            {
                Text = glyph == null ? text : text, AutoSize = true, Tag = primary ? "primary" : null,
                Padding = new Padding(Dpi.S(10), Dpi.S(3), Dpi.S(10), Dpi.S(3)), Margin = new Padding(0, 0, Dpi.S(8), Dpi.S(4)),
                MinimumSize = new Size(Dpi.S(80), Dpi.S(32))
            };
            if (click != null) b.Click += click;
            return b;
        }
    }
}
