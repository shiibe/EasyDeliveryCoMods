# __MOD_NAME__

This folder is a scaffold template for creating a new SebCore cartridge plugin.

## Use

Recommended: use the scaffolding script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/new-cartridge.ps1 -Name MyCart -Guid com.example.mycart
```

Optional parameters:

- `-DisplayName` (defaults to `-Name`)
- `-FileName` (defaults to `-Name.ToLowerInvariant()`)
- `-Version` (defaults to `1.0.0`)
- `-AddToSolution` (adds the new .csproj to `EasyDeliveryCoMods.sln`)

## What You Get

- A BepInEx plugin that depends on SebCore.
- A SebCore cartridge registration in `Plugin.Awake()`.
- A minimal menu window that demonstrates typical SebCore UI patterns.
