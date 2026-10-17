using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows.Forms;

namespace WinSpeed
{
    public enum AppPage { Overview, SpeedTest, Usage, Connection, Meter, Settings, About }

    /// <summary>The app window: navigation rail + pages.</summary>
    sealed class MainWindow : Form
    {
        readonly TrayApp app;
        readonly NavRail nav = new NavRail();
        readonly Panel host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Dpi.S(28), Dpi.S(18), Dpi.S(28), Dpi.S(20)) };
        readonly Dictionary<AppPage, Control> pages = new Dictionary<AppPage, Control>();
        readonly List<AppPage?> entryPages = new List<AppPage?>();
        AppPage current = AppPage.Overview;
        int sampleCount;

        public MainWindow(TrayApp app)
        {
            this.app = app;
            Text = "WinSpeed";
            Icon = TrayIcons.LoadAppIcon(new Size(32, 32));
            Font = SystemFonts.MessageBoxFont;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Dpi.S(1080), Dpi.S(720));
            MinimumSize = new Size(Dpi.S(820), Dpi.S(560));
            DoubleBuffered = true;

            void Entry(string text, string glyph, AppPage? page)
            {
                nav.Entries.Add(new NavRail.Entry { Text = text, Glyph = glyph, Separator = page == null });
                entryPages.Add(page);
            }
            Entry("Overview", Glyph.Home, AppPage.Overview);
            Entry("Speed test", Glyph.Speed, AppPage.SpeedTest);
            Entry("Data usage", Glyph.Chart, AppPage.Usage);
            Entry("Connection", Glyph.Globe, AppPage.Connection);
            Entry("", "", null);
            Entry("Meter & display", Glyph.Brush, AppPage.Meter);
            Entry("Settings", Glyph.Settings, AppPage.Settings);
            Entry("About", Glyph.Info, AppPage.About);
            nav.Footer = "v" + Program.Version + "  •  free & open source";
            nav.SelectionChanged += i => { if (entryPages[i] is AppPage p) ShowPage(p); };

            pages[AppPage.Overview] = BuildOverview();
            pages[AppPage.SpeedTest] = BuildSpeedTest();
            pages[AppPage.Usage] = BuildUsage();
            pages[AppPage.Connection] = BuildConnection();
            var sp = new SettingsPages(app);
            pages[AppPage.Meter] = sp.MeterPage;
            pages[AppPage.Settings] = sp.SettingsPage;
            pages[AppPage.About] = BuildAbout();
            foreach (var p in pages.Values) { p.Dock = DockStyle.Fill; p.Visible = false; host.Controls.Add(p); }

            Controls.Add(host);
            Controls.Add(nav);

            app.Sampled += OnSample;
            app.HealthUpdated += OnHealth;
            app.SpeedTestProgress += OnSpeedTestProgress;
            app.SpeedTestFinished += OnSpeedTestFinished;
            ApplyTheme();
            ShowPage(AppPage.Overview);
            UpdateOverview();
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); UiTheme.TitleBar(this); }

        public void ApplyTheme()
        {
            UiTheme.Apply(this);
            host.BackColor = UiTheme.Page;
            foreach (Control c in AllControls(this)) c.Invalidate();
        }

        static IEnumerable<Control> AllControls(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var d in AllControls(c)) yield return d;
            }
        }

        public void ShowPage(AppPage page)
        {
            current = page;
            int idx = entryPages.IndexOf(page);
            if (idx >= 0 && nav.Selected != idx) nav.Selected = idx;
            foreach (var kv in pages) kv.Value.Visible = kv.Key == page;
            if (page == AppPage.Usage) { RefreshIfaceFilter(); RefreshUsage(); }
            if (page == AppPage.Connection) { RefreshInterfaces(); OnHealth(); }
            if (page == AppPage.SpeedTest) RefreshSpeedHistory();
            if (page == AppPage.Overview) UpdateOverview();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            app.Sampled -= OnSample;
            app.HealthUpdated -= OnHealth;
            app.SpeedTestProgress -= OnSpeedTestProgress;
            app.SpeedTestFinished -= OnSpeedTestFinished;
            base.OnFormClosed(e);
        }

        void OnSample(Sample smp)
        {
            if (!Visible || WindowState == FormWindowState.Minimized) return;
            sampleCount++;
            if (current == AppPage.Overview) UpdateOverview();
            else if (current == AppPage.Usage && sampleCount % 5 == 0 && dtTo.Value.Date >= DateTime.Today) RefreshUsage();
        }

        static Control Page(string title, string subtitle, Control body)
        {
            var p = new Panel();
            body.Dock = DockStyle.Fill;
            p.Controls.Add(body);
            p.Controls.Add(new PageHeader { Title = title, Subtitle = subtitle });
            return p;
        }

        static Card CardWith(string title, Control content, Control headerRight = null)
        {
            var card = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 0, Dpi.S(12), Dpi.S(12)) };
            content.Dock = DockStyle.Fill;
            card.Controls.Add(content);
            var head = new Panel { Dock = DockStyle.Top, Height = Dpi.S(34) };
            var lbl = Ui.Label(title, null, 10.5f, FontStyle.Bold);
            lbl.Location = new Point(0, Dpi.S(2));
            head.Controls.Add(lbl);
            if (headerRight != null)
            {
                headerRight.Dock = DockStyle.Right;
                head.Controls.Add(headerRight);
            }
            card.Controls.Add(head);
            return card;
        }

        // ================================================================ OVERVIEW

        StatTile tDown, tUp, tPing, tToday, tPeriod, tSession;
        Meter capMeter;
        SpeedGraph graph;
        DbListView lvLive;
        readonly Dictionary<string, ListViewItem> liveItems = new Dictionary<string, ListViewItem>();

        Control BuildOverview()
        {
            var grid = new TableLayoutPanel { ColumnCount = 1, RowCount = 4, Margin = new Padding(0) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(116)));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(116)));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(170)));

            var tiles = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0) };
            for (int i = 0; i < 4; i++) tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            tDown = new StatTile("Download", Glyph.Download, () => Palette.Download);
            tUp = new StatTile("Upload", Glyph.Upload, () => Palette.Upload);
            tPing = new StatTile("Latency", Glyph.Clock, () => app.Health.Offline ? UiTheme.Danger : UiTheme.Success);
            tToday = new StatTile("Today", Glyph.Chart, () => Color.FromArgb(0x8B, 0x5C, 0xF6));
            tiles.Controls.Add(tDown, 0, 0); tiles.Controls.Add(tUp, 1, 0); tiles.Controls.Add(tPing, 2, 0); tiles.Controls.Add(tToday, 3, 0);

            graph = new SpeedGraph { History = app.History, Formatter = app.Fmt };
            var window = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Dpi.S(120) };
            window.Items.AddRange(new object[] { "Last minute", "Last 5 minutes", "Last 15 minutes", "Last 30 minutes", "Last hour" });
            window.SelectedIndex = 0;
            window.SelectedIndexChanged += (s, e) => { graph.WindowSeconds = new[] { 60, 300, 900, 1800, 3600 }[window.SelectedIndex]; graph.Invalidate(); };
            var graphCard = CardWith("Live speed", graph, window);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tPeriod = new StatTile("Billing period", Glyph.History, () => Color.FromArgb(0x0E, 0xA5, 0xA5));
            capMeter = new Meter { Visible = false };
            tPeriod.Controls.Add(capMeter);
            tPeriod.Resize += (s, e) => capMeter.SetBounds(tPeriod.Width / 2, Dpi.S(25), tPeriod.Width / 2 - Dpi.S(18), Dpi.S(6));
            tSession = new StatTile("This session", Glyph.Clock, () => Color.FromArgb(0xE1, 0x1D, 0x74));
            var reset = new LinkLabel { Text = "Reset", AutoSize = true };
            reset.LinkClicked += (s, e) => { app.ResetSession(); UpdateOverview(); };
            tSession.Controls.Add(reset);
            tSession.Resize += (s, e) => reset.Location = new Point(tSession.Width - reset.Width - Dpi.S(16), Dpi.S(19));
            bottom.Controls.Add(tPeriod, 0, 0);
            bottom.Controls.Add(tSession, 1, 0);

            lvLive = new DbListView();
            lvLive.Columns.Add("Adapter", Dpi.S(220));
            lvLive.Columns.Add("Type", Dpi.S(110));
            lvLive.Columns.Add("Status", Dpi.S(80));
            lvLive.Columns.Add("Download", Dpi.S(110), HorizontalAlignment.Right);
            lvLive.Columns.Add("Upload", Dpi.S(110), HorizontalAlignment.Right);
            lvLive.Columns.Add("Link", Dpi.S(90), HorizontalAlignment.Right);
            lvLive.Columns.Add("Counted", Dpi.S(70));
            var listCard = CardWith("Network adapters", lvLive);

            grid.Controls.Add(tiles, 0, 0);
            grid.Controls.Add(graphCard, 0, 1);
            grid.Controls.Add(bottom, 0, 2);
            grid.Controls.Add(listCard, 0, 3);
            return Page("Overview", "Live internet speed and today's usage at a glance", grid);
        }

        void UpdateOverview()
        {
            if (tDown == null) return;
            var f = app.Fmt;
            tDown.Set(f.Format(app.CurRx), "Peak " + f.Format(app.PeakRx));
            tUp.Set(f.Format(app.CurTx), "Peak " + f.Format(app.PeakTx));
            var h = app.Health;
            if (!app.Settings.PingEnabled) { tPing.ValueColor = Color.Empty; tPing.Set(h.Offline ? "Offline" : "Online", "Ping monitoring is off"); }
            else if (h.Offline) { tPing.ValueColor = UiTheme.Danger; tPing.Set("Offline", h.OfflineSince.HasValue ? "since " + h.OfflineSince.Value.ToString("t", CultureInfo.CurrentCulture) : ""); }
            else { tPing.ValueColor = Color.Empty; tPing.Set(h.LatencyMs.HasValue ? h.LatencyMs.Value.ToString("0") + " ms" : "…", $"Jitter {h.JitterMs:0} ms  •  loss {h.LossPercent:0}%  •  {app.Settings.PingHost}"); }
            tToday.Set(SpeedFormatter.FormatBytes(app.TodayRx + app.TodayTx), $"↓ {SpeedFormatter.FormatBytes(app.TodayRx)}   ↑ {SpeedFormatter.FormatBytes(app.TodayTx)}");

            var pct = app.CapPercent(out long cap);
            string since = "since " + app.PeriodStart.ToString("dd MMM", CultureInfo.CurrentCulture);
            tPeriod.Set(SpeedFormatter.FormatBytes(app.PeriodRx + app.PeriodTx) + (pct.HasValue ? $"  of {SpeedFormatter.FormatBytes(cap)}" : ""),
                pct.HasValue ? $"{pct.Value:0}% used  •  {since}" : $"{since}  •  no data limit set");
            capMeter.Visible = pct.HasValue;
            if (pct.HasValue)
            {
                capMeter.Value = pct.Value / 100;
                capMeter.BarColor = pct.Value >= 100 ? UiTheme.Danger : pct.Value >= app.Settings.AlertPercent ? UiTheme.Warning : UiTheme.Accent;
            }
            var dur = DateTime.Now - app.SessionStart;
            tSession.Set($"↓ {SpeedFormatter.FormatBytes(app.SessionRx)}   ↑ {SpeedFormatter.FormatBytes(app.SessionTx)}",
                $"Running {(int)dur.TotalHours}h {dur.Minutes:00}m  •  since {app.SessionStart.ToString("t", CultureInfo.CurrentCulture)}");
            graph.Invalidate();

            lvLive.BeginUpdate();
            var seen = new HashSet<string>();
            foreach (var i in app.Monitor.Interfaces)
            {
                if (i.IsVirtual && !i.Monitored && i.RxRate == 0 && i.TxRate == 0 && i.Status != OperationalStatus.Up) continue;
                seen.Add(i.Id);
                if (!liveItems.TryGetValue(i.Id, out var it))
                {
                    it = new ListViewItem(new string[7]) { Tag = i.Id };
                    liveItems[i.Id] = it;
                    lvLive.Items.Add(it);
                }
                SetSub(it, 0, i.Name);
                SetSub(it, 1, TypeText(i));
                SetSub(it, 2, i.Status == OperationalStatus.Up ? "Connected" : i.Status.ToString());
                SetSub(it, 3, f.Format(i.RxRate));
                SetSub(it, 4, f.Format(i.TxRate));
                SetSub(it, 5, LinkSpeed(i.LinkSpeed));
                SetSub(it, 6, i.Monitored ? "✓" : "");
                it.ForeColor = i.Status == OperationalStatus.Up ? UiTheme.Text : UiTheme.Muted;
            }
            foreach (var id in liveItems.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                lvLive.Items.Remove(liveItems[id]);
                liveItems.Remove(id);
            }
            lvLive.EndUpdate();
        }

        string M(string address) => Ui.Mask(address, app.Settings.PrivacyMode);

        static string TypeText(InterfaceInfo i) => i.TypeName + (i.IsVirtual && i.TypeName != "Virtual" ? " (virtual)" : "");

        static void SetSub(ListViewItem it, int i, string text)
        {
            if (it.SubItems[i].Text != text) it.SubItems[i].Text = text;
        }

        static string LinkSpeed(long bps)
        {
            if (bps <= 0) return "";
            if (bps >= 1_000_000_000) return (bps / 1e9).ToString("0.#", CultureInfo.CurrentCulture) + " Gbps";
            if (bps >= 1_000_000) return (bps / 1e6).ToString("0.#", CultureInfo.CurrentCulture) + " Mbps";
            return (bps / 1e3).ToString("0", CultureInfo.CurrentCulture) + " Kbps";
        }

        // ================================================================ SPEED TEST

        Gauge gauge;
        Button btnTest;
        StatTile sPing, sJitter, sDown, sUp;
        Label lblServer;
        DbListView lvTests;

        Control BuildSpeedTest()
        {
            var grid = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, Margin = new Padding(0) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(360)));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var gaugeBody = new Panel();
            gauge = new Gauge { Dock = DockStyle.Fill };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = Dpi.S(48), FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            btnTest = Ui.Button("   Start test   ", (s, e) => { if (app.SpeedTestRunning) app.CancelSpeedTest(); else app.StartSpeedTest(); }, true);
            btnTest.Font = new Font(Font.FontFamily, 11f, FontStyle.Bold);
            btnTest.MinimumSize = new Size(Dpi.S(160), Dpi.S(40));
            lblServer = Ui.Label("Measured against Cloudflare's global network (speed.cloudflare.com).", "muted");
            lblServer.Margin = new Padding(Dpi.S(6), Dpi.S(12), 0, 0);
            bar.Controls.Add(btnTest);
            bar.Controls.Add(lblServer);
            bar.Resize += (s, e) => btnTest.Margin = new Padding(Math.Max(0, (bar.Width - btnTest.Width) / 2 - Dpi.S(4)), 0, 0, 0);
            lblServer.Visible = false;
            gaugeBody.Controls.Add(gauge);
            gaugeBody.Controls.Add(bar);
            var gaugeCard = CardWith("Speed test", gaugeBody);

            var results = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Margin = new Padding(0) };
            results.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            results.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 2; i++) results.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(116)));
            results.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sDown = new StatTile("Download", Glyph.Download, () => Palette.Download);
            sUp = new StatTile("Upload", Glyph.Upload, () => Palette.Upload);
            sPing = new StatTile("Ping", Glyph.Clock, () => UiTheme.Success);
            sJitter = new StatTile("Jitter", Glyph.Wifi, () => Color.FromArgb(0x8B, 0x5C, 0xF6));
            results.Controls.Add(sDown, 0, 0); results.Controls.Add(sUp, 1, 0);
            results.Controls.Add(sPing, 0, 1); results.Controls.Add(sJitter, 1, 1);
            var note = Ui.Label("Tip: close downloads and streaming for the most accurate result. A test transfers roughly 50–500 MB depending on your speed.", "muted");
            note.MaximumSize = new Size(Dpi.S(380), 0);
            note.Margin = new Padding(Dpi.S(4), Dpi.S(6), 0, 0);
            results.Controls.Add(note, 0, 2);
            results.SetColumnSpan(note, 2);
            ShowLastResult();

            lvTests = new DbListView();
            lvTests.Columns.Add("Date", Dpi.S(170));
            lvTests.Columns.Add("Download", Dpi.S(110), HorizontalAlignment.Right);
            lvTests.Columns.Add("Upload", Dpi.S(110), HorizontalAlignment.Right);
            lvTests.Columns.Add("Ping", Dpi.S(80), HorizontalAlignment.Right);
            lvTests.Columns.Add("Jitter", Dpi.S(80), HorizontalAlignment.Right);
            lvTests.Columns.Add("Server", Dpi.S(120));
            lvTests.Columns.Add("Public IP", Dpi.S(150));
            var hdr = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            hdr.Controls.Add(Ui.Button("Export CSV", (s, e) => ExportTests()));
            hdr.Controls.Add(Ui.Button("Clear history", (s, e) =>
            {
                if (MessageBox.Show(this, "Delete all speed test results?", "WinSpeed", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                { app.SpeedTests.Clear(); RefreshSpeedHistory(); }
            }));
            var histCard = CardWith("History", lvTests, hdr);

            grid.Controls.Add(gaugeCard, 0, 0);
            grid.Controls.Add(results, 1, 0);
            grid.Controls.Add(histCard, 0, 1);
            grid.SetColumnSpan(histCard, 2);
            return Page("Speed test", "Measure your real download, upload, ping and jitter", grid);
        }

        static string Mbps(double bps) => (bps * 8 / 1e6).ToString(bps * 8 / 1e6 >= 100 ? "0" : "0.0", CultureInfo.CurrentCulture) + " Mbps";

        void ShowLastResult()
        {
            var r = app.SpeedTests.Items.LastOrDefault();
            if (r == null)
            {
                sDown.Set("—", "No test yet"); sUp.Set("—", ""); sPing.Set("—", ""); sJitter.Set("—", "");
                return;
            }
            string when = r.Time.ToString("g", CultureInfo.CurrentCulture);
            if (!app.SpeedTestRunning && gauge != null)
            {
                gauge.ArcColor = Palette.Download;
                gauge.Phase = "Last result";
                gauge.Detail = $"↑ {Mbps(r.UpBps)}   •   {r.PingMs:0} ms ping";
                gauge.Mbps = r.DownBps * 8 / 1e6;
            }
            sDown.Set(Mbps(r.DownBps), SpeedFormatter.FormatBytes(r.DownBps) + "/s");
            sUp.Set(Mbps(r.UpBps), SpeedFormatter.FormatBytes(r.UpBps) + "/s");
            sPing.Set(r.PingMs.ToString("0") + " ms", "Tested " + when);
            sJitter.Set(r.JitterMs.ToString("0.0") + " ms", "Server " + (r.Server.Length > 0 ? r.Server : "—"));
        }

        void RefreshSpeedHistory()
        {
            lvTests.BeginUpdate();
            lvTests.Items.Clear();
            foreach (var r in Enumerable.Reverse(app.SpeedTests.Items))
                lvTests.Items.Add(new ListViewItem(new[]
                {
                    r.Time.ToString("ddd dd MMM yyyy  HH:mm", CultureInfo.CurrentCulture), Mbps(r.DownBps), Mbps(r.UpBps),
                    r.PingMs.ToString("0") + " ms", r.JitterMs.ToString("0.0") + " ms", r.Server, M(r.Ip)
                }));
            lvTests.EndUpdate();
            ShowLastResult();
            UpdateTestButton();
        }

        void UpdateTestButton()
        {
            btnTest.Text = app.SpeedTestRunning ? "   Cancel   " : (app.SpeedTests.Items.Count > 0 ? "   Test again   " : "   Start test   ");
        }

        void OnSpeedTestProgress(string phase, double frac, double value)
        {
            if (gauge == null) return;
            UpdateTestButton();
            switch (phase)
            {
                case "ping":
                    gauge.ArcColor = UiTheme.Success; gauge.Phase = "Measuring latency"; gauge.Mbps = 0;
                    gauge.Detail = value > 0 ? value.ToString("0") + " ms" : "";
                    break;
                case "download":
                    gauge.ArcColor = Palette.Download; gauge.Phase = "Download"; gauge.Mbps = value * 8 / 1e6;
                    gauge.Detail = $"{frac * 100:0}%";
                    sDown.Set(Mbps(value), "measuring…");
                    break;
                case "upload":
                    gauge.ArcColor = Palette.Upload; gauge.Phase = "Upload"; gauge.Mbps = value * 8 / 1e6;
                    gauge.Detail = $"{frac * 100:0}%";
                    sUp.Set(Mbps(value), "measuring…");
                    break;
            }
        }

        void OnSpeedTestFinished(SpeedTestResult r, string error)
        {
            if (gauge == null) return;
            if (r != null)
            {
                gauge.ArcColor = Palette.Download; gauge.Phase = "Complete"; gauge.Mbps = r.DownBps * 8 / 1e6;
                gauge.Detail = $"↓ {Mbps(r.DownBps)}   ↑ {Mbps(r.UpBps)}   {r.PingMs:0} ms";
            }
            else
            {
                gauge.ArcColor = UiTheme.Danger; gauge.Mbps = 0;
                gauge.Phase = error == null ? "Cancelled" : "Test failed"; gauge.Detail = error ?? "";
            }
            RefreshSpeedHistory();
        }

        void ExportTests()
        {
            using (var dlg = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = "WinSpeed-speedtests.csv" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var sb = new StringBuilder("Date,Download (Mbps),Upload (Mbps),Ping (ms),Jitter (ms),Server,Public IP\r\n");
                foreach (var r in app.SpeedTests.Items)
                    sb.AppendLine(string.Join(",", r.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        (r.DownBps * 8 / 1e6).ToString("0.00", CultureInfo.InvariantCulture), (r.UpBps * 8 / 1e6).ToString("0.00", CultureInfo.InvariantCulture),
                        r.PingMs.ToString("0.0", CultureInfo.InvariantCulture), r.JitterMs.ToString("0.0", CultureInfo.InvariantCulture), Q(r.Server), Q(r.Ip)));
                try { File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true)); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        // ================================================================ USAGE

        ComboBox cbRange, cbGroup, cbIface;
        DateTimePicker dtFrom, dtTo;
        UsageChart chart;
        DbListView lvUsage;
        Label lblUsageTotal;
        CheckBox chkHideEmpty;
        List<UsageRow> usageRows = new List<UsageRow>(); // chronological (chart)
        List<UsageRow> listRows = new List<UsageRow>();  // newest first (table)
        bool suppressRange;

        static readonly string[] Ranges =
        {
            "Today", "Yesterday", "Last 7 days", "Last 30 days", "This month", "Last month",
            "Current billing period", "Previous billing period", "This year", "All time", "Custom range"
        };

        sealed class IfaceItem
        {
            public string Text; public Func<string, bool> Filter;
            public override string ToString() => Text;
        }

        Control BuildUsage()
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 0, 0, Dpi.S(8)) };
            cbRange = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Dpi.S(170) };
            cbRange.Items.AddRange(Ranges);
            dtFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = Dpi.S(115) };
            dtTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = Dpi.S(115) };
            cbGroup = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Dpi.S(90) };
            cbGroup.Items.AddRange(new object[] { "Hour", "Day", "Month", "Year" });
            cbIface = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Dpi.S(210) };
            chkHideEmpty = new CheckBox { Text = "Hide empty", Checked = true, AutoSize = true, Margin = new Padding(Dpi.S(8), Dpi.S(6), Dpi.S(8), 0) };
            chkHideEmpty.CheckedChanged += (s, e) => RefreshUsage();

            void Pair(string label, Control c)
            {
                var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, Dpi.S(10), Dpi.S(4)) };
                var l = Ui.Label(label, "muted");
                l.Margin = new Padding(0, Dpi.S(6), Dpi.S(4), 0);
                p.Controls.Add(l);
                p.Controls.Add(c);
                bar.Controls.Add(p);
            }
            Pair("Range", cbRange);
            Pair("From", dtFrom);
            Pair("To", dtTo);
            Pair("Group by", cbGroup);
            Pair("Adapter", cbIface);
            bar.Controls.Add(chkHideEmpty);
            bar.Controls.Add(Ui.Button("Export CSV", (s, e) => ExportCsv()));

            chart = new UsageChart { Dock = DockStyle.Top, Height = Dpi.S(220) };
            chart.BarClicked += i =>
            {
                if (i < 0 || i >= usageRows.Count) return;
                int li = listRows.IndexOf(usageRows[i]);
                if (li < 0) return;
                lvUsage.SelectedIndices.Clear();
                lvUsage.Items[li].Selected = true;
                lvUsage.EnsureVisible(li);
            };
            lvUsage = new DbListView { Dock = DockStyle.Fill };
            lvUsage.Columns.Add("Period", Dpi.S(240));
            lvUsage.Columns.Add("Download", Dpi.S(130), HorizontalAlignment.Right);
            lvUsage.Columns.Add("Upload", Dpi.S(130), HorizontalAlignment.Right);
            lvUsage.Columns.Add("Total", Dpi.S(130), HorizontalAlignment.Right);
            lvUsage.Columns.Add("Share", Dpi.S(80), HorizontalAlignment.Right);
            lvUsage.DoubleClick += (s, e) => DrillDown();
            lblUsageTotal = Ui.Label("", null, 0, FontStyle.Bold);
            lblUsageTotal.AutoSize = false;
            lblUsageTotal.Dock = DockStyle.Bottom;
            lblUsageTotal.Height = Dpi.S(30);
            lblUsageTotal.TextAlign = ContentAlignment.MiddleLeft;

            var body = new Panel();
            body.Controls.Add(lvUsage);
            body.Controls.Add(new Panel { Dock = DockStyle.Top, Height = Dpi.S(10) });
            body.Controls.Add(chart);
            body.Controls.Add(lblUsageTotal);
            var card = CardWith("History", body);
            card.Margin = new Padding(0);

            var wrap = new Panel();
            wrap.Controls.Add(card);
            wrap.Controls.Add(bar);
            card.Dock = DockStyle.Fill;

            cbRange.SelectedIndexChanged += (s, e) => ApplyRange();
            EventHandler custom = (s, e) =>
            {
                if (suppressRange) return;
                suppressRange = true;
                cbRange.SelectedIndex = Ranges.Length - 1;
                suppressRange = false;
                RefreshUsage();
            };
            dtFrom.ValueChanged += custom;
            dtTo.ValueChanged += custom;
            cbGroup.SelectedIndexChanged += (s, e) => { if (!suppressRange) RefreshUsage(); };
            cbIface.SelectedIndexChanged += (s, e) => { if (!suppressRange) RefreshUsage(); };

            RefreshIfaceFilter();
            cbRange.SelectedIndex = 3;
            return Page("Data usage", "How much data you used — by hour, day, month or year, per adapter. Double-click a row to drill down.", wrap);
        }

        void RefreshIfaceFilter()
        {
            var cur = cbIface.SelectedItem as IfaceItem;
            suppressRange = true;
            cbIface.Items.Clear();
            cbIface.Items.Add(new IfaceItem { Text = "Counted adapters", Filter = app.IsCountedId });
            cbIface.Items.Add(new IfaceItem { Text = "All adapters", Filter = id => true });
            cbIface.Items.Add(new IfaceItem { Text = "Physical adapters only", Filter = id => !app.Usage.IsVirtual(id) });
            foreach (var kv in app.Usage.Ifaces.OrderBy(k => k.Value.Name))
            {
                var id = kv.Key;
                cbIface.Items.Add(new IfaceItem { Text = kv.Value.Name + (kv.Value.Virtual ? " (virtual)" : ""), Filter = x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase) });
            }
            int idx = 0;
            if (cur != null)
                for (int i = 0; i < cbIface.Items.Count; i++)
                    if (((IfaceItem)cbIface.Items[i]).Text == cur.Text) { idx = i; break; }
            cbIface.SelectedIndex = idx;
            suppressRange = false;
        }

        void ApplyRange()
        {
            if (suppressRange) return;
            var today = DateTime.Today;
            DateTime from = today, to = today;
            Grouping g = Grouping.Day;
            switch (cbRange.SelectedIndex)
            {
                case 0: g = Grouping.Hour; break;
                case 1: from = to = today.AddDays(-1); g = Grouping.Hour; break;
                case 2: from = today.AddDays(-6); break;
                case 3: from = today.AddDays(-29); break;
                case 4: from = new DateTime(today.Year, today.Month, 1); break;
                case 5: from = new DateTime(today.Year, today.Month, 1).AddMonths(-1); to = from.AddMonths(1).AddDays(-1); break;
                case 6: from = TrayApp.BillingStart(today, app.Settings.BillingDay); to = from.AddMonths(1).AddDays(-1); break;
                case 7: from = TrayApp.BillingStart(today, app.Settings.BillingDay).AddMonths(-1); to = from.AddMonths(1).AddDays(-1); break;
                case 8: from = new DateTime(today.Year, 1, 1); g = Grouping.Month; break;
                case 9: from = app.Usage.Earliest?.Date ?? today; g = (today - from).TotalDays > 62 ? Grouping.Month : Grouping.Day; break;
                default: RefreshUsage(); return;
            }
            suppressRange = true;
            dtFrom.Value = from;
            dtTo.Value = to;
            cbGroup.SelectedIndex = (int)g;
            suppressRange = false;
            RefreshUsage();
        }

        void RefreshUsage()
        {
            if (cbGroup.SelectedIndex < 0 || cbIface.SelectedItem == null) return;
            var from = dtFrom.Value.Date;
            var to = dtTo.Value.Date;
            if (to < from) { var t = from; from = to; to = t; }
            var g = (Grouping)cbGroup.SelectedIndex;
            var filter = ((IfaceItem)cbIface.SelectedItem).Filter;
            usageRows = app.Usage.Query(from, to.AddDays(1), filter, g);
            chart.SetData(usageRows, g);

            long totRx = usageRows.Sum(r => r.Rx), totTx = usageRows.Sum(r => r.Tx);
            long tot = Math.Max(1, totRx + totTx);
            DateTime? selectedPeriod = lvUsage.SelectedIndices.Count > 0 ? listRows[lvUsage.SelectedIndices[0]].Period : (DateTime?)null;
            int topIndex = lvUsage.TopItem?.Index ?? 0;
            listRows = usageRows.Where(r => !chkHideEmpty.Checked || r.Total > 0).Reverse().ToList();
            lvUsage.BeginUpdate();
            lvUsage.Items.Clear();
            foreach (var r in listRows)
            {
                var it = new ListViewItem(new[]
                {
                    PeriodText(r.Period, g), SpeedFormatter.FormatBytes(r.Rx), SpeedFormatter.FormatBytes(r.Tx), SpeedFormatter.FormatBytes(r.Total),
                    (r.Total * 100.0 / tot).ToString("0.0", CultureInfo.CurrentCulture) + " %"
                });
                if (r.Total == 0) it.ForeColor = UiTheme.Muted;
                lvUsage.Items.Add(it);
            }
            int sel = selectedPeriod.HasValue ? listRows.FindIndex(r => r.Period == selectedPeriod.Value) : -1;
            if (sel >= 0) lvUsage.Items[sel].Selected = true;
            if (topIndex > 0 && topIndex < lvUsage.Items.Count) lvUsage.TopItem = lvUsage.Items[topIndex];
            lvUsage.EndUpdate();

            int days = (int)(to - from).TotalDays + 1;
            var first = app.Usage.Earliest?.Date ?? DateTime.Today;
            var avgFrom = from > first ? from : first;
            var avgTo = to < DateTime.Today ? to : DateTime.Today;
            int avgDays = Math.Max(1, (int)(avgTo - avgFrom).TotalDays + 1);
            lblUsageTotal.Text = $"Total   ↓ {SpeedFormatter.FormatBytes(totRx)}     ↑ {SpeedFormatter.FormatBytes(totTx)}     Σ {SpeedFormatter.FormatBytes(totRx + totTx)}" +
                                 (days > 1 ? $"          Daily average  {SpeedFormatter.FormatBytes((totRx + totTx) / (double)avgDays)}" : "");
        }

        static string PeriodText(DateTime p, Grouping g)
        {
            switch (g)
            {
                case Grouping.Hour: return p.ToString("ddd dd MMM yyyy  HH:00", CultureInfo.CurrentCulture) + "–" + p.AddHours(1).ToString("HH:00");
                case Grouping.Day: return p.ToString("ddd dd MMM yyyy", CultureInfo.CurrentCulture);
                case Grouping.Month: return p.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
                default: return p.Year.ToString(CultureInfo.CurrentCulture);
            }
        }

        void DrillDown()
        {
            if (lvUsage.SelectedIndices.Count == 0) return;
            var row = listRows[lvUsage.SelectedIndices[0]];
            DateTime from, to; Grouping ng;
            switch ((Grouping)cbGroup.SelectedIndex)
            {
                case Grouping.Year: from = row.Period; to = row.Period.AddYears(1).AddDays(-1); ng = Grouping.Month; break;
                case Grouping.Month: from = row.Period; to = row.Period.AddMonths(1).AddDays(-1); ng = Grouping.Day; break;
                case Grouping.Day: from = to = row.Period; ng = Grouping.Hour; break;
                default: return;
            }
            suppressRange = true;
            cbRange.SelectedIndex = Ranges.Length - 1;
            dtFrom.Value = from; dtTo.Value = to; cbGroup.SelectedIndex = (int)ng;
            suppressRange = false;
            RefreshUsage();
        }

        void ExportCsv()
        {
            using (var dlg = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = $"WinSpeed-usage-{dtFrom.Value:yyyyMMdd}-{dtTo.Value:yyyyMMdd}.csv" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var g = (Grouping)cbGroup.SelectedIndex;
                    var sb = new StringBuilder("Period,Download (bytes),Upload (bytes),Total (bytes),Download,Upload,Total\r\n");
                    string fmt = g == Grouping.Hour ? "yyyy-MM-dd HH:00" : g == Grouping.Day ? "yyyy-MM-dd" : g == Grouping.Month ? "yyyy-MM" : "yyyy";
                    foreach (var r in usageRows.Where(x => !chkHideEmpty.Checked || x.Total > 0))
                        sb.AppendLine(string.Join(",", r.Period.ToString(fmt, CultureInfo.InvariantCulture),
                            r.Rx.ToString(CultureInfo.InvariantCulture), r.Tx.ToString(CultureInfo.InvariantCulture), r.Total.ToString(CultureInfo.InvariantCulture),
                            Q(SpeedFormatter.FormatBytes(r.Rx)), Q(SpeedFormatter.FormatBytes(r.Tx)), Q(SpeedFormatter.FormatBytes(r.Total))));
                    File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

        // ================================================================ CONNECTION

        StatTile cStatus, cIp, cIsp, cLoc;
        DbListView lvIf;

        Control BuildConnection()
        {
            var grid = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi.S(116)));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var tiles = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0) };
            for (int i = 0; i < 4; i++) tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            cStatus = new StatTile("Internet", Glyph.Wifi, () => app.Health.Offline ? UiTheme.Danger : UiTheme.Success);
            cIp = new StatTile("Public IP", Glyph.Globe, () => UiTheme.Accent);
            cIsp = new StatTile("Provider (ISP)", Glyph.Network, () => Color.FromArgb(0x8B, 0x5C, 0xF6));
            cLoc = new StatTile("Location", Glyph.Home, () => Color.FromArgb(0x0E, 0xA5, 0xA5));
            tiles.Controls.Add(cStatus, 0, 0); tiles.Controls.Add(cIp, 1, 0); tiles.Controls.Add(cIsp, 2, 0); tiles.Controls.Add(cLoc, 3, 0);
            var reveal = new LinkLabel { Text = "Show", AutoSize = true };
            reveal.LinkClicked += async (s, e) =>
            {
                reveal.Text = "Looking up…";
                var info = await PublicInfoService.FetchAsync();
                reveal.Text = "Refresh";
                reveal.Location = new Point(cIp.Width - reveal.Width - Dpi.S(16), Dpi.S(19));
                cIp.Set(info.Ip.Length > 0 ? M(info.Ip) : "Unavailable", info.Colo.Length > 0 ? "Cloudflare edge " + info.Colo : "");
                cIsp.Set(info.Isp.Length > 0 ? info.Isp : "—", "");
                cLoc.Set(string.Join(", ", new[] { info.City, info.Country }.Where(x => x.Length > 0)) is string loc && loc.Length > 0 ? loc : "—", "approximate, from IP address");
            };
            cIp.Controls.Add(reveal);
            cIp.Resize += (s, e) => reveal.Location = new Point(cIp.Width - reveal.Width - Dpi.S(16), Dpi.S(19));
            cIp.Set("Hidden", "Click Show to look up (uses cloudflare.com and ipinfo.io)");
            cIsp.Set("—", ""); cLoc.Set("—", "");

            lvIf = new DbListView { FitColumns = false };
            foreach (var (name, w) in new[] { ("Name", 150), ("Description", 230), ("Type", 100), ("Status", 80), ("Counted", 60), ("Link speed", 80),
                                              ("MAC address", 130), ("IPv4", 120), ("IPv6", 200), ("Gateway", 110), ("DNS", 160), ("Received", 90), ("Sent", 90) })
                lvIf.Columns.Add(name, Dpi.S(w));
            var hdr = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            hdr.Controls.Add(Ui.Button("Refresh", (s, e) => { app.Monitor.Invalidate(); app.Monitor.Next(); RefreshInterfaces(); }));
            hdr.Controls.Add(Ui.Button("Copy details", (s, e) => CopyInterfaces()));
            hdr.Controls.Add(Ui.Button("Choose counted adapters", (s, e) => ShowPage(AppPage.Settings)));
            var card = CardWith("Network adapters", lvIf, hdr);

            grid.Controls.Add(tiles, 0, 0);
            grid.Controls.Add(card, 0, 1);
            return Page("Connection", "Internet status, public IP and every network adapter on this PC", grid);
        }

        void OnHealth()
        {
            if (cStatus == null) return;
            var h = app.Health;
            if (h.Offline) { cStatus.ValueColor = UiTheme.Danger; cStatus.Set("Offline", h.OfflineSince.HasValue ? "since " + h.OfflineSince.Value.ToString("T", CultureInfo.CurrentCulture) : ""); }
            else
            {
                cStatus.ValueColor = UiTheme.Success;
                cStatus.Set("Online", app.Settings.PingEnabled && h.LatencyMs.HasValue ? $"{h.LatencyMs:0} ms to {app.Settings.PingHost}  •  loss {h.LossPercent:0}%" : "");
            }
            if (current == AppPage.Overview) UpdateOverview();
        }

        void RefreshInterfaces()
        {
            lvIf.BeginUpdate();
            lvIf.Items.Clear();
            foreach (var i in app.Monitor.Interfaces)
            {
                string v4 = "", v6 = "", gw = "", dns = "";
                try
                {
                    var p = i.Ni?.GetIPProperties();
                    if (p != null)
                    {
                        v4 = string.Join(", ", p.UnicastAddresses.Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(a => a.Address.ToString()));
                        v6 = string.Join(", ", p.UnicastAddresses.Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6).Select(a => a.Address.ToString()));
                        gw = string.Join(", ", p.GatewayAddresses.Select(a => a.Address.ToString()));
                        dns = string.Join(", ", p.DnsAddresses.Select(a => a.ToString()));
                    }
                }
                catch { }
                var it = new ListViewItem(new[]
                {
                    i.Name, i.Description, TypeText(i), i.Status == OperationalStatus.Up ? "Connected" : i.Status.ToString(), i.Monitored ? "✓" : "",
                    LinkSpeed(i.LinkSpeed), M(i.Mac), M(v4), M(v6), M(gw), M(dns), SpeedFormatter.FormatBytes(i.RxTotal), SpeedFormatter.FormatBytes(i.TxTotal)
                });
                if (i.Status != OperationalStatus.Up) it.ForeColor = UiTheme.Muted;
                lvIf.Items.Add(it);
            }
            lvIf.EndUpdate();
        }

        void CopyInterfaces()
        {
            var sb = new StringBuilder();
            foreach (ListViewItem it in lvIf.Items)
            {
                for (int c = 0; c < lvIf.Columns.Count; c++) sb.Append(lvIf.Columns[c].Text).Append(": ").AppendLine(it.SubItems[c].Text);
                sb.AppendLine();
            }
            if (sb.Length > 0) Clipboard.SetText(sb.ToString());
        }

        // ================================================================ ABOUT

        Control BuildAbout()
        {
            var card = new Card { Dock = DockStyle.Top, Height = Dpi.S(380), Padding = new Padding(Dpi.S(28)) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            var logo = new PictureBox { Size = new Size(Dpi.S(64), Dpi.S(64)), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 0, Dpi.S(10)) };
            using (var ic = TrayIcons.LoadAppIcon(new Size(256, 256))) logo.Image = ic.ToBitmap();
            flow.Controls.Add(logo);
            flow.Controls.Add(Ui.Label("WinSpeed", null, 20f, FontStyle.Bold));
            flow.Controls.Add(Ui.Label($"Version {Program.Version}  •  Free & open source (MIT licence)", "muted"));
            var desc = Ui.Label("A tiny, privacy-friendly internet speed meter for the Windows taskbar — live speeds, data usage history, speed tests and data-cap alerts. No ads, no telemetry, no account.");
            desc.MaximumSize = new Size(Dpi.S(620), 0);
            desc.Margin = new Padding(0, Dpi.S(12), 0, Dpi.S(16));
            flow.Controls.Add(desc);

            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
            var upd = Ui.Button("Check for updates", null, true);
            upd.Click += async (s, e) => { upd.Enabled = false; await app.CheckForUpdatesAsync(true); upd.Enabled = true; };
            buttons.Controls.Add(upd);
            buttons.Controls.Add(Ui.Button("User guide (wiki)", (s, e) => Http.Open(Http.WikiUrl)));
            buttons.Controls.Add(Ui.Button("GitHub", (s, e) => Http.Open(Http.RepoUrl)));
            buttons.Controls.Add(Ui.Button("Report an issue", (s, e) => Http.Open(Http.IssuesUrl + "/new/choose")));
            buttons.Controls.Add(Ui.Button("Open data folder", (s, e) => { try { System.Diagnostics.Process.Start("explorer.exe", "\"" + Settings.DataDir + "\""); } catch { } }));
            flow.Controls.Add(buttons);

            var info = Ui.Label($"Data folder: {Settings.DataDir}{(Settings.IsPortable ? "  (portable mode)" : "")}\n" +
                                $"Show / hide hotkey: {(string.IsNullOrEmpty(app.Settings.Hotkey) ? "none" : app.Settings.Hotkey)}\n" +
                                "Speed tests use Cloudflare's public speed-test service. Nothing else leaves your PC unless you ask for it.", "muted");
            info.Margin = new Padding(0, Dpi.S(16), 0, 0);
            flow.Controls.Add(info);
            card.Controls.Add(flow);

            var wrap = new Panel();
            wrap.Controls.Add(card);
            return Page("About", "Made with care — contributions welcome", wrap);
        }
    }
}
