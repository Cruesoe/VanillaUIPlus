# Recolors the original bulky cog to muted vanilla play-setting gray.
# Keeps the original 64x64 silhouette; remaps bright fill to #9C9E9C and edges to black.
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$origPath = Join-Path $env:TEMP "cog-original.png"
if (-not (Test-Path $origPath)) {
    cmd /c "cd /d `"$RepoRoot`" && git show e37fb60:Textures/UI/Icons/MainButtons/cog.png > `"$origPath`""
}

$outDir = Join-Path $RepoRoot "Textures\UI\Icons\MainButtons"
$pngPath = Join-Path $outDir "cog.png"
$ddsPath = Join-Path $outDir "cog.dds"

$fill = [System.Drawing.Color]::FromArgb(255, 156, 158, 156)
$line = [System.Drawing.Color]::FromArgb(255, 0, 0, 0)
$empty = [System.Drawing.Color]::FromArgb(0, 0, 0, 0)

function Get-Lum([System.Drawing.Color]$c) {
    return [int](0.299 * $c.R + 0.587 * $c.G + 0.114 * $c.B)
}

$src = [System.Drawing.Bitmap]::FromFile($origPath)
try {
    $w = $src.Width
    $h = $src.Height
    $mask = New-Object 'bool[,]' $h, $w
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $mask[$y, $x] = $src.GetPixel($x, $y).A -gt 32
        }
    }

    $dst = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            if (-not $mask[$y, $x]) {
                $dst.SetPixel($x, $y, $empty)
                continue
            }

            $edge = $false
            foreach ($n in @(@(-1, 0), @(1, 0), @(0, -1), @(0, 1))) {
                $nx = $x + $n[0]
                $ny = $y + $n[1]
                if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $w -or $ny -ge $h -or -not $mask[$ny, $nx]) {
                    $edge = $true
                    break
                }
            }

            if ($edge) {
                $dst.SetPixel($x, $y, $line)
            }
            else {
                $srcPx = $src.GetPixel($x, $y)
                $lum = Get-Lum $srcPx
                if ($lum -ge 210) {
                    $dst.SetPixel($x, $y, $fill)
                }
                elseif ($lum -ge 120) {
                    $t = [Math]::Min(1.0, ($lum - 120) / 90.0)
                    $r = [int]([Math]::Round((1 - $t) * 110 + $t * 156))
                    $g = [int]([Math]::Round((1 - $t) * 112 + $t * 158))
                    $b = [int]([Math]::Round((1 - $t) * 110 + $t * 156))
                    $dst.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $r, $g, $b))
                }
                else {
                    $dst.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 110, 112, 110))
                }
            }
        }
    }

    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    $dst.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

    # Uncompressed BGRA DDS (same layout as prior cog.dds)
    $payload = New-Object byte[] ($w * $h * 4)
    $i = 0
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $c = $dst.GetPixel($x, $y)
            $payload[$i++] = $c.B
            $payload[$i++] = $c.G
            $payload[$i++] = $c.R
            $payload[$i++] = $c.A
        }
    }

    $header = New-Object byte[] 128
    [System.Text.Encoding]::ASCII.GetBytes("DDS ").CopyTo($header, 0)
    [BitConverter]::GetBytes([uint32]124).CopyTo($header, 4)
    [BitConverter]::GetBytes([uint32](0x1 -bor 0x2 -bor 0x4 -bor 0x8 -bor 0x1000)).CopyTo($header, 8)
    [BitConverter]::GetBytes([uint32]$h).CopyTo($header, 12)
    [BitConverter]::GetBytes([uint32]$w).CopyTo($header, 16)
    [BitConverter]::GetBytes([uint32]($w * 4)).CopyTo($header, 20)
    [BitConverter]::GetBytes([uint32]1).CopyTo($header, 28)
    [BitConverter]::GetBytes([uint32]32).CopyTo($header, 76)
    [BitConverter]::GetBytes([uint32]0x41).CopyTo($header, 80)
    [BitConverter]::GetBytes([uint32]32).CopyTo($header, 88)
    [BitConverter]::GetBytes([uint32]16711680).CopyTo($header, 92)   # R mask
    [BitConverter]::GetBytes([uint32]65280).CopyTo($header, 96)      # G mask
    [BitConverter]::GetBytes([uint32]255).CopyTo($header, 100)        # B mask
    [BitConverter]::GetBytes([uint32]4278190080).CopyTo($header, 104) # A mask
    [BitConverter]::GetBytes([uint32]0x1000).CopyTo($header, 108)

    $fs = [System.IO.File]::Create($ddsPath)
    try {
        $fs.Write($header, 0, $header.Length)
        $fs.Write($payload, 0, $payload.Length)
    }
    finally {
        $fs.Close()
    }

    Write-Output $pngPath
    Write-Output $ddsPath
}
finally {
    $src.Dispose()
    $dst.Dispose()
}
