# Network adapters

A PC often has several network adapters: Wi-Fi, Ethernet, Bluetooth, VPN adapters and virtual adapters for Hyper-V, WSL, Docker or VirtualBox. **Settings → Network adapters** decides which ones the meter and the "counted" totals include.

## Automatic (default)

Counts **every connected physical adapter**: Ethernet, Wi-Fi and mobile broadband. Virtual adapters are skipped. WinSpeed recognises VPN clients (WireGuard, OpenVPN/TAP, Wintun, Tailscale, ZeroTier, NordLynx, Cisco AnyConnect, FortiClient, GlobalProtect…), Hyper-V/WSL `vEthernet`, VMware, VirtualBox, Docker, loopback and tunnel adapters.

### Why skip VPN adapters?

When a VPN is on, the same traffic passes through two adapters: the encrypted stream on your Wi-Fi/Ethernet, and the decrypted stream on the VPN adapter. Counting both would roughly double your numbers. Counting only the physical adapter gives the real amount of data on your line.

Tick **Also count virtual adapters** if you really do want them included.

## Only the adapters I tick

Choose exactly which adapters count. Adapters that aren't connected right now are still listed (marked `[not present]` if they've been removed), so a USB dongle or hotspot keeps its selection.

Typical uses:

- Track only a **mobile hotspot** for a data cap while ignoring home Wi-Fi.
- Monitor only the **VPN adapter** to see how much goes through the tunnel.
- Exclude a **virtual machine's** adapter.

## Seeing what's what

- **Overview → Network adapters** shows live speeds per adapter and a ✓ for counted ones.
- **Connection → Network adapters** shows full details: type, link speed, MAC, IPs, gateway, DNS and totals.
- **Data usage → Adapter** filters history by any single adapter, so history is kept for *every* adapter, counted or not.
