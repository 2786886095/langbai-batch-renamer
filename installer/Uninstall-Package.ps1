param([Parameter(Mandatory = $true)][string]$CertificatePath)
$ErrorActionPreference = "SilentlyContinue"
Get-AppxPackage -Name "LangBai.BatchRename" | Remove-AppxPackage
Remove-Item "HKLM:\Software\Classes\AllFilesystemObjects\shell\BatchRename" -Recurse -Force
if (Test-Path $CertificatePath)
{
    $certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CertificatePath)
    Remove-Item "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)" -Force
}
