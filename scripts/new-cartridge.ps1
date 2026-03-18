param(
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [Parameter(Mandatory = $true)]
    [string]$Guid,

    [string]$DisplayName,
    [string]$FileName,
    [string]$Version = "1.0.0",
    [switch]$AddToSolution
)

$ErrorActionPreference = "Stop"

function Get-RepoRoot()
{
    return Split-Path -Parent $PSScriptRoot
}

function Read-ManifestVersion([string]$manifestPath)
{
    if (-not (Test-Path $manifestPath))
    {
        return ""
    }

    $raw = Get-Content -Path $manifestPath -Raw
    $m = [regex]::Match($raw, '"version_number"\s*:\s*"(?<v>\d+\.\d+\.\d+)"')
    if (-not $m.Success)
    {
        return ""
    }
    return $m.Groups["v"].Value
}

if ([string]::IsNullOrWhiteSpace($DisplayName))
{
    $DisplayName = $Name
}
if ([string]::IsNullOrWhiteSpace($FileName))
{
    $FileName = $Name.ToLowerInvariant()
}

$repoRoot = Get-RepoRoot
$templateRoot = Join-Path $repoRoot "templates\CartridgeTemplate"
$dstRoot = Join-Path $repoRoot ("plugins\" + $Name)

if (-not (Test-Path $templateRoot))
{
    throw "Template folder not found: $templateRoot"
}
if (Test-Path $dstRoot)
{
    throw "Destination already exists: $dstRoot"
}

Write-Host "Creating plugin '$Name' in $dstRoot"
New-Item -ItemType Directory -Force -Path $dstRoot | Out-Null
Copy-Item -Path (Join-Path $templateRoot "*") -Destination $dstRoot -Recurse -Force

# Rename the placeholder src folder.
$srcRoot = Join-Path $dstRoot "src"
$placeholderSrc = Join-Path $srcRoot "__MOD_NAME__"
$realSrc = Join-Path $srcRoot $Name
if (Test-Path $placeholderSrc)
{
    Rename-Item -Path $placeholderSrc -NewName $Name
}

# Rename any files with placeholders in their names.
Get-ChildItem -Path $dstRoot -Recurse -File | ForEach-Object {
    $n = $_.Name
    $newName = $n.Replace("__MOD_NAME__", $Name)
    if ($newName -ne $n)
    {
        Rename-Item -Path $_.FullName -NewName $newName
    }
}

# Fill in tokens.
$sebCoreVersion = Read-ManifestVersion (Join-Path $repoRoot "plugins\SebCore\manifest.json")
if ([string]::IsNullOrWhiteSpace($sebCoreVersion))
{
    $sebCoreVersion = "1.0.0"
}
$sebCoreDep = "shiibe-SebCore-$sebCoreVersion"

$replacements = @{
    "__MOD_NAME__" = $Name
    "__MOD_GUID__" = $Guid
    "__MOD_VERSION__" = $Version
    "__DISPLAY_NAME__" = $DisplayName
    "__FILE_NAME__" = $FileName
    "__SEBCORE_DEP__" = $sebCoreDep
}

Get-ChildItem -Path $dstRoot -Recurse -File | ForEach-Object {
    $path = $_.FullName
    $text = Get-Content -Path $path -Raw

    $updated = $text
    foreach ($k in $replacements.Keys)
    {
        $updated = $updated.Replace($k, $replacements[$k])
    }

    if ($updated -ne $text)
    {
        Set-Content -Path $path -Value $updated
    }
}

# Ensure assets folder + placeholder icon.
$assetsDir = Join-Path $dstRoot "assets"
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

$iconSrc = Join-Path $repoRoot "plugins\SebCore\assets\icon.png"
$iconDst = Join-Path $assetsDir "icon.png"
if ((Test-Path $iconSrc) -and (-not (Test-Path $iconDst)))
{
    Copy-Item -Path $iconSrc -Destination $iconDst -Force
}

if ($AddToSolution)
{
    $sln = Join-Path $repoRoot "EasyDeliveryCoMods.sln"
    $csproj = Join-Path $dstRoot ("src\\" + $Name + "\\" + $Name + ".csproj")
    if ((Test-Path $sln) -and (Test-Path $csproj))
    {
        Write-Host "Adding to solution: $csproj"
        dotnet sln $sln add $csproj
    }
}

Write-Host "Done. Next steps:"
Write-Host "- Edit $dstRoot\\manifest.json (name/description/dependencies)"
Write-Host "- Edit $dstRoot\\src\\$Name\\Plugin.cs (register cartridge fields)"
Write-Host "- Add screenshots under $dstRoot\\assets\\screenshots (optional)"
Write-Host "- Build: dotnet build EasyDeliveryCoMods.sln -c Release"
