# Troubleshooting & FAQ

## The meter isn't visible

- **Is it switched off?** Press **Ctrl + Alt + N**, or right-click the tray icon and tick **Show speed meter**.
- **Is the taskbar set to auto-hide?** The meter hides along with the taskbar; hover the bottom of the screen.
- **Is a full-screen app in front?** The meter hides during full-screen apps. Turn that off in Meter & display → Position.
- **Is "Hide meter below" set?** The meter then only appears when there is traffic.
- **Has it been moved off-screen?** Right-click the tray icon → **Position → Reset position**.
- **Can't find the tray icon?** On Windows 11 it may be in the overflow (^) area. See [The taskbar meter → tray icon](The-Taskbar-Meter#the-tray-icon).

## The meter covers part of the taskbar

Drag it to a free spot, or set an X offset in Meter & display → Position. If your taskbar has lots of pinned apps, try **Side by side** layout with a smaller font, or **Taskbar, left side**.

## The meter sometimes appears behind the taskbar

When you click the taskbar, Windows briefly raises it. WinSpeed brings itself back on top at every update (once a second by default). If you see this often, lower the update interval.

## The speeds look too high / too low

- **Double numbers with a VPN?** Make sure virtual adapters aren't counted (Settings → Network adapters → Automatic, with *Also count virtual adapters* off).
- **MB/s vs Mbps?** 100 Mbps ≈ 12 MB/s. See [Units explained](Units-Explained).
- **Different from my ISP's speed test?** The meter shows the traffic *this PC* is actually moving right now, not your line's maximum. Use the built-in [Speed test](Speed-Test) to measure capacity.

## Data usage doesn't match my bill

WinSpeed only sees this PC, and only while it's running. Phones, TVs and other devices on your connection aren't included. See [Data limits & alerts → Accuracy](Data-Limits-and-Alerts#accuracy).

## It says I'm offline but the internet works

Some networks block ping. Set **Ping host** to your router's address (often `192.168.1.1` or `192.168.0.1`) or another host that answers, or turn off connection monitoring.

## The hotkey doesn't work

Another app may already use it; WinSpeed shows a notification if it couldn't register it. Choose a different combination under Settings → Mouse, keyboard & tray.

## SmartScreen warns about the installer

The installer isn't code-signed yet. Click **More info → Run anyway**, or verify the file against `SHA256SUMS.txt` from the release. See [Installation](Installation#windows-protected-your-pc).

## How much memory and CPU does it use?

About 15–25 MB of RAM and well under 1 % CPU while running in the background. The main window uses a little more while it's open.

## Does it work on Windows 7 or 8?

It's built and tested for Windows 10 and 11. It may start on 8.1 with .NET Framework 4.8 installed, but that isn't supported.

## Where do I report a bug?

[Open an issue](https://github.com/anand34577/winspeed/issues/new/choose) and attach `error.log` from the data folder if there is one (About → Open data folder).
