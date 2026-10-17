using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace WinSpeed
{
    /// <summary>Statistic tile: icon, caption, big value and a secondary line.</summary>
    sealed class StatTile : Card
    {
        string caption = "", value = "—", sub = "", glyph = Glyph.Info;
        Color accent = UiTheme.Accent;
        public Func<Color> AccentProvider;

        public StatTile(string caption, string glyph, Func<Color> accent)
        {
            this.caption = caption; this.glyph = glyph; AccentProvider = accent;
            Height = Dpi.S(104);
            Margin = new Padding(0, 0, Dpi.S(12), Dpi.S(12));
            Dock = DockStyle.Fill;
        }

        public void Set(string value, string sub)
        {
            if (value == this.value && sub == this.sub) return;
            this.value = value; this.sub = sub ?? "";
            Invalidate();
        }

        public Color ValueColor = Color.Empty;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            accent = AccentProvider?.Invoke() ?? UiTheme.Accent;
            int pad = Dpi.S(16);

            // icon bubble
            var bubble = new Rectangle(pad, Dpi.S(14), Dpi.S(28), Dpi.S(28));
            using (var b = new SolidBrush(Color.FromArgb(UiTheme.Dark ? 48 : 30, accent))) g.FillEllipse(b, bubble);
            using (var f = UiTheme.IconFont(10.5f))
            using (var b = new SolidBrush(accent))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(glyph, f, b, new RectangleF(bubble.X, bubble.Y + 1, bubble.Width, bubble.Height), sf);

            using (var b = new SolidBrush(UiTheme.Muted))
                g.DrawString(caption, Font, b, bubble.Right + Dpi.S(8), Dpi.S(19));

            using (var f = new Font(Font.FontFamily, 18f, FontStyle.Bold))
            using (var b = new SolidBrush(ValueColor.IsEmpty ? UiTheme.Text : ValueColor))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(value, f, b, new RectangleF(pad - Dpi.S(2), Dpi.S(46), Width - pad * 2, Dpi.S(34)), sf);

            using (var b = new SolidBrush(UiTheme.Muted))
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(sub, Font, b, new RectangleF(pad, Dpi.S(80), Width - pad * 2, Dpi.S(20)), sf);
        }
    }

    /// <summary>Semicircular speedometer used by the speed test.</summary>
    sealed class Gauge : Control, ThemedControl
    {
        double mbps;
        public string Phase = "Ready", Detail = "";
        public Color ArcColor = Palette.Download;

        public Gauge()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void OnThemeChanged() => Invalidate();
        public double Mbps { get => mbps; set { mbps = Math.Max(0, value); Invalidate(); } }

        static readonly double[] Ticks = { 0, 5, 10, 50, 100, 250, 500, 1000 };

        // piecewise-linear scale over the tick marks so low speeds are still readable
        static double Map(double v)
        {
            if (v <= 0) return 0;
            for (int i = 1; i < Ticks.Length; i++)
                if (v <= Ticks[i]) return (i - 1 + (v - Ticks[i - 1]) / (Ticks[i] - Ticks[i - 1])) / (Ticks.Length - 1);
            return 1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? UiTheme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            float thick = Dpi.S(14);
            float below = Dpi.S(84);                       // room under the centre for value / phase text
            float size = Math.Min(Width - Dpi.S(40), (Height - below - thick) * 2f);
            size = Math.Max(Dpi.S(120), size);
            var rect = new RectangleF((Width - size) / 2, thick / 2 + Dpi.S(4), size, size);
            const float start = 150, sweep = 240;

            using (var pen = new Pen(UiTheme.Surface2, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(pen, rect, start, sweep);
            float val = (float)(Map(mbps) * sweep);
            if (val > 0.5f)
                using (var pen = new Pen(ArcColor, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(pen, rect, start, val);

            // tick labels
            var c = new PointF(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            float rLabel = rect.Width / 2 - thick - Dpi.S(12);
            using (var b = new SolidBrush(UiTheme.Muted))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                for (int i = 0; i < Ticks.Length; i++)
                {
                    double a = (start + sweep * i / (Ticks.Length - 1)) * Math.PI / 180;
                    var p = new PointF(c.X + (float)Math.Cos(a) * rLabel, c.Y + (float)Math.Sin(a) * rLabel);
                    g.DrawString(Ticks[i].ToString(CultureInfo.CurrentCulture), Font, b, p, sf);
                }

            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                using (var f = new Font(Font.FontFamily, 30f, FontStyle.Bold))
                using (var b = new SolidBrush(UiTheme.Text))
                    g.DrawString(mbps >= 100 ? mbps.ToString("0", CultureInfo.CurrentCulture) : mbps.ToString("0.0", CultureInfo.CurrentCulture),
                        f, b, new RectangleF(0, c.Y - Dpi.S(38), Width, Dpi.S(56)), sf);
                using (var b = new SolidBrush(UiTheme.Muted))
                    g.DrawString("Mbps", Font, b, new RectangleF(0, c.Y + Dpi.S(14), Width, Dpi.S(20)), sf);
                using (var f = new Font(Font, FontStyle.Bold))
                using (var b = new SolidBrush(ArcColor))
                    g.DrawString(Phase, f, b, new RectangleF(0, c.Y + Dpi.S(40), Width, Dpi.S(20)), sf);
                using (var b = new SolidBrush(UiTheme.Muted))
                    g.DrawString(Detail, Font, b, new RectangleF(0, c.Y + Dpi.S(60), Width, Dpi.S(20)), sf);
            }
        }
    }
}
