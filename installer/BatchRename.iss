#define MyAppName "Batch Rename"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "LangBai"

[Setup]
AppId={{6A72C194-2ECF-4C53-A6BD-35B5F3336B01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\BatchRenameInstaller
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=BatchRename-Setup-{#MyAppVersion}-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\BatchRename.App\Assets\BatchRename.ico
UninstallDisplayName=Batch Rename (Explorer context menu)
VersionInfoVersion=1.0.0.0
VersionInfoDescription=Safe, previewable and undoable Explorer batch rename utility

[Tasks]
Name: "restartexplorer"; Description: "Restart File Explorer after installation so the context-menu command is available immediately"; Flags: checkedonce

[Files]
Source: "..\artifacts\package\BatchRename.msix"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\certificate\BatchRename.cer"; DestDir: "{app}"; Flags: ignoreversion
Source: "Install-Package.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Uninstall-Package.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Restart-Explorer.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Install-Package.ps1"" -PackagePath ""{app}\BatchRename.msix"" -CertificatePath ""{app}\BatchRename.cer"""; Flags: runhidden waituntilterminated; StatusMsg: "Registering the File Explorer context-menu command..."
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Restart-Explorer.ps1"""; Flags: runhidden waituntilterminated; Tasks: restartexplorer; StatusMsg: "Restarting File Explorer..."

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Uninstall-Package.ps1"" -CertificatePath ""{app}\BatchRename.cer"""; Flags: runhidden waituntilterminated; RunOnceId: "RemoveBatchRenamePackage"
