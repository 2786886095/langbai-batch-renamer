param(
    [Parameter(Mandatory = $true)][string]$Reference,
    [Parameter(Mandatory = $true)][string]$Implementation,
    [Parameter(Mandatory = $true)][string]$Output
)

Add-Type -AssemblyName System.Drawing

$referenceImage = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $Reference))
$implementationImage = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $Implementation))

try {
    $contentHeight = 790
    $referenceWidth = [int][Math]::Round($referenceImage.Width * $contentHeight / $referenceImage.Height)
    $implementationWidth = [int][Math]::Round($implementationImage.Width * $contentHeight / $implementationImage.Height)
    $gutter = 24
    $labelHeight = 44
    $canvas = [System.Drawing.Bitmap]::new($referenceWidth + $implementationWidth + $gutter * 3, $contentHeight + $labelHeight + $gutter * 2)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        $graphics.Clear([System.Drawing.Color]::FromArgb(248, 250, 252))
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $font = [System.Drawing.Font]::new('Microsoft YaHei UI', 13, [System.Drawing.FontStyle]::Bold)
        $brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(15, 23, 42))
        try {
            $graphics.DrawString('Reference: MT Manager', $font, $brush, $gutter, 12)
            $implementationX = $referenceWidth + $gutter * 2
            $graphics.DrawString('Implementation: Windows app', $font, $brush, $implementationX, 12)
            $graphics.DrawImage($referenceImage, $gutter, $labelHeight, $referenceWidth, $contentHeight)
            $graphics.DrawImage($implementationImage, $implementationX, $labelHeight, $implementationWidth, $contentHeight)
        }
        finally {
            $brush.Dispose()
            $font.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    $outputDirectory = Split-Path -Parent $Output
    if ($outputDirectory) { New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null }
    $canvas.Save($Output, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Dispose()
}
finally {
    $referenceImage.Dispose()
    $implementationImage.Dispose()
}
