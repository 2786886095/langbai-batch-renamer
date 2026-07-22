param(
    [Parameter(Mandatory = $true)][string]$UpgradePackage,
    [Parameter(Mandatory = $true)][string]$RestorePackage,
    [Parameter(Mandatory = $true)][string]$CurrentVersion,
    [Parameter(Mandatory = $true)][string]$UpgradeVersion
)

$ErrorActionPreference = "Stop"
$installed = Get-AppxPackage -Name "LangBai.BatchRename"
if (-not $installed) { throw "LangBai.BatchRename is not installed." }
if ($installed.Version.ToString() -ne $CurrentVersion)
{
    throw "Expected installed version $CurrentVersion, got $($installed.Version)."
}

$packageDataRoot = Join-Path $env:LOCALAPPDATA "Packages\$($installed.PackageFamilyName)"
$probeDirectory = Join-Path $packageDataRoot "LocalCache\Local\BatchRename"
New-Item -ItemType Directory -Path $probeDirectory -Force | Out-Null
$probePath = Join-Path $probeDirectory "codex-update-retention-probe-$([Guid]::NewGuid().ToString('N')).txt"
$probeValue = [Guid]::NewGuid().ToString("N")
[System.IO.File]::WriteAllText($probePath, $probeValue, [System.Text.Encoding]::UTF8)

$upgradePreserved = $false
$restorePreserved = $false
try
{
    Add-AppxPackage -Path $UpgradePackage -ForceApplicationShutdown -ForceUpdateFromAnyVersion
    $afterUpgrade = Get-AppxPackage -Name "LangBai.BatchRename"
    if ($afterUpgrade.Version.ToString() -ne $UpgradeVersion)
    {
        throw "Expected upgraded version $UpgradeVersion, got $($afterUpgrade.Version)."
    }
    $upgradePreserved = (Test-Path -LiteralPath $probePath) -and
        ([System.IO.File]::ReadAllText($probePath, [System.Text.Encoding]::UTF8) -eq $probeValue)
    if (-not $upgradePreserved) { throw "Package data was lost during the in-place update." }
}
finally
{
    Add-AppxPackage -Path $RestorePackage -ForceApplicationShutdown -ForceUpdateFromAnyVersion
    $afterRestore = Get-AppxPackage -Name "LangBai.BatchRename"
    if ($afterRestore.Version.ToString() -ne $CurrentVersion)
    {
        throw "Expected restored version $CurrentVersion, got $($afterRestore.Version)."
    }
    $restorePreserved = (Test-Path -LiteralPath $probePath) -and
        ([System.IO.File]::ReadAllText($probePath, [System.Text.Encoding]::UTF8) -eq $probeValue)
    if (Test-Path -LiteralPath $probePath) { Remove-Item -LiteralPath $probePath -Force }
}

if (-not $restorePreserved) { throw "Package data was lost while restoring the original package." }
[pscustomobject]@{
    StartVersion = $CurrentVersion
    UpgradeVersion = $UpgradeVersion
    UpgradePreserved = $upgradePreserved
    RestoredVersion = (Get-AppxPackage -Name "LangBai.BatchRename").Version.ToString()
    RestorePreserved = $restorePreserved
    ProbeRemoved = -not (Test-Path -LiteralPath $probePath)
} | ConvertTo-Json
