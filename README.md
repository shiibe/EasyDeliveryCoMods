# EasyDeliveryCoMods

Monorepo for my Easy Delivery Co. mods.

## Layout

- `plugins/SebCore` - core mod (main menu desktop icon `mods.exe` + shared utilities)
- `plugins/SebBinds` - keybinding menu (keyboard/controller)
- `plugins/SebUltrawide` - ultrawide settings (SebCore app: `Graphics`)
- `plugins/SebLogiWheel` - Logitech wheel support (SebCore app: `Wheel`)
- `plugins/SebTruck` - vehicle/transmission tweaks

## Templates

- New SebCore cartridge scaffold: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/new-cartridge.ps1 -Name MyCart -Guid com.example.mycart`

## Mods

| Mod | Thunderstore | Docs |
| --- | --- | --- |
| SebCore | https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebCore/ | [README](plugins/SebCore/README.md) |
| SebBinds | https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebBinds/ | [README](plugins/SebBinds/README.md) |
| SebUltrawide | https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebUltrawide/ | [README](plugins/SebUltrawide/README.md) |
| SebLogiWheel | https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebLogiWheel/ | [README](plugins/SebLogiWheel/README.md) |
| SebTruck | https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebTruck/ | [README](plugins/SebTruck/README.md) |
| SebTweaks | https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebTweaks/ | [README](plugins/SebTweaks/README.md) |

## Local build config

This repo expects a working r2modman profile folder (with `BepInEx/core`) and the game install folder.

Defaults:
- `ProfileDir`: `C:\Users\Shibe\AppData\Roaming\r2modmanPlus-local\EasyDeliveryCo\profiles\SebCore`

Override per machine with either:
- env vars: `EASYDELIVERYCO_GAME_DIR`, `EASYDELIVERYCO_PROFILE_DIR`
- or `EasyDeliveryCoMods/Directory.Build.props.local` (gitignored)

Build without copying mods into the game profile:
`dotnet build EasyDeliveryCoMods.sln -c Release /p:DeployToProfile=false`

Release scripts share the plugin list in `scripts/plugin-defs.ps1`. Bump a mod with
`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bump-plugin.ps1 -Plugin SebTweaks -Note "Brief change description"`.
Package with `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-all.ps1`;
each mod uses its own version. Packaging rejects mismatched versions, failed builds,
and missing required DLLs, and does not deploy to the live profile.

Run the dependency-free logic checks with
`dotnet run --project tests/Regression/Regression.csproj` (requires .NET 10 SDK).
Run release-script checks with
`powershell -NoProfile -ExecutionPolicy Bypass -File tests/package-regression.ps1`.
These use in-memory game stand-ins and temporary packaging fixtures; wheel hardware
and live game behavior still need an in-game check.
