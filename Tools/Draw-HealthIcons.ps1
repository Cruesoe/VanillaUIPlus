# Draws the health-row icons into the full-size icon art (Tools\Art\PawnReadoutIcons.png); run Shrink-PawnReadoutIcons.ps1 afterwards.
param([string]$Atlas, [string]$Out)
Add-Type -AssemblyName System.Drawing
$old = [System.Drawing.Bitmap]::new($Atlas)
$w = $old.Width; $h = 1330
$bmp = [System.Drawing.Bitmap]::new($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
$g.DrawImage($old, [System.Drawing.Rectangle]::new(0, 0, $old.Width, $old.Height))
$g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

$fill = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(253, 208, 208, 208))
$shade = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(253, 186, 186, 186))
$dark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(253, 115, 116, 115))
$pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(253, 115, 116, 115), 7)
$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

# Fills the path, darkens its right half, then outlines it.
function Draw-Shape($path, [float]$cx) {
    $g.FillPath($fill, $path)
    $state = $g.Save()
    $g.SetClip([System.Drawing.RectangleF]::new($cx, 0, 400, $h))
    $g.IntersectClip([System.Drawing.Region]::new($path))
    $g.FillPath($shade, $path)
    $g.Restore($state)
    $g.DrawPath($pen, $path)
}

# Blood drop (bleeding), slot 8 of row 2.
$cx = 1545; $cy = 650
$p = [System.Drawing.Drawing2D.GraphicsPath]::new()
$p.AddBezier($cx, ($cy - 140), ($cx + 25), ($cy - 80), ($cx + 105), ($cy - 10), ($cx + 105), ($cy + 50))
$p.AddBezier(($cx + 105), ($cy + 50), ($cx + 105), ($cy + 115), ($cx + 55), ($cy + 140), $cx, ($cy + 140))
$p.AddBezier($cx, ($cy + 140), ($cx - 55), ($cy + 140), ($cx - 105), ($cy + 115), ($cx - 105), ($cy + 50))
$p.AddBezier(($cx - 105), ($cy + 50), ($cx - 105), ($cy - 10), ($cx - 25), ($cy - 80), $cx, ($cy - 140))
$p.CloseFigure()
Draw-Shape $p $cx
$g.FillEllipse([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(253, 236, 236, 236)), ($cx - 70), ($cy + 20), 30, 60)

# Germ (disease): body with knobbed spikes.
$cx = 245; $cy = 1100
for ($i = 0; $i -lt 8; $i++) {
    $a = $i * [Math]::PI / 4
    $x1 = $cx + [Math]::Cos($a) * 80; $y1 = $cy + [Math]::Sin($a) * 80
    $x2 = $cx + [Math]::Cos($a) * 120; $y2 = $cy + [Math]::Sin($a) * 120
    $g.DrawLine([System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(253, 115, 116, 115), 16), [float]$x1, [float]$y1, [float]$x2, [float]$y2)
    $k = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $k.AddEllipse([float]($x2 - 20), [float]($y2 - 20), 40, 40)
    Draw-Shape $k ([float]$x2)
}
$p = [System.Drawing.Drawing2D.GraphicsPath]::new()
$p.AddEllipse(($cx - 88), ($cy - 88), 176, 176)
Draw-Shape $p $cx
foreach ($s in @(@(-35, -30, 34), @(30, -10, 26), @(-15, 35, 30))) {
    $g.FillEllipse($dark, [float]($cx + $s[0] - $s[2] / 2), [float]($cy + $s[1] - $s[2] / 2), [float]$s[2], [float]$s[2])
}

# Skull (life threat).
$cx = 680; $cy = 1100
$p = [System.Drawing.Drawing2D.GraphicsPath]::new()
$p.AddArc(($cx - 115), ($cy - 135), 230, 210, 150, 240)
$p.AddLine(($cx + 100), ($cy + 22), ($cx + 62), ($cy + 60))
$p.AddLine(($cx + 62), ($cy + 60), ($cx + 62), ($cy + 125))
$p.AddLine(($cx + 62), ($cy + 125), ($cx - 62), ($cy + 125))
$p.AddLine(($cx - 62), ($cy + 125), ($cx - 62), ($cy + 60))
$p.AddLine(($cx - 62), ($cy + 60), ($cx - 100), ($cy + 22))
$p.CloseFigure()
Draw-Shape $p $cx
$g.FillEllipse($dark, ($cx - 78), ($cy - 35), 62, 66)
$g.FillEllipse($dark, ($cx + 16), ($cy - 35), 62, 66)
$nose = [System.Drawing.PointF[]]@([System.Drawing.PointF]::new($cx, ($cy + 38)), [System.Drawing.PointF]::new(($cx - 17), ($cy + 70)), [System.Drawing.PointF]::new(($cx + 17), ($cy + 70)))
$g.FillPolygon($dark, $nose)
foreach ($t in @(-31, 0, 31)) {
    $g.DrawLine([System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(253, 115, 116, 115), 7), [float]($cx + $t), [float]($cy + 92), [float]($cx + $t), [float]($cy + 125))
}

# Medical cross (needs tending).
$cx = 1110; $cy = 1100
$p = [System.Drawing.Drawing2D.GraphicsPath]::new()
$arm = 48; $len = 125; $r = 14
$pts = @(
    @(-$arm, -$len), @($arm, -$len), @($arm, -$arm), @($len, -$arm), @($len, $arm), @($arm, $arm),
    @($arm, $len), @(-$arm, $len), @(-$arm, $arm), @(-$len, $arm), @(-$len, -$arm), @(-$arm, -$arm))
$p.AddPolygon([System.Drawing.PointF[]]($pts | ForEach-Object { [System.Drawing.PointF]::new($cx + $_[0], $cy + $_[1]) }))
Draw-Shape $p $cx

# Lightning bolt (pain).
$cx = 1545; $cy = 1100
$p = [System.Drawing.Drawing2D.GraphicsPath]::new()
$bolt = @(@(25, -140), @(-85, 15), @(-10, 15), @(-40, 140), @(85, -25), @(10, -25), @(55, -140))
$p.AddPolygon([System.Drawing.PointF[]]($bolt | ForEach-Object { [System.Drawing.PointF]::new($cx + $_[0], $cy + $_[1]) }))
Draw-Shape $p $cx

$g.Dispose()
$old.Dispose()

# Tight bounds of each new icon for the drawer's IconPixels table.
foreach ($cell in @(@(1330, 444, 1774, 887), @(0, 887, 443, 1330), @(443, 887, 887, 1330), @(887, 887, 1330, 1330), @(1330, 887, 1774, 1330))) {
    $minX = 9999; $minY = 9999; $maxX = -1; $maxY = -1
    for ($y = $cell[1]; $y -lt $cell[3]; $y += 1) {
        for ($x = $cell[0]; $x -lt $cell[2]; $x += 1) {
            if ($bmp.GetPixel($x, $y).A -gt 8) {
                if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    "new Rect({0}f, {1}f, {2}f, {3}f)" -f $minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1)
}
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
