#define AppVersion "0.1.3"
[Setup]
AppId={{F6D68F63-F784-46CD-ACFD-72C3119C7D51}
AppName=BatteryGuard
AppVersion={#AppVersion}
AppPublisher=Artllex
AppPublisherURL=https://github.com/Artllex/battery-guard
DefaultDirName={localappdata}\Programs\BatteryGuard
DefaultGroupName=BatteryGuard
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=BatteryGuard-Setup-{#AppVersion}
SetupIconFile=..\assets\BatteryGuard-high.ico
UninstallDisplayIcon={app}\BatteryGuard.exe
UninstallDisplayName=BatteryGuard
Uninstallable=yes
AppMutex=Local\BatteryGuard60
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Uruchamiaj BatteryGuard po zalogowaniu"; Flags: unchecked
Name: "desktopicon"; Description: "Utwórz skrót na pulpicie"; Flags: unchecked

[Files]
Source: "..\dist\BatteryGuard.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\BatteryGuard"; Filename: "{app}\BatteryGuard.exe"
Name: "{userdesktop}\BatteryGuard"; Filename: "{app}\BatteryGuard.exe"; Tasks: desktopicon
Name: "{userstartup}\BatteryGuard"; Filename: "{app}\BatteryGuard.exe"; Tasks: startup

[Run]
Filename: "{app}\BatteryGuard.exe"; Description: "Uruchom BatteryGuard"; Flags: nowait postinstall skipifsilent

[Code]
function OpenEvent(Access: LongWord; Inherit: Boolean; Name: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(Event: THandle): Boolean;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

procedure StopMonitor;
var Event: THandle; Attempt: Integer;
begin
  Event := OpenEvent(2, False, 'Local\BatteryGuard60Shutdown');
  if Event <> 0 then begin
    SetEvent(Event);
    CloseHandle(Event);
    for Attempt := 1 to 40 do begin
      if not CheckForMutexes('Local\BatteryGuard60') then Break;
      Sleep(250);
    end;
  end;
end;

function InitializeSetup: Boolean;
begin
  StopMonitor;
  Result := True;
end;

function InitializeUninstall: Boolean;
begin
  StopMonitor;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var FileName: String; Contents: AnsiString;
begin
  if CurUninstallStep = usUninstall then begin
    FileName := ExpandConstant('{userstartup}\BatteryGuard60.vbs');
    if LoadStringFromFile(FileName, Contents) then
      if Pos(Lowercase(ExpandConstant('{app}\BatteryGuard.exe')), Lowercase(String(Contents))) > 0 then
        DeleteFile(FileName);
  end;
end;
