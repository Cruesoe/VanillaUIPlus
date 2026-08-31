# Rebuilds About/Preview.png.
#
# Lifts the real UI out of an in-game screenshot and drops it onto the branding
# plate, so the preview shows the actual mod rather than a mockup, without the
# colony behind it. Two regions are copied at their true relative scale and
# position: the main menu bar along the bottom, and the HUD stack on the right.
#
# Kept ASCII-only on purpose so the file's encoding cannot mangle anything.
Add-Type -AssemblyName System.Drawing

$root  = Split-Path -Parent $PSScriptRoot
$plate = Join-Path $root "About\branding\plate-source.png"
$shot  = Join-Path $root "About\branding\ui-source.png"
$out   = Join-Path $root "About\Preview.png"

# 1040x585 keeps the PNG under Steam's 1 MB preview limit. 1280x720 lands at about
# 1.39 MB, and the cost is photographic detail rather than grain, so denoising does
# not help: 2x softening still leaves it over. Downscaling is the only lever.
$W = 1040; $H = 585
$k = $W / 1280.0   # everything below was laid out against 1280 wide

# Source regions, measured from the 2559x1599 screenshot by dark-density profiling.
# The bar's bright top border sits on row 1548.
$barSrc = New-Object System.Drawing.Rectangle(0, 1548, 2559, 51)
$hudSrc = New-Object System.Drawing.Rectangle(2301, 1193, 258, 355)

$scale  = $W / 2559.0
$barW   = [int]($barSrc.Width  * $scale)
$barH   = [int][Math]::Ceiling($barSrc.Height * $scale)
$hudW   = [int]($hudSrc.Width  * $scale)
$hudH   = [int]($hudSrc.Height * $scale)
$barY   = $H - $barH
$hudY   = $barY - $hudH
$hudX   = $W - $hudW

$src = New-Object System.Drawing.Bitmap($plate)
$ui  = New-Object System.Drawing.Bitmap($shot)
# 24bpp: the page is opaque, and dropping the alpha channel keeps the file smaller.
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

# The game's bars are translucent, so the copied pixels carry some terrain colour.
# Darkening and desaturating settles that into neutral texture against the plate.
$cm = New-Object System.Drawing.Imaging.ColorMatrix
$lr = 0.30; $lg = 0.59; $lb = 0.11; $s = 0.45; $d = 0.86
$cm.Matrix00 = ($lr + $s * (1 - $lr)) * $d; $cm.Matrix01 = ($lr - $s * $lr) * $d;       $cm.Matrix02 = ($lr - $s * $lr) * $d
$cm.Matrix10 = ($lg - $s * $lg) * $d;       $cm.Matrix11 = ($lg + $s * (1 - $lg)) * $d; $cm.Matrix12 = ($lg - $s * $lg) * $d
$cm.Matrix20 = ($lb - $s * $lb) * $d;       $cm.Matrix21 = ($lb - $s * $lb) * $d;       $cm.Matrix22 = ($lb + $s * (1 - $lb)) * $d
$cm.Matrix33 = 1.0; $cm.Matrix44 = 1.0
$attr = New-Object System.Drawing.Imaging.ImageAttributes
$attr.SetColorMatrix($cm)

$g.DrawImage($ui, (New-Object System.Drawing.Rectangle(0, $barY, $barW, $barH)),
  $barSrc.X, $barSrc.Y, $barSrc.Width, $barSrc.Height, [System.Drawing.GraphicsUnit]::Pixel, $attr)
$g.DrawImage($ui, (New-Object System.Drawing.Rectangle($hudX, $hudY, $hudW, $hudH)),
  $hudSrc.X, $hudSrc.Y, $hudSrc.Width, $hudSrc.Height, [System.Drawing.GraphicsUnit]::Pixel, $attr)

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

$titleFont = New-Object System.Drawing.Font("Segoe UI", (76 * $k), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$white  = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(245,245,242))
$shadow = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(150,0,0,0))

Draw-Tracked $g "VANILLA UI+" $titleFont $shadow (643 * $k) (231 * $k) (7 * $k)
Draw-Tracked $g "VANILLA UI+" $titleFont $white  (640 * $k) (227 * $k) (7 * $k)

$g.Dispose()
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose(); $src.Dispose(); $ui.Dispose()
"wrote $out ({0:N0} bytes)" -f (Get-Item $out).Length
