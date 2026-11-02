#define AppName "Rift Coach"
#define AppExeName "LoLCoach.exe"
#define PublishDir "..\artifacts\installer-publish"
#ifndef AppVersion
  #define AppVersion "local"
#endif

[Setup]
AppId={{3C817D55-5D3C-4D64-91AF-57A41A420FE3}
AppName={#AppName}
AppVersion=Build {#AppVersion}
DefaultDirName={localappdata}\Programs\Rift Coach
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=..\artifacts\installer
OutputBaseFilename=RiftCoach-Setup-win-x64
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
PrivilegesRequired=lowest
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Iniciar {#AppName}"; Flags: postinstall nowait skipifsilent