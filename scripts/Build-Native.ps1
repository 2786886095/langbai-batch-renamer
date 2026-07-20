param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot "artifacts\native\x64"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$source = Join-Path $projectRoot "native\ExplorerCommand\ExplorerCommand.c"
$exports = Join-Path $projectRoot "native\ExplorerCommand\ExplorerCommand.def"
$dll = Join-Path $outputDirectory "ExplorerCommand.dll"
$uninstallEntries = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue
$llvmMingwEntry = $uninstallEntries | Where-Object DisplayName -Match 'LLVM MinGW' | Select-Object -First 1
if (-not $llvmMingwEntry) { throw "LLVM-MinGW is required. Install MartinStorsjo.LLVM-MinGW.UCRT with winget." }
$llvmMingwRoot = Get-ChildItem -LiteralPath $llvmMingwEntry.InstallLocation -Directory | Where-Object Name -Like 'llvm-mingw-*' | Select-Object -First 1
$compiler = Join-Path $llvmMingwRoot.FullName 'bin\x86_64-w64-mingw32-clang.exe'

& $compiler -std=c11 -O2 -shared -DUNICODE -D_UNICODE -DWIN32_LEAN_AND_MEAN -o $dll $source $exports -lole32 -lshell32 -lshlwapi -luuid
if ($LASTEXITCODE -ne 0) { throw "Native compilation or linking failed." }

Write-Output $dll
