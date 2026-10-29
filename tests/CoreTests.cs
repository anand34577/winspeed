using System;
using System.IO;
using System.Windows.Forms;
using Xunit;

namespace WinSpeed.Tests
{
    public class SpeedFormatterTests
    {
        static SpeedFormatter Make(Action<Settings> tweak = null)
        {
            var s = new Settings();
            tweak?.Invoke(s);
            return new SpeedFormatter(s);
        }

        [Fact]
        public void AutoBytesPicksUnit()
        {
            var f = Make();
            f.Split(0, out _, out var u);
            Assert.Equal("KB/s", u);              // AutoMinUnit defaults to Kilo
            f.Split(1536 * 1024, out var v, out u);
            Assert.Equal("MB/s", u);
            Assert.StartsWith("1", v);
        }

        [Fact]
        public void BitsAreEightTimesBytes()
        {
            var f = Make(s => { s.Unit = UnitMode.AutoBits; s.UnitBase = 1000; });
            f.Split(125000, out var v, out var u);   // 1 Mbit/s
            Assert.Equal("Mbps", u);
            Assert.StartsWith("1", v);
        }

        [Fact]
        public void NegativeAndNaNRatesFormatAsZero()
        {
            var f = Make();
            Assert.Equal(f.Format(0), f.Format(-5));
            Assert.Equal(f.Format(0), f.Format(double.NaN));
        }

        [Fact]
        public void FormatBytesUsesBinaryMultiples()
        {
            Assert.Equal("0 B", SpeedFormatter.FormatBytes(-1));
            Assert.EndsWith("MB", SpeedFormatter.FormatBytes(5 * 1048576.0));
        }
    }

    public class SettingsTests
    {
        static string TempFile() => Path.Combine(Path.GetTempPath(), "winspeed-test-" + Guid.NewGuid().ToString("N") + ".ini");

        [Fact]
        public void SanitizeReplacesNaNFontSize()
        {
            var s = new Settings { FontSize = float.NaN };
            s.Sanitize();
            Assert.Equal(9f, s.FontSize);
        }

        [Fact]
        public void SanitizeReplacesInvalidValues()
        {
            var s = new Settings
            {
                Mode = (DisplayMode)99, Unit = (UnitMode)(-3), Placement = (Placement)42,
                MonthlyCapMB = -5, DailyCapMB = -1, RetentionDays = -10, HideBelowKBps = -2,
                UpdateIntervalMs = 5, BillingDay = 40, UnitBase = 7
            };
            s.Sanitize();
            Assert.Equal(DisplayMode.Separate, s.Mode);
            Assert.Equal(UnitMode.AutoBytes, s.Unit);
            Assert.Equal(Placement.TaskbarRight, s.Placement);
            Assert.Equal(0, s.MonthlyCapMB);
            Assert.Equal(0, s.DailyCapMB);
            Assert.Equal(0, s.RetentionDays);
            Assert.Equal(0, s.HideBelowKBps);
            Assert.Equal(250, s.UpdateIntervalMs);
            Assert.Equal(28, s.BillingDay);
            Assert.Equal(1024, s.UnitBase);
        }

        [Fact]
        public void SaveAndLoadRoundTrip()
        {
            var path = TempFile();
            try
            {
                var s = new Settings { Mode = DisplayMode.Combined, MonthlyCapMB = 2048, DownloadLabel = "D", Hotkey = "Ctrl+Shift+X" };
                s.Save(path);
                var r = Settings.Load(path);
                Assert.Equal(DisplayMode.Combined, r.Mode);
                Assert.Equal(2048, r.MonthlyCapMB);
                Assert.Equal("D", r.DownloadLabel);
                Assert.Equal("Ctrl+Shift+X", r.Hotkey);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void LoadIgnoresGarbageLines()
        {
            var path = TempFile();
            try
            {
                File.WriteAllText(path, "Mode=99\nMonthlyCapMB=abc\nnonsense\n=x\nDecimals=2\n");
                var r = Settings.Load(path);
                Assert.Equal(DisplayMode.Separate, r.Mode);
                Assert.Equal(0, r.MonthlyCapMB);
                Assert.Equal(2, r.Decimals);
            }
            finally { File.Delete(path); }
        }
    }

    public class HotkeyTests
    {
        [Fact]
        public void ParsesModifiersAndKey()
        {
            Assert.True(HotkeyWindow.TryParse("Ctrl+Alt+N", out var mods, out var key));
            Assert.Equal(3u, mods);
            Assert.Equal(Keys.N, key);
        }

        [Theory]
        [InlineData("N")]            // no modifier
        [InlineData("Ctrl+5")]       // bare number is not a key name
        [InlineData("Ctrl+Nope")]
        [InlineData("Ctrl+")]
        public void RejectsInvalid(string text) => Assert.False(HotkeyWindow.TryParse(text, out _, out _));
    }

    public class UsageStoreTests
    {
        static string TempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "winspeed-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public void PersistsAndTotals()
        {
            var dir = TempDir();
            try
            {
                var u = new UsageStore(dir);
                var t = new DateTime(2026, 3, 5, 14, 30, 0);
                u.Add(t, "a", 100, 10);
                u.Add(t.AddMinutes(5), "a", 50, 5);
                u.Add(t.AddHours(1), "b", 7, 1);
                u.Save();

                var again = new UsageStore(dir);
                again.Total(t.Date, t.Date.AddDays(1), id => true, out var rx, out var tx);
                Assert.Equal(157, rx);
                Assert.Equal(16, tx);
                again.Total(t.Date, t.Date.AddDays(1), id => id == "a", out rx, out tx);
                Assert.Equal(150, rx);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void QueryFillsEmptyDays()
        {
            var dir = TempDir();
            try
            {
                var u = new UsageStore(dir);
                u.Add(new DateTime(2026, 3, 1, 9, 0, 0), "a", 1, 1);
                u.Add(new DateTime(2026, 3, 4, 9, 0, 0), "a", 1, 1);
                var rows = u.Query(new DateTime(2026, 3, 1), new DateTime(2026, 3, 5), id => true, Grouping.Day);
                Assert.Equal(4, rows.Count);
                Assert.Equal(0, rows[1].Total);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void PruneDropsOldHours()
        {
            var dir = TempDir();
            try
            {
                var u = new UsageStore(dir);
                u.Add(DateTime.Today.AddDays(-100), "a", 5, 5);
                u.Add(DateTime.Today, "a", 5, 5);
                u.Prune(30);
                u.Total(DateTime.Today.AddDays(-200), DateTime.Today.AddDays(1), id => true, out var rx, out _);
                Assert.Equal(5, rx);
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
