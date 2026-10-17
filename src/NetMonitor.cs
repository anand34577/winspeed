using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace WinSpeed
{
    public sealed class InterfaceInfo
    {
        public string Id, Name, Description, Mac;
        public NetworkInterfaceType Type;
        public OperationalStatus Status;
        public long LinkSpeed;
        public bool IsVirtual;
        public long RxTotal, TxTotal;
        public double RxRate, TxRate;
        public bool Monitored;
        internal NetworkInterface Ni;
        internal bool HasPrev;
        internal long PrevRx, PrevTx;

        public string TypeName
        {
            get
            {
                switch (Type)
                {
                    case NetworkInterfaceType.Ethernet: case NetworkInterfaceType.GigabitEthernet:
                    case NetworkInterfaceType.FastEthernetT: case NetworkInterfaceType.FastEthernetFx:
                    case NetworkInterfaceType.Ethernet3Megabit: return "Ethernet";
                    case NetworkInterfaceType.Wireless80211: return "Wi-Fi";
                    case NetworkInterfaceType.Ppp: return "PPP / Dial-up";
                    case NetworkInterfaceType.Wwanpp: case NetworkInterfaceType.Wwanpp2: return "Mobile broadband";
                    case NetworkInterfaceType.Tunnel: return "Tunnel";
                    default:
                        if ((int)Type == 53) return "Virtual";
                        if ((int)Type == 71) return "Wi-Fi";
                        return Enum.IsDefined(typeof(NetworkInterfaceType), Type) ? Type.ToString() : "Other";
                }
            }
        }

        public string Display => string.IsNullOrEmpty(Description) || Description == Name ? Name : Name + " — " + Description;
    }

    public struct IfDelta { public string Id; public long Rx, Tx; }

    public sealed class Sample
    {
        public DateTime Time;
        public double Seconds;
        public double RxRate, TxRate;   // monitored interfaces, bytes/s
        public long RxBytes, TxBytes;   // monitored interfaces, bytes in this interval
        public readonly List<IfDelta> Deltas = new List<IfDelta>(); // every interface with traffic
    }

    /// <summary>Samples per-interface byte counters (64-bit, via GetIfEntry2) and computes rates.</summary>
    public sealed class NetMonitor : IDisposable
    {
        readonly Dictionary<string, InterfaceInfo> map = new Dictionary<string, InterfaceInfo>(StringComparer.OrdinalIgnoreCase);
        readonly Stopwatch clock = Stopwatch.StartNew();
        volatile bool dirty = true;
        double lastRefresh = double.MinValue, lastSample = -1;

        public Func<InterfaceInfo, bool> MonitorPredicate = i => !i.IsVirtual && i.Status == OperationalStatus.Up;
        public event Action InterfacesChanged;

        static readonly string[] VirtualHints =
        {
            "virtual", "hyper-v", "vmware", "virtualbox", "vbox", "tap-", "tap adapter", "tunnel", "wintun", "wireguard",
            "loopback", "pseudo", "miniport", "vethernet", "wsl", "npcap", "teredo", "isatap", "6to4", "kernel debug",
            "docker", "zerotier", "tailscale", "hamachi", "openvpn", "nordlynx", "vpn", "fortinet", "anyconnect",
            "juniper", "pangp", "sonicwall", "wi-fi direct", "wfp", "filter", "qos packet"
        };

        public NetMonitor()
        {
            NetworkChange.NetworkAddressChanged += OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        }

        void OnNetworkChanged(object sender, EventArgs e) => dirty = true;

        public void Invalidate() => dirty = true;

        public List<InterfaceInfo> Interfaces =>
            map.Values.OrderByDescending(i => i.Status == OperationalStatus.Up).ThenBy(i => i.IsVirtual).ThenBy(i => i.Name).ToList();

        public static bool DetectVirtual(NetworkInterface ni)
        {
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) return true;
            var text = ((ni.Description ?? "") + " " + (ni.Name ?? "")).ToLowerInvariant();
            foreach (var h in VirtualHints) if (text.IndexOf(h, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        void Refresh()
        {
            NetworkInterface[] nis;
            try { nis = NetworkInterface.GetAllNetworkInterfaces(); }
            catch (Exception ex) { Program.Log(ex); return; }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ni in nis)
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var id = ni.Id;
                if (!seen.Add(id)) continue;
                if (!map.TryGetValue(id, out var info)) map[id] = info = new InterfaceInfo { Id = id };
                info.Ni = ni;
                info.Name = ni.Name;
                info.Description = ni.Description;
                info.Type = ni.NetworkInterfaceType;
                info.Status = ni.OperationalStatus;
                try { info.LinkSpeed = ni.Speed; } catch { info.LinkSpeed = 0; }
                try
                {
                    var b = ni.GetPhysicalAddress().GetAddressBytes();
                    info.Mac = b.Length == 0 ? "" : string.Join("-", b.Select(x => x.ToString("X2")));
                }
                catch { info.Mac = ""; }
                info.IsVirtual = DetectVirtual(ni);
            }
            foreach (var gone in map.Keys.Where(k => !seen.Contains(k)).ToList()) map.Remove(gone);
            InterfacesChanged?.Invoke();
        }

        /// <summary>Take a new sample. Call from the UI thread on a timer.</summary>
        public Sample Next()
        {
            double now = clock.Elapsed.TotalSeconds;
            if (dirty || now - lastRefresh >= 10)
            {
                dirty = false;
                lastRefresh = now;
                Refresh();
            }

            double dt = lastSample < 0 ? 0 : now - lastSample;
            lastSample = now;
            var smp = new Sample { Time = DateTime.Now, Seconds = dt };

            foreach (var info in map.Values)
            {
                long rx, tx;
                try
                {
                    var st = info.Ni.GetIPStatistics();
                    rx = st.BytesReceived;
                    tx = st.BytesSent;
                }
                catch
                {
                    info.HasPrev = false;
                    info.RxRate = info.TxRate = 0;
                    continue;
                }

                long drx = 0, dtx = 0;
                if (info.HasPrev && dt > 0)
                {
                    drx = rx - info.PrevRx;
                    dtx = tx - info.PrevTx;
                    // counter reset (adapter reconnect) or a bogus jump (> 100 Gbit/s) -> ignore this interval
                    if (drx < 0 || drx / dt > 12.5e9) drx = 0;
                    if (dtx < 0 || dtx / dt > 12.5e9) dtx = 0;
                }
                info.PrevRx = rx; info.PrevTx = tx; info.HasPrev = true;
                info.RxTotal = rx; info.TxTotal = tx;
                info.RxRate = dt > 0 ? drx / dt : 0;
                info.TxRate = dt > 0 ? dtx / dt : 0;
                info.Monitored = MonitorPredicate(info);

                if (drx > 0 || dtx > 0) smp.Deltas.Add(new IfDelta { Id = info.Id, Rx = drx, Tx = dtx });
                if (info.Monitored) { smp.RxBytes += drx; smp.TxBytes += dtx; }
            }

            if (dt > 0)
            {
                smp.RxRate = smp.RxBytes / dt;
                smp.TxRate = smp.TxBytes / dt;
            }
            return smp;
        }

        public void Dispose()
        {
            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
        }
    }
}
