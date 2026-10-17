using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace WinSpeed
{
    /// <summary>Owns everything: sampling timer, meter window, tray icon, statistics, persistence.</summary>
    sealed class TrayApp : ApplicationContext
    {
        public readonly Settings Settings;
        public readonly SpeedFormatter Fmt;
        public readonly NetMonitor Monitor;
        public readonly UsageStore Usage;
        public readonly SpeedHistory History = new SpeedHistory(3600);

        readonly SpeedOverlay overlay;
        readonly NotifyIcon tray;
        readonly Timer timer;
        readonly ContextMenuStrip menu;
        readonly HotkeyWindow hotkey;
        readonly SynchronizationContext ui;
        readonly RegisteredWaitHandle activateWait, exitWait;
        readonly Icon appIcon;
        Icon liveIcon;
        string liveIconKey;
        MainWindow window;
        public readonly NetworkHealth Health;
        public readonly SpeedTestHistory SpeedTests = new SpeedTestHistory();
        System.Threading.CancellationTokenSource speedTestCts;
        public bool SpeedTestRunning => speedTestCts != null;
        public event Action<string, double, double> SpeedTestProgress;
        public event Action<SpeedTestResult, string> SpeedTestFinished;
        public event Action HealthUpdated;
        string pendingUpdateUrl;
        HashSet<string> selected;
        bool exiting;

        public event Action<Sample> Sampled;

        // statistics (monitored interfaces only)
        public DateTime SessionStart { get; private set; } = DateTime.Now;
        public long SessionRx, SessionTx;
        public double PeakRx, PeakTx, CurRx, CurTx;
        public long TodayRx, TodayTx, PeriodRx, PeriodTx;
        public DateTime PeriodStart { get; private set; }
        DateTime totalsDate;
        readonly Queue<double[]> smooth = new Queue<double[]>();
        DateTime lastSave = DateTime.Now;
        int tickCount;

        public TrayApp(EventWaitHandle activate, EventWaitHandle exit, bool fromStartup)
        {
            Settings = Settings.Load();
            Fmt = new SpeedFormatter(Settings);
            Usage = new UsageStore(Settings.DataDir);
            Usage.Prune(Settings.RetentionDays);
            selected = Settings.SelectedSet();
            UiTheme.Resolve(Settings.Theme);

            Monitor = new NetMonitor { MonitorPredicate = IsMonitoredLive };
            Monitor.InterfacesChanged += () =>
            {
                foreach (var i in Monitor.Interfaces) Usage.Register(i.Id, i.Name, i.IsVirtual);
            };

            appIcon = TrayIcons.LoadAppIcon(SystemInformation.SmallIconSize);
            menu = BuildMenu();

            Health = new NetworkHealth(Settings);
            overlay = new SpeedOverlay(Settings, Fmt) { MeterMenu = menu, TooltipProvider = BuildTooltip, History = History, Health = Health };
            overlay.MeterDoubleClick += (s, e) => RunAction(Settings.DoubleClickAction);
            overlay.PositionCommitted += (s, e) => Settings.Save();
            var _ = overlay.Handle; // create the window (installs the WinForms sync context)
            ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            tray = new NotifyIcon { Icon = appIcon, Text = Program.AppName, ContextMenuStrip = menu };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) RunAction(Settings.TrayClickAction); };
            tray.BalloonTipClicked += (s, e) =>
            {
                if (pendingUpdateUrl != null) { Http.Open(pendingUpdateUrl); pendingUpdateUrl = null; }
                else ShowPage(AppPage.Overview);
            };
            Health.ConnectivityChanged += online =>
            {
                if (Settings.NotifyConnection && !exiting)
                    ShowBalloon(online ? "Internet connection restored" : "Internet connection lost",
                        online ? "You're back online." : "WinSpeed can't reach the internet. You'll be told when it's back.",
                        online ? ToolTipIcon.Info : ToolTipIcon.Warning);
                overlay.Render();
            };
            Health.Updated += () => HealthUpdated?.Invoke();
            UpdateTrayVisibility();

            hotkey = new HotkeyWindow();
            hotkey.Pressed += ToggleMeter;
            hotkey.Register(Settings.Hotkey);

            activateWait = ThreadPool.RegisterWaitForSingleObject(activate, (o, t) => ui.Post(x => ShowTarget(Program.TakeTarget()), null), null, -1, false);
            exitWait = ThreadPool.RegisterWaitForSingleObject(exit, (o, t) => ui.Post(x => Exit(), null), null, -1, false);

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionEnding += OnSessionEnding;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            RefreshTotals();
            Monitor.Next(); // prime counters

            timer = new Timer { Interval = Settings.UpdateIntervalMs };
            timer.Tick += OnTick;
            timer.Start();
            overlay.Tick();

            if (Settings.FirstRun) ui.Post(x => RunWelcome(), null);

            // update check shortly after startup, then every few hours (at most once a day)
            var updTimer = new Timer { Interval = fromStartup ? 120000 : 15000 };
            updTimer.Tick += async (s, e) => { updTimer.Interval = 6 * 3600 * 1000; await CheckForUpdatesAsync(false); };
            updTimer.Start();
        }

        void RunWelcome()
        {
            Settings.FirstRun = false;
            using (var w = new WelcomeForm())
            {
                if (w.ShowDialog() == DialogResult.OK)
                {
                    Settings.Unit = w.Unit;
                    Settings.Placement = w.Placement;
                    StartupManager.Enabled = w.Startup;
                }
            }
            SettingsChanged();
            ShowBalloon("WinSpeed is running",
                "Your internet speed is on the taskbar. Right-click the meter or this icon for options; drag the meter to move it.",
                ToolTipIcon.Info);
        }

        // ------------------------------------------------------------------ updates

        public async System.Threading.Tasks.Task CheckForUpdatesAsync(bool interactive)
        {
            string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (!interactive && (!Settings.CheckForUpdates || Settings.LastUpdateCheck == today)) return;
            UpdateChecker.Release rel = null;
            try { rel = await UpdateChecker.LatestAsync(); }
            catch (Exception ex) { Program.Log(ex); }
            Settings.LastUpdateCheck = today;
            Settings.Save();

            if (UpdateChecker.IsNewer(rel) && (interactive || rel.Tag != Settings.SkippedVersion))
            {
                if (interactive)
                {
                    if (MessageBox.Show($"WinSpeed {rel.Version.ToString(3)} is available (you have {Program.Version}).\n\nOpen the download page?",
                            "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        Http.Open(rel.Url);
                }
                else
                {
                    pendingUpdateUrl = rel.Url;
                    ShowBalloon($"WinSpeed {rel.Version.ToString(3)} is available", "Click here to open the download page.", ToolTipIcon.Info);
                    Settings.SkippedVersion = rel.Tag; // remind once per version
                    Settings.Save();
                }
            }
            else if (interactive)
            {
                MessageBox.Show(rel == null ? "Couldn't reach GitHub to check for updates. Please try again later."
                                            : $"You're running the latest version ({Program.Version}).",
                    "WinSpeed", MessageBoxButtons.OK, rel == null ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
        }

        // ------------------------------------------------------------------ speed test

        public async void StartSpeedTest()
        {
            if (SpeedTestRunning) return;
            speedTestCts = new System.Threading.CancellationTokenSource();
            var token = speedTestCts.Token;
            var test = new SpeedTest();
            test.Progress += (phase, frac, value) => ui.Post(x => SpeedTestProgress?.Invoke(phase, frac, value), null);
            SpeedTestProgress?.Invoke("ping", 0, 0);
            SpeedTestResult result = null;
            string error = null;
            try
            {
                result = await System.Threading.Tasks.Task.Run(() => test.RunAsync(token));
                SpeedTests.Add(result);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Program.Log(ex);
                error = ex is System.Net.Http.HttpRequestException || ex.GetBaseException() is System.Net.WebException
                    ? "Couldn't reach the speed-test server. Check your connection."
                    : ex.GetBaseException().Message;
            }
            finally
            {
                speedTestCts.Dispose();
                speedTestCts = null;
            }
            SpeedTestFinished?.Invoke(result, error);
            if (result != null && Settings.NotifySpeedTestDone && (window == null || !window.Visible || !window.ContainsFocus))
                ShowBalloon("Speed test complete",
                    $"↓ {result.DownBps * 8 / 1e6:0.0} Mbps   ↑ {result.UpBps * 8 / 1e6:0.0} Mbps   Ping {result.PingMs:0} ms", ToolTipIcon.Info);
        }

        public void CancelSpeedTest() => speedTestCts?.Cancel();

        // ------------------------------------------------------------------ interfaces

        bool IsMonitoredLive(InterfaceInfo i)
        {
            if (Settings.Interfaces == InterfaceMode.Selected) return selected.Contains(i.Id);
            return (Settings.IncludeVirtual || !i.IsVirtual) && i.Status == OperationalStatus.Up;
        }

        /// <summary>Filter for historical data: which interface ids count toward "monitored" totals.</summary>
        public bool IsCountedId(string id)
        {
            if (Settings.Interfaces == InterfaceMode.Selected) return selected.Contains(id);
            return Settings.IncludeVirtual || !Usage.IsVirtual(id);
        }

        // ------------------------------------------------------------------ sampling

        void OnTick(object sender, EventArgs e)
        {
            Sample smp;
            try { smp = Monitor.Next(); }
            catch (Exception ex) { Program.Log(ex); return; }
            if (smp.Seconds <= 0) return;

            var now = smp.Time;
            if (now.Date != totalsDate) RefreshTotals();

            foreach (var d in smp.Deltas) Usage.Add(now, d.Id, d.Rx, d.Tx);
            TodayRx += smp.RxBytes; TodayTx += smp.TxBytes;
            PeriodRx += smp.RxBytes; PeriodTx += smp.TxBytes;
            SessionRx += smp.RxBytes; SessionTx += smp.TxBytes;
            PeakRx = Math.Max(PeakRx, smp.RxRate);
            PeakTx = Math.Max(PeakTx, smp.TxRate);

            smooth.Enqueue(new[] { smp.RxRate, smp.TxRate });
            while (smooth.Count > Settings.Smoothing) smooth.Dequeue();
            CurRx = smooth.Average(v => v[0]);
            CurTx = smooth.Average(v => v[1]);

            History.Add(now, smp.RxRate, smp.TxRate);

            try
            {
                overlay.SetSpeed(CurRx, CurTx);
                overlay.Tick();
                UpdateTray();
                Sampled?.Invoke(smp);
            }
            catch (Exception ex) { Program.Log(ex); }

            if (++tickCount % 15 == 0) CheckCaps();
            if ((now - lastSave).TotalMinutes >= 5)
            {
                lastSave = now;
                Usage.Save();
            }
        }

        public static DateTime BillingStart(DateTime day, int billingDay)
        {
            var start = new DateTime(day.Year, day.Month, Math.Min(billingDay, 28));
            return day.Date >= start ? start : start.AddMonths(-1);
        }

        public void RefreshTotals()
        {
            var today = DateTime.Today;
            totalsDate = today;
            Usage.Total(today, today.AddDays(1), IsCountedId, out TodayRx, out TodayTx);
            PeriodStart = BillingStart(today, Settings.BillingDay);
            Usage.Total(PeriodStart, PeriodStart.AddMonths(1), IsCountedId, out PeriodRx, out PeriodTx);
        }

        public void ResetSession()
        {
            SessionStart = DateTime.Now;
            SessionRx = SessionTx = 0;
            PeakRx = PeakTx = 0;
            History.Clear();
        }

        // ------------------------------------------------------------------ alerts

        void CheckCaps()
        {
            var state = ParseAlertState();
            bool changed = false;

            void Check(string key, long used, long capMB, string what)
            {
                if (capMB <= 0) return;
                double pct = used * 100.0 / (capMB * 1048576.0);
                int level = pct >= 100 ? 100 : pct >= Settings.AlertPercent ? Settings.AlertPercent : 0;
                if (level == 0) return;
                state.TryGetValue(key, out int done);
                if (done >= level) return;
                state[key] = level;
                changed = true;
                ShowBalloon(level >= 100 ? $"{what} data limit reached" : $"{what} data usage at {pct:0}%",
                    $"Used {SpeedFormatter.FormatBytes(used)} of {SpeedFormatter.FormatBytes(capMB * 1048576.0)}.",
                    level >= 100 ? ToolTipIcon.Warning : ToolTipIcon.Info);
            }

            Check("M" + PeriodStart.ToString("yyyyMMdd", CultureInfo.InvariantCulture), PeriodRx + PeriodTx, Settings.MonthlyCapMB, "Monthly");
            Check("D" + DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture), TodayRx + TodayTx, Settings.DailyCapMB, "Daily");

            if (changed)
            {
                // keep only the current keys
                var keep = state.Where(kv => kv.Key == "M" + PeriodStart.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                                          || kv.Key == "D" + DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                Settings.AlertState = string.Join(";", keep.Select(kv => kv.Key + ":" + kv.Value));
                Settings.Save();
            }
        }

        Dictionary<string, int> ParseAlertState()
        {
            var d = new Dictionary<string, int>();
            foreach (var part in Settings.AlertState.Split(';'))
            {
                var p = part.Split(':');
                if (p.Length == 2 && int.TryParse(p[1], out int v)) d[p[0]] = v;
            }
            return d;
        }

        public double? CapPercent(out long capBytes)
        {
            capBytes = Settings.MonthlyCapMB * 1048576L;
            if (capBytes <= 0) return null;
            return (PeriodRx + PeriodTx) * 100.0 / capBytes;
        }

        void ShowBalloon(string title, string text, ToolTipIcon icon)
        {
            bool wasVisible = tray.Visible;
            tray.Visible = true;
            tray.ShowBalloonTip(5000, title, text, icon);
            if (!wasVisible)
            {
                var t = new Timer { Interval = 8000 };
                t.Tick += (s, e) => { t.Dispose(); UpdateTrayVisibility(); };
                t.Start();
            }
        }

        // ------------------------------------------------------------------ tray

        void UpdateTrayVisibility()
        {
            // Never leave the user without a way to reach the app.
            bool overlayReachable = Settings.ShowOverlay && !Settings.ClickThrough;
            tray.Visible = Settings.TrayIcon != TrayIconMode.Hidden || !overlayReachable;
            if (Settings.TrayIcon != TrayIconMode.Speed && tray.Icon != appIcon)
            {
                tray.Icon = appIcon;
                liveIcon?.Dispose();
                liveIcon = null;
                liveIconKey = null;
            }
        }

        void UpdateTray()
        {
            if (!tray.Visible) return;
            var text = $"↓ {Fmt.Format(CurRx)}   ↑ {Fmt.Format(CurTx)}\nToday: {SpeedFormatter.FormatBytes(TodayRx + TodayTx)}";
            if (text.Length > 63) text = text.Substring(0, 63);
            if (tray.Text != text) tray.Text = text;

            if (Settings.TrayIcon == TrayIconMode.Speed)
            {
                string top = Health.Offline ? "off" : Fmt.Tiny(CurRx), bottom = Health.Offline ? "line" : Fmt.Tiny(CurTx);
                var key = top + "|" + bottom + "|" + Theme.TaskbarLight;
                if (key == liveIconKey) return;
                liveIconKey = key;
                Color dn = Settings.DownloadColor, up = Settings.UploadColor;
                if (Settings.AutoTextColor) dn = up = Theme.TaskbarForeground;
                var icon = TrayIcons.RenderSpeed(top, bottom, dn, up);
                tray.Icon = icon;
                liveIcon?.Dispose();
                liveIcon = icon;
            }
        }

        string BuildTooltip()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Download:  {Fmt.Format(CurRx)}   (peak {Fmt.Format(PeakRx)})");
            sb.AppendLine($"Upload:      {Fmt.Format(CurTx)}   (peak {Fmt.Format(PeakTx)})");
            sb.AppendLine($"Today:        ↓ {SpeedFormatter.FormatBytes(TodayRx)}   ↑ {SpeedFormatter.FormatBytes(TodayTx)}");
            sb.Append($"This period: ↓ {SpeedFormatter.FormatBytes(PeriodRx)}   ↑ {SpeedFormatter.FormatBytes(PeriodTx)}");
            var pct = CapPercent(out long cap);
            if (pct.HasValue) sb.Append($"  ({pct.Value:0}% of {SpeedFormatter.FormatBytes(cap)})");
            sb.AppendLine();
            if (Settings.PingEnabled)
                sb.AppendLine(Health.Offline ? "Internet:      OFFLINE" : $"Ping:            {(Health.LatencyMs.HasValue ? Health.LatencyMs.Value.ToString("0") + " ms" : "…")}   (jitter {Health.JitterMs:0} ms)");
            var names = Monitor.Interfaces.Where(i => i.Monitored).Select(i => i.Name).ToList();
            sb.Append("Interfaces:  " + (names.Count == 0 ? "none connected" : string.Join(", ", names)));
            return sb.ToString();
        }

        // ------------------------------------------------------------------ menu

        ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip { ShowCheckMargin = true, ShowImageMargin = false };

            var open = new ToolStripMenuItem("Open WinSpeed", null, (s, e) => ShowPage(AppPage.Overview)) { Font = new Font(m.Font, FontStyle.Bold) };
            m.Items.Add(open);
            var testItem = new ToolStripMenuItem("Run speed test", null, (s, e) => { ShowPage(AppPage.SpeedTest); StartSpeedTest(); });
            m.Items.Add(testItem);
            m.Items.Add("Data usage…", null, (s, e) => ShowPage(AppPage.Usage));
            m.Items.Add("Connection && adapters…", null, (s, e) => ShowPage(AppPage.Connection));
            m.Items.Add("Meter && display…", null, (s, e) => ShowPage(AppPage.Meter));
            m.Items.Add("Settings…", null, (s, e) => ShowPage(AppPage.Settings));
            m.Items.Add(new ToolStripSeparator());

            var showMeter = new ToolStripMenuItem("Show speed meter", null, (s, e) => ToggleMeter());
            m.Items.Add(showMeter);

            var display = new ToolStripMenuItem("Display");
            void ModeItem(string text, DisplayMode mode) =>
                display.DropDownItems.Add(new ToolStripMenuItem(text, null, (s, e) => { Settings.Mode = mode; SettingsChanged(); }) { Tag = mode });
            ModeItem("Download && upload (separate)", DisplayMode.Separate);
            ModeItem("Download + upload combined", DisplayMode.Combined);
            ModeItem("Download, upload && total", DisplayMode.SeparateAndTotal);
            ModeItem("Download only", DisplayMode.DownloadOnly);
            ModeItem("Upload only", DisplayMode.UploadOnly);
            display.DropDownItems.Add(new ToolStripSeparator());
            display.DropDownItems.Add(new ToolStripMenuItem("Stacked (two lines)", null, (s, e) => { Settings.Layout = LayoutMode.TwoLines; SettingsChanged(); }) { Tag = LayoutMode.TwoLines });
            display.DropDownItems.Add(new ToolStripMenuItem("Side by side (one line)", null, (s, e) => { Settings.Layout = LayoutMode.SingleLine; SettingsChanged(); }) { Tag = LayoutMode.SingleLine });
            m.Items.Add(display);

            var units = new ToolStripMenuItem("Units");
            void UnitItem(string text, UnitMode u) =>
                units.DropDownItems.Add(new ToolStripMenuItem(text, null, (s, e) => { Settings.Unit = u; SettingsChanged(); }) { Tag = u });
            UnitItem("Auto (bytes: KB/s, MB/s…)", UnitMode.AutoBytes);
            UnitItem("Auto (bits: Kbps, Mbps…)", UnitMode.AutoBits);
            units.DropDownItems.Add(new ToolStripSeparator());
            UnitItem("B/s", UnitMode.Bytes);
            UnitItem("KB/s", UnitMode.KiloBytes);
            UnitItem("MB/s", UnitMode.MegaBytes);
            UnitItem("GB/s", UnitMode.GigaBytes);
            units.DropDownItems.Add(new ToolStripSeparator());
            UnitItem("bps", UnitMode.Bits);
            UnitItem("Kbps", UnitMode.KiloBits);
            UnitItem("Mbps", UnitMode.MegaBits);
            UnitItem("Gbps", UnitMode.GigaBits);
            m.Items.Add(units);

            var pos = new ToolStripMenuItem("Position");
            void PosItem(string text, Placement p) =>
                pos.DropDownItems.Add(new ToolStripMenuItem(text, null, (s, e) => { Settings.Placement = p; SettingsChanged(); }) { Tag = p });
            PosItem("Taskbar — next to tray", Placement.TaskbarRight);
            PosItem("Taskbar — left side", Placement.TaskbarLeft);
            PosItem("Floating window (desktop)", Placement.Floating);
            pos.DropDownItems.Add(new ToolStripSeparator());
            var lockItem = new ToolStripMenuItem("Lock position", null, (s, e) => { Settings.LockPosition = !Settings.LockPosition; SettingsChanged(); });
            pos.DropDownItems.Add(lockItem);
            pos.DropDownItems.Add("Reset position", null, (s, e) => { Settings.OffsetX = Settings.OffsetY = 0; Settings.FloatX = Settings.FloatY = -1; SettingsChanged(); });
            m.Items.Add(pos);

            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Reset session statistics", null, (s, e) => ResetSession());
            var startup = new ToolStripMenuItem("Start with Windows", null, (s, e) => StartupManager.Enabled = !StartupManager.Enabled);
            m.Items.Add(startup);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Help && user guide", null, (s, e) => Http.Open(Http.WikiUrl));
            m.Items.Add("About WinSpeed", null, (s, e) => ShowPage(AppPage.About));
            m.Items.Add("Exit", null, (s, e) => Exit());

            m.Opening += (s, e) =>
            {
                showMeter.Checked = Settings.ShowOverlay;
                testItem.Text = SpeedTestRunning ? "Speed test running…" : "Run speed test";
                testItem.Enabled = !SpeedTestRunning;
                startup.Checked = StartupManager.Enabled;
                lockItem.Checked = Settings.LockPosition;
                foreach (ToolStripItem it in display.DropDownItems)
                    if (it is ToolStripMenuItem mi) mi.Checked = mi.Tag is DisplayMode dm ? dm == Settings.Mode : mi.Tag is LayoutMode lm && lm == Settings.Layout;
                foreach (ToolStripItem it in units.DropDownItems)
                    if (it is ToolStripMenuItem mi) mi.Checked = mi.Tag is UnitMode um && um == Settings.Unit;
                foreach (ToolStripItem it in pos.DropDownItems)
                    if (it is ToolStripMenuItem mi && mi.Tag is Placement pl) mi.Checked = pl == Settings.Placement;
            };
            return m;
        }

        void RunAction(ClickAction a)
        {
            switch (a)
            {
                case ClickAction.Dashboard: ShowPage(AppPage.Overview); break;
                case ClickAction.DataUsage: ShowPage(AppPage.Usage); break;
                case ClickAction.Settings: ShowPage(AppPage.Settings); break;
                case ClickAction.SpeedTest: ShowPage(AppPage.SpeedTest); StartSpeedTest(); break;
                case ClickAction.ToggleMeter: ToggleMeter(); break;
            }
        }

        void ToggleMeter()
        {
            Settings.ShowOverlay = !Settings.ShowOverlay;
            SettingsChanged();
        }

        /// <summary>Quick changes from menus: apply to UI and persist.</summary>
        void SettingsChanged()
        {
            overlay.ApplySettings();
            overlay.Tick();
            UpdateTrayVisibility();
            Settings.Save();
        }

        /// <summary>Apply a full settings object coming from the Settings window.</summary>
        public void ApplySettings(Settings s)
        {
            s.Sanitize();
            Settings.CopyFrom(s);
            selected = Settings.SelectedSet();
            timer.Interval = Settings.UpdateIntervalMs;
            smooth.Clear();
            if (!hotkey.Register(Settings.Hotkey) && !string.IsNullOrWhiteSpace(Settings.Hotkey))
                ShowBalloon("Hotkey unavailable", $"\"{Settings.Hotkey}\" is already used by another program.", ToolTipIcon.Warning);
            Monitor.Invalidate();
            Health.Apply();
            Usage.Prune(Settings.RetentionDays);
            RefreshTotals();
            liveIconKey = null;
            SettingsChanged();
        }

        // ------------------------------------------------------------------ windows

        /// <summary>Opens the window requested on the command line / by a second instance.</summary>
        public void ShowTarget(string target)
        {
            switch ((target ?? "").ToLowerInvariant())
            {
                case "speedtest": ShowPage(AppPage.SpeedTest); break;
                case "run-speedtest": ShowPage(AppPage.SpeedTest); StartSpeedTest(); break;
                case "usage": ShowPage(AppPage.Usage); break;
                case "connection": case "interfaces": ShowPage(AppPage.Connection); break;
                case "meter": ShowPage(AppPage.Meter); break;
                case "settings": ShowPage(AppPage.Settings); break;
                case "about": ShowPage(AppPage.About); break;
                default: ShowPage(AppPage.Overview); break;
            }
        }

        public void ShowPage(AppPage page)
        {
            if (exiting) return;
            if (window == null || window.IsDisposed) window = new MainWindow(this);
            BringUp(window);
            window.ShowPage(page);
        }

        public void ApplyTheme()
        {
            UiTheme.Resolve(Settings.Theme);
            if (window != null && !window.IsDisposed) window.ApplyTheme();
        }

        static void BringUp(Form f)
        {
            if (!f.Visible) f.Show();
            if (f.WindowState == FormWindowState.Minimized) f.WindowState = FormWindowState.Normal;
            f.Activate();
            Native.SetForegroundWindow(f.Handle);
        }

        // ------------------------------------------------------------------ system events

        void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            Theme.Invalidate();
            liveIconKey = null;
            if (Settings.Theme == AppTheme.System && UiTheme.Dark != UiTheme.SystemAppsDark()) ApplyTheme();
        }

        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) { Monitor.Invalidate(); smooth.Clear(); }
            if (e.Mode == PowerModes.Suspend) SaveAll();
        }

        void OnSessionEnding(object sender, SessionEndingEventArgs e) => SaveAll();

        void OnDisplaySettingsChanged(object sender, EventArgs e) => overlay.ApplySettings();

        void SaveAll()
        {
            Usage.Save();
            Settings.Save();
        }

        public void ClearUsageData()
        {
            Usage.Clear();
            RefreshTotals();
        }

        public void Exit()
        {
            if (exiting) return;
            exiting = true;
            timer.Stop();
            SaveAll();
            tray.Visible = false;
            CancelSpeedTest();
            try { window?.Close(); } catch { }
            overlay.Hide();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                SystemEvents.SessionEnding -= OnSessionEnding;
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                activateWait?.Unregister(null);
                exitWait?.Unregister(null);
                timer?.Dispose();
                hotkey?.Dispose();
                tray?.Dispose();
                overlay?.Dispose();
                menu?.Dispose();
                liveIcon?.Dispose();
                Monitor?.Dispose();
                Health?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Fixed-size ring buffer of recent speed samples for the live graph.</summary>
    public sealed class SpeedHistory
    {
        public struct Point { public DateTime T; public float Rx, Tx; }
        readonly Point[] buf;
        int start, count;

        public SpeedHistory(int capacity) { buf = new Point[capacity]; }
        public int Count => count;
        public Point this[int i] => buf[(start + i) % buf.Length];

        public void Add(DateTime t, double rx, double tx)
        {
            var p = new Point { T = t, Rx = (float)rx, Tx = (float)tx };
            if (count < buf.Length) buf[(start + count++) % buf.Length] = p;
            else { buf[start] = p; start = (start + 1) % buf.Length; }
        }

        public void Clear() { start = count = 0; }
    }
}
