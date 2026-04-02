<h1>
<p align="center">
  <img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebCore/assets/icon.png" alt="Logo" width="128">
  <br>SebCore
</h1>
  <p align="center">
    A shared core mod and in-game launcher for Easy Delivery Co. mods.
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

### API
SebCore exposes a small public API so other mods can plug into the launcher.

- `SebCore.CartridgeApps.RegisterApp(...)`: register a custom cartridge so it appears in the SebCore launcher.
  - If more than 10 cartridges are installed, the launcher automatically shows paging controls.
- Full docs: [API Docs](https://github.com/shiibe/EasyDeliveryCoMods/blob/master/plugins/SebBinds/docs/API.md)

Example:
```csharp
SebCore.CartridgeApps.RegisterApp(new SebCore.CartridgeApps.App
{
    DisplayName = "My Cartridge",
    FileName = "mycart",
    PluginGuid = "com.example.mycart",
    ListenerName = "MyCartMenu",
    ListenerData = "listener_MyCartMenu",
    WindowTypeName = "MyCart.MyCartMenuWindow"
});
```

Cartridges are small, optional sub-mods you can install and enable as needed - like popping different games into the same console. The goal is a modular setup where you only run what you actually use.

### Config
- `Menu.sebcore_icon_name` (string, default: `mods`): Desktop icon file name.
- `Menu.sebcore_icon_x` / `Menu.sebcore_icon_y` (string): Desktop icon position.
- `Maintenance.clear_mod_prefs` (bool): Clears known mod PlayerPrefs at runtime, then flips back to false.
- `Logging.debug_logging` (bool): Extra debug logging.

## Screenshots
<table>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebCore/assets/screenshots/1.jpg" alt="Screenshot 1" width="100%"></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebCore/assets/screenshots/2.jpg" alt="Screenshot 2" width="100%"></td>
  </tr>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/shiibe/EasyDeliveryCoMods/refs/heads/master/plugins/SebCore/assets/screenshots/3.jpg" alt="Screenshot 3" width="100%"></td>
    <td width="50%"></td>
  </tr>
</table>

## Installation
Dependencies
- `BepInEx-BepInExPack-5.4.2304`

Install
- r2modman/Thunderstore: https://thunderstore.io/c/easy-delivery-co/p/shiibe/SebCore/
- Manual: copy `SebCore.dll` to `BepInEx/plugins/SebCore/`

## Build
- Build: `dotnet build EasyDeliveryCoMods.sln -c Release`
- Package all: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-all.ps1 -Version 1.0.1`
