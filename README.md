# EasyDeliveryCoMods

Monorepo for my Easy Delivery Co. mods.

## Layout

- `plugins/SebCore` - core mod (main menu desktop icon `mods.exe` + shared utilities)
- `plugins/SebBinds` - keybinding menu (keyboard/controller)
- `plugins/SebUltrawide` - ultrawide settings (SebCore app: `Graphics`)
- `plugins/SebLogiWheel` - Logitech wheel support (SebCore app: `Wheel`)
- `plugins/SebTruck` - vehicle/transmission tweaks

## Mods

- [SebCore](plugins/SebCore/README.md)
- [SebBinds](plugins/SebBinds/README.md)
- [SebUltrawide](plugins/SebUltrawide/README.md)
- [SebLogiWheel](plugins/SebLogiWheel/README.md)
- [SebTruck](plugins/SebTruck/README.md)

## Local build config

This repo expects a working r2modman profile folder (with `BepInEx/core`) and the game install folder.

Defaults:
- `ProfileDir`: `C:\Users\Shibe\AppData\Roaming\r2modmanPlus-local\EasyDeliveryCo\profiles\SebCore`

Override per machine with either:
- env vars: `EASYDELIVERYCO_GAME_DIR`, `EASYDELIVERYCO_PROFILE_DIR`
- or `EasyDeliveryCoMods/Directory.Build.props.local` (gitignored)
