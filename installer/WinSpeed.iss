; Inno Setup script for WinSpeed - per-user install, no admin rights required.
; Build with:  build.ps1   (or: ISCC.exe installer\WinSpeed.iss)

#define AppName "WinSpeed"
#ifndef AppVersion
  #define AppVersion "1.0.3"
#endif
#define AppExe "WinSpeed.exe"
#define AppPublisher "Anand"
#define AppURL "https://github.com/anand34577/winspeed"

[Setup]
AppId={{6B0E3B8E-2C7A-4D2B-9C55-5E1C0A7F3D21}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
DisableReadyPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=WinSpeed-Setup-{#AppVersion}
SetupIconFile=..\assets\WinSpeed.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName} - Internet Speed Meter
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0
CloseApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} Setup

[Tasks]
Name: "startup"; Description: "Start {#AppName} automatically when I sign in to Windows"; GroupDescription: "Options:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Options:"; Flags: unchecked

[Files]
Source: "..\dist\app\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\app\{#AppExe}.config"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; Parameters: "--dashboard"
Name: "{group}\{#AppName} Settings"; Filename: "{app}\{#AppExe}"; Parameters: "--settings"
Name: "{group}\{#AppName} Data Usage"; Filename: "{app}\{#AppExe}"; Parameters: "--usage"
Name: "{group}\{#AppName} Speed Test"; Filename: "{app}\{#AppExe}"; Parameters: "--speedtest"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Parameters: "--dashboard"; Tasks: desktopicon

[Registry]
; same value the app's "Start with Windows" option writes
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExe}"" --startup"; Tasks: startup; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "{#AppName}"; \
  Flags: deletevalue uninsdeletevalue; Tasks: not startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName} now"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; let the running copy save its data and quit cleanly
Filename: "{app}\{#AppExe}"; Parameters: "--exit"; Flags: runhidden waituntilterminated; RunOnceId: "StopWinSpeed"

[UninstallDelete]
Type: files; Name: "{app}\portable"

[Code]
// WinSpeed needs .NET Framework 4.6.2 or later, which every supported Windows 10 and 11 already includes.
// Only the very first Windows 10 builds (1507/1511) without updates lack it.
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := IsDotNetInstalled(net462, 0);
  if not Result then
  begin
    if SuppressibleMsgBox('WinSpeed needs Microsoft .NET Framework 4.6.2 or later, which is normally part of Windows.' #13#10#13#10 +
         'Install the latest Windows updates, or download .NET Framework 4.8 from Microsoft, then run this setup again.' #13#10#13#10 +
         'Open the download page now?', mbError, MB_YESNO, IDNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net48', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  // upgrading: stop the running copy so the exe can be replaced
  if FileExists(ExpandConstant('{app}\{#AppExe}')) then
    Exec(ExpandConstant('{app}\{#AppExe}'), '--exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{userappdata}\{#AppName}');
    if DirExists(DataDir) then
      if MsgBox('Also delete your WinSpeed settings and data usage history?' #13#10 + DataDir,
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
  end;
end;
