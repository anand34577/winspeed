using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace WinSpeed
{
    static class Palette
    {
        public static Color Download => UiTheme.Dark ? Color.FromArgb(0x4F, 0xA8, 0xFF) : Color.FromArgb(0x1E, 0x7F, 0xE5);
        public static Color Upload => UiTheme.Dark ? Color.FromArgb(0xFF, 0x9E, 0x45) : Color.FromArgb(0xF0, 0x7A, 0x00);
        public static Color Grid => UiTheme.Border;
        public static Color Axis => UiTheme.Muted;
        public static Color Surface => UiTheme.Surface;
        public static Color Page => UiTheme.Page;
        public static Color Muted => UiTheme.Muted;
        public static Color Text => UiTheme.Text;
    }

    static class NiceScale
    {
        /// <summary>Rounds up to 1, 2, 2.5, 5 × 10^n.</summary>
        public static double Ceil(double v)
        {
            if (v <= 0) return 1;
            double exp = Math.Pow(10, Math.Floor(Math.Log10(v)));
            double f = v / exp;
            double nice = f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10;
            return nice * exp;
        }

        public static string Num(double v) =>
            v.ToString(v >= 100 || Math.Abs(v - Math.Round(v)) < 1e-9 ? "0" : v >= 10 ? "0.#" : "0.##", CultureInfo.CurrentCulture);
    }

    /// <summary>Live download/upload graph over a sliding time window.</summary>
    sealed class SpeedGraph : Control
    {
        public SpeedHistory History;
        public SpeedFormatter Formatter;
        public int WindowSeconds = 60;
        public bool ShowDownload = true, ShowUpload = true;
        int hoverX = -1;

        public SpeedGraph()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Palette.Surface;
        }

        protected override void OnMouseMove(MouseEventArgs e) { hoverX = e.X; Invalidate(); base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hoverX = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Palette.Surface);
            if (History == null || Formatter == null) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int left = Dpi.S(70), right = Dpi.S(12), top = Dpi.S(26), bottom = Dpi.S(24);
            var plot = new Rectangle(left, top, Math.Max(10, Width - left - right), Math.Max(10, Height - top - bottom));

            DateTime now = DateTime.Now, t0 = now.AddSeconds(-WindowSeconds);
            var pts = new List<SpeedHistory.Point>();
            double max = 0;
            for (int i = 0; i < History.Count; i++)
            {
                var p = History[i];
                if (p.T < t0) continue;
                pts.Add(p);
                if (ShowDownload) max = Math.Max(max, p.Rx);
                if (ShowUpload) max = Math.Max(max, p.Tx);
            }

            double div = Formatter.ScaleFor(Math.Max(max, 1), out string unit);
            double top_ = NiceScale.Ceil(Math.Max(max / div, 1e-9) * 1.08);
            if (max <= 0) top_ = 1;
            double yMax = top_ * div;

            using (var gridPen = new Pen(Palette.Grid))
            using (var axisBrush = new SolidBrush(Palette.Axis))
            using (var sfR = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            using (var sfC = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near })
            {
                for (int i = 0; i <= 4; i++)
                {
                    float y = plot.Bottom - plot.Height * i / 4f;
                    g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                    g.DrawString(NiceScale.Num(top_ * i / 4) + " " + unit, Font, axisBrush, new RectangleF(0, y - 10, left - Dpi.S(6), 20), sfR);
                }
                for (int i = 0; i <= 4; i++)
                {
                    float x = plot.Left + plot.Width * i / 4f;
                    int secsAgo = WindowSeconds - WindowSeconds * i / 4;
                    string lbl = secsAgo == 0 ? "now" : secsAgo >= 120 ? $"-{secsAgo / 60}m" : $"-{secsAgo}s";
                    g.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
                    g.DrawString(lbl, Font, axisBrush, new RectangleF(x - 40, plot.Bottom + Dpi.S(4), 80, 20), sfC);
                }
            }

            float X(DateTime t) => plot.Left + (float)((t - t0).TotalSeconds / WindowSeconds * plot.Width);
            float Y(double v) => plot.Bottom - (float)(v / yMax * plot.Height);

            if (pts.Count >= 2)
            {
                g.SetClip(plot);
                if (ShowDownload) DrawSeries(g, pts, true, X, Y, plot, Palette.Download, true);
                if (ShowUpload) DrawSeries(g, pts, false, X, Y, plot, Palette.Upload, false);
                g.ResetClip();
            }

            // legend
            float lx = plot.Left;
            using (var tb = new SolidBrush(Palette.Text))
            {
                foreach (var (name, color, on) in new[] { ("Download", Palette.Download, ShowDownload), ("Upload", Palette.Upload, ShowUpload) })
                {
                    if (!on) continue;
                    using (var b = new SolidBrush(color)) g.FillRectangle(b, lx, Dpi.S(8), Dpi.S(10), Dpi.S(10));
                    g.DrawString(name, Font, tb, lx + Dpi.S(14), Dpi.S(5));
                    lx += Dpi.S(14) + g.MeasureString(name, Font).Width + Dpi.S(14);
                }
            }

            // hover readout
            if (hoverX >= plot.Left && hoverX <= plot.Right && pts.Count > 0)
            {
                var t = t0.AddSeconds((hoverX - plot.Left) / (double)plot.Width * WindowSeconds);
                var best = pts[0];
                foreach (var p in pts) if (Math.Abs((p.T - t).TotalSeconds) < Math.Abs((best.T - t).TotalSeconds)) best = p;
                float bx = X(best.T);
                using (var pen = new Pen(Color.FromArgb(120, Palette.Axis)) { DashStyle = DashStyle.Dash }) g.DrawLine(pen, bx, plot.Top, bx, plot.Bottom);
                string txt = $"{best.T:HH:mm:ss}   ↓ {Formatter.Format(best.Rx)}   ↑ {Formatter.Format(best.Tx)}";
                var sz = g.MeasureString(txt, Font);
                float tx = Math.Min(Math.Max(plot.Left, bx - sz.Width / 2), plot.Right - sz.Width - 8);
                var box = new RectangleF(tx, plot.Top + 4, sz.Width + 8, sz.Height + 4);
                using (var bg = new SolidBrush(Color.FromArgb(240, UiTheme.Surface2))) g.FillRectangle(bg, box);
                using (var pen = new Pen(Palette.Grid)) g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
                using (var tb = new SolidBrush(Palette.Text)) g.DrawString(txt, Font, tb, box.X + 4, box.Y + 2);
            }
        }

        static void DrawSeries(Graphics g, List<SpeedHistory.Point> pts, bool rx, Func<DateTime, float> X, Func<double, float> Y,
            Rectangle plot, Color color, bool fill)
        {
            var line = new PointF[pts.Count];
            for (int i = 0; i < pts.Count; i++) line[i] = new PointF(X(pts[i].T), Y(rx ? pts[i].Rx : pts[i].Tx));
            if (fill)
            {
                var poly = new PointF[line.Length + 2];
                line.CopyTo(poly, 0);
                poly[line.Length] = new PointF(line[line.Length - 1].X, plot.Bottom);
                poly[line.Length + 1] = new PointF(line[0].X, plot.Bottom);
                using (var b = new SolidBrush(Color.FromArgb(46, color))) g.FillPolygon(b, poly);
            }
            using (var pen = new Pen(color, Dpi.Scale * 1.6f) { LineJoin = LineJoin.Round }) g.DrawLines(pen, line);
        }
    }

    /// <summary>Stacked bar chart of data usage per period (download bottom, upload top).</summary>
    sealed class UsageChart : Control
    {
        List<UsageRow> rows = new List<UsageRow>();
        Grouping grouping;
        int hover = -1;
        public event Action<int> BarClicked;

        public UsageChart()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Palette.Surface;
        }

        public void SetData(List<UsageRow> data, Grouping g) { rows = data; grouping = g; hover = -1; Invalidate(); }

        Rectangle Plot => new Rectangle(Dpi.S(70), Dpi.S(26), Math.Max(10, Width - Dpi.S(82)), Math.Max(10, Height - Dpi.S(52)));

        int IndexAt(int x)
        {
            var p = Plot;
            if (rows.Count == 0 || x < p.Left || x > p.Right) return -1;
            int i = (int)((x - p.Left) / (p.Width / (double)rows.Count));
            return i >= 0 && i < rows.Count ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = IndexAt(e.X);
            if (h != hover) { hover = h; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = IndexAt(e.X);
            if (i >= 0) BarClicked?.Invoke(i);
            base.OnMouseClick(e);
        }

        string Label(DateTime p)
        {
            switch (grouping)
            {
                case Grouping.Hour: return p.ToString("HH:00", CultureInfo.CurrentCulture);
                case Grouping.Day: return p.ToString("dd MMM", CultureInfo.CurrentCulture);
                case Grouping.Month: return p.ToString("MMM yy", CultureInfo.CurrentCulture);
                default: return p.Year.ToString(CultureInfo.CurrentCulture);
            }
        }

        string LongLabel(DateTime p)
        {
            switch (grouping)
            {
                case Grouping.Hour: return p.ToString("ddd dd MMM yyyy, HH:00", CultureInfo.CurrentCulture) + "–" + p.AddHours(1).ToString("HH:00");
                case Grouping.Day: return p.ToString("dddd, dd MMM yyyy", CultureInfo.CurrentCulture);
                case Grouping.Month: return p.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
                default: return p.Year.ToString(CultureInfo.CurrentCulture);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Palette.Surface);
            var plot = Plot;
            using (var axisBrush = new SolidBrush(Palette.Axis))
            {
                if (rows.Count == 0)
                {
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString("No data recorded for this range yet.", Font, axisBrush, ClientRectangle, sf);
                    return;
                }

                long max = 0;
                foreach (var r in rows) max = Math.Max(max, r.Total);
                double div = SpeedFormatter.ScaleForBytes(Math.Max(max, 1), out string unit);
                double topV = NiceScale.Ceil(Math.Max(max / div, 1e-9) * 1.05);
                if (max == 0) topV = 1;
                double yMax = topV * div;

                using (var gridPen = new Pen(Palette.Grid))
                using (var sfR = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                    for (int i = 0; i <= 4; i++)
                    {
                        float y = plot.Bottom - plot.Height * i / 4f;
                        g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                        g.DrawString(NiceScale.Num(topV * i / 4) + " " + unit, Font, axisBrush, new RectangleF(0, y - 10, plot.Left - Dpi.S(6), 20), sfR);
                    }

                float slot = plot.Width / (float)rows.Count;
                float bw = Math.Max(1, slot * 0.72f);
                int labelEvery = Math.Max(1, (int)Math.Ceiling(Dpi.S(56) / slot));
                using (var db = new SolidBrush(Palette.Download))
                using (var ub = new SolidBrush(Palette.Upload))
                using (var hb = new SolidBrush(UiTheme.Dark ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(18, 0, 0, 0)))
                using (var sfC = new StringFormat { Alignment = StringAlignment.Center })
                {
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        float x = plot.Left + i * slot + (slot - bw) / 2;
                        if (i == hover) g.FillRectangle(hb, plot.Left + i * slot, plot.Top, slot, plot.Height);
                        float hRx = (float)(r.Rx / yMax * plot.Height);
                        float hTx = (float)(r.Tx / yMax * plot.Height);
                        if (hRx > 0) g.FillRectangle(db, x, plot.Bottom - hRx, bw, hRx);
                        if (hTx > 0) g.FillRectangle(ub, x, plot.Bottom - hRx - hTx, bw, hTx);
                        if (i % labelEvery == 0)
                            g.DrawString(Label(r.Period), Font, axisBrush, new RectangleF(plot.Left + i * slot + slot / 2 - 50, plot.Bottom + Dpi.S(4), 100, 20), sfC);
                    }
                }

                // legend
                using (var tb = new SolidBrush(Palette.Text))
                {
                    float lx = plot.Left;
                    foreach (var (name, color) in new[] { ("Download", Palette.Download), ("Upload", Palette.Upload) })
                    {
                        using (var b = new SolidBrush(color)) g.FillRectangle(b, lx, Dpi.S(8), Dpi.S(10), Dpi.S(10));
                        g.DrawString(name, Font, tb, lx + Dpi.S(14), Dpi.S(5));
                        lx += Dpi.S(14) + g.MeasureString(name, Font).Width + Dpi.S(14);
                    }

                    if (hover >= 0)
                    {
                        var r = rows[hover];
                        string txt = $"{LongLabel(r.Period)}   ↓ {SpeedFormatter.FormatBytes(r.Rx)}   ↑ {SpeedFormatter.FormatBytes(r.Tx)}   Σ {SpeedFormatter.FormatBytes(r.Total)}";
                        var sz = g.MeasureString(txt, Font);
                        g.DrawString(txt, Font, tb, Math.Max(lx, plot.Right - sz.Width), Dpi.S(5));
                    }
                }
            }
        }
    }
}
