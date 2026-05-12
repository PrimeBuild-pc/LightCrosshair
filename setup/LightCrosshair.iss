; LightCrosshair Installer (requires .NET 8.0 Desktop Runtime installed)
[Setup]
AppName=LightCrosshair
AppVersion=1.6.0
DefaultDirName={autopf}\LightCrosshair
DefaultGroupName=LightCrosshair
OutputBaseFilename=LightCrosshair-Setup-1.6.0
Compression=lzma
SolidCompression=yes
DisableDirPage=no
DisableProgramGroupPage=yes
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "..\releases\x64\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\LightCrosshair"; Filename: "{app}\LightCrosshair.exe"
Name: "{autodesktop}\LightCrosshair"; Filename: "{app}\LightCrosshair.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional tasks:"
Name: "runtimelink"; Description: "Open .NET 8.0 Desktop Runtime download page (required if not installed)"; GroupDescription: "Additional tasks:"; Flags: unchecked

[Run]
Filename: "{app}\LightCrosshair.exe"; Description: "Launch LightCrosshair"; Flags: nowait postinstall skipifsilent runasoriginaluser
Filename: "https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime"; Description: "Open .NET 8.0 Desktop Runtime download page"; Flags: postinstall shellexec skipifsilent runasoriginaluser; Tasks: runtimelink
