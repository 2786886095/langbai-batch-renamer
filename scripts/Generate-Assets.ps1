$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$packageAssets = Join-Path $projectRoot "packaging\Assets"
$appAssets = Join-Path $projectRoot "src\BatchRename.App\Assets"
$sourcePath = Join-Path $appAssets "BatchRenameSource.png"
New-Item -ItemType Directory -Path $packageAssets, $appAssets -Force | Out-Null
if (-not (Test-Path -LiteralPath $sourcePath)) { throw "The user-provided BatchRenameSource.png asset is missing." }

function New-SourceMark
{
    $source = [System.Drawing.Bitmap]::FromFile($sourcePath)
    try
    {
        $minX = $source.Width
        $minY = $source.Height
        $maxX = -1
        $maxY = -1
        for ($y = 0; $y -lt $source.Height; $y++)
        {
            for ($x = 0; $x -lt $source.Width; $x++)
            {
                $color = $source.GetPixel($x, $y)
                if ($color.A -gt 8 -and ($color.R -lt 245 -or $color.G -lt 245 -or $color.B -lt 245))
                {
                    if ($x -lt $minX) { $minX = $x }
                    if ($x -gt $maxX) { $maxX = $x }
                    if ($y -lt $minY) { $minY = $y }
                    if ($y -gt $maxY) { $maxY = $y }
                }
            }
        }
        if ($maxX -lt $minX -or $maxY -lt $minY) { throw "The source icon has no visible content." }

        $contentWidth = $maxX - $minX + 1
        $contentHeight = $maxY - $minY + 1
        $side = [Math]::Max($contentWidth, $contentHeight)
        $mark = [System.Drawing.Bitmap]::new($side, $side, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($mark)
        try
        {
            $graphics.Clear([System.Drawing.Color]::White)
            $destinationX = [int](($side - $contentWidth) / 2)
            $destinationY = [int](($side - $contentHeight) / 2)
            $destination = [System.Drawing.Rectangle]::new($destinationX, $destinationY, $contentWidth, $contentHeight)
            $sourceRectangle = [System.Drawing.Rectangle]::new($minX, $minY, $contentWidth, $contentHeight)
            $graphics.DrawImage($source, $destination, $sourceRectangle, [System.Drawing.GraphicsUnit]::Pixel)
        }
        finally { $graphics.Dispose() }
        return $mark
    }
    finally { $source.Dispose() }
}

function New-RenderedBitmap([System.Drawing.Bitmap]$Mark, [int]$Width, [int]$Height, [double]$Scale = 1.0)
{
    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try
    {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $side = [int]([Math]::Min($Width, $Height) * $Scale)
        $x = [int](($Width - $side) / 2)
        $y = [int](($Height - $side) / 2)
        $graphics.DrawImage($Mark, $x, $y, $side, $side)
    }
    finally { $graphics.Dispose() }
    return $bitmap
}

function Save-Png([System.Drawing.Bitmap]$Mark, [int]$Width, [int]$Height, [string]$Path, [double]$Scale = 1.0)
{
    $bitmap = New-RenderedBitmap $Mark $Width $Height $Scale
    try { $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png) }
    finally { $bitmap.Dispose() }
}

function Get-PngBytes([System.Drawing.Bitmap]$Mark, [int]$Size)
{
    $bitmap = New-RenderedBitmap $Mark $Size $Size 1.0
    $stream = [System.IO.MemoryStream]::new()
    try
    {
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    }
    finally { $stream.Dispose(); $bitmap.Dispose() }
}

$mark = New-SourceMark
try
{
    Save-Png $mark 128 128 (Join-Path $appAssets "BatchRenameMark.png")
    Save-Png $mark 44 44 (Join-Path $packageAssets "Square44x44Logo.png")
    Save-Png $mark 150 150 (Join-Path $packageAssets "Square150x150Logo.png")
    Save-Png $mark 310 150 (Join-Path $packageAssets "Wide310x150Logo.png") 0.78
    Save-Png $mark 50 50 (Join-Path $packageAssets "StoreLogo.png")

    $sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    $images = @()
    foreach ($size in $sizes) { $images += ,(Get-PngBytes $mark $size) }
    $iconPath = Join-Path $appAssets "BatchRename.ico"
    $fileStream = [System.IO.File]::Create($iconPath)
    $writer = [System.IO.BinaryWriter]::new($fileStream)
    try
    {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($index = 0; $index -lt $sizes.Count; $index++)
        {
            $size = $sizes[$index]
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$images[$index].Length)
            $writer.Write([UInt32]$offset)
            $offset += $images[$index].Length
        }
        foreach ($imageBytes in $images) { $writer.Write([byte[]]$imageBytes) }
    }
    finally { $writer.Dispose(); $fileStream.Dispose() }
}
finally { $mark.Dispose() }
