param(
    [Parameter(Mandatory = $true)]
    [string]$Plugin,

    [ValidateSet("patch", "minor", "major")]
    [string]$Bump = "patch",

    [string]$Version,
    [string]$Note = "Version bump"
)

$ErrorActionPreference = "Stop"

function Get-RepoRoot()
{
    return Split-Path -Parent $PSScriptRoot
}

function Get-PluginDefs()
{
    return @(
        @{ Name = "SebCore";      Csproj = "plugins/SebCore/src/SebCore/SebCore.csproj";      ExtraDlls = @(); },
        @{ Name = "SebBinds";     Csproj = "plugins/SebBinds/src/SebBinds/SebBinds.csproj";    ExtraDlls = @(); },
        @{ Name = "SebUltrawide"; Csproj = "plugins/SebUltrawide/src/SebUltrawide/SebUltrawide.csproj"; ExtraDlls = @(); },
        @{ Name = "SebTruck";     Csproj = "plugins/SebTruck/src/SebTruck/SebTruck.csproj";    ExtraDlls = @(); },
        @{ Name = "SebLogiWheel"; Csproj = "plugins/SebLogiWheel/src/SebLogiWheel/SebLogiWheel.csproj"; ExtraDlls = @("LogitechSteeringWheelEnginesWrapper.dll"); }
    )
}

function Get-PluginDef([string]$name)
{
    foreach ($p in (Get-PluginDefs))
    {
        if ($p.Name -ieq $name)
        {
            return $p
        }
    }
    return $null
}

function Parse-SemVer([string]$s)
{
    if ([string]::IsNullOrWhiteSpace($s))
    {
        throw "Empty version"
    }

    $m = [regex]::Match($s.Trim(), "^(\d+)\.(\d+)\.(\d+)$")
    if (-not $m.Success)
    {
        throw "Invalid version '$s' (expected x.y.z)"
    }

    return @(
        [int]$m.Groups[1].Value,
        [int]$m.Groups[2].Value,
        [int]$m.Groups[3].Value
    )
}

function Format-SemVer([int]$maj, [int]$min, [int]$pat)
{
    return "${maj}.${min}.${pat}"
}

function Read-ManifestVersion([string]$manifestPath)
{
    $raw = Get-Content -Path $manifestPath -Raw
    $m = [regex]::Match($raw, '"version_number"\s*:\s*"(?<v>\d+\.\d+\.\d+)"')
    if (-not $m.Success)
    {
        throw "Could not find version_number in $manifestPath"
    }
    return $m.Groups["v"].Value
}

function Write-ManifestVersion([string]$manifestPath, [string]$newVersion)
{
    $raw = Get-Content -Path $manifestPath -Raw
    $updated = [regex]::Replace(
        $raw,
        '"version_number"\s*:\s*"\d+\.\d+\.\d+"',
        '"version_number": "' + $newVersion + '"',
        1
    )

    if ($updated -eq $raw)
    {
        throw "Failed to update version_number in $manifestPath"
    }
    Set-Content -Path $manifestPath -Value $updated
}

function Find-PluginCs([string]$pluginRoot)
{
    $src = Join-Path $pluginRoot "src"
    $candidates = Get-ChildItem -Path $src -Recurse -Filter "Plugin.cs" -File -ErrorAction Stop
    if ($candidates.Count -eq 0)
    {
        throw "Could not find Plugin.cs under $src"
    }
    if ($candidates.Count -gt 1)
    {
        throw "Multiple Plugin.cs files found under $src; please disambiguate"
    }
    return $candidates[0].FullName
}

function Read-PluginCsVersion([string]$pluginCsPath)
{
    $raw = Get-Content -Path $pluginCsPath -Raw
    $m = [regex]::Match($raw, 'public\s+const\s+string\s+PluginVersion\s*=\s*"(?<v>\d+\.\d+\.\d+)"\s*;')
    if (-not $m.Success)
    {
        throw "Could not find PluginVersion constant in $pluginCsPath"
    }
    return $m.Groups["v"].Value
}

function Write-PluginCsVersion([string]$pluginCsPath, [string]$newVersion)
{
    $raw = Get-Content -Path $pluginCsPath -Raw
    $updated = [regex]::Replace(
        $raw,
        'public\s+const\s+string\s+PluginVersion\s*=\s*"\d+\.\d+\.\d+"\s*;',
        'public const string PluginVersion = "' + $newVersion + '";',
        1
    )
    if ($updated -eq $raw)
    {
        throw "Failed to update PluginVersion in $pluginCsPath"
    }
    Set-Content -Path $pluginCsPath -Value $updated
}

function Prepend-ChangelogHeader([string]$changelogPath, [string]$newVersion, [string]$note)
{
    if (-not (Test-Path $changelogPath))
    {
        return
    }

    $raw = Get-Content -Path $changelogPath -Raw
    if ($raw -match "(?m)^##\s+$([regex]::Escape($newVersion))\s*$")
    {
        return
    }

    $header = "## $newVersion`r`n- $note`r`n`r`n"
    Set-Content -Path $changelogPath -Value ($header + $raw.Trim())
}

$repoRoot = Get-RepoRoot
$def = Get-PluginDef $Plugin
if ($def -eq $null)
{
    throw "Unknown plugin '$Plugin'. Expected one of: " + ((Get-PluginDefs | ForEach-Object { $_.Name }) -join ", ")
}

$pluginRoot = Join-Path $repoRoot ("plugins\" + $def.Name)
$manifestPath = Join-Path $pluginRoot "manifest.json"
$changelogPath = Join-Path $pluginRoot "CHANGELOG.md"
$pluginCsPath = Find-PluginCs $pluginRoot

if (-not (Test-Path $manifestPath))
{
    throw "Missing manifest.json at $manifestPath"
}

$manifestVer = Read-ManifestVersion $manifestPath
$pluginVer = Read-PluginCsVersion $pluginCsPath
if ($manifestVer -ne $pluginVer)
{
    Write-Host "Warning: manifest version ($manifestVer) != PluginVersion ($pluginVer)" -ForegroundColor Yellow
}

$current = Parse-SemVer $manifestVer

if (-not [string]::IsNullOrWhiteSpace($Version))
{
    $target = Parse-SemVer $Version
}
else
{
    $maj = $current[0]
    $min = $current[1]
    $pat = $current[2]
    if ($Bump -eq "patch") { $pat = $pat + 1 }
    elseif ($Bump -eq "minor") { $min = $min + 1; $pat = 0 }
    else { $maj = $maj + 1; $min = 0; $pat = 0 }
    $target = @($maj, $min, $pat)
}

$newVersion = Format-SemVer $target[0] $target[1] $target[2]

Write-Host "Bumping $($def.Name): $manifestVer -> $newVersion"
Write-ManifestVersion $manifestPath $newVersion
Write-PluginCsVersion $pluginCsPath $newVersion
Prepend-ChangelogHeader $changelogPath $newVersion $Note

Write-Host "Updated: $manifestPath"
Write-Host "Updated: $pluginCsPath"
if (Test-Path $changelogPath)
{
    Write-Host "Updated: $changelogPath"
}
