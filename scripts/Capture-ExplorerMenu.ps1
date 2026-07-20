param(
    [Parameter(Mandatory = $true)][string]$Folder,
    [Parameter(Mandatory = $true)][string]$Output,
    [switch]$Classic
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ExplorerCaptureNative
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

$resolvedFolder = (Resolve-Path -LiteralPath $Folder).Path
$shell = New-Object -ComObject Shell.Application
$shell.Open($resolvedFolder)
$explorerWindow = $null

try
{
    for ($attempt = 0; $attempt -lt 60 -and -not $explorerWindow; $attempt++)
    {
        Start-Sleep -Milliseconds 150
        $explorerWindow = @($shell.Windows()) | Where-Object {
            try { $_.Document.Folder.Self.Path -eq $resolvedFolder } catch { $false }
        } | Select-Object -First 1
    }
    if (-not $explorerWindow) { throw 'Explorer fixture window did not become visible.' }

    $items = $explorerWindow.Document.Folder.Items()
    if ($items.Count -lt 2) { throw 'At least two fixture items are required.' }
    $selectionCount = [Math]::Min(3, $items.Count)
    $explorerWindow.Document.SelectItem($items.Item(0), 29)
    for ($index = 1; $index -lt $selectionCount; $index++)
    {
        $explorerWindow.Document.SelectItem($items.Item($index), 1)
    }

    [ExplorerCaptureNative]::SetForegroundWindow([IntPtr]$explorerWindow.HWND) | Out-Null
    Start-Sleep -Milliseconds 500
    [System.Windows.Forms.SendKeys]::SendWait('+{F10}')
    Start-Sleep -Milliseconds 1800
    if ($Classic)
    {
        [System.Windows.Forms.SendKeys]::SendWait('{UP}{ENTER}')
        Start-Sleep -Milliseconds 900
    }

    $bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bitmap = [System.Drawing.Bitmap]::new($bounds.Width, $bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try
    {
        $graphics.CopyFromScreen($bounds.Left, $bounds.Top, 0, 0, $bounds.Size)
        $outputDirectory = Split-Path -Parent $Output
        if ($outputDirectory) { New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null }
        $bitmap.Save($Output, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally
    {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
}
finally
{
    if ($explorerWindow) { try { $explorerWindow.Quit() } catch {} }
    if ($shell) { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null }
}
