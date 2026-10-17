# Portable mode

Portable mode keeps WinSpeed and all its data in one folder, which suits a USB stick or a PC where you don't want to install anything.

## How it works

WinSpeed is portable when a file named **`portable`** (or `portable.txt`) is next to `WinSpeed.exe`. The portable zip from the [releases page](https://github.com/anand34577/winspeed/releases/latest) already includes it.

| | Installed | Portable |
|---|---|---|
| Program | `%LOCALAPPDATA%\Programs\WinSpeed` | wherever you unzip it |
| Settings and history | `%APPDATA%\WinSpeed` | `Data\` next to the exe |
| Start with Windows | optional | optional (points to the folder's exe) |
| Updates | run the new installer | replace `WinSpeed.exe` with the new one |

## Switching

- **Installed → portable:** copy `WinSpeed.exe` to a folder, create an empty file named `portable` next to it, and copy the contents of `%APPDATA%\WinSpeed` into `Data\`.
- **Portable → installed:** run the installer, then copy the contents of `Data\` into `%APPDATA%\WinSpeed` while WinSpeed is closed.

## Data folder contents

| File | Contents |
|---|---|
| `settings.ini` | All settings (see [Settings reference](Settings-Reference)) |
| `usage.tsv` | Hourly traffic per adapter |
| `interfaces.tsv` | Adapter names |
| `speedtests.tsv` | Speed test history |
| `error.log` | Only exists if something went wrong; attach it to bug reports |
