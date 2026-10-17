using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace WinSpeed
{
    static class TrayIcons
    {
        public static Icon LoadAppIcon(Size size)
        {
            using (var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("WinSpeed.ico"))
            {
                if (st == null) return SystemIcons.Application;
                return new Icon(st, size);
            }
        }

        /// <summary>Draws a live two-line speed icon ("download" over "upload") for the notification area.</summary>
        public static Icon RenderSpeed(string top, string bottom, Color topColor, Color bottomColor)
        {
            var sz = SystemInformation.SmallIconSize;
            using (var bmp = new Bitmap(sz.Width, sz.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    float half = sz.Height / 2f;
                    DrawFit(g, top, topColor, new RectangleF(0, -1, sz.Width, half + 1));
                    DrawFit(g, bottom, bottomColor, new RectangleF(0, half - 1, sz.Width, half + 1));
                }
                IntPtr h = bmp.GetHicon();
                try
                {
                    using (var tmp = Icon.FromHandle(h)) return (Icon)tmp.Clone();
                }
                finally { Native.DestroyIcon(h); }
            }
        }

        static void DrawFit(Graphics g, string text, Color color, RectangleF r)
        {
            using (var sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                sf.FormatFlags |= StringFormatFlags.NoWrap;
                float px = r.Height * 1.05f;
                Font f = null;
                for (; px > 4; px -= 0.5f)
                {
                    f?.Dispose();
                    f = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel);
                    if (g.MeasureString(text, f, 1000, sf).Width <= r.Width + 0.5f) break;
                }
                using (f)
                using (var b = new SolidBrush(color))
                    g.DrawString(text, f, b, r, sf);
            }
        }
    }
}
