# Generates the DisplayDeck brand mark:
#   - src/DisplayDeck.App/Assets/DisplayDeck.ico  (multi-size app + tray icon)
#   - assets/logo.png                             (512px marketing logo)
#
# Brand system (shared across all RJF apps):
#   * rounded-square tile with the signature blue -> purple gradient
#   * a per-app glyph in the centre (DisplayDeck = a monitor showing two arranged displays)
#   * a small "RJF" maker's mark so you can tell who built it
#
# Run:  powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$root      = Split-Path -Parent $PSScriptRoot
$icoPath   = Join-Path $root 'src\DisplayDeck.App\Assets\DisplayDeck.ico'
$pngPath   = Join-Path $root 'assets\logo.png'

New-Item -ItemType Directory -Force -Path (Split-Path $icoPath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path $pngPath) | Out-Null

function New-RoundedRectPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    if ($d -gt $w) { $d = $w }
    if ($d -gt $h) { $d = $h }
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-BrandBitmap([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # --- background tile with signature gradient ---
    $pad  = [single]([Math]::Max(1, [int]($s * 0.055)))
    $tile = New-Object System.Drawing.RectangleF($pad, $pad, ($s - 2 * $pad), ($s - 2 * $pad))
    $c1   = [System.Drawing.Color]::FromArgb(0x2F, 0x6B, 0xFF)
    $c2   = [System.Drawing.Color]::FromArgb(0x8A, 0x4B, 0xFF)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($tile, $c1, $c2, 55)
    $tilePath = New-RoundedRectPath $tile.X $tile.Y $tile.Width $tile.Height ($s * 0.22)
    $g.FillPath($grad, $tilePath)

    # --- monitor glyph ---
    $mw = $tile.Width * 0.60
    $mh = $mw * 0.60
    $mx = ($s - $mw) / 2.0
    $my = ($s - $mh) / 2.0 - $s * 0.05
    $screenPath = New-RoundedRectPath $mx $my $mw $mh ($s * 0.045)
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $g.FillPath($white, $screenPath)

    # two little "displays" inside the screen (the arrange concept)
    $inset  = $mw * 0.12
    $innerH = $mh - $inset * 2
    $innerW = ($mw - $inset * 3) / 2.0
    $t1 = New-RoundedRectPath ($mx + $inset) ($my + $inset) $innerW $innerH ($s * 0.02)
    $t2 = New-RoundedRectPath ($mx + $inset * 2 + $innerW) ($my + $inset) $innerW $innerH ($s * 0.02)
    $fill1 = New-Object System.Drawing.SolidBrush $c1
    $fill2 = New-Object System.Drawing.SolidBrush $c2
    $g.FillPath($fill1, $t1)
    $g.FillPath($fill2, $t2)

    # stand (neck + base)
    $neckW = $mw * 0.12
    $neckH = $s * 0.05
    $neckX = ($s - $neckW) / 2.0
    $neckY = $my + $mh
    $g.FillRectangle($white, $neckX, $neckY, $neckW, $neckH)
    $baseW = $mw * 0.34
    $baseH = $s * 0.035
    $basePath = New-RoundedRectPath (($s - $baseW) / 2.0) ($neckY + $neckH) $baseW $baseH ($baseH / 2.0)
    $g.FillPath($white, $basePath)

    # --- "RJF" maker's mark (only where it stays legible) ---
    if ($s -ge 48) {
        $fontSize = [single]($s * 0.12)
        $font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $fmt = New-Object System.Drawing.StringFormat
        $fmt.Alignment = [System.Drawing.StringAlignment]::Center
        $mark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 255, 255, 255))
        $markRect = New-Object System.Drawing.RectangleF(0, ($s * 0.80), $s, ($s * 0.16))
        $g.DrawString('RJF', $font, $mark, $markRect, $fmt)
        $font.Dispose(); $fmt.Dispose(); $mark.Dispose()
    }

    $g.Dispose()
    return $bmp
}

# --- write the marketing PNG ---
$logo = New-BrandBitmap 512
$logo.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$logo.Dispose()
Write-Host "Wrote $pngPath"

# --- write the multi-size .ico (PNG-compressed entries) ---
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngBlobs = @()
foreach ($sz in $sizes) {
    $bmp = New-BrandBitmap $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBlobs += ,($ms.ToArray())
    $ms.Dispose()
    $bmp.Dispose()
}

$fs = New-Object System.IO.FileStream($icoPath, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0)                 # reserved
$bw.Write([uint16]1)                 # type = icon
$bw.Write([uint16]$sizes.Count)      # image count

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $len = $pngBlobs[$i].Length
    $bw.Write([byte]($(if ($sz -ge 256) { 0 } else { $sz })))  # width
    $bw.Write([byte]($(if ($sz -ge 256) { 0 } else { $sz })))  # height
    $bw.Write([byte]0)               # palette
    $bw.Write([byte]0)               # reserved
    $bw.Write([uint16]1)             # planes
    $bw.Write([uint16]32)            # bpp
    $bw.Write([uint32]$len)          # bytes in resource
    $bw.Write([uint32]$offset)       # offset
    $offset += $len
}
foreach ($blob in $pngBlobs) { $bw.Write($blob) }
$bw.Flush(); $bw.Close(); $fs.Close()
Write-Host "Wrote $icoPath ($($sizes.Count) sizes)"
