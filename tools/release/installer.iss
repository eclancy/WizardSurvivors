; Wizard Survivors Windows installer (Inno Setup 6). Built by tools/release/release.sh:
;
;   ISCC.exe -DAppVersion=0.2.1 tools\release\installer.iss
;
; Packs the export in build\windows\ into build\release\WizardSurvivors-Setup.exe.
;
; Per-user, no admin prompt: it installs to %LOCALAPPDATA%\Programs\WizardSurvivors, which the
; player can write to - and must, because the game updates itself by robocopying new files into
; its own folder (scripts/UpdateScreen.cs). Program Files would leave it stuck on its version.
; See .ai/releasing.md.

#ifndef AppVersion
  #error Pass the version: ISCC -DAppVersion=X.Y.Z tools\release\installer.iss
#endif

[Setup]
; Never change this GUID: it is how Windows knows a new setup is the same program, so it upgrades
; in place instead of installing a second copy.
AppId={{B8EAA8FA-C3BC-4790-ACE3-438B4BC1F103}
AppName=Wizard Survivors
AppVersion={#AppVersion}
; Apps & features shows the name alone; the version has its own column there.
UninstallDisplayName=Wizard Survivors
AppPublisher=ECEC
AppPublisherURL=https://ecec.dev/games/wizard-survivors
; Paths below are relative to the repo root.
SourceDir=..\..
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\WizardSurvivors
DisableProgramGroupPage=yes
DisableDirPage=auto
UsePreviousAppDir=yes
WizardStyle=modern
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\WizardSurvivors.exe
OutputDir=build\release
OutputBaseFilename=WizardSurvivors-Setup
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; The export only. "How to play.txt" is written into the zip by package.py, never into
; build\windows, so it is not picked up here.
Source: "build\windows\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\Wizard Survivors"; Filename: "{app}\WizardSurvivors.exe"
Name: "{autodesktop}\Wizard Survivors"; Filename: "{app}\WizardSurvivors.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\WizardSurvivors.exe"; Description: "Play Wizard Survivors now"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
; The whole folder, including files the self-updater added. The save lives in
; %APPDATA%\Godot\app_userdata\Wizard Survivors\ and is deliberately left alone.
Type: filesandordirs; Name: "{app}"
