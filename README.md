# EasyDeliveryCoMods

Monorepo for my Easy Delivery Co. mods.

## Layout

- `plugins/SebCore` - core mod (desktop/menu + shared utilities)
- `plugins/SebBinds` - keybinding menu (keyboard/controller)
- `plugins/SebUltrawide` - ultrawide settings (`wide` desktop app)
- `plugins/SebLogiWheel` - Logitech wheel support (`wheel` desktop app)
- `plugins/SebTruck` - vehicle/transmission tweaks

## Local build config

This repo expects a working r2modman profile folder (with `BepInEx/core`) and the game install folder.

Defaults:
- `ProfileDir`: `C:\Users\Shibe\AppData\Roaming\r2modmanPlus-local\EasyDeliveryCo\profiles\SebCore`

Override per machine with either:
- env vars: `EASYDELIVERYCO_GAME_DIR`, `EASYDELIVERYCO_PROFILE_DIR`
- or `EasyDeliveryCoMods/Directory.Build.props.local` (gitignored)
