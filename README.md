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

## Local build config

This repo expects a working r2modman profile folder (with `BepInEx/core`) and the game install folder.

Defaults:
- `ProfileDir`: `C:\Users\Shibe\AppData\Roaming\r2modmanPlus-local\EasyDeliveryCo\profiles\SebCore`

Override per machine with either:
- env vars: `EASYDELIVERYCO_GAME_DIR`, `EASYDELIVERYCO_PROFILE_DIR`
- or `EasyDeliveryCoMods/Directory.Build.props.local` (gitignored)
