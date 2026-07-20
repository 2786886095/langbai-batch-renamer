#define MyAppName "浪白重命名工具"
#ifndef MyAppVersion
  #define MyAppVersion "1.1.2"
#endif
#define MyAppPublisher "LangBai"

[Setup]
AppId={{6A72C194-2ECF-4C53-A6BD-35B5F3336B01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\浪白重命名工具
DisableDirPage=no
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
UninstallDisplayName=浪白重命名工具
VersionInfoVersion={#MyAppVersion}.0
VersionInfoDescription=安全、可预览、可回退的资源管理器批量重命名工具

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl"

[Messages]
chinesesimp.SetupWindowTitle=安装 - %1
chinesesimp.WelcomeLabel1=欢迎使用 [name] 安装向导
chinesesimp.WelcomeLabel2=此向导将把 [name] 安装到您的电脑。%n%n建议在继续安装前关闭其他应用程序。
chinesesimp.ClickNext=点击“下一步”继续，或点击“取消”退出安装程序。
chinesesimp.WizardSelectDir=选择安装位置
chinesesimp.SelectDirDesc=请选择“浪白重命名工具”的安装位置。
chinesesimp.SelectDirLabel3=程序将安装到下列文件夹。若要选择其他文件夹，请点击“浏览”。
chinesesimp.SelectDirBrowseLabel=点击“下一步”继续。若要选择其他文件夹，请点击“浏览”。
chinesesimp.DiskSpaceMBLabel=至少需要 [mb] MB 可用磁盘空间。
chinesesimp.WizardSelectTasks=选择附加任务
chinesesimp.SelectTasksDesc=请选择安装时需要执行的附加任务。
chinesesimp.SelectTasksLabel2=请选择需要执行的任务，然后点击“下一步”。
chinesesimp.WizardReady=准备安装
chinesesimp.ReadyLabel1=安装程序已经准备好开始安装“浪白重命名工具”。
chinesesimp.ReadyLabel2a=点击“安装”继续。如果要查看或更改设置，请点击“上一步”。
chinesesimp.ReadyMemoDir=安装位置：
chinesesimp.ReadyMemoTasks=附加任务：
chinesesimp.WizardInstalling=正在安装
chinesesimp.InstallingLabel=请稍候，安装程序正在安装“浪白重命名工具”。
chinesesimp.FinishedHeadingLabel=安装完成
chinesesimp.FinishedLabelNoIcons=“浪白重命名工具”已安装到此电脑。
chinesesimp.ButtonNext=下一步(&N) >
chinesesimp.ButtonBack=< 上一步(&B)
chinesesimp.ButtonInstall=安装(&I)
chinesesimp.ButtonCancel=取消
chinesesimp.ButtonFinish=完成(&F)
chinesesimp.ButtonBrowse=浏览(&B)...
chinesesimp.ButtonWizardBrowse=浏览(&B)...
chinesesimp.BrowseDialogTitle=选择文件夹
chinesesimp.BrowseDialogLabel=请选择一个文件夹，然后点击“确定”。
chinesesimp.ErrorTitle=错误
chinesesimp.ExitSetupTitle=退出安装程序
chinesesimp.ExitSetupMessage=安装尚未完成。如果现在退出，程序将不会安装。%n%n确定要退出吗？

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: checkedonce
Name: "restartexplorer"; Description: "安装完成后重启文件资源管理器，使右键菜单立即生效"; Flags: checkedonce

[Files]
Source: "..\artifacts\package\BatchRename.msix"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\certificate\BatchRename.cer"; DestDir: "{app}"; Flags: ignoreversion
Source: "Install-Package.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Uninstall-Package.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Restart-Explorer.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Create-DesktopShortcut.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\BatchRename.App\Assets\BatchRename.ico"; DestDir: "{app}"; Flags: ignoreversion

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Install-Package.ps1"" -PackagePath ""{app}\BatchRename.msix"" -CertificatePath ""{app}\BatchRename.cer"""; Flags: runhidden waituntilterminated; StatusMsg: "正在注册文件资源管理器右键菜单..."
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Create-DesktopShortcut.ps1"" -IconPath ""{app}\BatchRename.ico"""; Flags: runhidden waituntilterminated; Tasks: desktopicon; StatusMsg: "正在创建桌面快捷方式..."
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Restart-Explorer.ps1"""; Flags: runhidden waituntilterminated; Tasks: restartexplorer; StatusMsg: "正在重启文件资源管理器..."

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\Uninstall-Package.ps1"" -CertificatePath ""{app}\BatchRename.cer"""; Flags: runhidden waituntilterminated; RunOnceId: "RemoveBatchRenamePackage"

[UninstallDelete]
Type: files; Name: "{commondesktop}\浪白重命名工具.lnk"
