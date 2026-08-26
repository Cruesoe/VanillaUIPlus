# Renders a Steam / RimWorld About/Preview.png for a Vanilla UI+ module.
# Usage: .\Tools\make-preview.ps1 -Subtitle Alerts
#        .\Tools\make-preview.ps1 -Subtitle "Colonist Bar" -ShowAlerts:$false
param(
    [Parameter(Mandatory = $true)]
    [string]$Subtitle,
    [string]$RepoRoot,
    [bool]$ShowAlerts = $true
)

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$brandingDir = Join-Path $RepoRoot "About\branding"
$sourcePath = Join-Path $brandingDir "plate-source.png"
$previewPath = Join-Path $RepoRoot "About\Preview.png"

if (-not (Test-Path $sourcePath)) {
    throw "Missing plate source: $sourcePath"
}

function Get-UiFont([string]$family, [single]$size, [System.Drawing.FontStyle]$style) {
    try {
        return New-Object System.Drawing.Font $family, $size, $style, ([System.Drawing.GraphicsUnit]::Pixel)
    }
    catch {
        return New-Object System.Drawing.Font "Segoe UI", $size, $style, ([System.Drawing.GraphicsUnit]::Pixel)
    }
}

$finalW = 1280
$finalH = 720
$preview = New-Object System.Drawing.Bitmap $finalW, $finalH
$preview.SetResolution(96, 96)
$g = [System.Drawing.Graphics]::FromImage($preview)
try {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::FromArgb(255, 4, 5, 8))

    $source = [System.Drawing.Bitmap]::FromFile($sourcePath)
    try {
        $cm = New-Object System.Drawing.Imaging.ColorMatrix
        $cm.Matrix00 = 1.22
        $cm.Matrix11 = 1.18
        $cm.Matrix22 = 1.15
        $cm.Matrix33 = 1.0
        $cm.Matrix40 = 0.04
        $cm.Matrix41 = 0.04
        $cm.Matrix42 = 0.05
        $attrs = New-Object System.Drawing.Imaging.ImageAttributes
        $attrs.SetColorMatrix($cm)
        $dest = New-Object System.Drawing.Rectangle 0, 0, $finalW, $finalH
        $g.DrawImage($source, $dest, 0, 0, $source.Width, $source.Height, [System.Drawing.GraphicsUnit]::Pixel, $attrs)
        $attrs.Dispose()
    }
    finally {
        $source.Dispose()
    }

    $scrim = New-Object System.Drawing.Drawing2D.LinearGradientBrush (
        (New-Object System.Drawing.Point 0, 0),
        (New-Object System.Drawing.Point 0, 430),
        [System.Drawing.Color]::FromArgb(160, 0, 0, 0),
        [System.Drawing.Color]::FromArgb(0, 0, 0, 0)
    )
    $g.FillRectangle($scrim, 0, 0, $finalW, 430)
    $scrim.Dispose()

    $centerFmt = New-Object System.Drawing.StringFormat
    $centerFmt.Alignment = [System.Drawing.StringAlignment]::Center
    $centerFmt.LineAlignment = [System.Drawing.StringAlignment]::Near
    $rightFmt = New-Object System.Drawing.StringFormat
    $rightFmt.Alignment = [System.Drawing.StringAlignment]::Far
    $rightFmt.LineAlignment = [System.Drawing.StringAlignment]::Center

    $brandFont = Get-UiFont "Bahnschrift" 78 ([System.Drawing.FontStyle]::Bold)
    $subFont = Get-UiFont "Bahnschrift" 52 ([System.Drawing.FontStyle]::Bold)
    $alertFont = Get-UiFont "Segoe UI" 28 ([System.Drawing.FontStyle]::Regular)

    $cream = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 232, 232, 228))
    $silver = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 196, 198, 202))
    $orange = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 229, 154, 76))
    $shadow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(180, 0, 0, 0))
    $barFill = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(220, 18, 18, 18))
    $barText = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 230, 230, 226))
    $critFill = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(210, 90, 28, 22))

    try {
        $brand = "VANILLA UI+"
        $sub = $Subtitle.ToUpperInvariant()
        $brandRect = New-Object System.Drawing.RectangleF 40, 88, ($finalW - 80), 140
        $g.DrawString($brand, $brandFont, $shadow, (New-Object System.Drawing.RectangleF ($brandRect.X + 3), ($brandRect.Y + 4), $brandRect.Width, $brandRect.Height), $centerFmt)
        $g.DrawString($brand, $brandFont, $silver, (New-Object System.Drawing.RectangleF ($brandRect.X - 1), ($brandRect.Y - 1), $brandRect.Width, $brandRect.Height), $centerFmt)
        $g.DrawString($brand, $brandFont, $cream, $brandRect, $centerFmt)

        $subRect = New-Object System.Drawing.RectangleF 40, 216, ($finalW - 80), 80
        $g.DrawString($sub, $subFont, $shadow, (New-Object System.Drawing.RectangleF ($subRect.X + 2), ($subRect.Y + 2), $subRect.Width, $subRect.Height), $centerFmt)
        $g.DrawString($sub, $subFont, $orange, $subRect, $centerFmt)

        if ($ShowAlerts) {
            $labels = @(
                "Need meal source",
                "Need defenses",
                "Need warm clothes",
                "Need recreation"
            )
            $barW = 400
            $barH = 48
            $gap = 8
            $barX = $finalW - 36 - $barW
            $stackH = ($labels.Count * $barH) + (($labels.Count - 1) * $gap)
            $barY = $finalH - 56 - $stackH
            for ($i = 0; $i -lt $labels.Count; $i++) {
                $y = $barY + ($i * ($barH + $gap))
                $rect = New-Object System.Drawing.Rectangle $barX, $y, $barW, $barH
                $fill = if ($i -eq 1) { $critFill } else { $barFill }
                $g.FillRectangle($fill, $rect)
                $textRect = New-Object System.Drawing.RectangleF ($barX + 12), $y, ($barW - 24), $barH
                $g.DrawString($labels[$i], $alertFont, $barText, $textRect, $rightFmt)
            }
        }
    }
    finally {
        $brandFont.Dispose()
        $subFont.Dispose()
        $alertFont.Dispose()
        $cream.Dispose()
        $silver.Dispose()
        $orange.Dispose()
        $shadow.Dispose()
        $barFill.Dispose()
        $barText.Dispose()
        $critFill.Dispose()
        $centerFmt.Dispose()
        $rightFmt.Dispose()
    }

    $preview.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $g.Dispose()
    $preview.Dispose()
}

Write-Output $previewPath
