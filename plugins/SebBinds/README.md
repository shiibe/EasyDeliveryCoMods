# SebBinds

<h1>
<p align="center">
  <img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/main/plugins/SebBinds/assets/icon.png" alt="Logo" width="128">
  <br>SebBinds
</h1>
  <p align="center">
    Centralized binds UI + input rebinding layer for my Easy Delivery Co. mods.
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
- Other mods can add pages via `SebBindsApi.RegisterActionsPage(...)`

## Screenshots
(Coming soon)

## Installation
Dependencies
- `BepInEx-BepInExPack-5.4.2304`
- `shiibe-SebCore-1.0.0`

Install
- r2modman/Thunderstore: https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebBinds/
- Manual: copy `SebBinds.dll` to `BepInEx/plugins/SebBinds/`

## In-game menu
- Open `seb.exe` (SebCore) then launch `Binds`
- Pick a scheme and bind actions
- Wheel scheme only listens to the Logitech wheel device (and requires calibration first)

## Build
- Build: `dotnet build EasyDeliveryCoMods.sln -c Release`
- Package all: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-all.ps1 -Version 1.0.0`
