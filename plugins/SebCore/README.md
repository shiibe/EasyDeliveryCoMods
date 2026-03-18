# SebCore

<h1>
<p align="center">
  <img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebCore/assets/icon.png" alt="Logo" width="128">
  <br>SebCore
</h1>
  <p align="center">
    The required launcher/core for my Easy Delivery Co. mods.
    <br />
    <a href="#about">About</a>
    ·
    <a href="#mods">Mods</a>
    ·
    <a href="#screenshots">Screenshots</a>
    ·
    <a href="#installation">Installation</a>
    ·
    <a href="#build">Build</a>
  </p>
</p>
<hr/>

## About
SebCore is the base mod that provides the in-game launcher UI and some shared utilities for my other mods.

- Adds desktop icon/program (`mods.exe`) on the main menu.
- Hosts the launcher window where you open the other mods ("Cartridges").

Cartridges are small, optional sub-mods you can install and enable as needed - like popping different games into the same console. The goal is a modular setup where you only run what you actually use.

### Config
- `Menu.sebcore_icon_name` (string, default: `mods`): Desktop icon file name.
- `Menu.sebcore_icon_x` / `Menu.sebcore_icon_y` (string): Desktop icon position.
- `Maintenance.clear_mod_prefs` (bool): Clears known mod PlayerPrefs at runtime, then flips back to false.
- `Logging.debug_logging` (bool): Extra debug logging.

## Mods
- SebBinds: https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebBinds/ (`../SebBinds/README.md`)
- SebTruck: https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebTruck/ (`../SebTruck/README.md`)
- SebUltrawide: https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebUltrawide/ (`../SebUltrawide/README.md`)
- SebLogiWheel: https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebLogiWheel/ (`../SebLogiWheel/README.md`)

## Screenshots
Recommended shots:
- `assets/screenshots/1.jpg`: Main menu desktop with `mods.exe`
- `assets/screenshots/2.jpg`: SebCore launcher showing installed cartridges

## Installation
Dependencies
- `BepInEx-BepInExPack-5.4.2304`

Install
- r2modman/Thunderstore: https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebCore/
- Manual: copy `SebCore.dll` to `BepInEx/plugins/SebCore/`

## Build
- Build: `dotnet build EasyDeliveryCoMods.sln -c Release`
- Package all: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-all.ps1 -Version 1.0.0`
