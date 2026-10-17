using System;
using System.Collections.Generic;
using System.Globalization;

namespace WinSpeed
{
    /// <summary>Turns bytes/second into display strings according to the user's unit settings.</summary>
    public sealed class SpeedFormatter
    {
        readonly Settings s;
        public SpeedFormatter(Settings settings) { s = settings; }

        static readonly string[] StdBytes = { "B/s", "KB/s", "MB/s", "GB/s", "TB/s" };
        static readonly string[] StdBits = { "bps", "Kbps", "Mbps", "Gbps", "Tbps" };
        static readonly string[] CmpBytes = { "B", "K", "M", "G", "T" };
        static readonly string[] CmpBits = { "b", "Kb", "Mb", "Gb", "Tb" };
        static readonly string[] IecBytes = { "B/s", "KiB/s", "MiB/s", "GiB/s", "TiB/s" };
        static readonly string[] IecBits = { "bit/s", "Kibit/s", "Mibit/s", "Gibit/s", "Tibit/s" };

        public bool UsesBits => s.Unit == UnitMode.AutoBits || (s.Unit >= UnitMode.Bits && s.Unit <= UnitMode.GigaBits);
        bool IsAuto => s.Unit == UnitMode.AutoBytes || s.Unit == UnitMode.AutoBits;

        int FixedExp()
        {
            switch (s.Unit)
            {
                case UnitMode.Bytes: case UnitMode.Bits: return 0;
                case UnitMode.KiloBytes: case UnitMode.KiloBits: return 1;
                case UnitMode.MegaBytes: case UnitMode.MegaBits: return 2;
                case UnitMode.GigaBytes: case UnitMode.GigaBits: return 3;
                default: return 0;
            }
        }

        public string[] UnitLabels
        {
            get
            {
                bool bits = UsesBits;
                switch (s.Style)
                {
                    case UnitStyle.Compact: return bits ? CmpBits : CmpBytes;
                    case UnitStyle.Iec: return bits ? IecBits : IecBytes;
                    default: return bits ? StdBits : StdBytes;
                }
            }
        }

        /// <summary>Exponent (0=B,1=K,2=M...) that would be used for the given rate.</summary>
        public int ExpFor(double bytesPerSec)
        {
            double v = UsesBits ? bytesPerSec * 8 : bytesPerSec;
            if (!IsAuto) return FixedExp();
            int exp = 0;
            while (v >= 999.5 && exp < 4) { v /= s.UnitBase; exp++; }
            return Math.Max(exp, (int)s.AutoMinUnit);
        }

        /// <summary>Divisor (in bytes/s) and unit label for a given rate - used by graph axes.</summary>
        public double ScaleFor(double bytesPerSec, out string unit)
        {
            int exp = ExpFor(bytesPerSec);
            unit = UnitLabels[exp];
            double div = Math.Pow(s.UnitBase, exp);
            return UsesBits ? div / 8.0 : div;
        }

        int DecimalsFor(double v, int exp)
        {
            if (s.Decimals >= 0) return exp == 0 && IsAuto ? 0 : s.Decimals;
            if (exp == 0) return 0;
            if (v < 9.995) return 2;
            if (v < 99.95) return 1;
            return 0;
        }

        public void Split(double bytesPerSec, out string value, out string unit)
        {
            if (bytesPerSec < 0 || double.IsNaN(bytesPerSec)) bytesPerSec = 0;
            int exp = ExpFor(bytesPerSec);
            double v = (UsesBits ? bytesPerSec * 8 : bytesPerSec) / Math.Pow(s.UnitBase, exp);
            value = v.ToString("F" + DecimalsFor(v, exp), CultureInfo.CurrentCulture);
            unit = UnitLabels[exp];
        }

        public string Format(double bytesPerSec)
        {
            Split(bytesPerSec, out var v, out var u);
            return v + " " + u;
        }

        /// <summary>Candidate strings that bound the width of the numeric part (keeps the meter from jittering).</summary>
        public IEnumerable<string> WidestValues()
        {
            string dec = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            if (s.Decimals < 0)
            {
                yield return "888";
                yield return "88" + dec + "8";
                yield return "8" + dec + "88";
            }
            else
            {
                yield return s.Decimals == 0 ? "888" : "888" + dec + new string('8', s.Decimals);
                if (!IsAuto) yield return s.Decimals == 0 ? "8888" : "8888" + dec + new string('8', s.Decimals);
            }
        }

        /// <summary>Very short form for the 16px tray icon, e.g. "1.2M", "56K".</summary>
        public string Tiny(double bytesPerSec)
        {
            bool bits = UsesBits;
            double v = bits ? bytesPerSec * 8 : bytesPerSec;
            int exp = 1;
            v /= s.UnitBase;
            while (v >= 999.5 && exp < 4) { v /= s.UnitBase; exp++; }
            if (v < 0.05) return "0";
            string num = v < 9.95 ? v.ToString("0.0", CultureInfo.CurrentCulture) : v.ToString("0", CultureInfo.CurrentCulture);
            string suffix = bits ? "kmgt".Substring(exp - 1, 1) : "KMGT".Substring(exp - 1, 1);
            return num + suffix;
        }

        static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB", "PB" };

        /// <summary>Data volume (totals), always binary multiples.</summary>
        public static string FormatBytes(double bytes)
        {
            if (bytes < 0) bytes = 0;
            int i = 0;
            while (bytes >= 1024 && i < SizeUnits.Length - 1) { bytes /= 1024; i++; }
            return i == 0
                ? bytes.ToString("0", CultureInfo.CurrentCulture) + " B"
                : bytes.ToString(bytes < 100 ? "0.00" : "0.0", CultureInfo.CurrentCulture) + " " + SizeUnits[i];
        }

        public static double ScaleForBytes(double bytes, out string unit)
        {
            int i = 0; double div = 1;
            while (bytes / div >= 1024 && i < SizeUnits.Length - 1) { div *= 1024; i++; }
            unit = SizeUnits[i];
            return div;
        }
    }
}
