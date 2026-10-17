using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace WinSpeed
{
    public enum Grouping { Hour, Day, Month, Year }

    public sealed class UsageRow
    {
        public DateTime Period;
        public long Rx, Tx;
        public long Total => Rx + Tx;
    }

    /// <summary>
    /// Hourly per-interface traffic totals. Stored as tab-separated text:
    ///   usage.tsv:      yyyy-MM-dd HH  interfaceId  rxBytes  txBytes
    ///   interfaces.tsv: interfaceId    name         virtual(0/1)
    /// </summary>
    public sealed class UsageStore
    {
        public sealed class IfaceMeta { public string Name; public bool Virtual; }

        const string HourFormat = "yyyy-MM-dd HH";
        readonly string usagePath, ifPath;
        readonly Dictionary<(DateTime hour, string id), long[]> data = new Dictionary<(DateTime, string), long[]>();
        public readonly Dictionary<string, IfaceMeta> Ifaces = new Dictionary<string, IfaceMeta>(StringComparer.OrdinalIgnoreCase);
        bool dirty;

        public UsageStore(string dir)
        {
            usagePath = Path.Combine(dir, "usage.tsv");
            ifPath = Path.Combine(dir, "interfaces.tsv");
            Load();
        }

        public string DataFile => usagePath;

        static DateTime Hour(DateTime t) => new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0);

        public void Add(DateTime t, string id, long rx, long tx)
        {
            var key = (Hour(t), id);
            if (!data.TryGetValue(key, out var v)) data[key] = v = new long[2];
            v[0] += rx; v[1] += tx;
            dirty = true;
        }

        public void Register(string id, string name, bool isVirtual)
        {
            if (Ifaces.TryGetValue(id, out var m) && m.Name == name && m.Virtual == isVirtual) return;
            Ifaces[id] = new IfaceMeta { Name = name, Virtual = isVirtual };
            dirty = true;
        }

        public string NameOf(string id) => Ifaces.TryGetValue(id, out var m) ? m.Name : id;
        public bool IsVirtual(string id) => Ifaces.TryGetValue(id, out var m) && m.Virtual;

        public DateTime? Earliest => data.Count == 0 ? (DateTime?)null : data.Keys.Min(k => k.hour);

        void Load()
        {
            try
            {
                if (File.Exists(ifPath))
                    foreach (var line in File.ReadAllLines(ifPath, Encoding.UTF8))
                    {
                        var p = line.Split('\t');
                        if (p.Length >= 2) Ifaces[p[0]] = new IfaceMeta { Name = p[1], Virtual = p.Length > 2 && p[2] == "1" };
                    }
                if (File.Exists(usagePath))
                    foreach (var line in File.ReadAllLines(usagePath, Encoding.UTF8))
                    {
                        var p = line.Split('\t');
                        if (p.Length < 4) continue;
                        if (!DateTime.TryParseExact(p[0], HourFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var h)) continue;
                        if (!long.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rx)) continue;
                        if (!long.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tx)) continue;
                        var key = (h, p[1]);
                        if (data.TryGetValue(key, out var v)) { v[0] += rx; v[1] += tx; }
                        else data[key] = new[] { rx, tx };
                    }
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        public void Save(bool force = false)
        {
            if (!dirty && !force) return;
            try
            {
                var sb = new StringBuilder(data.Count * 64);
                foreach (var kv in data.OrderBy(k => k.Key.hour))
                    sb.Append(kv.Key.hour.ToString(HourFormat, CultureInfo.InvariantCulture)).Append('\t')
                      .Append(kv.Key.id).Append('\t')
                      .Append(kv.Value[0].ToString(CultureInfo.InvariantCulture)).Append('\t')
                      .Append(kv.Value[1].ToString(CultureInfo.InvariantCulture)).Append('\n');
                WriteAtomic(usagePath, sb.ToString());

                sb.Clear();
                foreach (var kv in Ifaces)
                    sb.Append(kv.Key).Append('\t').Append((kv.Value.Name ?? "").Replace('\t', ' ')).Append('\t')
                      .Append(kv.Value.Virtual ? "1" : "0").Append('\n');
                WriteAtomic(ifPath, sb.ToString());
                dirty = false;
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        static void WriteAtomic(string path, string content)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        public void Prune(int retentionDays)
        {
            if (retentionDays <= 0) return;
            var cutoff = DateTime.Today.AddDays(-retentionDays);
            var old = data.Keys.Where(k => k.hour < cutoff).ToList();
            foreach (var k in old) data.Remove(k);
            if (old.Count > 0) dirty = true;
        }

        public void Clear()
        {
            data.Clear();
            dirty = true;
            Save(true);
        }

        public void Total(DateTime from, DateTime toExclusive, Func<string, bool> filter, out long rx, out long tx)
        {
            rx = tx = 0;
            foreach (var kv in data)
            {
                if (kv.Key.hour < from || kv.Key.hour >= toExclusive || !filter(kv.Key.id)) continue;
                rx += kv.Value[0]; tx += kv.Value[1];
            }
        }

        public static DateTime Bucket(DateTime h, Grouping g)
        {
            switch (g)
            {
                case Grouping.Hour: return h;
                case Grouping.Day: return h.Date;
                case Grouping.Month: return new DateTime(h.Year, h.Month, 1);
                default: return new DateTime(h.Year, 1, 1);
            }
        }

        public static DateTime NextBucket(DateTime b, Grouping g)
        {
            switch (g)
            {
                case Grouping.Hour: return b.AddHours(1);
                case Grouping.Day: return b.AddDays(1);
                case Grouping.Month: return b.AddMonths(1);
                default: return b.AddYears(1);
            }
        }

        /// <summary>Grouped rows in [from, toExclusive). Empty buckets are included so charts have a continuous axis.</summary>
        public List<UsageRow> Query(DateTime from, DateTime toExclusive, Func<string, bool> filter, Grouping g)
        {
            var buckets = new Dictionary<DateTime, UsageRow>();
            foreach (var kv in data)
            {
                if (kv.Key.hour < from || kv.Key.hour >= toExclusive || !filter(kv.Key.id)) continue;
                var b = Bucket(kv.Key.hour, g);
                if (!buckets.TryGetValue(b, out var row)) buckets[b] = row = new UsageRow { Period = b };
                row.Rx += kv.Value[0]; row.Tx += kv.Value[1];
            }

            // fill gaps (bounded so "all time / by hour" can't explode)
            if (buckets.Count > 0)
            {
                var first = Bucket(from, g);
                var last = Bucket(toExclusive.AddTicks(-1), g);
                var minKey = buckets.Keys.Min();
                if (first < minKey && (g == Grouping.Hour ? (minKey - first).TotalHours > 24 * 62 : false)) first = minKey;
                if (first.Year < 2000) first = minKey;
                int guard = 0;
                for (var b = first; b <= last && guard < 5000; b = NextBucket(b, g), guard++)
                    if (!buckets.ContainsKey(b)) buckets[b] = new UsageRow { Period = b };
            }
            return buckets.Values.OrderBy(r => r.Period).ToList();
        }
    }
}
