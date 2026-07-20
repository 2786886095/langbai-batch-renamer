param([Parameter(Mandatory = $true)][string]$Executable)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Find-ByAutomationId([System.Windows.Automation.AutomationElement]$Root, [string]$Id)
{
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $Id)
    return $Root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $condition)
}

function Wait-ForElement([System.Windows.Automation.AutomationElement]$Root, [string]$Id, [int]$ProcessId = 0)
{
    for ($attempt = 0; $attempt -lt 80; $attempt++)
    {
        if ($ProcessId -eq 0)
        {
            $element = Find-ByAutomationId $Root $Id
        }
        else
        {
            $condition = [System.Windows.Automation.AndCondition]::new(@(
                [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId),
                [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
            ))
            $element = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
                [System.Windows.Automation.TreeScope]::Descendants,
                $condition)
        }
        if ($element) { return $element }
        Start-Sleep -Milliseconds 100
    }
    return $null
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("BatchRename.UiFlow." + [Guid]::NewGuid().ToString('N'))
$oldDataDirectory = $env:BATCH_RENAME_DATA_DIR
$process = $null
try
{
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    $fileA = Join-Path $testRoot 'a.txt'
    $fileB = Join-Path $testRoot 'b.txt'
    $folder = Join-Path $testRoot 'folder'
    [IO.File]::WriteAllText($fileA, 'a')
    [IO.File]::WriteAllText($fileB, 'bb')
    New-Item -ItemType Directory -Path $folder | Out-Null
    $env:BATCH_RENAME_DATA_DIR = Join-Path $testRoot 'AppData'

    $process = Start-Process -FilePath $Executable -ArgumentList @($fileB, $folder, $fileA) -PassThru
    $process.WaitForInputIdle(15000) | Out-Null
    for ($attempt = 0; $attempt -lt 60 -and $process.MainWindowHandle -eq 0; $attempt++)
    {
        Start-Sleep -Milliseconds 100
        $process.Refresh()
    }
    Start-Sleep -Milliseconds 700
    $process.Refresh()
    if ($process.MainWindowHandle -eq 0) { throw 'Application window did not become visible.' }
    $window = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)

    $template = Wait-ForElement $window 'TemplateBox'
    if (-not $template) { throw 'TemplateBox was not found.' }
    $templateValue = [System.Windows.Automation.ValuePattern]$template.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $templateValue.SetValue('renamed_{zN}_{P}{S}')

    $previewButton = Wait-ForElement $window 'FinalPreviewButton'
    if (-not $previewButton) { throw 'FinalPreviewButton was not found.' }
    for ($attempt = 0; $attempt -lt 50 -and -not $previewButton.Current.IsEnabled; $attempt++)
    {
        Start-Sleep -Milliseconds 100
    }
    if (-not $previewButton.Current.IsEnabled) { throw 'FinalPreviewButton did not become enabled.' }
    ([System.Windows.Automation.InvokePattern]$previewButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()

    $confirmButton = Wait-ForElement ([System.Windows.Automation.AutomationElement]::RootElement) 'ConfirmRenameButton' $process.Id
    if (-not $confirmButton) { throw 'ConfirmRenameButton was not found.' }
    ([System.Windows.Automation.InvokePattern]$confirmButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()

    $expected = @(
        (Join-Path $testRoot 'renamed_001_a.txt'),
        (Join-Path $testRoot 'renamed_002_b.txt'),
        (Join-Path $testRoot 'renamed_003_folder')
    )
    for ($attempt = 0; $attempt -lt 80 -and -not (($expected | Where-Object { Test-Path -LiteralPath $_ }).Count -eq 3); $attempt++)
    {
        Start-Sleep -Milliseconds 100
    }
    foreach ($path in $expected)
    {
        if (-not (Test-Path -LiteralPath $path)) { throw "Expected renamed item was not created: $path" }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $env:BATCH_RENAME_DATA_DIR 'history.json')))
    {
        throw 'The final confirmation did not persist an undo history entry.'
    }
    Write-Output 'PASS  Final preview confirmation renamed two files and one folder, then persisted undo history.'
}
finally
{
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
    $env:BATCH_RENAME_DATA_DIR = $oldDataDirectory
    $resolvedRoot = [IO.Path]::GetFullPath($testRoot)
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ((Test-Path -LiteralPath $resolvedRoot) -and $resolvedRoot.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase))
    {
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
