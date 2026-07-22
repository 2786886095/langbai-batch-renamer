param([string]$Version = "1.1.4")

$ErrorActionPreference = "Stop"
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must use major.minor.patch format, for example 1.1.0." }
$projectRoot = Split-Path -Parent $PSScriptRoot
$sdkRoot = "C:\Program Files (x86)\Windows Kits\10"
$sdkVersion = (Get-ChildItem "$sdkRoot\bin" -Directory | Where-Object Name -Match '^10\.0\.' | Sort-Object Name -Descending | Select-Object -First 1).Name
$makeAppx = "$sdkRoot\bin\$sdkVersion\x64\makeappx.exe"
$signTool = "$sdkRoot\bin\$sdkVersion\x64\signtool.exe"
$publishDirectory = Join-Path $projectRoot "artifacts\publish\win-x64"
$stageDirectory = Join-Path $projectRoot "artifacts\package-stage"
$packageDirectory = Join-Path $projectRoot "artifacts\package"
$certificateDirectory = Join-Path $projectRoot "artifacts\certificate"
$packagePath = Join-Path $packageDirectory "BatchRename.msix"

& (Join-Path $PSScriptRoot "Generate-Assets.ps1")
& (Join-Path $PSScriptRoot "Build-Native.ps1")
dotnet publish (Join-Path $projectRoot "src\BatchRename.App\BatchRename.App.csproj") -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:Version=$Version -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw "Application publish failed." }

if (Test-Path $stageDirectory) { Remove-Item -LiteralPath $stageDirectory -Recurse -Force }
New-Item -ItemType Directory -Path $stageDirectory, $packageDirectory, $certificateDirectory -Force | Out-Null
Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $stageDirectory -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot "artifacts\native\x64\ExplorerCommand.dll") -Destination $stageDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot "packaging\AppxManifest.xml") -Destination $stageDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot "packaging\Assets") -Destination $stageDirectory -Recurse
$stageManifest = Join-Path $stageDirectory "AppxManifest.xml"
[xml]$manifest = Get-Content -LiteralPath $stageManifest -Raw -Encoding UTF8
$manifest.Package.Identity.Version = "$Version.0"
$manifest.Save($stageManifest)
Get-ChildItem -LiteralPath $stageDirectory -Recurse -Filter *.pdb -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }

$pfxPath = Join-Path $certificateDirectory "BatchRename.pfx"
$cerPath = Join-Path $certificateDirectory "BatchRename.cer"
$passwordPath = Join-Path $certificateDirectory ".pfx-password"
if (Test-Path $passwordPath)
{
    $passwordText = (Get-Content -LiteralPath $passwordPath -Raw).Trim()
}
elseif (-not (Test-Path $pfxPath))
{
    $passwordText = "$([Guid]::NewGuid().ToString('N'))$([Guid]::NewGuid().ToString('N'))"
    Set-Content -LiteralPath $passwordPath -Value $passwordText -NoNewline
}
else
{
    throw "Signing password is missing. Restore artifacts\certificate\.pfx-password or remove the local PFX to generate a new certificate."
}
$password = ConvertTo-SecureString $passwordText -AsPlainText -Force
if (-not (Test-Path $pfxPath))
{
    $cert = New-SelfSignedCertificate -Type Custom -Subject "CN=LangBai Batch Rename" -FriendlyName "BatchRename local package signing" -CertStoreLocation "Cert:\CurrentUser\My" -KeyUsage DigitalSignature -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")
    Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $password | Out-Null
    Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
}

if (Test-Path $packagePath) { Remove-Item -LiteralPath $packagePath -Force }
& $makeAppx pack /d $stageDirectory /p $packagePath /o
if ($LASTEXITCODE -ne 0) { throw "MSIX packaging failed." }
& $signTool sign /fd SHA256 /f $pfxPath /p $passwordText $packagePath
if ($LASTEXITCODE -ne 0) { throw "MSIX signing failed." }

& "C:\ProgramData\chocolatey\bin\ISCC.exe" "/DMyAppVersion=$Version" (Join-Path $projectRoot "installer\BatchRename.iss")
if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }

$installerPath = Join-Path $projectRoot "artifacts\installer\BatchRename-Setup-$Version-x64.exe"
& $signTool sign /fd SHA256 /f $pfxPath /p $passwordText $installerPath
if ($LASTEXITCODE -ne 0) { throw "Installer signing failed." }

Write-Output $installerPath
