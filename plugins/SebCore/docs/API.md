# SebCore API

This is the developer-facing API for integrating with the SebCore launcher.

## Dependency

```csharp
[BepInDependency("shibe.easydeliveryco.sebcore", BepInDependency.DependencyFlags.HardDependency)]
```

## Registering A Cartridge

Register a cartridge so it shows up as a button in the SebCore launcher.

API: `SebCore.CartridgeApps.RegisterApp(SebCore.CartridgeApps.App app, bool replaceIfExists = false)`

Fields (`SebCore.CartridgeApps.App`):

- `DisplayName`: label shown in the launcher.
- `FileName`: the in-game "program" name used to open your window (keep unique; typically lowercase).
- `PluginGuid`: BepInEx plugin GUID that owns the cartridge.
- `ListenerName`: GameObject name created under `DesktopDotExe` to host the window component.
- `ListenerData`: listener key passed to `DesktopDotExe` when opening the program (convention: `listener_<ListenerName>`).
- `WindowTypeName`: fully-qualified type name of your window component (e.g. `MyCart.MyCartMenuWindow`).

Minimal example:

```csharp
using BepInEx;

[BepInPlugin("com.example.mycart", "MyCart", "1.0.0")]
[BepInDependency("shibe.easydeliveryco.sebcore", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        SebCore.CartridgeApps.RegisterApp(new SebCore.CartridgeApps.App
        {
            DisplayName = "My Cartridge",
            FileName = "mycart",
            PluginGuid = "com.example.mycart",
            ListenerName = "MyCartMenu",
            ListenerData = "listener_MyCartMenu",
            WindowTypeName = "MyCart.MyCartMenuWindow"
        });
    }
}
```

Notes:

- `RegisterApp(...)` returns `false` if required fields are missing or an app with the same `FileName` is already registered (unless you pass `replaceIfExists: true`).
- SebCore only shows apps that are installed/loaded (checked via `PluginGuid`).

Related helpers:

- `CartridgeApps.GetRegisteredAppsSnapshot() -> CartridgeApps.App[]`: returns the currently registered apps.
- `CartridgeApps.IsInstalled(CartridgeApps.App app) -> bool`: checks whether the app's `PluginGuid` is present in BepInEx.

## Implementing A Cartridge Window

Your `WindowTypeName` type should be a `MonoBehaviour` that implements the same method names used by the game window system:

- `FrameUpdate(DesktopDotExe.WindowView view)`: called every frame while the window is open.
- `BackButtonPressed()`: called when the window back action is triggered.

Common pattern (also used by the built-in cartridges):

```csharp
using SebCore;
using UnityEngine;

namespace MyCart
{
    public sealed class MyCartMenuWindow : MonoBehaviour
    {
        public const string FileName = "mycart";
        public const string ListenerName = "MyCartMenu";
        public const string ListenerData = "listener_MyCartMenu";

        private UIUtil _util;
        private DesktopDotExe.WindowView _view;

        public void FrameUpdate(DesktopDotExe.WindowView view)
        {
            if (view == null) return;
            _view = view;

            _util ??= new UIUtil();
            _util.M = view.M;
            _util.R = view.R;
            _util.Nav = view.M.nav;

            // Draw your UI...
        }

        public void BackButtonPressed()
        {
            // Typical behavior: return to SebCore.
            SebCore.DesktopAppLauncher.TryOpenProgramListener(
                _util?.M,
                _util?.R,
                SebCore.SebCoreMenuWindow.FileName,
                SebCore.SebCoreMenuWindow.ListenerData
            );
            _view?.Kill();
        }
    }
}
```

## Launch Helpers

`SebCore.DesktopAppLauncher` helps you open launcher programs from code:

- `TryOpen(DesktopDotExe desktop, string fileName)`: open an existing desktop file/program by name.
- `TryOpenProgramListener(DesktopDotExe desktop, MiniRenderer renderer, string fileName, string listenerData)`: open a program window backed by a listener.
  - Use this when you want to return to SebCore from inside a cartridge.
  - If `desktop.R` is null in your context, pass the caller's `view.R` as the `renderer` fallback.

## Listener Helper

`SebCore.CartridgeApps.EnsureListener(DesktopDotExe desktop, SebCore.CartridgeApps.App app)`:

- Creates a `GameObject(app.ListenerName)` under the given `desktop` (if missing) and adds `app.WindowTypeName` as a component.
- Returns `false` if the target plugin isn't loaded (by `PluginGuid`) or the window type can't be resolved.

## Threading

All of this is intended to run on Unity's main thread.
