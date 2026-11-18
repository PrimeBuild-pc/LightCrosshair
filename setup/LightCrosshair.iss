; LightCrosshair Installer (requires .NET 10 Desktop Runtime installed)
#ifndef AppVersion
  #define AppVersion "2.0.1"
#endif
#ifndef SourceDir
  #define SourceDir "..\releases\LightCrosshair-v2.0.1-win-x64"
#endif
[Setup]
AppId=LightCrosshair
AppName=LightCrosshair
AppVersion={#AppVersion}
PrivilegesRequired=admin
DefaultDirName={autopf}\LightCrosshair
DefaultGroupName=LightCrosshair
OutputBaseFilename=LightCrosshair-Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes
DisableDirPage=no
DisableProgramGroupPage=yes
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\LightCrosshair"; Filename: "{app}\LightCrosshair.exe"
Name: "{autodesktop}\LightCrosshair"; Filename: "{app}\LightCrosshair.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional tasks:"
Name: "runtimelink"; Description: "Open .NET 10 Desktop Runtime download page (required if not installed)"; GroupDescription: "Additional tasks:"; Flags: unchecked

[Run]
Filename: "{app}\LightCrosshair.exe"; Description: "Launch LightCrosshair"; Flags: nowait postinstall skipifsilent runasoriginaluser
Filename: "https://dotnet.microsoft.com/en-us/download/dotnet/10.0/runtime"; Description: "Open .NET 10 Desktop Runtime download page"; Flags: postinstall shellexec skipifsilent runasoriginaluser; Tasks: runtimelink
