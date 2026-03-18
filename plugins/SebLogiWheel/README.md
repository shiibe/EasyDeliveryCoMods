<h1>
<p align="center">
  <img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/main/plugins/SebLogiWheel/assets/icon.png" alt="Logo" width="128">
  <br>SebLogiWheel
</h1>
  <p align="center">
    Logitech wheel support for Easy Delivery Co.
    <br />
    <a href="#about">About</a>
    ·
    <a href="#features">Features</a>
    ·
    <a href="#compatibility">Compatibility</a>
    ·
    <a href="#screenshots">Screenshots</a>
    ·
    <a href="#installation">Installation</a>
    ·
    <a href="#configuration">Configuration</a>
    ·
    <a href="#in-game-menu">In-game menu</a>
    ·
    <a href="#troubleshooting">Troubleshooting</a>
    ·
    <a href="#build">Build</a>
  </p>
</p>
<hr/>

## About
SebLogiWheel adds Logitech wheel input + Force Feedback (FFB) support.

Wheel bindings live in `SebBinds` (Binds -> Wheel). Driving feature extensions (manual transmission / ignition / HUD readouts) live in `SebTruck`.

## Features
- Wheel steering + pedals
- Force Feedback (FFB)
- Calibration + axis mapping menu
- Integrates with SebBinds: Wheel scheme for binds + axes

## Compatibility
- Intended for modern Logitech wheels supported by Logitech G HUB / LGS.
- Tested on: G920
- Likely compatible: G29/G923 and similar Logitech wheels (not guaranteed).

## Screenshots
<table>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/main/plugins/SebLogiWheel/assets/screenshots/1.jpg" alt="Screenshot 1" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/main/plugins/SebLogiWheel/assets/screenshots/2.jpg" alt="Screenshot 2" width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/main/plugins/SebLogiWheel/assets/screenshots/3.jpg" alt="Screenshot 3" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/main/plugins/SebLogiWheel/assets/screenshots/4.jpg" alt="Screenshot 4" width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/main/plugins/SebLogiWheel/assets/screenshots/5.jpg" alt="Screenshot 5" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/main/plugins/SebLogiWheel/assets/screenshots/6.jpg" alt="Screenshot 6" width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/main/plugins/SebLogiWheel/assets/screenshots/7.jpg" alt="Screenshot 7" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/main/plugins/SebLogiWheel/assets/screenshots/8.jpg" alt="Screenshot 8" width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/main/plugins/SebLogiWheel/assets/screenshots/9.jpg" alt="Screenshot 9" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/main/plugins/SebLogiWheel/assets/screenshots/10.jpg" alt="Screenshot 10" width="100%"></td>
  </tr>
</table>

## Installation
Dependencies
- `BepInEx-BepInExPack-5.4.2304`
- `shiibe-SebCore-1.0.0`
- `shiibe-SebBinds-1.0.0`

Force Feedback (FFB)
- Install Logitech G HUB (or older Logitech Gaming Software) so the wheel drivers/components are present.
- `LogitechSteeringWheelEnginesWrapper.dll` must be alongside the plugin DLL (the build/package includes it).

Install
- r2modman/Thunderstore: https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebLogiWheel/
- Manual: copy `SebLogiWheel.dll` to `BepInEx/plugins/SebLogiWheel/`
- FFB DLL: make sure `LogitechSteeringWheelEnginesWrapper.dll` is alongside `SebLogiWheel.dll`

Quick start
1. Install the mod and start the game once.
2. Open `seb.exe` (SebCore) then launch `Wheel`.
3. Run calibration if needed.
4. Open `Binds` -> `Wheel` and bind buttons/axes.

## Configuration
- Config file: `BepInEx/config/shibe.easydeliveryco.logiwheel.cfg`

General
- `enable_mod` (bool, default: `true`): Enables/disables the mod entirely.
- `ignore_xinput_controllers` (bool, default: `true`): Pass `ignoreXInputControllers` to the Logitech SDK init (recommended).

Debug
- `log_detected_devices` (bool, default: `true`): Log joystick names detected by Unity on startup.
- `debug_logging` (bool, default: `false`): Log debug information.

## In-game menu
- Open `seb.exe` (SebCore) then launch `Wheel`.
- Use this menu for wheel-specific setup (calibration, axis mapping, FFB tuning).

## Troubleshooting
My wheel/pedals aren't working or are stuck at full throttle.
- Make sure Logitech G HUB (or older Logitech Gaming Software) is installed.
- Open `seb.exe` -> `Wheel` and check `Axis Mapping`.
- Run `Calibration` and follow the prompts.

The SDK fails to load or I get errors about missing DLLs.
- Make sure `LogitechSteeringWheelEnginesWrapper.dll` is in the same folder as `SebLogiWheel.dll`.

## Build
- Build: `dotnet build EasyDeliveryCoMods.sln -c Release`
- Package all: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-all.ps1 -Version 1.0.0`
