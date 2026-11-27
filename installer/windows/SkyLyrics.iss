#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif
#define AppName "SkyLyrics Desktop"
#define AppExeName "SkyLyrics.Desktop.exe"

[Setup]
AppId={{3D1A2B90-7B37-4BA2-9C45-2C1ED3A6E711}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=SkyLyrics
DefaultDirName={localappdata}\Programs\SkyLyrics Desktop
DefaultGroupName=SkyLyrics Desktop
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\..\src\SkyLyrics.Desktop\Assets\SkyLyrics.ico
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=..\..\artifacts
OutputBaseFilename=SkyLyricsDesktop-Setup-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
Uninstallable=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\SkyLyrics Desktop"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\SkyLyrics Desktop"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch SkyLyrics Desktop"; Flags: postinstall nowait skipifsilent
