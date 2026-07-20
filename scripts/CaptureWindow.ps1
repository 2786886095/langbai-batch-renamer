param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [Parameter(Mandatory = $true)][string]$Output,
    [string[]]$Arguments = @(),
    [int]$Width = 0,
    [int]$Height = 0,
    [string]$Template = '',
    [switch]$OpenHistory
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class WindowCaptureNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint flags);
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int width, int height, uint flags);
}
"@

$startParameters = @{ FilePath = $Executable; PassThru = $true }
if ($Arguments.Count -gt 0) { $startParameters.ArgumentList = $Arguments }
$captureProcess = Start-Process @startParameters
try
{
    $captureProcess.WaitForInputIdle(15000) | Out-Null
    for ($attempt = 0; $attempt -lt 50 -and $captureProcess.MainWindowHandle -eq 0; $attempt++)
    {
        Start-Sleep -Milliseconds 100
        $captureProcess.Refresh()
    }
    if ($captureProcess.MainWindowHandle -eq 0) { throw "Application window did not become visible." }
    $captureHandle = $captureProcess.MainWindowHandle
    if ($Width -gt 0 -and $Height -gt 0)
    {
        [WindowCaptureNative]::SetWindowPos($captureProcess.MainWindowHandle, [IntPtr]::Zero, 40, 40, $Width, $Height, 0x0004) | Out-Null
    }
    if ($Template)
    {
        Start-Sleep -Milliseconds 500
        $windowElement = [System.Windows.Automation.AutomationElement]::FromHandle($captureProcess.MainWindowHandle)
        $condition = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            'TemplateBox')
        $templateElement = $windowElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $condition)
        if (-not $templateElement) { throw 'TemplateBox automation element was not found.' }
        $valuePattern = $templateElement.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        $valuePattern.SetValue($Template)
    }
    if ($OpenHistory)
    {
        Start-Sleep -Milliseconds 400
        $windowElement = [System.Windows.Automation.AutomationElement]::FromHandle($captureProcess.MainWindowHandle)
        $historyCondition = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            'HistoryButton')
        $historyButton = $windowElement.FindFirst([System.Windows.Automation.TreeScope]::Subtree, $historyCondition)
        if (-not $historyButton) { throw 'HistoryButton automation element was not found.' }
        $invokePattern = $historyButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $invokePattern.Invoke()
        Start-Sleep -Milliseconds 600
        $processCondition = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
            $captureProcess.Id)
        $windows = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            $processCondition)
        for ($index = 0; $index -lt $windows.Count; $index++)
        {
            $candidate = $windows.Item($index)
            if ($candidate.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and
                $candidate.Current.NativeWindowHandle -ne 0 -and
                $candidate.Current.NativeWindowHandle -ne $captureProcess.MainWindowHandle)
            {
                $captureHandle = [IntPtr]$candidate.Current.NativeWindowHandle
                break
            }
        }
        if ($captureHandle -eq $captureProcess.MainWindowHandle) { throw 'History window did not become visible.' }
    }
    Start-Sleep -Milliseconds 600
    $rect = New-Object WindowCaptureNative+RECT
    if (-not [WindowCaptureNative]::GetWindowRect($captureHandle, [ref]$rect)) { throw "GetWindowRect failed." }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try
    {
        $deviceContext = $graphics.GetHdc()
        try
        {
            if (-not [WindowCaptureNative]::PrintWindow($captureHandle, $deviceContext, 2))
            {
                throw "PrintWindow failed."
            }
        }
        finally { $graphics.ReleaseHdc($deviceContext) }
        $outputDirectory = Split-Path -Parent $Output
        if ($outputDirectory) { New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null }
        $bitmap.Save($Output, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally
    {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}
finally
{
    if ($captureProcess -and -not $captureProcess.HasExited) { Stop-Process -Id $captureProcess.Id }
}
