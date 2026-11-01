# Checks run by Deploy-Mod before deploying: translation keys match the source, no tiny fonts outside the bar label measure, mod textures exist, both game versions, preview size.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$failures = New-Object System.Collections.Generic.List[string]

$sourceRoot = Join-Path $root 'Source'
$sourceFiles = Get-ChildItem $sourceRoot -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
$sourceText = ($sourceFiles | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"

$keyed = New-Object System.Collections.Generic.HashSet[string]
foreach ($file in Get-ChildItem (Join-Path $root 'Languages\English\Keyed') -Filter *.xml) {
    [xml]$xml = [IO.File]::ReadAllText($file.FullName)
    foreach ($node in $xml.LanguageData.ChildNodes) {
        if ($node.NodeType -eq 'Element' -and -not $keyed.Add($node.Name)) { $failures.Add("Duplicate key $($node.Name)") }
    }
}

# GUI control names share the VUIP. prefix but are not translation keys.
$controlNames = New-Object System.Collections.Generic.HashSet[string]
foreach ($m in [regex]::Matches($sourceText, '(?:ControlName\s*=>|controlName\s*=)\s*"(VUIP\.[A-Za-z.]+)"')) { [void]$controlNames.Add($m.Groups[1].Value) }

$used = New-Object System.Collections.Generic.HashSet[string]
foreach ($m in [regex]::Matches($sourceText, '"(VUIP\.[A-Za-z0-9_.]+)"')) {
    if (-not $controlNames.Contains($m.Groups[1].Value)) { [void]$used.Add($m.Groups[1].Value) }
}

foreach ($key in $used) { if (-not $keyed.Contains($key)) { $failures.Add("Missing translation: $key") } }

# Settings rows add "Tip" to their label key.
foreach ($key in $keyed) {
    $isTip = $key.EndsWith('Tip') -and $used.Contains($key.Substring(0, $key.Length - 3))
    if (-not $used.Contains($key) -and -not $isTip) { $failures.Add("Unused translation: $key") }
}

# BarLabel measures text for WidgetRow.FillableBar, which draws in the tiny font.
foreach ($file in $sourceFiles) {
    $count = ([regex]::Matches([IO.File]::ReadAllText($file.FullName), 'GameFont\.Tiny')).Count
    $allowed = if ($file.Name -eq 'PawnReadoutDrawer.cs') { 1 } else { 0 }
    if ($count -gt $allowed) { $failures.Add("$($file.Name) uses GameFont.Tiny") }
}

# Texture paths are case-sensitive on some systems, so the comparison is ordinal.
$textures = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$textureRoot = Join-Path $root 'Textures'
foreach ($file in Get-ChildItem $textureRoot -Recurse -File) {
    [void]$textures.Add($file.FullName.Substring($textureRoot.Length + 1).Replace('\', '/') -replace '\.[^./]+$', '')
}
foreach ($m in [regex]::Matches($sourceText, '"(UI/VanillaUIPlus/[A-Za-z0-9_/]*[A-Za-z0-9_])"')) {
    if (-not $textures.Contains($m.Groups[1].Value)) { $failures.Add("Missing texture: $($m.Groups[1].Value)") }
}

foreach ($version in '1.6', '1.7') {
    if (-not (Test-Path (Join-Path $root "$version\Assemblies\VanillaUIPlus.dll"))) { $failures.Add("$version\Assemblies\VanillaUIPlus.dll is missing") }
}
[xml]$about = [IO.File]::ReadAllText((Join-Path $root 'About\About.xml'))
$versions = @($about.ModMetaData.supportedVersions.li)
foreach ($version in '1.6', '1.7') { if ($versions -notcontains $version) { $failures.Add("About.xml supportedVersions does not list $version") } }

if ((Get-Item (Join-Path $root 'About\Preview.png')).Length -ge 1000000) { $failures.Add('About\Preview.png is 1,000,000 bytes or more') }

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "FAIL $_" }
    exit 1
}
Write-Host "Test-All passed: $($keyed.Count) keys checked"
