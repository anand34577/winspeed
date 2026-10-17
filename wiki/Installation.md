# Installation

WinSpeed runs on **Windows 10 and Windows 11**. It needs no runtime downloads: it is a single exe that uses .NET Framework (4.6.2 or later), which is already part of Windows 10 version 1607 and later and every version of Windows 11. It runs on 64-bit, 32-bit and ARM PCs.

## Download

Go to the **[latest release](https://github.com/anand34577/winspeed/releases/latest)** and pick one:

| File | Best for |
|---|---|
| `WinSpeed-Setup-x.y.z.exe` | Most people. Installs for your user account; no admin rights needed. |
| `WinSpeed-Portable-x.y.z.zip` | USB sticks, locked-down PCs, or trying it out without installing anything. |
| `SHA256SUMS.txt` | Optional: checksums to verify the downloads. |

## Using the installer

1. Run `WinSpeed-Setup-x.y.z.exe`.
2. Choose the options you want:
   - **Start WinSpeed automatically when I sign in to Windows** (recommended)
   - **Create a desktop shortcut**
3. Click **Install**. WinSpeed starts straight away and shows a short welcome screen.

WinSpeed installs to `%LOCALAPPDATA%\Programs\WinSpeed` and adds Start-menu shortcuts for **WinSpeed**, **Settings**, **Data Usage** and **Speed Test**.

### "Windows protected your PC"

The installer isn't code-signed yet, so Microsoft Defender SmartScreen may warn you the first time. Click **More info → Run anyway**. Every release is built from the public source code by GitHub Actions, and `SHA256SUMS.txt` lets you check that the file wasn't changed. To check:

```powershell
Get-FileHash .\WinSpeed-Setup-1.0.0.exe -Algorithm SHA256
```

## Using the portable zip

1. Extract the zip to any folder, for example `D:\Tools\WinSpeed`.
2. Run `WinSpeed.exe`.

The zip contains a file named `portable`. While that file is there, settings and history are stored in a `Data` folder next to the exe instead of your user profile. See [Portable mode](Portable-Mode).

## Updating

WinSpeed checks GitHub for new versions once a day and shows a notification when one is available. You can turn this off under **Settings → General**. To update, just run the new installer: it closes the running copy, replaces it and keeps all your settings and history.

You can also check by hand at any time: **About → Check for updates**.

## Uninstalling

Use **Settings → Apps → Installed apps → WinSpeed → Uninstall** in Windows, or the **Uninstall WinSpeed** Start-menu shortcut. The uninstaller asks whether to delete your settings and usage history as well. If you say no, they stay in `%APPDATA%\WinSpeed`.

For the portable version, exit WinSpeed (right-click the meter → **Exit**) and delete the folder.
