# Units explained

## Bytes or bits?

A **byte** is 8 **bits**. Speeds are quoted both ways, which is why your "100 Mbps" plan downloads at about 12 MB/s.

| You see | Unit | Typical use |
|---|---|---|
| `12.5 MB/s` | megabytes per second | Browsers, Steam, file copies |
| `100 Mbps` | megabits per second | ISPs, speed tests, router specs |

Divide Mbps by 8 to get MB/s: 100 Mbps ≈ 12.5 MB/s.

**Which should I pick?**
- Choose **bytes (MB/s)** if you mostly watch downloads and file transfers.
- Choose **bits (Mbps)** if you want to compare against your internet plan.

The speed test always reports Mbps, because that's the industry convention.

## 1024 or 1000?

- **Binary (1024):** 1 KB = 1024 bytes. Windows Explorer and most apps count this way.
- **Decimal (1000):** 1 KB = 1000 bytes. ISPs, storage makers and network gear count this way.

The difference is small (about 2.4 % per step, 4.9 % at MB). If you want the meter to match your ISP's numbers exactly, pick decimal.

## Label styles

| Style | Bytes | Bits |
|---|---|---|
| Standard | B/s, KB/s, MB/s, GB/s | bps, Kbps, Mbps, Gbps |
| Compact | B, K, M, G | b, Kb, Mb, Gb |
| IEC | B/s, KiB/s, MiB/s, GiB/s | bit/s, Kibit/s, Mibit/s, Gibit/s |

**IEC** labels make the binary multiplier explicit (KiB = 1024 bytes). Use them with the binary multiplier if you want to be precise.

## Automatic vs fixed

**Automatic** picks the unit that keeps the number below 1000 (`845 KB/s`, then `1.02 MB/s`). **Fixed** always uses the same unit, which is handy if you want to compare at a glance (`0.8 MB/s`, `12.4 MB/s`).

## Data totals

Usage totals (today, billing period, history) always use binary multiples: B, KB, MB, GB, TB.
