<h1>
<p align="center">
  <img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTweaks/assets/icon.png" alt="Logo" width="128">
  <br>SebTweaks
</h1>
  <p align="center">
    A small tweaks menu for Easy Delivery Co.
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
SebTweaks adds an in-game tweaks menu (launched from SebCore) for small quality-of-life and sandbox options.

## Features
SebTweaks currently includes the following options:

**Gameplay**

| Option | Values | Notes |
| --- | --- | --- |
| Job Payout | `0.10x` - `5.00x` | Also affects the job list/menu payout display |
| Gas Price | `0.10x` - `3.00x` | Multiplies gas station prices |
| Gas Use | `0.10x` - `3.00x` | Multiplies fuel consumption |
| Energy Loss | `0.10x` - `3.00x` | Multiplies energy drain |
| Temp Loss | `0.10x` - `3.00x` | Multiplies freezing/temperature loss |
| Ice Cracking | `on/off` | Toggles falling-through-ice mechanic |

**Atmosphere**

| Option | Values | Notes |
| --- | --- | --- |
| Fog | `0.00x` - `3.00x` | Fog density multiplier |
| World Light | `0.00x` - `2.00x` | Multiplies ambient + fog color brightness |
| Light Red | `0.00x` - `2.00x` | Multiplies ambient + fog red channel |
| Light Green | `0.00x` - `2.00x` | Multiplies ambient + fog green channel |
| Light Blue | `0.00x` - `2.00x` | Multiplies ambient + fog blue channel |

**Time & Weather**

| Option | Values | Notes |
| --- | --- | --- |
| Time | `00:00` - `23:59` | Tracks current world time unless Freeze Time is on |
| Freeze Time | `on/off` | Freezes time at the slider value |
| Weather | `Auto/Manual` | Auto = vanilla behavior |
| Intensity | `0%` - `100%` | Manual weather intensity |
| Preset | `Clear/Snow/Storm` | Cycles a few common intensity presets |

**Cheats**

| Option | Values | Notes |
| --- | --- | --- |
| Add/Remove Money | `$10/$20/$50/$100` | Adds/removes money instantly |
| Energy | `0%` - `100%` | Sets current energy |
| Fuel | `0%` - `100%` | Sets current fuel |

**God Mode**

| Option | Values | Notes |
| --- | --- | --- |
| No Energy Loss | `on/off` | Prevents energy drain |
| No Gas Loss | `on/off` | Prevents fuel drain |
| No Temp Loss | `on/off` | Prevents freezing |
| Invincible Truck | `on/off` | Disables collision damage |

## Screenshots
<table>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTweaks/assets/screenshots/1.jpg" alt="Screenshot 1" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTweaks/assets/screenshots/2.jpg" alt="Screenshot 2" width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTweaks/assets/screenshots/3.jpg" alt="Screenshot 3" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebTweaks/assets/screenshots/4.jpg" alt="Screenshot 4" width="100%"></td>
  </tr>
</table>

## Installation
Install via Thunderstore / r2modman.

Dependencies:

- `BepInEx-BepInExPack-5.4.2304`
- `shiibe-SebCore-1.0.2`

## In-game menu
Open the in-game desktop icon `mods.exe`, then launch `Tweaks`.

## Build
Build the full repo:

```powershell
dotnet build EasyDeliveryCoMods.sln -c Release
```
