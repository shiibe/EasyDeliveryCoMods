# SebBinds

<h1>
<p align="center">
  <img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebBinds/assets/icon.png" alt="Logo" width="128">
  <br>SebBinds
</h1>
  <p align="center">
    Binds UI + keybind/controller rebinding for Easy Delivery Co, with an API for other mods.
    <br />
    <a href="#about">About</a>
    ·
    <a href="#features">Features</a>
    ·
    <a href="#screenshots">Screenshots</a>
    ·
    <a href="#installation">Installation</a>
    ·
    <a href="#in-game-menu">In-game menu</a>
    ·
    <a href="#build">Build</a>
  </p>
</p>
<hr/>

## About
SebBinds provides the binding UI + runtime input mapping used by the rest of the mod set.

## Features
- Scheme picker: `Controller` / `Keyboard` / `Wheel` (Wheel appears when SebLogiWheel is installed)
- Dedicated `Axes` page for movement/camera/vehicle axes
- Modifier layer (`Modif.`): bind extra inputs by holding the modifier
- Vehicle page includes `1P/3P Camera`
- Other mods can add pages via `SebBindsApi.RegisterActionsPage(...)`

## Screenshots
Recommended shots:
- `assets/screenshots/1.jpg`: Scheme picker + page tabs
- `assets/screenshots/2.jpg`: Binding table showing `Modif.` column
- `assets/screenshots/3.jpg`: Modifier binding capture screen (hold modifier)
- `assets/screenshots/4.jpg`: Axes page
- `assets/screenshots/5.jpg`: Vehicle page with `1P/3P Camera`

## Installation
Dependencies
- `BepInEx-BepInExPack-5.4.2304`
- `shiibe-SebCore-1.0.0`

Install
- r2modman/Thunderstore: https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebBinds/
- Manual: copy `SebBinds.dll` to `BepInEx/plugins/SebBinds/`

## In-game menu
- Open `mods.exe` (SebCore) then launch `Binds`
- Pick a scheme and bind actions
- Wheel scheme only listens to the Logitech wheel device (and requires calibration first)

## Configuration
- Config file: `BepInEx/config/shibe.easydeliveryco.sebbinds.cfg`
- `Logging.debug_logging` (bool): Extra debug logging

## Build
- Build: `dotnet build EasyDeliveryCoMods.sln -c Release`
- Package all: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-all.ps1 -Version 1.0.0`
