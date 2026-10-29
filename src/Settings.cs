using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace WinSpeed
{
    public enum DisplayMode { Separate, Combined, DownloadOnly, UploadOnly, SeparateAndTotal }
    public enum LayoutMode { TwoLines, SingleLine }
    public enum UnitMode { AutoBytes, AutoBits, Bytes, KiloBytes, MegaBytes, GigaBytes, Bits, KiloBits, MegaBits, GigaBits }
    public enum UnitStyle { Standard, Compact, Iec }
    public enum MinUnit { None, Kilo, Mega }
    public enum Placement { TaskbarRight, TaskbarLeft, Floating }
    public enum TrayIconMode { Static, Speed, Hidden }
    public enum InterfaceMode { Auto, Selected }
    public enum ClickAction { Dashboard, DataUsage, Settings, ToggleMeter, SpeedTest, None }
    public enum AppTheme { System, Light, Dark }

    /// <summary>All user settings. Persisted as key=value lines in settings.ini.</summary>
    public sealed class Settings
    {
        // ---- General ----
        public int UpdateIntervalMs { get; set; } = 1000;
        public int Smoothing { get; set; } = 1;
        public ClickAction DoubleClickAction { get; set; } = ClickAction.Dashboard;
        public ClickAction TrayClickAction { get; set; } = ClickAction.Dashboard;
        public string Hotkey { get; set; } = "Ctrl+Alt+N";
        public TrayIconMode TrayIcon { get; set; } = TrayIconMode.Static;
        public bool FirstRun { get; set; } = true;
        public AppTheme Theme { get; set; } = AppTheme.System;
        public bool CheckForUpdates { get; set; } = true;
        public string LastUpdateCheck { get; set; } = "";
        public string SkippedVersion { get; set; } = "";
        public bool PrivacyMode { get; set; } = false; // mask IP / MAC addresses on screen

        // ---- Display ----
        public DisplayMode Mode { get; set; } = DisplayMode.Separate;
        public LayoutMode Layout { get; set; } = LayoutMode.TwoLines;
        public bool UploadFirst { get; set; } = false;
        public UnitMode Unit { get; set; } = UnitMode.AutoBytes;
        public int UnitBase { get; set; } = 1024;
        public UnitStyle Style { get; set; } = UnitStyle.Standard;
        public int Decimals { get; set; } = -1; // -1 = automatic (3 significant digits)
        public MinUnit AutoMinUnit { get; set; } = MinUnit.Kilo;
        public bool ShowUnit { get; set; } = true;
        public bool ShowLabels { get; set; } = true;
        public string DownloadLabel { get; set; } = "↓";
        public string UploadLabel { get; set; } = "↑";
        public string TotalLabel { get; set; } = "⇅";
        public string FontFamily { get; set; } = "Segoe UI";
        public float FontSize { get; set; } = 9f;
        public bool FontBold { get; set; } = false;
        public bool AutoTextColor { get; set; } = true;
        public Color DownloadColor { get; set; } = Color.FromArgb(0x29, 0xB6, 0xF6);
        public Color UploadColor { get; set; } = Color.FromArgb(0xFF, 0xA7, 0x26);
        public Color TotalColor { get; set; } = Color.FromArgb(0x9C, 0xCC, 0x65);
        public Color BackgroundColor { get; set; } = Color.FromArgb(0x20, 0x20, 0x20);
        public int BackgroundOpacity { get; set; } = 0; // 0..100 %
        public bool HighlightEnabled { get; set; } = false;
        public int HighlightKBps { get; set; } = 5120;
        public Color HighlightColor { get; set; } = Color.FromArgb(0xFF, 0x52, 0x52);
        public int HideBelowKBps { get; set; } = 0; // 0 = never hide
        public bool MeterGraph { get; set; } = false;   // mini sparkline behind the text
        public bool ShowPingOnMeter { get; set; } = false;
        public bool ShowOfflineDot { get; set; } = true;

        // ---- Network health ----
        public bool PingEnabled { get; set; } = true;
        public string PingHost { get; set; } = "1.1.1.1";
        public int PingIntervalSec { get; set; } = 5;
        public bool NotifyConnection { get; set; } = true;
        public bool NotifySpeedTestDone { get; set; } = true;

        // ---- Position ----
        public bool ShowOverlay { get; set; } = true;
        public Placement Placement { get; set; } = Placement.TaskbarRight;
        public int OffsetX { get; set; } = 0;
        public int OffsetY { get; set; } = 0;
        public int FloatX { get; set; } = -1;
        public int FloatY { get; set; } = -1;
        public bool LockPosition { get; set; } = false;
        public bool HideOnFullscreen { get; set; } = true;
        public bool ClickThrough { get; set; } = false;

        // ---- Interfaces ----
        public InterfaceMode Interfaces { get; set; } = InterfaceMode.Auto;
        public string SelectedInterfaces { get; set; } = "";
        public bool IncludeVirtual { get; set; } = false;

        // ---- Data usage & alerts ----
        public int MonthlyCapMB { get; set; } = 0;
        public int DailyCapMB { get; set; } = 0;
        public int BillingDay { get; set; } = 1;
        public int AlertPercent { get; set; } = 90;
        public string AlertState { get; set; } = "";
        public int RetentionDays { get; set; } = 400; // 0 = keep forever

        // ------------------------------------------------------------------

        public static string ExeDir => Path.GetDirectoryName(Application.ExecutablePath);

        /// <summary>Portable mode: a file named "portable" (or portable.txt) beside the exe keeps data next to it.</summary>
        public static bool IsPortable =>
            File.Exists(Path.Combine(ExeDir, "portable")) || File.Exists(Path.Combine(ExeDir, "portable.txt"));

        static string dataDir;
        public static string DataDir
        {
            get
            {
                if (dataDir != null) return dataDir;
                var dir = IsPortable
                    ? Path.Combine(ExeDir, "Data")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinSpeed");
                try { Directory.CreateDirectory(dir); } catch { }
                return dataDir = dir;
            }
        }

        static string FilePath => Path.Combine(DataDir, "settings.ini");

        public Settings Clone() => (Settings)MemberwiseClone();

        static IEnumerable<PropertyInfo> Props()
        {
            foreach (var p in typeof(Settings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (p.CanRead && p.CanWrite) yield return p;
        }

        public void CopyFrom(Settings other)
        {
            foreach (var p in Props()) p.SetValue(this, p.GetValue(other));
        }

        public HashSet<string> SelectedSet() =>
            new HashSet<string>(SelectedInterfaces.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

        public static Settings Load() => Load(FilePath);

        public static Settings Load(string path)
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(path)) return s;
                var props = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in Props()) props[p.Name] = p;
                foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';' || line[0] == '[') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = line.Substring(0, eq).Trim();
                    var val = line.Substring(eq + 1).Trim();
                    if (!props.TryGetValue(key, out var prop)) continue;
                    try { prop.SetValue(s, Parse(prop.PropertyType, val)); } catch { /* ignore bad value, keep default */ }
                }
            }
            catch { }
            s.Sanitize();
            return s;
        }

        public void Sanitize()
        {
            UpdateIntervalMs = Clamp(UpdateIntervalMs, 250, 10000);
            Smoothing = Clamp(Smoothing, 1, 10);
            if (UnitBase != 1000 && UnitBase != 1024) UnitBase = 1024;
            Decimals = Clamp(Decimals, -1, 3);
            FontSize = float.IsNaN(FontSize) ? 9f : Math.Max(5f, Math.Min(36f, FontSize));
            BackgroundOpacity = Clamp(BackgroundOpacity, 0, 100);
            BillingDay = Clamp(BillingDay, 1, 28);
            AlertPercent = Clamp(AlertPercent, 1, 100);
            if (string.IsNullOrWhiteSpace(FontFamily)) FontFamily = "Segoe UI";
            DownloadLabel ??= ""; UploadLabel ??= ""; TotalLabel ??= "";
            SelectedInterfaces ??= ""; AlertState ??= ""; Hotkey ??= "";
            PingIntervalSec = Clamp(PingIntervalSec, 1, 300);
            if (string.IsNullOrWhiteSpace(PingHost)) PingHost = "1.1.1.1";
            LastUpdateCheck ??= ""; SkippedVersion ??= "";
            MonthlyCapMB = Math.Max(0, MonthlyCapMB);
            DailyCapMB = Math.Max(0, DailyCapMB);
            HighlightKBps = Math.Max(0, HighlightKBps);
            HideBelowKBps = Math.Max(0, HideBelowKBps);
            RetentionDays = Math.Max(0, RetentionDays);
            // a hand-edited numeric enum value (e.g. Mode=99) would otherwise reach the switch statements as-is
            Mode = Valid(Mode, DisplayMode.Separate); Layout = Valid(Layout, LayoutMode.TwoLines);
            Unit = Valid(Unit, UnitMode.AutoBytes); Style = Valid(Style, UnitStyle.Standard);
            AutoMinUnit = Valid(AutoMinUnit, MinUnit.Kilo); Placement = Valid(Placement, Placement.TaskbarRight);
            TrayIcon = Valid(TrayIcon, TrayIconMode.Static); Interfaces = Valid(Interfaces, InterfaceMode.Auto);
            DoubleClickAction = Valid(DoubleClickAction, ClickAction.Dashboard);
            TrayClickAction = Valid(TrayClickAction, ClickAction.Dashboard);
            Theme = Valid(Theme, AppTheme.System);
        }

        static T Valid<T>(T value, T fallback) where T : struct => Enum.IsDefined(typeof(T), value) ? value : fallback;

        public void Save() => Save(FilePath);

        public void Save(string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# WinSpeed settings - edited by the app; safe to delete to reset.");
                foreach (var p in Props()) sb.Append(p.Name).Append('=').AppendLine(Format(p.GetValue(this)));
                var tmp = filePath + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(filePath)) File.Replace(tmp, filePath, null);
                else File.Move(tmp, filePath);
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        static string Format(object v)
        {
            switch (v)
            {
                case null: return "";
                case Color c: return "#" + c.ToArgb().ToString("X8");
                case bool b: return b ? "true" : "false";
                case IFormattable f: return f.ToString(null, CultureInfo.InvariantCulture);
                default: return v.ToString().Replace("\r", "").Replace("\n", " ");
            }
        }

        static object Parse(Type t, string v)
        {
            if (t == typeof(string)) return v;
            if (t == typeof(int)) return int.Parse(v, CultureInfo.InvariantCulture);
            if (t == typeof(long)) return long.Parse(v, CultureInfo.InvariantCulture);
            if (t == typeof(float)) return float.Parse(v, CultureInfo.InvariantCulture);
            if (t == typeof(double)) return double.Parse(v, CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (t == typeof(Color)) return Color.FromArgb(int.Parse(v.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            if (t.IsEnum) return Enum.Parse(t, v, true);
            throw new NotSupportedException(t.Name);
        }

        public static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
