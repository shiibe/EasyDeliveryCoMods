<h1>
<p align="center">
  <img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebUltrawide/assets/icon.png" alt="Logo" width="128">
  <br>SebUltrawide
</h1>
  <p align="center">
    Ultrawide fixes + a few extra graphics knobs for Easy Delivery Co.
    <br />
    <a href="#about">About</a>
    ·
    <a href="#features">Features</a>
    ·
    <a href="#screenshots">Screenshots</a>
    ·
    <a href="#installation">Installation</a>
    ·
    <a href="#configuration">Configuration</a>
    ·
    <a href="#in-game-menu">In-Game Menu</a>
    ·
    <a href="#build">Build</a>
  </p>
</p>
<hr/>

## About
This is a BepInEx + Harmony mod that fixes Easy Delivery Co's ultrawide presentation and adds an in-game settings panel (launched from SebCore).

Ultrawide behavior is optional: set `aspect_ratio=default` to keep the game's original presentation while still using the extra settings.

- Full-width gameplay viewport on ultrawide (with optional aspect override)
- Menu/pause/transition overlay scaling so fades/backdrops cover the screen
- HUD rendering fixes (`pixelPerfectView`/`MiniRenderer`) so UI and world-space labels stay aligned (even with pixelation)
- In-game settings: FOV (1st/3rd person), pixelation, view distance

## Features
- Full-width gameplay viewport on ultrawide
- Optional aspect ratio override for gameplay + menu cameras
- Overlay fixes (menus, pause, transitions) scaled to full width
- HUD scaling and positioning fixes
- In-game settings menu launched from SebCore
  - Separate `1st Person` / `3rd Person` FOV sliders (max 110)
  - `Pixelation` slider for the 3D view (None/Finer/Fine/Default/Large)
  - `View Distance` slider (Near/Default/Far/Max)
  - Automatically skips FOV overrides while inside buildings

## Screenshots
<table>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebUltrawide/assets/screenshots/1.jpg" alt="Screenshot 1" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebUltrawide/assets/screenshots/2.jpg" alt="Screenshot 2" width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebUltrawide/assets/screenshots/3.jpg" alt="Screenshot 3" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebUltrawide/assets/screenshots/4.jpg" alt="Screenshot 4" width="100%"></td>
  </tr>
</table>

## Installation
Dependencies
- `BepInEx-BepInExPack-5.4.2304`
- `shiibe-SebCore-1.0.2`

Install
- r2modman/Thunderstore: https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebUltrawide/
- Manual: copy `SebUltrawide.dll` to `BepInEx/plugins/SebUltrawide/`

## Configuration
- Config file: `BepInEx/config/shibe.easydeliveryco.sebultrawide.cfg`
- `aspect_ratio`: `default`, `auto`, `w:h` (e.g. `21:9`), or a decimal ratio (e.g. `2.39`)
  - Use `aspect_ratio=default` to disable ultrawide fixes while keeping the in-game settings (FOV/pixelation/view distance).
- `Logging.debug_logging` (bool): Debug info about applied adjustments
- `Logging.perf_logging` (bool): Periodic perf counters (noisy)
- `Logging.perf_log_interval_seconds` (float): How often to emit perf logs

## In-Game Menu
- Open `mods.exe` (SebCore) then launch `Graphics`
- FOV: separate saved values for `1st Person` and `3rd Person`
- Pixelation: adjusts the 3D view render target (does not affect HUD/menu rendering)
- View Distance: adjusts gameplay camera draw distance + LOD/shadow distance
- Saved in PlayerPrefs (not the BepInEx config)

## Build
- Build: `dotnet build EasyDeliveryCoMods/EasyDeliveryCoMods.sln -c Release`
- Package all: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-all.ps1 -Version 1.0.0`
