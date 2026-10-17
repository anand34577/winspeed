# Building from source

## Requirements

- Windows 10 or 11
- [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer. The SDK builds the `net48` target; the reference assemblies come from NuGet.
- Optional: [Inno Setup 6](https://jrsoftware.org/isdl.php) for the installer

## Build

```powershell
git clone https://github.com/anand34577/winspeed.git
cd winspeed
./build.ps1              # version from WinSpeed.csproj
./build.ps1 -Version 1.2.3
```

Output in `dist\`:

| File | |
|---|---|
| `app\WinSpeed.exe` | the app |
| `WinSpeed-Portable-<ver>.zip` | portable package |
| `WinSpeed-Setup-<ver>.exe` | installer (if Inno Setup is installed) |
| `SHA256SUMS.txt` | checksums |

For a quick development build: `dotnet build -c Release` → `bin\Release\net48\WinSpeed.exe`. Put an empty `portable` file next to it so your test runs keep their data in `bin\Release\net48\Data` instead of `%APPDATA%`.

## Project layout

```text
src/Program.cs            entry point, single instance, command line
src/TrayApp.cs            controller: sampling timer, tray, menu, stats, alerts, speed test, updates
src/NetMonitor.cs         per-adapter 64-bit counters → rates; virtual-adapter detection
src/SpeedOverlay.cs       the taskbar / floating meter (per-pixel-alpha layered window)
src/SpeedFormatter.cs     units, multipliers, label styles, decimals
src/UsageStore.cs         hourly per-adapter history: storage, queries, grouping
src/Services.cs           ping/offline monitor, speed test, public IP lookup, update checker
src/Settings.cs           settings model + settings.ini
src/SystemIntegration.cs  start with Windows, global hotkey
src/Native.cs             Win32 interop, taskbar detection, theme, DPI
src/TrayIcons.cs          app icon and live-speed tray icon
src/Forms/                main window, settings pages, welcome screen, themed controls, charts
installer/WinSpeed.iss    Inno Setup script
wiki/                     this user guide (published automatically)
site/                     the project website (GitHub Pages)
```

## Design principles

- **Small and fast.** No third-party runtime dependencies; .NET Framework 4.8 ships with Windows. Keep the exe tiny and idle CPU near zero.
- **Private by default.** Nothing leaves the PC unless the user asks for it, or for the documented ping and update check.
- **Instant settings.** Every option applies immediately and is documented in [Settings reference](Settings-Reference).

## How releases are made

1. Update the version in `WinSpeed.csproj` and add a section to `CHANGELOG.md`.
2. Merge to `main`. CI builds and smoke-tests every push and pull request.
3. Push a tag such as `v1.2.3`. The **Release** workflow builds the installer, portable zip and checksums, and publishes a GitHub release with the notes from `CHANGELOG.md`.

The wiki (`wiki/`) and website (`site/`) are published automatically when they change on `main`.
