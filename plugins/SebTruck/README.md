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
SebTruck adds driving upgrades like manual transmission, Rally transmission settings, ignition controls, turn signals, and a dashboard HUD with round gauges and an odometer for just a bit more realism.

## Features

| Feature | Menu page | Details |
| --- | --- | --- |
| Manual transmission | Handling | Story mode manual with shared Rally shift/direct-gear binds, including H-shifter style `R`, `N`, and `Gear 1-6` inputs |
| Rally transmission | Handling | Auto/Manual setting that unlocks the game's vanilla manual shifting without entering the cheat |
| Overrev sound | — | Engine overrev sound near the tachometer redline |
| Ignition | Ignition | Toggle with hold-to-start behavior and sound effect |
| Turn signals | Turn Signals | Indicators that actually glow and react to steering input, plus a 4-way hazard mode |
| Backup alarm | Backup Alarm | Synthesized beep with volume, beep rate, and tone controls |
| HUD gauges | HUD | Speedometer, tachometer, and gear indicator in `Rally` (the game's rally look) or `Fancy` (round dials) style; Fancy works in Story and Rally mode |
| Odometer | HUD | Rolling odometer and trip meter in km or miles for Story mode, saved per save slot |
| Mirror quality | Tweaks | Rear-view mirror at Original, 2x, 4x, or 8x resolution |
| Upgrades | Equipment | Mount or remove purchased Snow Tires, Ice Chains, and Ram Bar without losing the upgrade |
| Horn | Equipment | Meow (the game's default horn) or Woof |
| Cosmetics | Equipment | Pick your unlocked Bobblehead and Truck Paint |

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
- `shiibe-SebCore-1.1.1`
- `shiibe-SebBinds-1.1.5`

Install
- r2modman/Thunderstore: https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebTruck/
- Manual: copy `SebTruck.dll` to `BepInEx/plugins/SebTruck/`

## In-game menu
- Open `mods.exe` (SebCore) then launch `Truck`
- Binds: open `Binds`, then use the `Truck`, `Rally`, and `Rally Gears` bind pages

## Binds
Truck page:
- `Ignition Toggle`
- `L Indicator`
- `R Indicator`
- `4Way Indicator`
- `Toggle Gearbox`
- Keyboard defaults: `F1` ignition, `F2` left indicator, `F3` right indicator, `F4` hazards.

Shared Rally pages from SebBinds:
- `Clutch`
- `Shift Up` / `Shift Down`
- `Reverse`, `Neutral`, and `Gear 1-6` direct gear binds for Rally manual mode and SebTruck Story manual mode.
- `Shift Mode: Hold` makes held H-shifter gear buttons control the active gear; no held gear means Neutral. `Toggle` keeps normal press-to-shift/direct-gear behavior.

## Configuration
- Config file: `BepInEx/config/shibe.easydeliveryco.sebtruck.cfg`
- Optional SFX: place `ignition_on.wav` at `BepInEx/plugins/SebTruck/sfx/ignition_on.wav`
- `Debug.debug_logging` (bool): Verbose logging
- `Ignition.sfx_on_path` (string): Ignition ON sound file name inside `sfx/`
- `Ignition.sfx_volume` (float): Ignition sound volume (0..1)

Backup Alarm page:
- `Backup Alarm`: On/off, off by default
- `4-Ways`: Automatically flash hazards while the backup alarm is active, off by default
- `Volume`: beep volume
- `Beep Rate`: beep length and matching pause length while reverse is active
- `Tone`: synthesized beep frequency

HUD (Truck > HUD):

- `Style`: `Off`, `Rally`, or `Fancy` in Story mode; `Rally` or `Fancy` in Rally mode. The old text-based Classic style was removed; anyone using it starts on `Off`.
- `Units`, `Speedomtr`, `Tachomtr`, `Gear Ind`: shared by both styles. Tachometer and gear show with manual transmission.
- Fancy dials sit bottom right; the game's radio readout moves up to make room.
- `Odometer` (Fancy): show the total, the trip, or nothing under the speedometer. In Rally mode it shows the game's own Easy Rally distance.
- The odometer and trip are stored in the game's save file, so each save slot has its own mileage and it is written when the game saves. `Reset Trip` zeroes the trip. Tunnel trips between towns aren't driven, so they don't count.

Equipment (Truck > Equipment):

- `Snow Tires` / `Ice Chains` / `Ram Bar`: mount or remove upgrades you have already bought in the game. They stay purchased either way; options for upgrades you don't own are greyed out. All start mounted. An unmounted ram bar is hidden and no longer smashes trees or protects the truck. The game only shows chains at full size while you drive on ice.
- `Horn`: `Meow` (default) or `Woof`. Both are the game's own horn sounds, which have the animal sound mixed in. If the chosen sound isn't loaded in a scene, the game's horn plays instead.
- `Cosmetics`: pick your unlocked `Bobblehead` and `Truck Paint`.
- Reset Defaults on the Equipment page mounts all upgrades and restores the Meow horn.

Mirror quality (Truck > Tweaks):

Mirror quality multiplies both dimensions of the game's rear-view texture (110 x 32 in the Story truck), preserving its aspect ratio. Original is the default and follows the game's existing behavior, including Tweaks/Ultrawide pixelation settings. Higher mirror settings override mirror pixelation only and may cost performance. Reset Defaults on the Tweaks page restores Original. Update Tweaks/Ultrawide alongside SebTruck when using them together.

## Build

- Build: `dotnet build EasyDeliveryCoMods.sln -c Release`
- Package all: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-all.ps1`
