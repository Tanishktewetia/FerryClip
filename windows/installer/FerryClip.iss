#define AppName "FerryClip"
#define AppPublisher "Tanishk Tewetia"
#ifndef SourceExe
  #define SourceExe AddBackslash(SourcePath) + "..\..\dist\FerryClip-win-x64.exe"
#endif
#ifndef AppVersion
  #define AppVersion GetFileVersionString(SourceExe)
#endif
#ifndef OutputDir
  #define OutputDir AddBackslash(SourcePath) + "..\..\dist"
#endif

[Setup]
AppId={{08316783-CFA6-4013-938B-597A7E6459EC}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://www.ferryclip.app
AppSupportURL=https://github.com/Tanishktewetia/FerryClip/issues
AppUpdatesURL=https://github.com/Tanishktewetia/FerryClip/releases
DefaultDirName={localappdata}\Programs\FerryClip
DefaultGroupName=FerryClip
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=FerryClip-Windows-Setup
SetupIconFile=..\ClipSync.Windows\Assets\ferryclip.ico
UninstallDisplayIcon={app}\FerryClip.exe
VersionInfoProductName=FerryClip
VersionInfoProductVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright=Copyright (c) 2026 Tanishk Tewetia
LicenseFile=..\..\LICENSE
CloseApplications=yes
CloseApplicationsFilter=FerryClip.exe
RestartApplications=no
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Start FerryClip when Windows starts"; GroupDescription: "Additional options:"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "FerryClip.exe"; Flags: ignoreversion
Source: "..\..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\FerryClip"; Filename: "{app}\FerryClip.exe"
Name: "{autodesktop}\FerryClip"; Filename: "{app}\FerryClip.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ClipSync"; ValueData: """{app}\FerryClip.exe"" --startup"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\FerryClip.exe"; Description: "Launch FerryClip"; Flags: postinstall nowait skipifsilent

[Code]
var
  DeleteFerryClipData: Boolean;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  DeleteFerryClipData := False;
  if CheckForMutexes('ClipSync-SingleInstance-48653') then
  begin
    if not UninstallSilent then
      MsgBox('FerryClip is still running. Close FerryClip normally, then run uninstall again.', mbError, MB_OK);
    Result := False;
    Exit;
  end;
  if not UninstallSilent then
    DeleteFerryClipData := MsgBox(
      'Do you also want to delete FerryClip saved data? This removes settings, pairing information, clipboard history, and logs from this Windows user account. You will need to pair your phone again.',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    { The app and installer share this exact startup entry; remove it either way. }
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ClipSync');
    if DeleteFerryClipData then
    begin
      DelTree(ExpandConstant('{localappdata}\FerryClip'), True, True, True);
      DelTree(ExpandConstant('{userappdata}\ClipSync'), True, True, True);
    end;
  end;
end;
