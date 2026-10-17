# Settings reference

Everything here can be changed in the app. Settings are also saved as plain `key=value` lines in **`settings.ini`** in the data folder (`%APPDATA%\WinSpeed`, or `Data\` in [portable mode](Portable-Mode)). If you edit the file by hand, exit WinSpeed first. Deleting the file resets everything to defaults.

**Backup:** Settings → History, backup & reset → **Export settings…** / **Import settings…** saves or loads a copy, which is handy for moving to a new PC.

Colours are written as `#AARRGGBB`.

## General

| Key | Default | In the app |
|---|---|---|
| `Theme` | `System` | App theme: `System`, `Light`, `Dark` |
| `UpdateIntervalMs` | `1000` | Update the meter every… (250–10000 ms) |
| `Smoothing` | `1` | Moving average over N samples (1 = off) |
| `CheckForUpdates` | `true` | Check GitHub for new versions once a day |
| `PrivacyMode` | `false` | Mask IP and MAC addresses on screen |
| `DoubleClickAction` | `Dashboard` | `Dashboard`, `DataUsage`, `SpeedTest`, `Settings`, `ToggleMeter`, `None` |
| `TrayClickAction` | `Dashboard` | Same choices, for a left-click on the tray icon |
| `Hotkey` | `Ctrl+Alt+N` | Show/hide hotkey, e.g. `Ctrl+Shift+F9`; empty = none |
| `TrayIcon` | `Static` | `Static` (app icon), `Speed` (live numbers), `Hidden` |

## Meter: what to show

| Key | Default | Notes |
|---|---|---|
| `ShowOverlay` | `true` | Show the taskbar meter |
| `Mode` | `Separate` | `Separate`, `Combined`, `SeparateAndTotal`, `DownloadOnly`, `UploadOnly` |
| `Layout` | `TwoLines` | `TwoLines` (stacked) or `SingleLine` |
| `UploadFirst` | `false` | Upload above / before download |
| `ShowPingOnMeter` | `false` | Add latency to the meter |
| `MeterGraph` | `false` | Mini graph behind the numbers |
| `ShowOfflineDot` | `true` | Red dot when offline |

## Meter: units and labels

| Key | Default | Notes |
|---|---|---|
| `Unit` | `AutoBytes` | `AutoBytes`, `AutoBits`, `Bytes`, `KiloBytes`, `MegaBytes`, `GigaBytes`, `Bits`, `KiloBits`, `MegaBits`, `GigaBits` |
| `UnitBase` | `1024` | `1024` or `1000` |
| `Style` | `Standard` | `Standard`, `Compact`, `Iec` |
| `Decimals` | `-1` | `-1` = automatic, or `0`–`3` |
| `AutoMinUnit` | `Kilo` | Smallest automatic unit: `None` (B/s), `Kilo`, `Mega` |
| `ShowUnit` | `true` | |
| `ShowLabels` | `true` | |
| `DownloadLabel` / `UploadLabel` / `TotalLabel` | `↓` / `↑` / `⇅` | Any text |

## Meter: look

| Key | Default | Notes |
|---|---|---|
| `FontFamily` / `FontSize` / `FontBold` | `Segoe UI` / `9` / `false` | |
| `AutoTextColor` | `true` | Follow the light/dark taskbar |
| `DownloadColor` / `UploadColor` / `TotalColor` | blue / orange / green | |
| `BackgroundColor` / `BackgroundOpacity` | dark grey / `0` | Opacity 0–100 % |
| `HighlightEnabled` / `HighlightKBps` / `HighlightColor` | `false` / `5120` / red | Change colour at ≥ N KB/s |
| `HideBelowKBps` | `0` | Hide the meter below N KB/s (0 = never) |

## Meter: position

| Key | Default | Notes |
|---|---|---|
| `Placement` | `TaskbarRight` | `TaskbarRight`, `TaskbarLeft`, `Floating` |
| `OffsetX` / `OffsetY` | `0` | Pixel offset on the taskbar (set by dragging) |
| `FloatX` / `FloatY` | `-1` | Floating window position (`-1` = automatic) |
| `LockPosition` | `false` | Disable dragging |
| `HideOnFullscreen` | `true` | Hide during full-screen apps |
| `ClickThrough` | `false` | Mouse clicks pass through |

## Connection monitoring

| Key | Default | Notes |
|---|---|---|
| `PingEnabled` | `true` | Latency and offline detection |
| `PingHost` | `1.1.1.1` | Host to ping |
| `PingIntervalSec` | `5` | 1–300 |
| `NotifyConnection` | `true` | Notify on disconnect / reconnect |
| `NotifySpeedTestDone` | `true` | Notify when a speed test finishes |

## Adapters

| Key | Default | Notes |
|---|---|---|
| `Interfaces` | `Auto` | `Auto` or `Selected` |
| `SelectedInterfaces` | *(empty)* | Adapter GUIDs separated by `;` (used in `Selected` mode) |
| `IncludeVirtual` | `false` | Count virtual adapters in `Auto` mode |

## Data plan and history

| Key | Default | Notes |
|---|---|---|
| `MonthlyCapMB` | `0` | Monthly limit in MB (0 = none) |
| `DailyCapMB` | `0` | Daily limit in MB (0 = none) |
| `BillingDay` | `1` | 1–28 |
| `AlertPercent` | `90` | First warning at this percentage |
| `RetentionDays` | `400` | Days of hourly history to keep (0 = forever) |

## Internal (managed by the app)

`FirstRun`, `LastUpdateCheck`, `SkippedVersion` and `AlertState` record state such as which alerts were already shown. You don't need to edit them.
