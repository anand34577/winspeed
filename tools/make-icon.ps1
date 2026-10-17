# Generates assets\WinSpeed.ico (multi-size, PNG-compressed) using System.Drawing.
param([string]$Out = (Join-Path $PSScriptRoot '..\assets\WinSpeed.ico'))
Add-Type -AssemblyName System.Drawing

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # rounded square background
    $r = [Math]::Max(2, [int]($size * 0.22))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $w = $size - 1
    $path.AddArc(0, 0, $r * 2, $r * 2, 180, 90)
    $path.AddArc($w - $r * 2, 0, $r * 2, $r * 2, 270, 90)
    $path.AddArc($w - $r * 2, $w - $r * 2, $r * 2, $r * 2, 0, 90)
    $path.AddArc(0, $w - $r * 2, $r * 2, $r * 2, 90, 90)
    $path.CloseFigure()
    $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 21, 32, 61)), ([System.Drawing.Color]::FromArgb(255, 10, 16, 32)), 90
    $g.FillPath($bg, $path)

    # arrows: down (cyan, left) and up (orange, right)
    $down = [System.Drawing.Color]::FromArgb(255, 56, 189, 248)
    $up   = [System.Drawing.Color]::FromArgb(255, 251, 146, 60)
    $s = $size / 16.0
    function Arrow($g, $color, [double]$cx, [bool]$isDown, [double]$s) {
        $shaft = 2.2 * $s; $head = 3.4 * $s
        $top = 2.8 * $s; $bottom = 13.2 * $s; $headLen = 4.2 * $s
        $b = New-Object System.Drawing.SolidBrush $color
        if ($isDown) {
            $g.FillRectangle($b, [single]($cx - $shaft / 2), [single]$top, [single]$shaft, [single]($bottom - $top - $headLen + 0.5 * $s))
            $pts = @(
                (New-Object System.Drawing.PointF ([single]($cx - $head)), ([single]($bottom - $headLen))),
                (New-Object System.Drawing.PointF ([single]($cx + $head)), ([single]($bottom - $headLen))),
                (New-Object System.Drawing.PointF ([single]$cx), ([single]$bottom)))
        } else {
            $g.FillRectangle($b, [single]($cx - $shaft / 2), [single]($top + $headLen - 0.5 * $s), [single]$shaft, [single]($bottom - $top - $headLen + 0.5 * $s))
            $pts = @(
                (New-Object System.Drawing.PointF ([single]($cx - $head)), ([single]($top + $headLen))),
                (New-Object System.Drawing.PointF ([single]($cx + $head)), ([single]($top + $headLen))),
                (New-Object System.Drawing.PointF ([single]$cx), ([single]$top)))
        }
        $g.FillPolygon($b, [System.Drawing.PointF[]]$pts)
        $b.Dispose()
    }
    Arrow $g $down (5.0 * $s) $true $s
    Arrow $g $up (11.0 * $s) $false $s

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = @()
foreach ($sz in $sizes) { $images += ,(New-IconPng $sz) }

$fs = [System.IO.File]::Create($Out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $data = $images[$i]
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$data.Length); $bw.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($data in $images) { $bw.Write([byte[]]$data) }
$bw.Close()
Write-Host "Wrote $Out"
