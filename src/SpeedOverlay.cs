using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace WinSpeed
{
    /// <summary>
    /// The speed meter itself: a per-pixel-alpha layered window that sits on top of the taskbar
    /// (next to the tray) or floats anywhere on the desktop.
    /// </summary>
    sealed class SpeedOverlay : Form
    {
        struct Row { public string Label, Value, Unit; public Color LabelColor, ValueColor; }

        readonly Settings s;
        readonly SpeedFormatter fmt;
        readonly ToolTip tip = new ToolTip { InitialDelay = 500, ReshowDelay = 200, AutoPopDelay = 30000, ShowAlways = true };
        readonly StringFormat sf;
        readonly Bitmap measureBmp = new Bitmap(1, 1);
        Font font;
        double rx, tx;
        bool hovering, mouseDown, dragging;
        Point dragStartCursor, dragStartLoc;
        string lastTip;
        Size lastSize;

        public Func<string> TooltipProvider;
        public SpeedHistory History;
        public NetworkHealth Health;
        public ContextMenuStrip MeterMenu;
        public event EventHandler MeterDoubleClick;
        public event EventHandler PositionCommitted;

        public SpeedOverlay(Settings settings, SpeedFormatter formatter)
        {
            s = settings;
            fmt = formatter;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Text = "WinSpeed Meter";
            Size = new Size(1, 1);
            sf = (StringFormat)StringFormat.GenericTypographic.Clone();
            sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
            ApplySettings();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
                if (s != null && s.ClickThrough) cp.ExStyle |= Native.WS_EX_TRANSPARENT;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        // When the taskbar or one of its flyouts (tray overflow, Start, calendar) takes the foreground, Explorer
        // raises the taskbar to the top of the topmost band and covers the meter. Waiting for the next sample
        // makes the meter blink, so we watch foreground changes and push the meter back on top straight away,
        // and keep doing so briefly while a shell surface stays in front (it can re-raise itself as it animates).
        Native.WinEventProc fgProc;
        IntPtr fgHook;
        readonly Timer zGuard = new Timer { Interval = 40 };
        // slower watcher: follows the taskbar layout and steps out of the way of windows stacked over it
        readonly Timer watcher = new Timer { Interval = 250 };
        int zGuardTicks;
        bool covered;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (fgHook != IntPtr.Zero) return;
            fgProc = OnForegroundChanged;
            fgHook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
                fgProc, 0, 0, Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);
            zGuard.Tick += (_, __) =>
            {
                KeepOnTop();
                if (++zGuardTicks > 25 && !Native.IsShellSurface(Native.GetForegroundWindow())) zGuard.Stop();
            };
            watcher.Tick += (_, __) => Watch();
            watcher.Start();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (fgHook != IntPtr.Zero) { Native.UnhookWinEvent(fgHook); fgHook = IntPtr.Zero; }
            zGuard.Stop();
            watcher.Stop();
            base.OnHandleDestroyed(e);
        }

        void OnForegroundChanged(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (!Visible || s.Placement == Placement.Floating) return;
            KeepOnTop();
            zGuardTicks = 0;
            if (!zGuard.Enabled) zGuard.Start();
        }

        void Watch()
        {
            if (!Visible || dragging || !IsHandleCreated || lastSize.IsEmpty) return;
            var tb = TaskbarInfo.Get();
            Point loc = ComputeLocation(tb, lastSize.Width, lastSize.Height);
            if (loc != Location)
                Native.SetWindowPos(Handle, IntPtr.Zero, loc.X, loc.Y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_NOZORDER);
            UpdateCovered(tb);
        }

        /// <summary>The meter is topmost, so it would otherwise draw over any app that gets stacked above the taskbar.</summary>
        void UpdateCovered(TaskbarInfo tb)
        {
            bool now = s.Placement != Placement.Floating && tb.Found && !dragging
                && Native.IsCoveredByWindow(tb.Handle, tb.Bounds, new Rectangle(Location, lastSize), Handle);
            if (now == covered) return;
            covered = now;
            Native.ShowWindow(Handle, covered ? Native.SW_HIDE : Native.SW_SHOWNOACTIVATE);
            if (!covered) KeepOnTop();
        }

        void KeepOnTop()
        {
            if (!Visible || covered || dragging || !IsHandleCreated) return;
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_MOUSEACTIVATE) { m.Result = (IntPtr)Native.MA_NOACTIVATE; return; }
            base.WndProc(ref m);
        }

        public void ApplySettings()
        {
            font?.Dispose();
            float px = s.FontSize * Dpi.Value / 72f;
            try { font = new Font(s.FontFamily, px, s.FontBold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); }
            catch { font = new Font("Segoe UI", px, FontStyle.Regular, GraphicsUnit.Pixel); }

            if (IsHandleCreated)
            {
                int ex = Native.GetWindowLong(Handle, Native.GWL_EXSTYLE);
                ex = s.ClickThrough ? ex | Native.WS_EX_TRANSPARENT : ex & ~Native.WS_EX_TRANSPARENT;
                Native.SetWindowLong(Handle, Native.GWL_EXSTYLE, ex);
            }
            lastSize = Size.Empty;
            Render();
        }

        public void SetSpeed(double down, double up)
        {
            rx = down; tx = up;
        }

        /// <summary>Called on every sample: decides visibility, keeps z-order and redraws.</summary>
        public void Tick()
        {
            var tb = TaskbarInfo.Get();
            bool show = s.ShowOverlay;
            if (show && s.Placement != Placement.Floating && tb.Found && !tb.Visible) show = false; // auto-hidden taskbar
            if (show && s.HideOnFullscreen && Native.IsFullscreenAppActive(tb.Found ? tb.Screen : Screen.PrimaryScreen)) show = false;
            if (show && s.HideBelowKBps > 0 && Math.Max(rx, tx) < s.HideBelowKBps * 1024.0 && !hovering) show = false;

            if (show && !Visible) Show();
            else if (!show && Visible) { covered = false; Hide(); return; }
            if (!show) return;

            Render(tb);
            UpdateCovered(tb);
            KeepOnTop();

            if (hovering && TooltipProvider != null)
            {
                var t = TooltipProvider();
                if (t != lastTip) { lastTip = t; tip.SetToolTip(this, t); }
            }
        }

        List<Row> BuildRows()
        {
            var rows = new List<Row>(3);
            Color fg = Theme.TaskbarForeground;
            if (s.Placement == Placement.Floating && s.BackgroundOpacity >= 40)
                fg = Theme.IsDark(s.BackgroundColor) ? Color.FromArgb(245, 245, 245) : Color.FromArgb(24, 24, 24);

            void Add(string label, double v, Color c)
            {
                fmt.Split(v, out var val, out var unit);
                var vc = s.AutoTextColor ? fg : c;
                if (s.HighlightEnabled && v >= s.HighlightKBps * 1024.0) vc = s.HighlightColor;
                rows.Add(new Row
                {
                    Label = s.ShowLabels ? label : "",
                    Value = val,
                    Unit = s.ShowUnit ? unit : "",
                    LabelColor = s.AutoTextColor && s.Mode == DisplayMode.Combined && c == s.TotalColor ? fg : c,
                    ValueColor = vc
                });
            }

            void Ping()
            {
                if (!s.ShowPingOnMeter || Health == null || !s.PingEnabled) return;
                string label = s.ShowLabels ? "⏱" : "";
                if (Health.Offline)
                    rows.Add(new Row { Label = label, Value = "offline", Unit = "", LabelColor = Color.FromArgb(0xFF, 0x52, 0x52), ValueColor = Color.FromArgb(0xFF, 0x52, 0x52) });
                else
                    rows.Add(new Row
                    {
                        Label = label, Value = Health.LatencyMs.HasValue ? Health.LatencyMs.Value.ToString("0") : "…", Unit = s.ShowUnit ? "ms" : "",
                        LabelColor = Color.FromArgb(0x66, 0xBB, 0x6A), ValueColor = s.AutoTextColor ? fg : Color.FromArgb(0x66, 0xBB, 0x6A)
                    });
            }

            void Down() => Add(s.DownloadLabel, rx, s.DownloadColor);
            void Up() => Add(s.UploadLabel, tx, s.UploadColor);

            switch (s.Mode)
            {
                case DisplayMode.Combined: Add(s.TotalLabel, rx + tx, s.TotalColor); break;
                case DisplayMode.DownloadOnly: Down(); break;
                case DisplayMode.UploadOnly: Up(); break;
                default:
                    if (s.UploadFirst) { Up(); Down(); } else { Down(); Up(); }
                    if (s.Mode == DisplayMode.SeparateAndTotal) Add(s.TotalLabel, rx + tx, s.TotalColor);
                    break;
            }
            Ping();
            return rows;
        }

        public void Render() => Render(TaskbarInfo.Get());

        void Render(TaskbarInfo tb)
        {
            if (!IsHandleCreated || !Visible || font == null) return;
            var rows = BuildRows();
            if (rows.Count == 0) return;

            float labelW = 0, valueW = 0, unitW = 0, rowH;
            using (var mg = Graphics.FromImage(measureBmp))
            {
                mg.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                float M(string t) => string.IsNullOrEmpty(t) ? 0 : mg.MeasureString(t, font, 10000, sf).Width;
                foreach (var r in rows) { labelW = Math.Max(labelW, M(r.Label)); valueW = Math.Max(valueW, M(r.Value)); }
                foreach (var sample in fmt.WidestValues()) valueW = Math.Max(valueW, M(sample));
                if (s.ShowUnit) foreach (var u in fmt.UnitLabels) unitW = Math.Max(unitW, M(u));
                rowH = font.GetHeight(mg);
            }

            float gap = font.Size * 0.35f;
            float padX = Dpi.S(4), padY = Dpi.S(2);
            float cellW = labelW + (labelW > 0 ? gap : 0) + valueW + (unitW > 0 ? gap * 0.7f : 0) + unitW;
            float colGap = font.Size * 0.9f;
            bool stacked = s.Layout == LayoutMode.TwoLines;

            int w = (int)Math.Ceiling(padX * 2 + (stacked ? cellW : cellW * rows.Count + colGap * (rows.Count - 1)));
            int h = (int)Math.Ceiling(padY * 2 + (stacked ? rowH * rows.Count : rowH));

            // never grow taller than the taskbar when docked: shrink line spacing instead
            if (s.Placement != Placement.Floating && tb.Found && tb.Horizontal && h > tb.Bounds.Height)
            {
                float avail = tb.Bounds.Height - padY * 2;
                rowH = Math.Max(1, stacked ? avail / rows.Count : avail);
                h = tb.Bounds.Height;
            }

            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    // alpha 1 keeps the whole rectangle clickable while looking fully transparent
                    g.Clear(Color.FromArgb(1, 0, 0, 0));
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    if (s.BackgroundOpacity > 0)
                    {
                        int a = Math.Max(1, s.BackgroundOpacity * 255 / 100);
                        using (var b = new SolidBrush(Color.FromArgb(a, s.BackgroundColor)))
                        using (var path = RoundedRect(new RectangleF(0, 0, w - 1, h - 1), Dpi.S(5)))
                            g.FillPath(b, path);
                    }

                    if (s.MeterGraph && History != null) DrawSparkline(g, w, h);
                    if (s.ShowOfflineDot && Health != null && Health.Offline)
                    {
                        float d = Dpi.S(5);
                        using (var b = new SolidBrush(Color.FromArgb(0xFF, 0x45, 0x45))) g.FillEllipse(b, 1, 1, d, d);
                    }

                    float textBlockH = stacked ? rowH * rows.Count : rowH;
                    float y0 = (h - textBlockH) / 2f;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        float x = padX + (stacked ? 0 : i * (cellW + colGap));
                        float y = y0 + (stacked ? i * rowH : 0) + (rowH - font.GetHeight(g)) / 2f;
                        if (labelW > 0 && r.Label.Length > 0)
                            using (var b = new SolidBrush(r.LabelColor)) g.DrawString(r.Label, font, b, x, y, sf);
                        float valueRight = x + labelW + (labelW > 0 ? gap : 0) + valueW;
                        using (var b = new SolidBrush(r.ValueColor))
                        {
                            float vw = g.MeasureString(r.Value, font, 10000, sf).Width;
                            g.DrawString(r.Value, font, b, valueRight - vw, y, sf);
                            if (r.Unit.Length > 0) g.DrawString(r.Unit, font, b, valueRight + gap * 0.7f, y, sf);
                        }
                    }
                }

                Point loc = dragging ? Location : ComputeLocation(tb, w, h);
                Push(bmp, loc);
            }
        }

        void DrawSparkline(Graphics g, int w, int h)
        {
            int n = Math.Min(History.Count, 40);
            if (n < 2) return;
            double max = 1024;
            for (int i = History.Count - n; i < History.Count; i++) max = Math.Max(max, Math.Max(History[i].Rx, History[i].Tx));
            float step = (w - 2) / (float)(n - 1);
            var down = new PointF[n + 2];
            var up = new PointF[n];
            for (int k = 0; k < n; k++)
            {
                var p = History[History.Count - n + k];
                float x = 1 + k * step;
                down[k] = new PointF(x, h - 1 - (float)(p.Rx / max * (h - 3)));
                up[k] = new PointF(x, h - 1 - (float)(p.Tx / max * (h - 3)));
            }
            down[n] = new PointF(down[n - 1].X, h - 1);
            down[n + 1] = new PointF(down[0].X, h - 1);
            using (var b = new SolidBrush(Color.FromArgb(70, s.DownloadColor))) g.FillPolygon(b, down);
            using (var pen = new Pen(Color.FromArgb(150, s.UploadColor), Dpi.Scale)) g.DrawLines(pen, up);
        }

        static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        Point AutoLocation(TaskbarInfo tb, int w, int h)
        {
            if (s.Placement == Placement.Floating || !tb.Found)
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                return new Point(wa.Right - w - Dpi.S(24), wa.Bottom - h - Dpi.S(24));
            }
            var b = tb.Bounds;
            var others = Native.ForeignTaskbarWindows(tb.Handle, b, IsHandleCreated ? Handle : IntPtr.Zero);
            int gapPx = Dpi.S(4);
            if (tb.Horizontal)
            {
                int y = b.Top + (b.Height - h) / 2;
                bool left = s.Placement == Placement.TaskbarLeft;
                int x = left ? b.Left + Dpi.S(8) : tb.Tray.Left - w - gapPx;
                // step aside for other apps' widgets: move away (left of them, or right of them when docked left)
                for (int i = 0; i < others.Count + 1; i++)
                {
                    var me = new Rectangle(x, y, w, h);
                    var hit = others.FirstOrDefault(o => o.IntersectsWith(me));
                    if (hit.IsEmpty) break;
                    x = left ? hit.Right + gapPx : hit.Left - w - gapPx;
                }
                return new Point(x, y);
            }
            else
            {
                int x = b.Left + (b.Width - w) / 2;
                bool top = s.Placement == Placement.TaskbarLeft;
                int y = top ? b.Top + Dpi.S(8) : tb.Tray.Top - h - gapPx;
                for (int i = 0; i < others.Count + 1; i++)
                {
                    var me = new Rectangle(x, y, w, h);
                    var hit = others.FirstOrDefault(o => o.IntersectsWith(me));
                    if (hit.IsEmpty) break;
                    y = top ? hit.Bottom + gapPx : hit.Top - h - gapPx;
                }
                return new Point(x, y);
            }
        }

        Point ComputeLocation(TaskbarInfo tb, int w, int h)
        {
            if (s.Placement == Placement.Floating)
            {
                if (s.FloatX == -1 && s.FloatY == -1) return AutoLocation(tb, w, h);
                var p = new Point(s.FloatX, s.FloatY);
                var vs = SystemInformation.VirtualScreen;
                if (!vs.IntersectsWith(new Rectangle(p, new Size(w, h)))) return AutoLocation(tb, w, h);
                return p;
            }
            var a = AutoLocation(tb, w, h);
            return new Point(a.X + s.OffsetX, a.Y + s.OffsetY);
        }

        void Push(Bitmap bmp, Point loc)
        {
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                old = Native.SelectObject(memDc, hBmp);
                var size = new Native.SIZE(bmp.Width, bmp.Height);
                var src = new Native.POINT(0, 0);
                var dst = new Native.POINT(loc.X, loc.Y);
                var blend = new Native.BLENDFUNCTION
                {
                    BlendOp = Native.AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = Native.AC_SRC_ALPHA
                };
                Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Native.ULW_ALPHA);
                lastSize = bmp.Size;
            }
            finally
            {
                Native.ReleaseDC(IntPtr.Zero, screenDc);
                if (hBmp != IntPtr.Zero) { Native.SelectObject(memDc, old); Native.DeleteObject(hBmp); }
                Native.DeleteDC(memDc);
            }
        }

        // ---------------- mouse ----------------

        protected override void OnMouseEnter(EventArgs e)
        {
            hovering = true;
            if (TooltipProvider != null) { lastTip = TooltipProvider(); tip.SetToolTip(this, lastTip); }
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hovering = false; base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !s.LockPosition)
            {
                mouseDown = true;
                dragStartCursor = Cursor.Position;
                dragStartLoc = Location;
                Capture = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (mouseDown)
            {
                var c = Cursor.Position;
                int dx = c.X - dragStartCursor.X, dy = c.Y - dragStartCursor.Y;
                if (!dragging && (Math.Abs(dx) > SystemInformation.DragSize.Width || Math.Abs(dy) > SystemInformation.DragSize.Height))
                {
                    dragging = true;
                    tip.Hide(this);
                }
                if (dragging) Location = new Point(dragStartLoc.X + dx, dragStartLoc.Y + dy);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && mouseDown)
            {
                mouseDown = false;
                Capture = false;
                if (dragging)
                {
                    dragging = false;
                    CommitDrag();
                }
            }
            else if (e.Button == MouseButtons.Right && MeterMenu != null)
            {
                tip.Hide(this);
                Native.SetForegroundWindow(Handle);
                MeterMenu.Show(Cursor.Position);
            }
            base.OnMouseUp(e);
        }

        void CommitDrag()
        {
            if (s.Placement == Placement.Floating)
            {
                s.FloatX = Location.X;
                s.FloatY = Location.Y;
            }
            else
            {
                var tb = TaskbarInfo.Get();
                var auto = AutoLocation(tb, lastSize.Width, lastSize.Height);
                s.OffsetX = Location.X - auto.X;
                s.OffsetY = Location.Y - auto.Y;
            }
            PositionCommitted?.Invoke(this, EventArgs.Empty);
            Render();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) MeterDoubleClick?.Invoke(this, EventArgs.Empty);
            base.OnMouseDoubleClick(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                font?.Dispose();
                watcher.Dispose();
                zGuard.Dispose();
                tip.Dispose();
                sf.Dispose();
                measureBmp.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
