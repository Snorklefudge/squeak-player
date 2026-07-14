; Inno Setup script for Squeak (framework-dependent build).
; Build the app first:
;   dotnet publish SqueakPlayer.csproj -c Release -r win-x64 --self-contained false -o publish
; Then compile this script (Inno Setup 6.1+):
;   iscc installer\Squeak.iss
; CI passes the version, e.g.:  iscc /DMyAppVersion=1.2.3 installer\Squeak.iss

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "Squeak"
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
; The framework-dependent publish output (Squeak.exe, app dlls, libvlc, ...).
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipwaitforidle

[Code]
const
  DotNetUrl = 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe';

// True if a Microsoft.WindowsDesktop.App 8.x runtime is already installed.
function DesktopRuntimeInstalled(): Boolean;
var
  FindRec: TFindRec;
  BaseDir: String;
begin
  Result := False;
  BaseDir := ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if FindFirst(BaseDir + '\8.*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          Result := True;
          Break;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;
end;

// Runs before files are copied: fetch + install the .NET Desktop Runtime if needed.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if DesktopRuntimeInstalled() then
    Exit;

  try
    DownloadTemporaryFile(DotNetUrl, 'windowsdesktop-runtime.exe', '', @OnDownloadProgress);
  except
    Result := 'Squeak needs the .NET 8 Desktop Runtime, but it could not be downloaded automatically.' + #13#10 +
              'Please install it from https://dotnet.microsoft.com/download/dotnet/8.0 and run this installer again.';
    Exit;
  end;

  if Exec(ExpandConstant('{tmp}\windowsdesktop-runtime.exe'), '/install /quiet /norestart', '',
          SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 3010 then
      NeedsRestart := True
    else if ResultCode <> 0 then
      Result := 'The .NET 8 Desktop Runtime installer failed (exit code ' + IntToStr(ResultCode) + ').';
  end
  else
    Result := 'Could not launch the .NET 8 Desktop Runtime installer.';
end;
