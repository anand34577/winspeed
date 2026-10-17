using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinSpeed
{
    /// <summary>First-run setup: pick units, position and startup in one screen.</summary>
    sealed class WelcomeForm : Form
    {
        sealed class Choice : Control
        {
            public string Title, Sub, Preview;
            public bool Selected;
            public event EventHandler Picked;
            public Choice(string title, string sub, string preview)
            {
                Title = title; Sub = sub; Preview = preview;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                Size = new Size(Dpi.S(200), Dpi.S(96));
                Cursor = Cursors.Hand;
                Margin = new Padding(0, 0, Dpi.S(12), Dpi.S(12));
            }
            protected override void OnClick(EventArgs e) { Picked?.Invoke(this, e); base.OnClick(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Parent.BackColor);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = UiTheme.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), Dpi.S(8)))
                using (var b = new SolidBrush(Selected ? UiTheme.NavSelected : UiTheme.Surface))
                using (var pen = new Pen(Selected ? UiTheme.Accent : UiTheme.Border, Selected ? 2 : 1))
                { g.FillPath(b, p); g.DrawPath(pen, p); }
                using (var f = new Font(Font, FontStyle.Bold)) using (var b = new SolidBrush(UiTheme.Text)) g.DrawString(Title, f, b, Dpi.S(12), Dpi.S(10));
                using (var b = new SolidBrush(UiTheme.Muted)) g.DrawString(Sub, Font, b, new RectangleF(Dpi.S(12), Dpi.S(30), Width - Dpi.S(24), Dpi.S(36)));
                using (var f = new Font("Segoe UI", 11f, FontStyle.Bold)) using (var b = new SolidBrush(UiTheme.Accent)) g.DrawString(Preview, f, b, Dpi.S(12), Height - Dpi.S(30));
            }
        }

        public UnitMode Unit = UnitMode.AutoBytes;
        public Placement Placement = Placement.TaskbarRight;
        public bool Startup = true;

        public WelcomeForm()
        {
            Text = "Welcome to WinSpeed";
            Icon = TrayIcons.LoadAppIcon(new Size(32, 32));
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Dpi.S(700), Dpi.S(560));
            ShowInTaskbar = true;

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(Dpi.S(32), Dpi.S(26), Dpi.S(32), 0) };
            var head = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            var logo = new PictureBox { Size = new Size(Dpi.S(48), Dpi.S(48)), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, Dpi.S(12), 0) };
            using (var ic = TrayIcons.LoadAppIcon(new Size(256, 256))) logo.Image = ic.ToBitmap();
            var titles = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            titles.Controls.Add(Ui.Label("Welcome to WinSpeed", null, 18f, FontStyle.Bold));
            titles.Controls.Add(Ui.Label("Your internet speed, always visible on the taskbar. Three quick choices and you're done.", "muted"));
            head.Controls.Add(logo); head.Controls.Add(titles);
            flow.Controls.Add(head);

            Choice[] Group(string caption, params (string t, string s, string p, Action pick, bool sel)[] items)
            {
                var l = Ui.Label(caption, null, 10.5f, FontStyle.Bold);
                l.Margin = new Padding(0, Dpi.S(22), 0, Dpi.S(8));
                flow.Controls.Add(l);
                var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
                var list = new Choice[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    var it = items[i];
                    var c = new Choice(it.t, it.s, it.p) { Selected = it.sel };
                    list[i] = c;
                    c.Picked += (s, e) => { foreach (var o in list) { o.Selected = o == c; o.Invalidate(); } it.pick(); };
                    row.Controls.Add(c);
                }
                flow.Controls.Add(row);
                return list;
            }

            Group("How should speeds be shown?",
                ("Bytes per second", "Like your browser's download speed", "↓ 2.4 MB/s", () => Unit = UnitMode.AutoBytes, true),
                ("Bits per second", "Like your internet plan (Mbps)", "↓ 19.6 Mbps", () => Unit = UnitMode.AutoBits, false));
            Group("Where should the meter sit?",
                ("Next to the clock", "On the taskbar, beside the tray icons", "▭▭▭  ↓↑  12:30", () => Placement = Placement.TaskbarRight, true),
                ("Left of the taskbar", "On the taskbar, far left", "↓↑  ▭▭▭", () => Placement = Placement.TaskbarLeft, false),
                ("Floating window", "A small widget you can put anywhere", "⧉  ↓↑", () => Placement = Placement.Floating, false));

            var chk = new CheckBox { Text = "Start WinSpeed automatically when I sign in to Windows", Checked = true, AutoSize = true, Margin = new Padding(0, Dpi.S(20), 0, 0) };
            chk.CheckedChanged += (s, e) => Startup = chk.Checked;
            flow.Controls.Add(chk);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = Dpi.S(64), Padding = new Padding(Dpi.S(28), Dpi.S(14), Dpi.S(28), 0) };
            var go = Ui.Button("Get started", (s, e) => { DialogResult = DialogResult.OK; Close(); }, true);
            go.MinimumSize = new Size(Dpi.S(140), Dpi.S(36));
            bottom.Controls.Add(go);
            var hint = Ui.Label("You can change everything later: right-click the meter → Settings.", "muted");
            hint.Margin = new Padding(0, Dpi.S(9), Dpi.S(16), 0);
            bottom.Controls.Add(hint);
            AcceptButton = go;

            Controls.Add(flow);
            Controls.Add(bottom);
            UiTheme.Apply(this);
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); UiTheme.TitleBar(this); }
    }
}
