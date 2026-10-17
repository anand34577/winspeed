# Connection monitoring

## Latency and offline detection

With **Settings → Connection monitoring → Measure latency and detect when the internet is down** switched on (the default), WinSpeed sends one small ping every few seconds.

| Setting | Default | Notes |
|---|---|---|
| **Ping host** | `1.1.1.1` | Any IP or host name: `8.8.8.8`, `9.9.9.9`, your router (`192.168.1.1`) or a game server. |
| **Ping every** | 5 seconds | 1–300 seconds. Each ping is a few dozen bytes. |
| **Notify me when the internet disconnects or comes back** | On | A Windows notification on each change. |

What you get:

- **Latency** (ping time), **jitter** (how much the latency varies) and **packet loss** over the last 20 pings, on the Overview and Connection pages.
- **Offline detection**: after three failed pings in a row (or when Windows reports no network), WinSpeed marks the connection as **Offline**. The meter shows a red dot, the tray icon shows `off line` in live-speed mode, and a notification appears. When pings succeed again you get an "Internet connection restored" notification.
- Optionally the **ping on the meter** itself (Meter & display → *Also show ping*).

If your chosen host doesn't answer (some networks block ping), WinSpeed also tries `8.8.8.8` before deciding you're offline.

## Public IP, ISP and location

The **Connection** page has tiles for **Public IP**, **Provider (ISP)** and **Location**. They stay hidden until you click **Show**, so nothing is looked up unless you ask. The lookup uses `cloudflare.com/cdn-cgi/trace` (IP and country) and `ipinfo.io` (ISP and city).

## Network adapters

Below the tiles, every adapter is listed with type, status, whether it's counted, link speed, MAC address, IPv4/IPv6 addresses, gateway, DNS servers and total bytes since it was enabled. **Copy details** copies everything as text, which is handy for support requests.

## Privacy mode

**Settings → General → Privacy mode** masks IP and MAC addresses everywhere in the app (`•••.•••.•.••`). It's useful when you share your screen, stream or take screenshots.
