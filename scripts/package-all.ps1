param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$Configuration = "Release",
    [string[]]$Only = @()
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$distRoot = Join-Path $repoRoot "dist"
$stageRoot = Join-Path $distRoot ".staging"

$plugins = @(
    @{ Name = "SebCore";     Csproj = "plugins/SebCore/src/SebCore/SebCore.csproj";     ExtraDlls = @(); },
    @{ Name = "SebBinds";    Csproj = "plugins/SebBinds/src/SebBinds/SebBinds.csproj";  ExtraDlls = @(); },
    @{ Name = "SebUltrawide";Csproj = "plugins/SebUltrawide/src/SebUltrawide/SebUltrawide.csproj"; ExtraDlls = @(); },
    @{ Name = "SebTruck";    Csproj = "plugins/SebTruck/src/SebTruck/SebTruck.csproj";  ExtraDlls = @(); },
    @{ Name = "SebLogiWheel";Csproj = "plugins/SebLogiWheel/src/SebLogiWheel/SebLogiWheel.csproj"; ExtraDlls = @("LogitechSteeringWheelEnginesWrapper.dll"); }
)

if ($Only.Count -eq 1 -and $Only[0] -match "[,;]")
{
    $Only = $Only[0].Split(@(',', ';')) | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }
}

if ($Only.Count -gt 0)
{
    $plugins = $plugins | Where-Object { $Only -contains $_.Name }
    if ($plugins.Count -eq 0)
    {
        throw "No matching plugins for -Only: $($Only -join ', ')"
    }
}

if (Test-Path $distRoot)
{
    Remove-Item -Recurse -Force $distRoot
}
New-Item -ItemType Directory -Force -Path $distRoot | Out-Null
New-Item -ItemType Directory -Force -Path $stageRoot | Out-Null

Write-Host "Building solution ($Configuration)..."
dotnet build (Join-Path $repoRoot "EasyDeliveryCoMods.sln") -c $Configuration

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

foreach ($p in $plugins)
{
    $name = $p.Name
    $pluginRoot = Join-Path $repoRoot ("plugins\\" + $name)

    $manifestPath = Join-Path $pluginRoot "manifest.json"
    $readmePath = Join-Path $pluginRoot "README.md"
    $changelogPath = Join-Path $pluginRoot "CHANGELOG.md"
    $iconPath = Join-Path $pluginRoot "assets\\icon.png"

    if (-not (Test-Path $manifestPath))
    {
        throw "Missing manifest.json for $name at $manifestPath"
    }
    if (-not (Test-Path $readmePath))
    {
        throw "Missing README.md for $name at $readmePath"
    }
    if (-not (Test-Path $changelogPath))
    {
        throw "Missing CHANGELOG.md for $name at $changelogPath"
    }
    if (-not (Test-Path $iconPath))
    {
        throw "Missing assets\\icon.png for $name at $iconPath"
    }

    Write-Host "Packaging $name..."

    $csprojPath = Join-Path $repoRoot $p.Csproj
    $outputDir = Join-Path (Split-Path -Parent $csprojPath) ("bin\\" + $Configuration + "\\net472")
    $dllPath = Join-Path $outputDir ($name + ".dll")
    $pdbPath = Join-Path $outputDir ($name + ".pdb")
    if (-not (Test-Path $dllPath))
    {
        throw "Build output not found at $dllPath"
    }

    $stage = Join-Path $stageRoot $name
    $stagePlugins = Join-Path $stage ("BepInEx\\plugins\\" + $name)
    New-Item -ItemType Directory -Force -Path $stagePlugins | Out-Null

    Copy-Item $dllPath -Destination $stagePlugins -Force
    if (Test-Path $pdbPath)
    {
        Copy-Item $pdbPath -Destination $stagePlugins -Force
    }

    foreach ($extra in $p.ExtraDlls)
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

    $emissivesDir = Join-Path $outputDir "emissives"
    if (Test-Path $emissivesDir)
    {
        Copy-Item $emissivesDir -Destination (Join-Path $stagePlugins "emissives") -Recurse -Force
    }

    Write-ManifestWithVersion $manifestPath (Join-Path $stage "manifest.json") $Version
    Copy-Item $readmePath -Destination (Join-Path $stage "README.md") -Force
    Write-ChangelogWithHeader $changelogPath (Join-Path $stage "CHANGELOG.md") $Version
    Copy-Item $iconPath -Destination (Join-Path $stage "icon.png") -Force

    $zipPath = Join-Path $distRoot ("${name}_${Version}.zip")
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zipPath
    Write-Host "Created: $zipPath"
}
