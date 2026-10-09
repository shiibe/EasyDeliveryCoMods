function Get-PluginDefs()
{
    return @(
        @{ Name = "SebCore"; Csproj = "plugins/SebCore/src/SebCore/SebCore.csproj"; ExtraDlls = @(); },
        @{ Name = "SebBinds"; Csproj = "plugins/SebBinds/src/SebBinds/SebBinds.csproj"; ExtraDlls = @(); },
        @{ Name = "SebUltrawide"; Csproj = "plugins/SebUltrawide/src/SebUltrawide/SebUltrawide.csproj"; ExtraDlls = @(); },
        @{ Name = "SebTweaks"; Csproj = "plugins/SebTweaks/src/SebTweaks/SebTweaks.csproj"; ExtraDlls = @(); },
        @{ Name = "SebTruck"; Csproj = "plugins/SebTruck/src/SebTruck/SebTruck.csproj"; ExtraDlls = @(); },
        @{ Name = "SebLogiWheel"; Csproj = "plugins/SebLogiWheel/src/SebLogiWheel/SebLogiWheel.csproj"; ExtraDlls = @("LogitechSteeringWheelEnginesWrapper.dll"); }
    )
}
