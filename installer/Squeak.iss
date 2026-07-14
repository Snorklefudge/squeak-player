; Inno Setup script for Squeak.
; Build the app first:
;   dotnet publish SqueakPlayer.csproj -c Release -r win-x64 --self-contained true -o publish
; Then compile this script (Inno Setup):  iscc installer\Squeak.iss

#define MyAppName "Squeak"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "kjedruszek"
#define MyAppExeName "Squeak.exe"

[Setup]
; Keep this AppId stable across versions so upgrades/uninstall work correctly.
AppId={{BAB90D0D-261C-4B1E-9807-E6A16407637C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\dist
OutputBaseFilename=Squeak-Setup-{#MyAppVersion}
SetupIconFile=..\icon.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The entire self-contained publish output (Squeak.exe, .NET runtime, libvlc, ...).
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipwaitforidle
