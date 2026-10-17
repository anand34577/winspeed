using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WinSpeed
{
    static class Http
    {
        public const string Repo = "anand34577/winspeed";
        public const string RepoUrl = "https://github.com/" + Repo;
        public const string WikiUrl = RepoUrl + "/wiki";
        public const string IssuesUrl = RepoUrl + "/issues";
        public const string ReleasesUrl = RepoUrl + "/releases";

        static HttpClient client;

        public static HttpClient Client
        {
            get
            {
                if (client != null) return client;
                try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)12288; }
                catch { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
                ServicePointManager.DefaultConnectionLimit = Math.Max(ServicePointManager.DefaultConnectionLimit, 32);
                ServicePointManager.Expect100Continue = false;
                var h = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None, UseCookies = false };
                client = new HttpClient(h) { Timeout = TimeSpan.FromSeconds(30) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("WinSpeed/" + Program.Version + " (+" + RepoUrl + ")");
                return client;
            }
        }

        public static void Open(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
    }

    // =====================================================================================
    /// <summary>Periodic ICMP ping for latency, jitter and online/offline detection.</summary>
    sealed class NetworkHealth : IDisposable
    {
        readonly Settings s;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Queue<double> recent = new Queue<double>();
        bool busy;
        int failures;

        public double? LatencyMs { get; private set; }
        public double JitterMs { get; private set; }
        public double LossPercent { get; private set; }
        public bool Offline { get; private set; }
        public DateTime? OfflineSince { get; private set; }
        readonly Queue<bool> lossWindow = new Queue<bool>();

        public event Action<bool> ConnectivityChanged; // true = back online
        public event Action Updated;

        public NetworkHealth(Settings settings)
        {
            s = settings;
            timer.Tick += async (o, e) => await Probe();
            NetworkChange.NetworkAvailabilityChanged += OnAvailability;
            Apply();
        }

        void OnAvailability(object sender, NetworkAvailabilityEventArgs e)
        {
            // fast path: adapter-level change, verify with a probe soon
            failures = e.IsAvailable ? 0 : failures;
        }

        public void Apply()
        {
            timer.Interval = Math.Max(1, s.PingIntervalSec) * 1000;
            timer.Enabled = s.PingEnabled;
            if (!s.PingEnabled)
            {
                LatencyMs = null; JitterMs = 0; LossPercent = 0;
                SetOffline(!NetworkInterface.GetIsNetworkAvailable());
            }
            else _ = Probe();
        }

        async Task Probe()
        {
            if (busy) return;
            busy = true;
            try
            {
                double? rtt = await PingOnce(s.PingHost);
                if (rtt == null && s.PingHost != "8.8.8.8") rtt = await PingOnce("8.8.8.8"); // ICMP to the chosen host may be blocked

                lossWindow.Enqueue(rtt == null);
                while (lossWindow.Count > 20) lossWindow.Dequeue();
                LossPercent = lossWindow.Count(x => x) * 100.0 / lossWindow.Count;

                if (rtt.HasValue)
                {
                    failures = 0;
                    if (LatencyMs.HasValue) JitterMs = JitterMs * 0.7 + Math.Abs(rtt.Value - LatencyMs.Value) * 0.3;
                    LatencyMs = rtt;
                    recent.Enqueue(rtt.Value);
                    while (recent.Count > 30) recent.Dequeue();
                    SetOffline(false);
                }
                else
                {
                    failures++;
                    LatencyMs = null;
                    if (failures >= 3 || !NetworkInterface.GetIsNetworkAvailable()) SetOffline(true);
                }
                Updated?.Invoke();
            }
            catch (Exception ex) { Program.Log(ex); }
            finally { busy = false; }
        }

        static async Task<double?> PingOnce(string host)
        {
            try
            {
                using (var p = new Ping())
                {
                    var reply = await p.SendPingAsync(host, 2000);
                    return reply.Status == IPStatus.Success ? reply.RoundtripTime : (double?)null;
                }
            }
            catch { return null; }
        }

        void SetOffline(bool offline)
        {
            if (offline == Offline) return;
            Offline = offline;
            OfflineSince = offline ? DateTime.Now : (DateTime?)null;
            ConnectivityChanged?.Invoke(!offline);
        }

        public void Dispose()
        {
            NetworkChange.NetworkAvailabilityChanged -= OnAvailability;
            timer.Dispose();
        }
    }

    // =====================================================================================
    public sealed class SpeedTestResult
    {
        public DateTime Time;
        public double PingMs, JitterMs, DownBps, UpBps;
        public string Server = "", Ip = "";
    }

    /// <summary>
    /// Internet speed test against Cloudflare's public speed-test endpoints (speed.cloudflare.com):
    /// latency from tiny requests, then multi-stream download and upload for a fixed duration.
    /// </summary>
    sealed class SpeedTest
    {
        const string Base = "https://speed.cloudflare.com";
        public const double PhaseSeconds = 10;

        /// <summary>phase ("ping" | "download" | "upload"), progress 0..1, current bytes/s or ms.</summary>
        public event Action<string, double, double> Progress;

        public async Task<SpeedTestResult> RunAsync(CancellationToken ct)
        {
            var http = Http.Client;
            var res = new SpeedTestResult { Time = DateTime.Now };

            // server / ip
            try
            {
                var trace = await http.GetStringAsync(Base + "/cdn-cgi/trace");
                res.Server = Field(trace, "colo") + (Field(trace, "loc") is string loc && loc.Length > 0 ? " (" + loc + ")" : "");
                res.Ip = Field(trace, "ip");
            }
            catch { }
            ct.ThrowIfCancellationRequested();

            // latency
            var samples = new List<double>();
            for (int i = 0; i < 12; i++)
            {
                ct.ThrowIfCancellationRequested();
                var sw = Stopwatch.StartNew();
                using (var r = await http.GetAsync(Base + "/__down?bytes=0", HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    sw.Stop();
                    if (i > 1) samples.Add(sw.Elapsed.TotalMilliseconds); // first requests include TLS setup
                }
                Progress?.Invoke("ping", (i + 1) / 12.0, samples.Count > 0 ? samples.Last() : 0);
            }
            if (samples.Count > 0)
            {
                var sorted = samples.OrderBy(x => x).ToList();
                res.PingMs = sorted[sorted.Count / 2];
                double j = 0;
                for (int i = 1; i < samples.Count; i++) j += Math.Abs(samples[i] - samples[i - 1]);
                res.JitterMs = samples.Count > 1 ? j / (samples.Count - 1) : 0;
            }

            res.DownBps = await Measure("download", 6, ct, async (counter, token) =>
            {
                using (var r = await http.GetAsync(Base + "/__down?bytes=25000000", HttpCompletionOption.ResponseHeadersRead, token))
                using (var st = await r.Content.ReadAsStreamAsync())
                {
                    var buf = new byte[64 * 1024];
                    int n;
                    while ((n = await st.ReadAsync(buf, 0, buf.Length, token)) > 0) Interlocked.Add(ref counter[0], n);
                }
            });

            var payload = new byte[2 * 1024 * 1024];
            new Random().NextBytes(payload);
            res.UpBps = await Measure("upload", 4, ct, async (counter, token) =>
            {
                using (var content = new CountingContent(payload, counter))
                using (var r = await http.PostAsync(Base + "/__up", content, token)) { }
            });

            return res;
        }

        async Task<double> Measure(string phase, int streams, CancellationToken outer, Func<long[], CancellationToken, Task> work)
        {
            var counter = new long[1];
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(outer))
            {
                var sw = Stopwatch.StartNew();
                var workers = Enumerable.Range(0, streams).Select(_ => Task.Run(async () =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        try { await work(counter, cts.Token); }
                        catch when (cts.IsCancellationRequested) { break; }
                        catch { await Task.Delay(200); }
                    }
                })).ToArray();

                const double warmup = 1.5;
                long warmBytes = -1;
                double warmTime = 0, lastT = 0;
                long lastB = 0;
                double live = 0;
                while (sw.Elapsed.TotalSeconds < PhaseSeconds)
                {
                    await Task.Delay(200);
                    outer.ThrowIfCancellationRequested();
                    double t = sw.Elapsed.TotalSeconds;
                    long b = Interlocked.Read(ref counter[0]);
                    if (warmBytes < 0 && t >= warmup) { warmBytes = b; warmTime = t; }
                    double inst = (b - lastB) / Math.Max(0.001, t - lastT);
                    live = live == 0 ? inst : live * 0.6 + inst * 0.4;
                    lastB = b; lastT = t;
                    Progress?.Invoke(phase, Math.Min(1, t / PhaseSeconds), live);
                }
                cts.Cancel();
                try { await Task.WhenAll(workers); } catch { }
                double total = sw.Elapsed.TotalSeconds;
                long bytes = Interlocked.Read(ref counter[0]);
                if (warmBytes < 0) return bytes / total;
                return (bytes - warmBytes) / Math.Max(0.1, total - warmTime);
            }
        }

        sealed class CountingContent : HttpContent
        {
            readonly byte[] data; readonly long[] counter;
            public CountingContent(byte[] data, long[] counter)
            {
                this.data = data; this.counter = counter;
                Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            }
            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                const int chunk = 64 * 1024;
                for (int off = 0; off < data.Length; off += chunk)
                {
                    int n = Math.Min(chunk, data.Length - off);
                    await stream.WriteAsync(data, off, n);
                    Interlocked.Add(ref counter[0], n);
                }
            }
            protected override bool TryComputeLength(out long length) { length = data.Length; return true; }
        }

        static string Field(string trace, string key)
        {
            var m = Regex.Match(trace, "^" + key + "=(.*)$", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }
    }

    /// <summary>Speed test results, one per line in speedtests.tsv.</summary>
    sealed class SpeedTestHistory
    {
        readonly string path = Path.Combine(Settings.DataDir, "speedtests.tsv");
        public readonly List<SpeedTestResult> Items = new List<SpeedTestResult>();

        public SpeedTestHistory()
        {
            try
            {
                if (!File.Exists(path)) return;
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var p = line.Split('\t');
                    if (p.Length < 5) continue;
                    if (!DateTime.TryParseExact(p[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) continue;
                    double D(string v) => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
                    Items.Add(new SpeedTestResult
                    {
                        Time = t, PingMs = D(p[1]), JitterMs = D(p[2]), DownBps = D(p[3]), UpBps = D(p[4]),
                        Server = p.Length > 5 ? p[5] : "", Ip = p.Length > 6 ? p[6] : ""
                    });
                }
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        public void Add(SpeedTestResult r)
        {
            Items.Add(r);
            try
            {
                var line = string.Join("\t", r.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    r.PingMs.ToString("0.0", CultureInfo.InvariantCulture), r.JitterMs.ToString("0.0", CultureInfo.InvariantCulture),
                    r.DownBps.ToString("0", CultureInfo.InvariantCulture), r.UpBps.ToString("0", CultureInfo.InvariantCulture),
                    r.Server.Replace('\t', ' '), r.Ip.Replace('\t', ' '));
                File.AppendAllText(path, line + "\n", new UTF8Encoding(false));
            }
            catch (Exception ex) { Program.Log(ex); }
        }

        public void Clear()
        {
            Items.Clear();
            try { File.Delete(path); } catch { }
        }
    }

    // =====================================================================================
    public sealed class PublicInfo
    {
        public string Ip = "", Country = "", City = "", Isp = "", Colo = "";
    }

    static class PublicInfoService
    {
        public static async Task<PublicInfo> FetchAsync()
        {
            var info = new PublicInfo();
            var http = Http.Client;
            try
            {
                var trace = await http.GetStringAsync("https://www.cloudflare.com/cdn-cgi/trace");
                foreach (var line in trace.Split('\n'))
                {
                    var kv = line.Split(new[] { '=' }, 2);
                    if (kv.Length != 2) continue;
                    if (kv[0] == "ip") info.Ip = kv[1].Trim();
                    if (kv[0] == "loc") info.Country = kv[1].Trim();
                    if (kv[0] == "colo") info.Colo = kv[1].Trim();
                }
            }
            catch { }
            try
            {
                // ISP / city (free, no key). Optional: ignored if unavailable or rate-limited.
                var json = await http.GetStringAsync("https://ipinfo.io/json");
                string J(string key) { var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\""); return m.Success ? m.Groups[1].Value : ""; }
                if (info.Ip.Length == 0) info.Ip = J("ip");
                info.City = J("city");
                info.Isp = Regex.Replace(J("org"), @"^AS\d+\s+", "");
                if (info.Country.Length == 0) info.Country = J("country");
            }
            catch { }
            return info;
        }
    }

    // =====================================================================================
    static class UpdateChecker
    {
        public sealed class Release { public Version Version; public string Tag, Url, Name; }

        public static async Task<Release> LatestAsync()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/" + Http.Repo + "/releases/latest");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            using (var r = await Http.Client.SendAsync(req))
            {
                if (!r.IsSuccessStatusCode) return null;
                var json = await r.Content.ReadAsStringAsync();
                string J(string key) { var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\""); return m.Success ? m.Groups[1].Value : ""; }
                var tag = J("tag_name");
                if (!Version.TryParse(tag.TrimStart('v', 'V'), out var v)) return null;
                return new Release { Version = v, Tag = tag, Url = J("html_url"), Name = J("name") };
            }
        }

        public static Version Current => typeof(Program).Assembly.GetName().Version;

        public static bool IsNewer(Release r) =>
            r != null && new Version(r.Version.Major, r.Version.Minor, Math.Max(0, r.Version.Build)) >
                         new Version(Current.Major, Current.Minor, Math.Max(0, Current.Build));
    }
}
