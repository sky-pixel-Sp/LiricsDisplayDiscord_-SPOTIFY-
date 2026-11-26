#define MyAppName "SkyLyrics Desktop"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "SkyLyrics"
#define MyAppExeName "SkyLyrics.Desktop.exe"

[Setup]
AppId={{7C4E2E2B-1A8F-4B63-AE32-0D9D7C76C6A1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\SkyLyrics Desktop
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\artifacts\installer
OutputBaseFilename=SkyLyrics-Desktop-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
Uninstallable=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\SkyLyrics-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SkyLyrics Desktop"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\SkyLyrics Desktop"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch SkyLyrics Desktop"; Flags: postinstall nowait skipifsilent
