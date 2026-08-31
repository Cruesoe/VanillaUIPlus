# Rebuilds About/Preview.png from About/branding/plate-source.png.
# Keeps the original framing and palette; only the wording and alert rows change.
# Kept ASCII-only on purpose: the separator is built from a char code so the file's
# encoding cannot mangle it.
Add-Type -AssemblyName System.Drawing

$root  = Split-Path -Parent $PSScriptRoot
$plate = Join-Path $root "About\branding\plate-source.png"
$out   = Join-Path $root "About\Preview.png"

$W = 1280; $H = 720
$src = New-Object System.Drawing.Bitmap($plate)
# 24bpp: the page is fully opaque, and dropping the alpha channel cuts about a
# quarter of the file, which matters against Steam's 1 MB preview limit.
$bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

# Crop the 3:2 plate to 16:9, matching the framing of the original preview.
$g.DrawImage($src,
  (New-Object System.Drawing.Rectangle(0, 0, $W, $H)),
  (New-Object System.Drawing.Rectangle(0, 120, 1536, 864)),
  [System.Drawing.GraphicsUnit]::Pixel)

$fmt = [System.Drawing.StringFormat]::GenericTypographic

# System.Drawing has no letter tracking, so advance per glyph by hand. Typographic
# measuring reports no width for a space, so spaces get an explicit advance.
function Get-Advance($g, $ch, $font) {
  if ($ch -eq ' ') { return $font.Size * 0.30 }
  return $g.MeasureString([string]$ch, $font, 0, [System.Drawing.StringFormat]::GenericTypographic).Width
}
function Measure-Tracked($g, $text, $font, $track) {
  $w = 0.0
  foreach ($ch in $text.ToCharArray()) { $w += (Get-Advance $g $ch $font) + $track }
  return $w - $track
}
function Draw-Tracked($g, $text, $font, $brush, $cx, $y, $track) {
  $x = $cx - (Measure-Tracked $g $text $font $track) / 2.0
  foreach ($ch in $text.ToCharArray()) {
    if ($ch -ne ' ') {
      $g.DrawString([string]$ch, $font, $brush, $x, $y, [System.Drawing.StringFormat]::GenericTypographic)
    }
    $x += (Get-Advance $g $ch $font) + $track
  }
}

$dot = [string][char]0x00B7
$subtitle = "HUD   $dot   NOTIFICATIONS   $dot   MAIN MENU"

$titleFont = New-Object System.Drawing.Font("Segoe UI", 62, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$subFont   = New-Object System.Drawing.Font("Segoe UI", 30, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$rowFont   = New-Object System.Drawing.Font("Segoe UI", 26, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)

$white  = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(245,245,242))
$orange = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(229,154,76))
$shadow = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(140,0,0,0))

Draw-Tracked $g "VANILLA UI+" $titleFont $shadow 642 86 6
Draw-Tracked $g "VANILLA UI+" $titleFont $white  640 83 6
Draw-Tracked $g $subtitle $subFont $shadow 642 224 3
Draw-Tracked $g $subtitle $subFont $orange 640 222 3

# Alert rows, matching the original geometry: x=844, top=448, height 48, gap 8.
$barX = 844; $barW = 396; $barH = 48; $gap = 8; $barY = 448
$rows = @(
  @{ Text = "Hostiles present"; Color = [System.Drawing.Color]::FromArgb(217,107,20,20) },
  @{ Text = "Bleeding out";     Color = [System.Drawing.Color]::FromArgb(210,20,20,20) },
  @{ Text = "Trader available"; Color = [System.Drawing.Color]::FromArgb(199,20,20,20) },
  @{ Text = "Batteries low";    Color = [System.Drawing.Color]::FromArgb(199,20,20,20) }
)
$rf = New-Object System.Drawing.StringFormat
$rf.Alignment     = [System.Drawing.StringAlignment]::Far
$rf.LineAlignment = [System.Drawing.StringAlignment]::Center
foreach ($row in $rows) {
  $brush = New-Object System.Drawing.SolidBrush($row.Color)
  $g.FillRectangle($brush, (New-Object System.Drawing.RectangleF($barX, $barY, $barW, $barH)))
  $g.DrawString($row.Text, $rowFont, $white,
    (New-Object System.Drawing.RectangleF($barX, $barY, ($barW - 14), $barH)), $rf)
  $brush.Dispose()
  $barY += $barH + $gap
}

$g.Dispose()
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose(); $src.Dispose()
"wrote $out ({0:N0} bytes)" -f (Get-Item $out).Length
