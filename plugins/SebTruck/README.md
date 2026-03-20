<h1>
<p align="center">
  <img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTruck/assets/icon.png" alt="Logo" width="128">
  <br>SebTruck
</h1>
  <p align="center">
    Vehicle and driving upgrades for Easy Delivery Co.
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
SebTruck adds driving upgrades like manual transmission, ignition controls, and compact HUD readouts (speed/tach/gear) while you drive for just a bit more realism.

## Features
- Manual transmission mode with shift up/down binds, and is bindable to a toggle for those who want to switch between auto/manual on the fly.
- Engine overrev sound when nearing the redline of the tachometer.
- Compact HUD elements for speed, tachometer, and gear display that can be toggled on/off and moved.
- Turn signals that actually glow and react to steering input. Also a 4-way hazard mode.
- Ignition toggle with hold-to-start behavior and sound effect.
- Change selected Bobblehead and Truck Paint in the menu (if you have them unlocked).

## Screenshots
<table>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTruck/assets/screenshots/1.gif" alt="Screenshot 1" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTruck/assets/screenshots/2.gif" alt="Screenshot 2" width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTruck/assets/screenshots/3.gif" alt="Screenshot 3" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTruck/assets/screenshots/4.jpg" alt="Screenshot 4" width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTruck/assets/screenshots/5.jpg" alt="Screenshot 5" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTruck/assets/screenshots/6.jpg" alt="Screenshot 6" width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTruck/assets/screenshots/7.jpg" alt="Screenshot 7" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTruck/assets/screenshots/8.jpg" alt="Screenshot 8" width="100%"></td>
  </tr>
</table>

## Installation
Dependencies
- `BepInEx-BepInExPack-5.4.2304`
- `shiibe-SebCore-1.0.1`
- `shiibe-SebBinds-1.0.2`

Install
- r2modman/Thunderstore: https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebTruck/
- Manual: copy `SebTruck.dll` to `BepInEx/plugins/SebTruck/`

## In-game menu
- Open `mods.exe` (SebCore) then launch `Truck`
- Binds: open `Binds` then use the `Truck` binds page

## Binds
Truck page:
- `Ignition Toggle`
- `L Indicator`
- `R Indicator`
- `4Way Indicator`
- `Toggle Gearbox`
- `Shift Up` / `Shift Down`

## Configuration
- Config file: `BepInEx/config/shibe.easydeliveryco.sebtruck.cfg`
- Optional SFX: place `ignition_on.wav` at `BepInEx/plugins/SebTruck/sfx/ignition_on.wav`
- `Debug.debug_logging` (bool): Verbose logging
- `Ignition.sfx_on_path` (string): Ignition ON sound file name inside `sfx/`
- `Ignition.sfx_volume` (float): Ignition sound volume (0..1)

## Future plans
- Add indicator blinking
- Custom truck sound effect overrides
- Better HUD elements

## Build
- Build: `dotnet build EasyDeliveryCoMods.sln -c Release`
- Package all: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-all.ps1 -Version 1.0.4`
