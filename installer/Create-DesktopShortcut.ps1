param(
    [Parameter(Mandatory = $true)][string]$IconPath,
    [string]$DesktopDirectory = ""
)
$ErrorActionPreference = "Stop"
$package = Get-AppxPackage -Name "LangBai.BatchRename" | Select-Object -First 1
if (-not $package) { throw "The installed LangBai.BatchRename package was not found." }

$desktop = if ([string]::IsNullOrWhiteSpace($DesktopDirectory))
{
    [Environment]::GetFolderPath("CommonDesktopDirectory")
}
else
{
    [IO.Path]::GetFullPath($DesktopDirectory)
}
[IO.Directory]::CreateDirectory($desktop) | Out-Null
$shortcutName = (-join @([char]0x6D6A, [char]0x767D, [char]0x91CD, [char]0x547D, [char]0x540D, [char]0x5DE5, [char]0x5177)) + ".lnk"
$shortcutPath = Join-Path $desktop $shortcutName
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $env:WINDIR "explorer.exe"
$shortcut.Arguments = "shell:AppsFolder\$($package.PackageFamilyName)!BatchRename"
$shortcut.IconLocation = "$IconPath,0"
$shortcut.Description = -join @([char]0x6253, [char]0x5F00, [char]0x6D6A, [char]0x767D, [char]0x91CD, [char]0x547D, [char]0x540D, [char]0x5DE5, [char]0x5177)
$shortcut.Save()
