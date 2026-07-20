param([Parameter(Mandatory = $true)][string]$PackagePath, [Parameter(Mandatory = $true)][string]$CertificatePath)
$ErrorActionPreference = "Stop"
$certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CertificatePath)
if (-not (Test-Path "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)"))
{
    Import-Certificate -FilePath $CertificatePath -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
}
$existing = Get-AppxPackage -Name "LangBai.BatchRename"
if ($existing) { $existing | Remove-AppxPackage }
Add-AppxPackage -Path $PackagePath -ForceApplicationShutdown

# The packaged verbs cover the Windows 11 first-level menu for file-only and
# folder-only selections. AllFilesystemObjects keeps mixed file/folder
# selections available in the classic menu and on Windows 10.
$legacyKey = "HKLM:\Software\Classes\AllFilesystemObjects\shell\BatchRename"
$menuTitle = -join @([char]0x6279, [char]0x91CF, [char]0x91CD, [char]0x547D, [char]0x540D)
New-Item -Path $legacyKey -Force | Out-Null
New-ItemProperty -Path $legacyKey -Name "MUIVerb" -Value $menuTitle -PropertyType String -Force | Out-Null
New-ItemProperty -Path $legacyKey -Name "ExplorerCommandHandler" -Value "{5E57551A-5925-4B90-9D55-70759F511A91}" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $legacyKey -Name "MultiSelectModel" -Value "Player" -PropertyType String -Force | Out-Null
