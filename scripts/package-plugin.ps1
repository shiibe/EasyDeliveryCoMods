param(
    [Parameter(Mandatory = $true)]
    [string]$Plugin,

    [string]$Version,
    [string]$Configuration = "Release"
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

function Write-ManifestWithVersion($srcManifestPath, $dstManifestPath, $version)
{
    $manifest = Get-Content $srcManifestPath -Raw | ConvertFrom-Json
    $manifest.version_number = $version
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $dstManifestPath
}

function Write-ChangelogWithHeader($srcChangelogPath, $dstChangelogPath, $version)
{
    if (-not (Test-Path $srcChangelogPath))
    {
        return
    }

    $changelog = Get-Content $srcChangelogPath -Raw
    if ($changelog -notmatch "(?m)^##\s+$version\s*$")
    {
        $changelog = "## $version`r`n- Packaged build`r`n`r`n" + $changelog.Trim()
    }
    Set-Content -Path $dstChangelogPath -Value $changelog
}

$repoRoot = Get-RepoRoot
$distRoot = Join-Path $repoRoot "dist"
$stageRoot = Join-Path $distRoot ".staging"

$def = Get-PluginDef $Plugin
if ($def -eq $null)
{
    throw "Unknown plugin '$Plugin'. Expected one of: " + ((Get-PluginDefs | ForEach-Object { $_.Name }) -join ", ")
}

$name = $def.Name
$pluginRoot = Join-Path $repoRoot ("plugins\" + $name)
$manifestPath = Join-Path $pluginRoot "manifest.json"
$readmePath = Join-Path $pluginRoot "README.md"
$changelogPath = Join-Path $pluginRoot "CHANGELOG.md"
$iconPath = Join-Path $pluginRoot "assets\icon.png"

if (-not (Test-Path $manifestPath)) { throw "Missing manifest.json for $name at $manifestPath" }
if (-not (Test-Path $readmePath)) { throw "Missing README.md for $name at $readmePath" }
if (-not (Test-Path $changelogPath)) { throw "Missing CHANGELOG.md for $name at $changelogPath" }
if (-not (Test-Path $iconPath)) { throw "Missing assets\icon.png for $name at $iconPath" }

if ([string]::IsNullOrWhiteSpace($Version))
{
    $Version = Read-ManifestVersion $manifestPath
}

if (-not (Test-Path $distRoot))
{
    New-Item -ItemType Directory -Force -Path $distRoot | Out-Null
}
if (-not (Test-Path $stageRoot))
{
    New-Item -ItemType Directory -Force -Path $stageRoot | Out-Null
}

Write-Host "Building $name ($Configuration)..."
$csprojPath = Join-Path $repoRoot $def.Csproj
dotnet build $csprojPath -c $Configuration

$outputDir = Join-Path (Split-Path -Parent $csprojPath) ("bin\\" + $Configuration + "\\net472")
$dllPath = Join-Path $outputDir ($name + ".dll")
$pdbPath = Join-Path $outputDir ($name + ".pdb")
if (-not (Test-Path $dllPath))
{
    throw "Build output not found at $dllPath"
}

Write-Host "Packaging $name $Version..."
$stage = Join-Path $stageRoot $name
if (Test-Path $stage)
{
    Remove-Item -Recurse -Force $stage
}

$stagePlugins = Join-Path $stage ("BepInEx\\plugins\\" + $name)
New-Item -ItemType Directory -Force -Path $stagePlugins | Out-Null

Copy-Item $dllPath -Destination $stagePlugins -Force
if (Test-Path $pdbPath)
{
    Copy-Item $pdbPath -Destination $stagePlugins -Force
}

foreach ($extra in $def.ExtraDlls)
{
    $extraPath = Join-Path $outputDir $extra
    if (Test-Path $extraPath)
    {
        Copy-Item $extraPath -Destination $stagePlugins -Force
    }
}

$sfxDir = Join-Path $outputDir "sfx"
if (Test-Path $sfxDir)
{
    Copy-Item $sfxDir -Destination (Join-Path $stagePlugins "sfx") -Recurse -Force
}

Write-ManifestWithVersion $manifestPath (Join-Path $stage "manifest.json") $Version
Copy-Item $readmePath -Destination (Join-Path $stage "README.md") -Force
Write-ChangelogWithHeader $changelogPath (Join-Path $stage "CHANGELOG.md") $Version
Copy-Item $iconPath -Destination (Join-Path $stage "icon.png") -Force

$zipPath = Join-Path $distRoot ("${name}_${Version}.zip")
if (Test-Path $zipPath)
{
    Remove-Item -Force $zipPath
}
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zipPath

Write-Host "Created: $zipPath"
