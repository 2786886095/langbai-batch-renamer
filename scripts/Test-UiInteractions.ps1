param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [string[]]$Arguments = @()
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class BatchRenameInputNative
{
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, int data, UIntPtr extraInfo);
}
"@

function Find-ByAutomationId([System.Windows.Automation.AutomationElement]$Root, [string]$Id)
{
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $Id)
    return $Root.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $condition)
}

function Wait-ByAutomationId([System.Windows.Automation.AutomationElement]$Root, [string]$Id)
{
    for ($attempt = 0; $attempt -lt 50; $attempt++)
    {
        $element = Find-ByAutomationId $Root $Id
        if ($element) { return $element }
        Start-Sleep -Milliseconds 100
    }
    return $null
}

function Scroll-IntoView([System.Windows.Automation.AutomationElement]$Element)
{
    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.ScrollItemPattern]::Pattern, [ref]$pattern))
    {
        ([System.Windows.Automation.ScrollItemPattern]$pattern).ScrollIntoView()
        Start-Sleep -Milliseconds 250
    }
}

function Set-ClipboardTextWithRetry([string]$Value)
{
    for ($attempt = 0; $attempt -lt 20; $attempt++)
    {
        try
        {
            [System.Windows.Clipboard]::SetText($Value)
            return
        }
        catch [System.Runtime.InteropServices.COMException]
        {
            Start-Sleep -Milliseconds 100
        }
    }
    throw 'Clipboard remained busy after 20 attempts.'
}

$start = @{ FilePath = $Executable; PassThru = $true }
if ($Arguments.Count -gt 0) { $start.ArgumentList = $Arguments }
$process = Start-Process @start
try
{
    $process.WaitForInputIdle(15000) | Out-Null
    for ($attempt = 0; $attempt -lt 60 -and $process.MainWindowHandle -eq 0; $attempt++)
    {
        Start-Sleep -Milliseconds 100
        $process.Refresh()
    }
    if ($process.MainWindowHandle -eq 0) { throw 'Application window did not become visible.' }

    Start-Sleep -Milliseconds 700
    $process.Refresh()
    $window = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    $timeFormat = Wait-ByAutomationId $window 'TimeFormatBox'
    if (-not $timeFormat)
    {
        $all = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        $ids = for ($index = 0; $index -lt $all.Count; $index++)
        {
            $id = $all.Item($index).Current.AutomationId
            if ($id) { $id }
        }
        throw "TimeFormatBox was not found. Window='$($window.Current.Name)', ids=$($ids -join ',')"
    }
    Scroll-IntoView $timeFormat
    $valuePattern = [System.Windows.Automation.ValuePattern]$timeFormat.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $beforeWheel = $valuePattern.Current.Value
    $timeFormat.SetFocus()
    $rect = $timeFormat.Current.BoundingRectangle
    [BatchRenameInputNative]::SetCursorPos([int]($rect.Left + $rect.Width / 2), [int]($rect.Top + $rect.Height / 2)) | Out-Null
    [BatchRenameInputNative]::mouse_event(0x0800, 0, 0, -120, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 350
    $afterWheel = $valuePattern.Current.Value
    if ($beforeWheel -ne $afterWheel)
    {
        throw "Mouse wheel changed the time format from '$beforeWheel' to '$afterWheel'."
    }
    Write-Output "PASS  Mouse wheel did not change the time-format value: $afterWheel"

    $ruleScroll = Wait-ByAutomationId $window 'RuleScrollViewer'
    $scrollPattern = $null
    if ($ruleScroll -and $ruleScroll.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$scrollPattern))
    {
        ([System.Windows.Automation.ScrollPattern]$scrollPattern).SetScrollPercent(
            [System.Windows.Automation.ScrollPattern]::NoScroll,
            0)
        Start-Sleep -Milliseconds 350
    }
    $template = Wait-ByAutomationId $window 'TemplateBox'
    if (-not $template) { throw 'TemplateBox was not found.' }
    Scroll-IntoView $template
    $templateValue = [System.Windows.Automation.ValuePattern]$template.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $templateValue.SetValue('before-context-menu-test')
    $template.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait('^a')
    $pasteText = 'context-menu-paste-test'
    Set-ClipboardTextWithRetry $pasteText
    $rect = $template.Current.BoundingRectangle
    [BatchRenameInputNative]::SetCursorPos([int]($rect.Left + $rect.Width / 2), [int]($rect.Top + $rect.Height / 2)) | Out-Null
    [BatchRenameInputNative]::mouse_event(0x0008, 0, 0, 0, [UIntPtr]::Zero)
    [BatchRenameInputNative]::mouse_event(0x0010, 0, 0, 0, [UIntPtr]::Zero)

    $menuItems = @{}
    $cutLabel = -join @([char]0x526A, [char]0x5207)
    $copyLabel = -join @([char]0x590D, [char]0x5236)
    $pasteLabel = -join @([char]0x7C98, [char]0x8D34)
    $selectAllLabel = -join @([char]0x5168, [char]0x9009)
    for ($attempt = 0; $attempt -lt 30 -and $menuItems.Count -lt 4; $attempt++)
    {
        Start-Sleep -Milliseconds 100
        foreach ($name in @($cutLabel, $copyLabel, $pasteLabel, $selectAllLabel))
        {
            if ($menuItems.ContainsKey($name)) { continue }
            $condition = [System.Windows.Automation.AndCondition]::new(@(
                [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id),
                [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::MenuItem),
                [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $name)
            ))
            $item = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
            if ($item) { $menuItems[$name] = $item }
        }
    }
    if ($menuItems.Count -ne 4) { throw "Right-click edit menu is incomplete: $($menuItems.Keys -join ', ')" }

    $pastePattern = [System.Windows.Automation.InvokePattern]$menuItems[$pasteLabel].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pastePattern.Invoke()
    Start-Sleep -Milliseconds 350
    if ($templateValue.Current.Value -ne $pasteText)
    {
        throw "Right-click paste failed. Current value: '$($templateValue.Current.Value)'"
    }
    Write-Output 'PASS  Context menu exposes Cut, Copy, Paste, and Select All; Paste executed successfully.'
}
finally
{
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
}
