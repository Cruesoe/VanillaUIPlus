# Packs the full-size icon art (Tools\Art\PawnReadoutIcons.png) into the shipped 128px-cell atlas and prints the IconPixels rects.
param(
    [string]$Source = (Join-Path $PSScriptRoot 'Art\PawnReadoutIcons.png'),
    [string]$Out = (Join-Path $PSScriptRoot '..\Textures\UI\VanillaUIPlus\PawnReadoutIcons.png')
)
Add-Type -AssemblyName System.Drawing

# Tight bounds of each icon in the full-size art, in IconPixels order.
$rects = @(
    @(130, 111, 230, 268), @(580, 99, 212, 287), @(1015, 98, 213, 291), @(1464, 98, 154, 283),
    @(145, 519, 221, 262), @(557, 532, 251, 249), @(967, 512, 279, 278), @(1437, 507, 217, 287),
    @(102, 957, 287, 287), @(562, 962, 237, 267), @(982, 972, 257, 257), @(1457, 957, 177, 287)
)
$cell = 128
$largest = ($rects | ForEach-Object { [Math]::Max($_[2], $_[3]) } | Measure-Object -Maximum).Maximum
$scale = 120.0 / $largest

$src = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Source))
$dst = [System.Drawing.Bitmap]::new(4 * $cell, 3 * $cell, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($dst)
$g.Clear([System.Drawing.Color]::Transparent)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$attr = [System.Drawing.Imaging.ImageAttributes]::new()
$attr.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)

$printed = @()
for ($i = 0; $i -lt $rects.Count; $i++) {
    $r = $rects[$i]
    $w = [int][Math]::Round($r[2] * $scale)
    $h = [int][Math]::Round($r[3] * $scale)
    $x = ($i % 4) * $cell + [int](($cell - $w) / 2)
    $y = [Math]::Floor($i / 4) * $cell + [int](($cell - $h) / 2)
    $g.DrawImage($src, [System.Drawing.Rectangle]::new($x, $y, $w, $h), $r[0], $r[1], $r[2], $r[3], [System.Drawing.GraphicsUnit]::Pixel, $attr)
    $printed += 'new Rect({0}f, {1}f, {2}f, {3}f)' -f $x, $y, $w, $h
}

$g.Dispose()
$src.Dispose()
$dst.Save([System.IO.Path]::GetFullPath($Out), [System.Drawing.Imaging.ImageFormat]::Png)
$dst.Dispose()
$printed -join ', '
