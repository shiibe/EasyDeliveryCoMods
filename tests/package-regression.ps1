$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $repoRoot "scripts/plugin-defs.ps1")
. (Join-Path $repoRoot "scripts/package-util.ps1")
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("EasyDeliveryCoMods-tests-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path (Join-Path $testRoot "scripts") -Force | Out-Null
Copy-Item -Path (Join-Path $repoRoot "scripts/*.ps1") -Destination (Join-Path $testRoot "scripts")
$checks = 0

function Expect-Failure([scriptblock]$action, [string]$message)
{
    $caught = $null
    try { & $action }
    catch { $caught = $_.Exception.Message }
    if ($caught -notlike "*$message*") { throw "Expected '$message', got '$caught'" }
    $script:checks++
}

foreach ($def in Get-PluginDefs)
{
    $source = Join-Path $repoRoot ("plugins/" + $def.Name)
    $dest = Join-Path $testRoot ("plugins/" + $def.Name)
    $srcDir = Join-Path $dest ("src/" + $def.Name)
    New-Item -ItemType Directory -Force -Path $srcDir, (Join-Path $dest "assets") | Out-Null
    Copy-Item -LiteralPath (Join-Path $source "manifest.json"), (Join-Path $source "README.md"), (Join-Path $source "CHANGELOG.md") -Destination $dest
    Copy-Item -LiteralPath (Join-Path $source "assets/icon.png") -Destination (Join-Path $dest "assets/icon.png")
    Copy-Item -LiteralPath (Join-Path $source ("src/" + $def.Name + "/Plugin.cs")) -Destination (Join-Path $srcDir "Plugin.cs")
    $output = Join-Path $srcDir "bin/Release/net472"
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    Set-Content -LiteralPath (Join-Path $output ($def.Name + ".dll")) -Value "stale build fixture"
    $version = (Get-Content -LiteralPath (Join-Path $dest "manifest.json") -Raw | ConvertFrom-Json).version_number
    Assert-PackageVersion $dest $version
    $checks++
}

# Simulate native build exit codes while exercising the actual packaging scripts.
$global:PackageTestBuildExit = 7
function dotnet { $global:LASTEXITCODE = $global:PackageTestBuildExit }
$single = Join-Path $testRoot "scripts/package-plugin.ps1"
$all = Join-Path $testRoot "scripts/package-all.ps1"
Expect-Failure { & $single -Plugin SebCore } "Build failed"
Expect-Failure { & $all } "Solution build failed"
if (Get-ChildItem -LiteralPath (Join-Path $testRoot "dist") -Filter '*.zip') { throw "Failed build produced a ZIP" }
$checks++
Expect-Failure { & $single -Plugin SebCore -Version '9.9.9' } "Version mismatch"
Expect-Failure { & $all -Version '9.9.9' } "Version mismatch"
$global:PackageTestBuildExit = 0
Expect-Failure { & $single -Plugin SebLogiWheel } "Required DLL missing"
Expect-Failure { & $all } "Required DLL missing"

$wheelOutput = Join-Path $testRoot "plugins/SebLogiWheel/src/SebLogiWheel/bin/Release/net472"
Set-Content -LiteralPath (Join-Path $wheelOutput "LogitechSteeringWheelEnginesWrapper.dll") -Value "wrapper fixture"
& $all
foreach ($def in Get-PluginDefs)
{
    $version = (Get-Content -LiteralPath (Join-Path $testRoot ("plugins/" + $def.Name + "/manifest.json")) -Raw | ConvertFrom-Json).version_number
    if (-not (Test-Path -LiteralPath (Join-Path $testRoot ("dist/" + $def.Name + "_" + $version + ".zip"))))
    {
        throw "Missing package for $($def.Name)"
    }
    $checks++
}

& (Join-Path $testRoot "scripts/bump-plugin.ps1") -Plugin SebTweaks -Note "Test bump"
$tweaksRoot = Join-Path $testRoot "plugins/SebTweaks"
$version = (Get-Content -LiteralPath (Join-Path $tweaksRoot "manifest.json") -Raw | ConvertFrom-Json).version_number
Assert-PackageVersion $tweaksRoot $version
if ((Get-Content -LiteralPath (Join-Path $tweaksRoot "CHANGELOG.md") -First 1) -ne "## $version") { throw "Bump missed changelog" }
$checks++

Write-Host "Passed $checks packaging checks. Fixtures: $testRoot"
