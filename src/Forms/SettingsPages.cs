using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace WinSpeed
{
    /// <summary>"Meter & display" and "Settings" pages. Every change is applied instantly (debounced).</summary>
    sealed class SettingsPages
    {
        sealed class Item<T>
        {
            public T Value; public string Text;
            public override string ToString() => Text;
        }

        sealed class ColorButton : Button
        {
            Color value;
            public event EventHandler ValueChanged;
            public Color Value { get => value; set { if (this.value == value) return; this.value = value; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); } }
            public ColorButton() { Width = Dpi.S(64); Height = Dpi.S(28); Text = ""; Margin = new Padding(0, 0, Dpi.S(6), 0); }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var r = new Rectangle(Dpi.S(5), Dpi.S(5), Width - Dpi.S(11), Height - Dpi.S(11));
                using (var b = new SolidBrush(value)) e.Graphics.FillRectangle(b, r);
                using (var p = new Pen(UiTheme.Border)) e.Graphics.DrawRectangle(p, r);
            }
            protected override void OnClick(EventArgs e)
            {
                using (var d = new ColorDialog { Color = value, FullOpen = true, AnyColor = true })
                    if (d.ShowDialog(FindForm()) == DialogResult.OK) Value = d.Color;
                base.OnClick(e);
            }
        }

        readonly TrayApp app;
        readonly Timer debounce = new Timer { Interval = 350 };
        bool loading;

        public Control MeterPage { get; }
        public Control SettingsPage { get; }

        // meter
        CheckBox chkShowMeter, chkUploadFirst, chkShowUnit, chkShowLabels, chkAutoColor, chkHighlight, chkLock, chkFullscreen, chkClickThrough, chkGraph, chkPingMeter, chkOfflineDot;
        ComboBox cbMode, cbLayout, cbUnit, cbBase, cbStyle, cbDecimals, cbMinUnit, cbPlacement;
        TextBox txtDownLabel, txtUpLabel, txtTotalLabel;
        Button btnFont;
        ColorButton clrDown, clrUp, clrTotal, clrBack, clrHighlight;
        NumericUpDown numOpacity, numHighlight, numHideBelow, numOffX, numOffY;
        string fontFamily; float fontSize; bool fontBold;
        // settings
        CheckBox chkStartup, chkUpdates, chkPrivacy, chkPing, chkNotifyConn, chkNotifyTest, chkVirtual;
        ComboBox cbTheme, cbInterval, cbDblClick, cbTrayClick, cbTrayIcon;
        NumericUpDown numSmoothing, numPingInterval, numMonthlyCap, numDailyCap, numBillingDay, numAlert, numRetention;
        TextBox txtHotkey, txtPingHost;
        RadioButton rbAuto, rbSelected;
        CheckedListBox clbIfaces;
        Label lblIfaceWarn;

        public SettingsPages(TrayApp app)
        {
            this.app = app;
            debounce.Tick += (s, e) => { debounce.Stop(); Apply(); };
            MeterPage = Wrap("Meter & display", "How the taskbar meter looks, which units it uses and where it sits. Changes apply instantly.", BuildMeter());
            SettingsPage = Wrap("Settings", "Startup, network monitoring, adapters, data plan and backups. Changes apply instantly.", BuildSettings());
            Load(app.Settings);
        }

        // ------------------------------------------------------------ layout helpers

        static Control Wrap(string title, string subtitle, Control body)
        {
            var p = new Panel();
            var scroller = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 0, Dpi.S(8), 0) };
            body.Dock = DockStyle.Top;
            scroller.Controls.Add(body);
            p.Controls.Add(scroller);
            p.Controls.Add(new PageHeader { Title = title, Subtitle = subtitle });
            return p;
        }

        static TableLayoutPanel Column()
        {
            var t = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return t;
        }

        static TableLayoutPanel Section(TableLayoutPanel column, string title, string glyph)
        {
            var card = new Card { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, Dpi.S(14)) };
            var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dpi.S(210)));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var head = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, Dpi.S(8)) };
            var ic = new Label { Text = glyph, AutoSize = true, Font = UiTheme.IconFont(12f), Tag = "accent", Margin = new Padding(0, Dpi.S(3), Dpi.S(6), 0) };
            head.Controls.Add(ic);
            head.Controls.Add(Ui.Label(title, null, 11f, FontStyle.Bold));
            t.Controls.Add(head, 0, 0);
            t.SetColumnSpan(head, 2);
            t.RowCount = 1;
            card.Controls.Add(t);
            column.Controls.Add(card, 0, column.RowCount++);
            return t;
        }

        static void Row(TableLayoutPanel t, string label, Control c, string hint = null)
        {
            var lbl = Ui.Label(label);
            lbl.Anchor = AnchorStyles.Left;
            lbl.Margin = new Padding(0, Dpi.S(7), Dpi.S(12), Dpi.S(7));
            c.Anchor = AnchorStyles.Left;
            c.Margin = new Padding(0, Dpi.S(4), 0, Dpi.S(4));
            t.Controls.Add(lbl, 0, t.RowCount);
            if (hint != null)
            {
                var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), Anchor = AnchorStyles.Left };
                flow.Controls.Add(c);
                var h = Ui.Label(hint, "muted");
                h.Margin = new Padding(Dpi.S(8), Dpi.S(7), 0, 0);
                flow.Controls.Add(h);
                t.Controls.Add(flow, 1, t.RowCount);
            }
            else t.Controls.Add(c, 1, t.RowCount);
            t.RowCount++;
        }

        static void Full(TableLayoutPanel t, Control c)
        {
            c.Margin = new Padding(0, Dpi.S(4), 0, Dpi.S(4));
            t.Controls.Add(c, 0, t.RowCount);
            t.SetColumnSpan(c, 2);
            t.RowCount++;
        }

        static void Note(TableLayoutPanel t, string text)
        {
            var l = Ui.Label(text, "muted");
            l.MaximumSize = new Size(Dpi.S(640), 0);
            Full(t, l);
        }

        static ComboBox Combo<T>(int width, params (T value, string text)[] items)
        {
            var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Dpi.S(width) };
            foreach (var (v, t) in items) cb.Items.Add(new Item<T> { Value = v, Text = t });
            return cb;
        }

        static void SetCombo<T>(ComboBox cb, T value)
        {
            for (int i = 0; i < cb.Items.Count; i++)
                if (EqualityComparer<T>.Default.Equals(((Item<T>)cb.Items[i]).Value, value)) { cb.SelectedIndex = i; return; }
            if (cb.Items.Count > 0) cb.SelectedIndex = 0;
        }

        static T GetCombo<T>(ComboBox cb) => cb.SelectedItem is Item<T> it ? it.Value : default;

        static NumericUpDown Num(decimal min, decimal max, int width = 90, int decimals = 0) =>
            new NumericUpDown { Minimum = min, Maximum = max, Width = Dpi.S(width), DecimalPlaces = decimals, ThousandsSeparator = true };

        static CheckBox Check(string text) => new CheckBox { Text = text, AutoSize = true };

        static decimal ClampDec(NumericUpDown n, decimal v) => Math.Max(n.Minimum, Math.Min(n.Maximum, v));

        static Label Small(string s) { var l = Ui.Label(s, "muted"); l.Margin = new Padding(Dpi.S(4), Dpi.S(7), Dpi.S(4), 0); return l; }

        void Changed() { if (!loading) { debounce.Stop(); debounce.Start(); } }

        void Hook(Control root)
        {
            foreach (Control c in root.Controls)
            {
                switch (c)
                {
                    case CheckBox cb: cb.CheckedChanged += (s, e) => Changed(); break;
                    case RadioButton rb: rb.CheckedChanged += (s, e) => Changed(); break;
                    case ComboBox co: co.SelectedIndexChanged += (s, e) => Changed(); break;
                    case NumericUpDown n: n.ValueChanged += (s, e) => Changed(); break;
                    case TextBox tb: tb.TextChanged += (s, e) => Changed(); break;
                    case ColorButton clr: clr.ValueChanged += (s, e) => Changed(); break;
                    case CheckedListBox cl: cl.ItemCheck += (s, e) => { if (!loading && cl.IsHandleCreated) cl.BeginInvoke((Action)Changed); }; break;
                }
                Hook(c);
            }
        }

        // ------------------------------------------------------------ meter page

        Control BuildMeter()
        {
            var col = Column();

            var t = Section(col, "What to show", Glyph.Speed);
            chkShowMeter = Check("Show the speed meter");
            Full(t, chkShowMeter);
            cbMode = Combo(280, (DisplayMode.Separate, "Download and upload (separate)"), (DisplayMode.Combined, "Combined total (down + up)"),
                (DisplayMode.SeparateAndTotal, "Download, upload and total"), (DisplayMode.DownloadOnly, "Download only"), (DisplayMode.UploadOnly, "Upload only"));
            Row(t, "Show", cbMode);
            cbLayout = Combo(280, (LayoutMode.TwoLines, "Stacked (one per line)"), (LayoutMode.SingleLine, "Side by side (one line)"));
            Row(t, "Layout", cbLayout);
            chkUploadFirst = Check("Show upload before download");
            Full(t, chkUploadFirst);
            chkPingMeter = Check("Also show ping (latency) on the meter");
            Full(t, chkPingMeter);
            chkGraph = Check("Mini speed graph behind the numbers");
            Full(t, chkGraph);
            chkOfflineDot = Check("Red dot on the meter when the internet is down");
            Full(t, chkOfflineDot);

            t = Section(col, "Units", Glyph.Chart);
            cbUnit = Combo(320,
                (UnitMode.AutoBytes, "Automatic — bytes (B/s, KB/s, MB/s, GB/s)"), (UnitMode.AutoBits, "Automatic — bits (bps, Kbps, Mbps, Gbps)"),
                (UnitMode.Bytes, "Always B/s (bytes per second)"), (UnitMode.KiloBytes, "Always KB/s"), (UnitMode.MegaBytes, "Always MB/s"), (UnitMode.GigaBytes, "Always GB/s"),
                (UnitMode.Bits, "Always bps (bits per second)"), (UnitMode.KiloBits, "Always Kbps"), (UnitMode.MegaBits, "Always Mbps"), (UnitMode.GigaBits, "Always Gbps"));
            Row(t, "Unit", cbUnit);
            cbBase = Combo(320, (1024, "Binary — 1 KB = 1024 bytes"), (1000, "Decimal — 1 KB = 1000 bytes (ISP style)"));
            Row(t, "Multiplier", cbBase);
            cbStyle = Combo(320, (UnitStyle.Standard, "Standard (KB/s, Mbps)"), (UnitStyle.Compact, "Compact (K, M, Kb, Mb)"), (UnitStyle.Iec, "IEC (KiB/s, Mibit/s)"));
            Row(t, "Unit labels", cbStyle);
            cbDecimals = Combo(320, (-1, "Automatic (3 significant digits)"), (0, "0  (12 MB/s)"), (1, "1  (12.3 MB/s)"), (2, "2  (12.34 MB/s)"), (3, "3"));
            Row(t, "Decimal places", cbDecimals);
            cbMinUnit = Combo(320, (MinUnit.None, "B/s — show small values in bytes"), (MinUnit.Kilo, "KB/s — never show B/s"), (MinUnit.Mega, "MB/s"));
            Row(t, "Smallest automatic unit", cbMinUnit);
            chkShowUnit = Check("Show the unit");
            Full(t, chkShowUnit);

            t = Section(col, "Labels", Glyph.Info);
            chkShowLabels = Check("Show labels / arrows");
            Full(t, chkShowLabels);
            var lblFlow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            txtDownLabel = new TextBox { Width = Dpi.S(56) };
            txtUpLabel = new TextBox { Width = Dpi.S(56) };
            txtTotalLabel = new TextBox { Width = Dpi.S(56) };
            lblFlow.Controls.Add(Small("Down")); lblFlow.Controls.Add(txtDownLabel);
            lblFlow.Controls.Add(Small("Up")); lblFlow.Controls.Add(txtUpLabel);
            lblFlow.Controls.Add(Small("Total")); lblFlow.Controls.Add(txtTotalLabel);
            Row(t, "Label text", lblFlow);
            var presets = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(Dpi.S(520), 0) };
            foreach (var (d, u, tot) in new[] { ("↓", "↑", "⇅"), ("▼", "▲", "◆"), ("D:", "U:", "T:"), ("DL", "UL", "Σ"), ("⬇", "⬆", "⇵"), ("", "", "") })
            {
                var b = Ui.Button(d.Length == 0 ? "None" : $"{d} {u} {tot}", null);
                b.MinimumSize = new Size(Dpi.S(56), Dpi.S(30));
                b.Click += (s, e) => { txtDownLabel.Text = d; txtUpLabel.Text = u; txtTotalLabel.Text = tot; };
                presets.Controls.Add(b);
            }
            Row(t, "Presets", presets);

            t = Section(col, "Look", Glyph.Brush);
            btnFont = Ui.Button("", null);
            btnFont.MinimumSize = new Size(Dpi.S(220), Dpi.S(30));
            btnFont.Click += (s, e) =>
            {
                using (var f = new Font(fontFamily, fontSize, fontBold ? FontStyle.Bold : FontStyle.Regular))
                using (var d = new FontDialog { Font = f, ShowEffects = false, AllowVerticalFonts = false, MinSize = 5, MaxSize = 36, FontMustExist = true })
                {
                    if (d.ShowDialog(btnFont.FindForm()) != DialogResult.OK) return;
                    fontFamily = d.Font.FontFamily.Name; fontSize = d.Font.SizeInPoints; fontBold = d.Font.Bold;
                    UpdateFontButton();
                    Changed();
                }
            };
            Row(t, "Font", btnFont);
            chkAutoColor = Check("Automatic text colour (follows light / dark taskbar); arrows keep their colours");
            Full(t, chkAutoColor);
            var colors = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            clrDown = new ColorButton(); clrUp = new ColorButton(); clrTotal = new ColorButton();
            colors.Controls.Add(Small("Down")); colors.Controls.Add(clrDown);
            colors.Controls.Add(Small("Up")); colors.Controls.Add(clrUp);
            colors.Controls.Add(Small("Total")); colors.Controls.Add(clrTotal);
            Row(t, "Colours", colors);
            var back = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            clrBack = new ColorButton();
            numOpacity = Num(0, 100, 64);
            back.Controls.Add(clrBack); back.Controls.Add(Small("Opacity")); back.Controls.Add(numOpacity); back.Controls.Add(Small("%  (0 = transparent)"));
            Row(t, "Background", back);
            var hl = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            chkHighlight = Check("Highlight at ≥");
            numHighlight = Num(1, 10_000_000, 90);
            clrHighlight = new ColorButton();
            hl.Controls.Add(chkHighlight); hl.Controls.Add(numHighlight); hl.Controls.Add(Small("KB/s in")); hl.Controls.Add(clrHighlight);
            Row(t, "Speed highlight", hl);
            numHideBelow = Num(0, 1_000_000, 90);
            Row(t, "Hide meter below", numHideBelow, "KB/s  (0 = always visible)");

            t = Section(col, "Position", Glyph.Home);
            cbPlacement = Combo(320, (Placement.TaskbarRight, "Taskbar — next to the tray / clock"), (Placement.TaskbarLeft, "Taskbar — left side"), (Placement.Floating, "Floating window on the desktop"));
            Row(t, "Location", cbPlacement);
            var off = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            numOffX = Num(-5000, 5000, 76);
            numOffY = Num(-500, 500, 76);
            off.Controls.Add(Small("X")); off.Controls.Add(numOffX);
            off.Controls.Add(Small("Y")); off.Controls.Add(numOffY);
            var reset = Ui.Button("Reset", (s, e) => { numOffX.Value = 0; numOffY.Value = 0; app.Settings.FloatX = app.Settings.FloatY = -1; Changed(); });
            reset.Margin = new Padding(Dpi.S(10), 0, 0, 0);
            off.Controls.Add(reset);
            Row(t, "Offset (pixels)", off);
            Note(t, "Tip: just drag the meter with the mouse. On the taskbar the drag is remembered as an offset from the tray; a floating meter remembers its screen position.");
            chkLock = Check("Lock position (disable dragging)");
            Full(t, chkLock);
            chkFullscreen = Check("Hide while a full-screen app (game, video, presentation) is active");
            Full(t, chkFullscreen);
            chkClickThrough = Check("Click-through — mouse clicks pass through the meter (use the tray icon for the menu)");
            Full(t, chkClickThrough);

            Hook(col);
            return col;
        }

        void UpdateFontButton() => btnFont.Text = $"{fontFamily}, {fontSize:0.#} pt{(fontBold ? ", bold" : "")}";

        // ------------------------------------------------------------ settings page

        Control BuildSettings()
        {
            var col = Column();

            var t = Section(col, "General", Glyph.Settings);
            chkStartup = Check("Start WinSpeed when I sign in to Windows");
            Full(t, chkStartup);
            cbTheme = Combo(200, (AppTheme.System, "Use Windows setting"), (AppTheme.Light, "Light"), (AppTheme.Dark, "Dark"));
            Row(t, "App theme", cbTheme);
            cbInterval = Combo(200, (250, "0.25 seconds"), (500, "0.5 seconds"), (1000, "1 second"), (2000, "2 seconds"), (3000, "3 seconds"), (5000, "5 seconds"));
            Row(t, "Update the meter every", cbInterval);
            numSmoothing = Num(1, 10, 64);
            Row(t, "Smoothing", numSmoothing, "samples averaged (1 = off)");
            chkUpdates = Check("Check for new versions automatically (once a day, via GitHub)");
            Full(t, chkUpdates);
            chkPrivacy = Check("Privacy mode — mask IP and MAC addresses on screen (handy when screen-sharing)");
            Full(t, chkPrivacy);

            t = Section(col, "Mouse, keyboard & tray", Glyph.Heart);
            var actions = new[] { (ClickAction.Dashboard, "Open WinSpeed"), (ClickAction.DataUsage, "Open data usage"), (ClickAction.SpeedTest, "Run a speed test"),
                                  (ClickAction.Settings, "Open settings"), (ClickAction.ToggleMeter, "Show / hide meter"), (ClickAction.None, "Do nothing") };
            cbDblClick = Combo(200, actions);
            Row(t, "Double-click the meter", cbDblClick);
            cbTrayClick = Combo(200, actions);
            Row(t, "Click the tray icon", cbTrayClick);
            txtHotkey = new TextBox { Width = Dpi.S(200), ReadOnly = true, ShortcutsEnabled = false };
            txtHotkey.KeyDown += (s, e) =>
            {
                e.SuppressKeyPress = true;
                if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete || e.KeyCode == Keys.Escape) { txtHotkey.Text = ""; return; }
                var f = HotkeyWindow.Format(e.KeyData);
                if (f != null) txtHotkey.Text = f;
            };
            Row(t, "Show / hide hotkey", txtHotkey, "click, then press keys (Backspace clears)");
            cbTrayIcon = Combo(240, (TrayIconMode.Static, "App icon"), (TrayIconMode.Speed, "Live speed (numbers in the icon)"), (TrayIconMode.Hidden, "Hidden"));
            Row(t, "Tray icon", cbTrayIcon);
            Note(t, "The tray icon always stays visible while the meter is hidden or click-through, so the menu is always reachable.");

            t = Section(col, "Connection monitoring", Glyph.Wifi);
            chkPing = Check("Measure latency and detect when the internet is down (small ping)");
            Full(t, chkPing);
            txtPingHost = new TextBox { Width = Dpi.S(200) };
            Row(t, "Ping host", txtPingHost, "e.g. 1.1.1.1, 8.8.8.8 or your router");
            numPingInterval = Num(1, 300, 64);
            Row(t, "Ping every", numPingInterval, "seconds");
            chkNotifyConn = Check("Notify me when the internet disconnects or comes back");
            Full(t, chkNotifyConn);
            chkNotifyTest = Check("Notify me when a speed test finishes");
            Full(t, chkNotifyTest);

            t = Section(col, "Network adapters", Glyph.Ethernet);
            rbAuto = new RadioButton { Text = "Automatic — all connected physical adapters (Ethernet, Wi-Fi, mobile)", AutoSize = true };
            Full(t, rbAuto);
            chkVirtual = Check("Also count virtual adapters (VPN, Hyper-V, WSL, VirtualBox…) — may double-count");
            chkVirtual.Margin = new Padding(Dpi.S(22), 0, 0, Dpi.S(4));
            t.Controls.Add(chkVirtual, 0, t.RowCount); t.SetColumnSpan(chkVirtual, 2); t.RowCount++;
            rbSelected = new RadioButton { Text = "Only the adapters I tick below", AutoSize = true };
            Full(t, rbSelected);
            clbIfaces = new CheckedListBox { Width = Dpi.S(600), Height = Dpi.S(150), CheckOnClick = true, IntegralHeight = false, HorizontalScrollbar = true };
            Full(t, clbIfaces);
            lblIfaceWarn = Ui.Label("Tick at least one adapter, or choose Automatic.", "danger");
            lblIfaceWarn.Visible = false;
            Full(t, lblIfaceWarn);
            rbSelected.CheckedChanged += (s, e) => { clbIfaces.Enabled = rbSelected.Checked; chkVirtual.Enabled = rbAuto.Checked; };

            t = Section(col, "Data plan & alerts", Glyph.Warning);
            numMonthlyCap = Num(0, 100000, 90, 1);
            Row(t, "Monthly limit", numMonthlyCap, "GB  (0 = no limit)");
            numBillingDay = Num(1, 28, 64);
            Row(t, "Billing cycle starts on day", numBillingDay, "of each month");
            numDailyCap = Num(0, 10_000_000, 90);
            Row(t, "Daily limit", numDailyCap, "MB  (0 = no limit)");
            numAlert = Num(1, 100, 64);
            Row(t, "Warn me at", numAlert, "% of the limit (and again at 100 %)");

            t = Section(col, "History, backup & reset", Glyph.History);
            numRetention = Num(0, 10000, 90);
            Row(t, "Keep hourly history for", numRetention, "days  (0 = forever)");
            var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(Dpi.S(700), 0) };
            flow.Controls.Add(Ui.Button("Export settings…", (s, e) => ExportSettings()));
            flow.Controls.Add(Ui.Button("Import settings…", (s, e) => ImportSettings()));
            flow.Controls.Add(Ui.Button("Open data folder", (s, e) => { try { Process.Start("explorer.exe", "\"" + Settings.DataDir + "\""); } catch { } }));
            flow.Controls.Add(Ui.Button("Restore default settings", (s, e) =>
            {
                if (MessageBox.Show(flow.FindForm(), "Reset all settings to their defaults? Your usage history is kept.", "WinSpeed",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                app.ApplySettings(new Settings { FirstRun = false, AlertState = app.Settings.AlertState });
                Load(app.Settings);
            }));
            flow.Controls.Add(Ui.Button("Delete usage history…", (s, e) =>
            {
                if (MessageBox.Show(flow.FindForm(), "Permanently delete all recorded data usage history?", "WinSpeed",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK) app.ClearUsageData();
            }));
            Full(t, flow);
            Note(t, $"Data folder: {Settings.DataDir}" + (Settings.IsPortable ? "  (portable mode)" : ""));

            Hook(col);
            return col;
        }

        void ExportSettings()
        {
            using (var d = new SaveFileDialog { Filter = "WinSpeed settings (*.ini)|*.ini", FileName = "WinSpeed-settings.ini" })
                if (d.ShowDialog(MeterPage.FindForm()) == DialogResult.OK) app.Settings.Save(d.FileName);
        }

        void ImportSettings()
        {
            using (var d = new OpenFileDialog { Filter = "WinSpeed settings (*.ini)|*.ini|All files|*.*" })
            {
                if (d.ShowDialog(MeterPage.FindForm()) != DialogResult.OK) return;
                var s = Settings.Load(d.FileName);
                s.FirstRun = false;
                app.ApplySettings(s);
                Load(app.Settings);
            }
        }

        // ------------------------------------------------------------ load / apply

        public void Load(Settings s)
        {
            loading = true;
            try
            {
                chkShowMeter.Checked = s.ShowOverlay;
                SetCombo(cbMode, s.Mode);
                SetCombo(cbLayout, s.Layout);
                chkUploadFirst.Checked = s.UploadFirst;
                chkPingMeter.Checked = s.ShowPingOnMeter;
                chkGraph.Checked = s.MeterGraph;
                chkOfflineDot.Checked = s.ShowOfflineDot;
                SetCombo(cbUnit, s.Unit);
                SetCombo(cbBase, s.UnitBase);
                SetCombo(cbStyle, s.Style);
                SetCombo(cbDecimals, s.Decimals);
                SetCombo(cbMinUnit, s.AutoMinUnit);
                chkShowUnit.Checked = s.ShowUnit;
                chkShowLabels.Checked = s.ShowLabels;
                txtDownLabel.Text = s.DownloadLabel; txtUpLabel.Text = s.UploadLabel; txtTotalLabel.Text = s.TotalLabel;
                fontFamily = s.FontFamily; fontSize = s.FontSize; fontBold = s.FontBold;
                UpdateFontButton();
                chkAutoColor.Checked = s.AutoTextColor;
                clrDown.Value = s.DownloadColor; clrUp.Value = s.UploadColor; clrTotal.Value = s.TotalColor;
                clrBack.Value = s.BackgroundColor;
                numOpacity.Value = ClampDec(numOpacity, s.BackgroundOpacity);
                chkHighlight.Checked = s.HighlightEnabled;
                numHighlight.Value = ClampDec(numHighlight, s.HighlightKBps);
                clrHighlight.Value = s.HighlightColor;
                numHideBelow.Value = ClampDec(numHideBelow, s.HideBelowKBps);
                SetCombo(cbPlacement, s.Placement);
                numOffX.Value = ClampDec(numOffX, s.OffsetX);
                numOffY.Value = ClampDec(numOffY, s.OffsetY);
                chkLock.Checked = s.LockPosition;
                chkFullscreen.Checked = s.HideOnFullscreen;
                chkClickThrough.Checked = s.ClickThrough;

                chkStartup.Checked = StartupManager.Enabled;
                SetCombo(cbTheme, s.Theme);
                SetCombo(cbInterval, s.UpdateIntervalMs);
                numSmoothing.Value = ClampDec(numSmoothing, s.Smoothing);
                chkUpdates.Checked = s.CheckForUpdates;
                chkPrivacy.Checked = s.PrivacyMode;
                SetCombo(cbDblClick, s.DoubleClickAction);
                SetCombo(cbTrayClick, s.TrayClickAction);
                txtHotkey.Text = s.Hotkey;
                SetCombo(cbTrayIcon, s.TrayIcon);
                chkPing.Checked = s.PingEnabled;
                txtPingHost.Text = s.PingHost;
                numPingInterval.Value = ClampDec(numPingInterval, s.PingIntervalSec);
                chkNotifyConn.Checked = s.NotifyConnection;
                chkNotifyTest.Checked = s.NotifySpeedTestDone;

                rbAuto.Checked = s.Interfaces == InterfaceMode.Auto;
                rbSelected.Checked = s.Interfaces == InterfaceMode.Selected;
                clbIfaces.Enabled = rbSelected.Checked;
                chkVirtual.Enabled = rbAuto.Checked;
                chkVirtual.Checked = s.IncludeVirtual;
                var sel = s.SelectedSet();
                clbIfaces.Items.Clear();
                var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var i in app.Monitor.Interfaces)
                {
                    listed.Add(i.Id);
                    var text = $"{i.Name}  —  {i.Description}   [{i.TypeName}{(i.IsVirtual && i.TypeName != "Virtual" ? ", virtual" : "")}, {(i.Status == OperationalStatus.Up ? "connected" : i.Status.ToString().ToLowerInvariant())}]";
                    clbIfaces.Items.Add(new Item<string> { Value = i.Id, Text = text }, s.Interfaces == InterfaceMode.Selected ? sel.Contains(i.Id) : i.Monitored);
                }
                foreach (var id in sel.Where(x => !listed.Contains(x)))
                    clbIfaces.Items.Add(new Item<string> { Value = id, Text = $"{app.Usage.NameOf(id)}   [not present]" }, true);

                numMonthlyCap.Value = ClampDec(numMonthlyCap, Math.Round(s.MonthlyCapMB / 1024m, 1));
                numBillingDay.Value = ClampDec(numBillingDay, s.BillingDay);
                numDailyCap.Value = ClampDec(numDailyCap, s.DailyCapMB);
                numAlert.Value = ClampDec(numAlert, s.AlertPercent);
                numRetention.Value = ClampDec(numRetention, s.RetentionDays);
            }
            finally { loading = false; }
        }

        void Apply()
        {
            var s = app.Settings.Clone();
            s.ShowOverlay = chkShowMeter.Checked;
            s.Mode = GetCombo<DisplayMode>(cbMode);
            s.Layout = GetCombo<LayoutMode>(cbLayout);
            s.UploadFirst = chkUploadFirst.Checked;
            s.ShowPingOnMeter = chkPingMeter.Checked;
            s.MeterGraph = chkGraph.Checked;
            s.ShowOfflineDot = chkOfflineDot.Checked;
            s.Unit = GetCombo<UnitMode>(cbUnit);
            s.UnitBase = GetCombo<int>(cbBase);
            s.Style = GetCombo<UnitStyle>(cbStyle);
            s.Decimals = GetCombo<int>(cbDecimals);
            s.AutoMinUnit = GetCombo<MinUnit>(cbMinUnit);
            s.ShowUnit = chkShowUnit.Checked;
            s.ShowLabels = chkShowLabels.Checked;
            s.DownloadLabel = txtDownLabel.Text; s.UploadLabel = txtUpLabel.Text; s.TotalLabel = txtTotalLabel.Text;
            s.FontFamily = fontFamily; s.FontSize = fontSize; s.FontBold = fontBold;
            s.AutoTextColor = chkAutoColor.Checked;
            s.DownloadColor = clrDown.Value; s.UploadColor = clrUp.Value; s.TotalColor = clrTotal.Value;
            s.BackgroundColor = clrBack.Value;
            s.BackgroundOpacity = (int)numOpacity.Value;
            s.HighlightEnabled = chkHighlight.Checked;
            s.HighlightKBps = (int)numHighlight.Value;
            s.HighlightColor = clrHighlight.Value;
            s.HideBelowKBps = (int)numHideBelow.Value;
            s.Placement = GetCombo<Placement>(cbPlacement);
            s.OffsetX = (int)numOffX.Value;
            s.OffsetY = (int)numOffY.Value;
            s.LockPosition = chkLock.Checked;
            s.HideOnFullscreen = chkFullscreen.Checked;
            s.ClickThrough = chkClickThrough.Checked;

            var oldTheme = app.Settings.Theme;
            s.Theme = GetCombo<AppTheme>(cbTheme);
            s.UpdateIntervalMs = GetCombo<int>(cbInterval);
            s.Smoothing = (int)numSmoothing.Value;
            s.CheckForUpdates = chkUpdates.Checked;
            s.PrivacyMode = chkPrivacy.Checked;
            s.DoubleClickAction = GetCombo<ClickAction>(cbDblClick);
            s.TrayClickAction = GetCombo<ClickAction>(cbTrayClick);
            s.Hotkey = txtHotkey.Text.Trim();
            s.TrayIcon = GetCombo<TrayIconMode>(cbTrayIcon);
            s.PingEnabled = chkPing.Checked;
            s.PingHost = txtPingHost.Text.Trim();
            s.PingIntervalSec = (int)numPingInterval.Value;
            s.NotifyConnection = chkNotifyConn.Checked;
            s.NotifySpeedTestDone = chkNotifyTest.Checked;

            var ids = new List<string>();
            for (int i = 0; i < clbIfaces.Items.Count; i++)
                if (clbIfaces.GetItemChecked(i)) ids.Add(((Item<string>)clbIfaces.Items[i]).Value);
            bool invalidIfaces = rbSelected.Checked && ids.Count == 0;
            lblIfaceWarn.Visible = invalidIfaces;
            if (!invalidIfaces)
            {
                s.Interfaces = rbSelected.Checked ? InterfaceMode.Selected : InterfaceMode.Auto;
                s.SelectedInterfaces = string.Join(";", ids);
            }
            s.IncludeVirtual = chkVirtual.Checked;

            s.MonthlyCapMB = (int)Math.Round(numMonthlyCap.Value * 1024m);
            s.BillingDay = (int)numBillingDay.Value;
            s.DailyCapMB = (int)numDailyCap.Value;
            s.AlertPercent = (int)numAlert.Value;
            s.RetentionDays = (int)numRetention.Value;
            if (s.MonthlyCapMB != app.Settings.MonthlyCapMB || s.DailyCapMB != app.Settings.DailyCapMB || s.AlertPercent != app.Settings.AlertPercent)
                s.AlertState = "";

            if (chkStartup.Checked != StartupManager.Enabled) StartupManager.Enabled = chkStartup.Checked;
            app.ApplySettings(s);
            if (oldTheme != s.Theme) app.ApplyTheme();
        }
    }
}
