function Assert-PackageVersion([string]$pluginRoot, [string]$version)
{
    $manifestVersion = (Get-Content -LiteralPath (Join-Path $pluginRoot "manifest.json") -Raw | ConvertFrom-Json).version_number
    $pluginName = Split-Path -Leaf $pluginRoot
    $pluginSource = Get-Content -LiteralPath (Join-Path $pluginRoot "src/$pluginName/Plugin.cs") -Raw
    $pluginVersion = [regex]::Match($pluginSource, 'public\s+const\s+string\s+PluginVersion\s*=\s*"(?<v>\d+\.\d+\.\d+)"\s*;').Groups["v"].Value
    if ($version -notmatch '^\d+\.\d+\.\d+$' -or $version -ne $manifestVersion -or $version -ne $pluginVersion)
    {
        throw "Version mismatch for ${pluginName}: requested '$version', manifest '$manifestVersion', plugin '$pluginVersion'. Run bump-plugin.ps1 before packaging."
    }
}

function Assert-ExtraDlls($pluginDef, [string]$outputDir)
{
    foreach ($extra in $pluginDef.ExtraDlls)
    {
        $path = Join-Path $outputDir $extra
        if (-not (Test-Path -LiteralPath $path -PathType Leaf))
        {
            throw "Required DLL missing for $($pluginDef.Name): $path"
        }
    }
}
