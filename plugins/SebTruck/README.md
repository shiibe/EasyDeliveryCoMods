# SebTruck

<h1>
<p align="center">
  <img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTruck/assets/icon.png" alt="Logo" width="128">
  <br>SebTruck
</h1>
  <p align="center">
    Driving upgrades for Easy Delivery Co.
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
    <a href="#configuration">Configuration</a>
    ·
    <a href="#build">Build</a>
  </p>
</p>
<hr/>

## About
SebTruck adds practical driving upgrades like manual transmission, ignition controls, and compact HUD readouts (speed/tach/gear) while you drive.

## Features
- Manual transmission mode (gears, neutral, reverse)
- Ignition toggle with hold-to-start behavior
- HUD readouts (speed/tach/gear)
- Optional ignition sound (`ignition_on.wav`)
- Works with any input bindings from SebBinds

## Screenshots
Recommended shots:
- `assets/screenshots/1.jpg`: HUD readouts (speed/tach/gear) while driving
- `assets/screenshots/2.jpg`: Truck menu settings
- `assets/screenshots/3.jpg`: SebBinds `Truck` page (Ignition/Shift binds)

## Installation
Dependencies
- `BepInEx-BepInExPack-5.4.2304`
- `shiibe-SebCore-1.0.0`
- `shiibe-SebBinds-1.0.0`

Install
- r2modman/Thunderstore: https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebTruck/
- Manual: copy `SebTruck.dll` to `BepInEx/plugins/SebTruck/`

## In-game menu
- Open `mods.exe` (SebCore) then launch `Truck`
- Binds: open `Binds` then use the `Truck` binds page

## Configuration
- Config file: `BepInEx/config/shibe.easydeliveryco.sebtruck.cfg`
- Optional SFX: place `ignition_on.wav` at `BepInEx/plugins/SebTruck/sfx/ignition_on.wav`
- `Debug.debug_logging` (bool): Verbose logging
- `Ignition.sfx_on_path` (string): Ignition ON sound file name inside `sfx/`
- `Ignition.sfx_volume` (float): Ignition sound volume (0..1)

## Build
- Build: `dotnet build EasyDeliveryCoMods.sln -c Release`
- Package all: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-all.ps1 -Version 1.0.0`
