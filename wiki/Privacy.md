# Privacy

WinSpeed has **no telemetry, no analytics, no ads and no account**. Everything it records stays on your PC, in plain-text files you can read.

## What stays on your PC

- Settings (`settings.ini`)
- Hourly byte counts per network adapter (`usage.tsv`) and adapter names (`interfaces.tsv`)
- Speed test results (`speedtests.tsv`)

WinSpeed never records *what* you do online: no websites, apps, IP addresses of connections or content. It only reads the byte counters that Windows keeps for each adapter.

## What goes online, and when

| Feature | When | What is contacted | Can I turn it off? |
|---|---|---|---|
| Latency / offline detection | every few seconds | an ICMP ping to `1.1.1.1` (or the host you choose; `8.8.8.8` as fallback) | Yes: Settings → Connection monitoring |
| Update check | at most once a day | `api.github.com` (latest release info) | Yes: Settings → General |
| Speed test | only when you start one | `speed.cloudflare.com` | Just don't run it |
| Public IP / ISP | only when you click **Show** | `cloudflare.com/cdn-cgi/trace`, `ipinfo.io` | Just don't click it |

The User-Agent sent with these requests is `WinSpeed/<version>` and contains nothing identifying.

## Privacy mode

**Settings → General → Privacy mode** masks IP and MAC addresses on screen, for screen-sharing, streaming and screenshots.
