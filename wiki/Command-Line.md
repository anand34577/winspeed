# Command line

```text
WinSpeed.exe                   Start WinSpeed (or bring up the window if it's already running)
WinSpeed.exe --overview        Open the main window on the Overview page
WinSpeed.exe --speedtest       Open the Speed test page
WinSpeed.exe --run-speedtest   Open the Speed test page and start a test
WinSpeed.exe --usage           Open Data usage
WinSpeed.exe --connection      Open Connection (adapters, public IP)
WinSpeed.exe --meter           Open Meter & display
WinSpeed.exe --settings        Open Settings
WinSpeed.exe --about           Open About
WinSpeed.exe --exit            Close the running copy (it saves its data first)
WinSpeed.exe --startup         Used by "Start with Windows" (starts quietly)
```

Only one copy of WinSpeed runs at a time. If it's already running, these switches tell the running copy what to do, so they work well in shortcuts, scripts and launchers.

## Schedule a daily speed test

Use Windows **Task Scheduler** to log your connection speed automatically. Results land in the speed-test history like any other test.

PowerShell:

```powershell
$exe = "$env:LOCALAPPDATA\Programs\WinSpeed\WinSpeed.exe"
$action  = New-ScheduledTaskAction -Execute $exe -Argument "--run-speedtest"
$trigger = New-ScheduledTaskTrigger -Daily -At 9pm
Register-ScheduledTask -TaskName "WinSpeed daily speed test" -Action $action -Trigger $trigger
```

To remove it: `Unregister-ScheduledTask -TaskName "WinSpeed daily speed test"`.

## Keyboard

| Shortcut | Action |
|---|---|
| **Ctrl + Alt + N** (default) | Show or hide the taskbar meter. Change it under Settings → Mouse, keyboard & tray. |
