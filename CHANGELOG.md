# Changelog

All notable changes to WinSpeed are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [semantic versioning](https://semver.org/).

## [Unreleased]

## [1.0.4] - 2026-10-25

### Changed
- Internal only: added automated tests for unit formatting, settings loading, hotkey parsing and usage history, and CI now runs them. The app itself is unchanged.

## [1.0.3] - 2026-10-25

### Fixed
- A failed update check (for example while offline) no longer counts as done for the day; WinSpeed tries again on the next check.
- An out-of-range value in a hand-edited settings file (such as a negative data cap or an unknown display mode) is replaced with the default instead of causing odd behaviour.
- A hotkey entered as a bare number in settings.ini is rejected instead of being registered as an unrelated key.
- No update-check notification or dialog is attempted while WinSpeed is closing.

## [1.0.2] - 2026-10-20

### Fixed
- The meter no longer draws on top of other apps' taskbar widgets or newly added tray items; it moves aside to make room.
- The meter no longer shows through windows that are stacked above the taskbar (an app dragged over it, a topmost tool); it hides until they move away.
- The meter now follows taskbar layout changes within a fraction of a second instead of waiting for the next speed sample.

## [1.0.1] - 2026-10-17

### Fixed
- The meter no longer blinks off and back on when you open the tray's hidden-icons menu (^), click the taskbar, or open Start, search or the calendar.

### Changed
- Runs on every Windows 10 release from version 1607 and on all of Windows 11 with nothing to install. WinSpeed now needs only .NET Framework 4.6.2, which these versions include, and it is still a single exe with no extra DLLs. On the rare system without it, setup explains what to install instead of failing to start.

## [1.0.0] - 2026-10-17

First public release.

### Taskbar meter
- Live download and upload speed on the taskbar, next to the tray, on the left side, or as a floating desktop widget.
- Show download and upload separately, combined, both plus total, or only one of them; stacked or on one line.
- Automatic byte or bit units, or a fixed unit; 1024 or 1000 multiplier; standard, compact or IEC labels; automatic or fixed decimals.
- Custom labels, any font, automatic light/dark text colour, custom colours, background with adjustable opacity.
- Optional mini speed graph, ping on the meter, red offline dot, colour highlight above a speed threshold, and hide-when-idle.
- Drag to move, lock position, click-through, and auto-hide during full-screen apps and with an auto-hiding taskbar.
- Live-speed tray icon, tooltip with peak speed, usage, ping and active adapters.

### App
- New main window with Overview, Speed test, Data usage, Connection, Meter & display, Settings and About pages, in light and dark themes (follows Windows by default).
- Built-in speed test (download, upload, ping, jitter) using Cloudflare's speed-test network, with history and CSV export.
- Data usage history per hour, day, month or year, filterable by adapter, with drill-down, bar chart and CSV export.
- Monthly and daily data limits with billing-cycle day and notifications at a chosen percentage and at 100 %.
- Latency, jitter and packet-loss monitoring with notifications when the internet goes down and comes back.
- Public IP, ISP and location lookup on request; full details for every network adapter.
- Automatic counting of physical adapters only (VPN and virtual adapters skipped to avoid double counting), or a hand-picked list.
- Privacy mode that masks IP and MAC addresses on screen.
- Welcome screen on first run; all settings apply instantly; export and import settings.
- Global show/hide hotkey, configurable click actions, start with Windows, daily update check.
- Command-line switches for every page plus `--run-speedtest` for scheduled tests.
- Per-user installer (no admin rights) and portable zip.

[Unreleased]: https://github.com/anand34577/winspeed/compare/v1.0.4...HEAD
[1.0.4]: https://github.com/anand34577/winspeed/compare/v1.0.3...v1.0.4
[1.0.3]: https://github.com/anand34577/winspeed/compare/v1.0.2...v1.0.3
[1.0.2]: https://github.com/anand34577/winspeed/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/anand34577/winspeed/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/anand34577/winspeed/releases/tag/v1.0.0
