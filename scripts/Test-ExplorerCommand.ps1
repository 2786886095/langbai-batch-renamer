param([string[]]$Paths = @())

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$entry = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*' | Where-Object DisplayName -Match 'LLVM MinGW' | Select-Object -First 1
$root = Get-ChildItem -LiteralPath $entry.InstallLocation -Directory | Where-Object Name -Like 'llvm-mingw-*' | Select-Object -First 1
$compiler = Join-Path $root.FullName 'bin\x86_64-w64-mingw32-clang.exe'
$probe = Join-Path $projectRoot 'artifacts\native\x64\ExplorerCommand.Probe.exe'
& $compiler -municode -O2 -o $probe (Join-Path $projectRoot 'tests\ExplorerCommand.Probe.c') -lole32 -lshell32 -luuid
if ($LASTEXITCODE -ne 0) { throw "Explorer command probe build failed." }
$existingIds = @(Get-Process -Name 'BatchRename' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
& $probe @Paths
if ($LASTEXITCODE -ne 0) { throw "Explorer command COM activation failed." }
if ($Paths.Count -gt 0)
{
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    $appProcess = $null
    try
    {
        for ($attempt = 0; $attempt -lt 80 -and -not $appProcess; $attempt++)
        {
            Start-Sleep -Milliseconds 150
            $appProcess = Get-Process -Name 'BatchRename' -ErrorAction SilentlyContinue |
                Where-Object { $_.Id -notin $existingIds -and $_.MainWindowHandle -ne 0 } |
                Select-Object -First 1
        }
        if (-not $appProcess) { throw 'Batch Rename app did not launch from IExplorerCommand.Invoke.' }
        $appWindow = [System.Windows.Automation.AutomationElement]::FromHandle($appProcess.MainWindowHandle)
        $summaryCondition = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            'SelectionSummary')
        $summary = $appWindow.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $summaryCondition)
        if (-not $summary -or $summary.Current.Name -notmatch $Paths.Count)
        {
            throw "Selection transfer failed. Summary: $($summary.Current.Name)"
        }
        Write-Output "Selection transfer verified: $($summary.Current.Name)"
    }
    finally
    {
        if ($appProcess -and -not $appProcess.HasExited) { Stop-Process -Id $appProcess.Id }
    }
}
