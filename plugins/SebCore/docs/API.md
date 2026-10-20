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

- `RegisterApp(...)` returns `false` if `DisplayName`, `FileName`, `PluginGuid`, `ListenerName` or `ListenerData` is blank, or if an app with the same `FileName` (trimmed, case-insensitive) is already registered, unless you pass `replaceIfExists: true`.
- `WindowTypeName` is not checked at registration. A wrong name only shows up when the cartridge is opened (`EnsureListener` returns `false` and logs a debug message).
- SebCore only shows apps that are installed/loaded (checked via `PluginGuid`).

Related helpers:

- `CartridgeApps.GetRegisteredAppsSnapshot() -> CartridgeApps.App[]`: returns the currently registered apps.
- `CartridgeApps.IsInstalled(CartridgeApps.App app) -> bool`: checks whether the app's `PluginGuid` is present in BepInEx.
- `CartridgeApps.TryOpen(DesktopDotExe desktop, MiniRenderer renderer, CartridgeApps.App app) -> bool`: `EnsureListener` followed by `DesktopAppLauncher.TryOpenProgramListener`, in one call.

## Implementing A Cartridge Window

Your `WindowTypeName` type should be a `MonoBehaviour` that implements the same method names used by the game window system:

- `FrameUpdate(DesktopDotExe.WindowView view)`: called every frame while the window is open.
- `BackButtonPressed()`: called when the window back action is triggered.

### Optional Base Class

SebCore includes an optional base class you can derive from:

- `SebCore.CartridgeWindowBase`

It wires up `UIUtil`, computes the standard window content rect, includes the common slider `MouseYLock` behavior, and implements a default `BackButtonPressed()` that returns to the SebCore launcher. All built-in cartridges use it.

You must override `DrawWindow(Rect p)`, which receives the content rect each frame. Protected members you can use:

- `Util` (`UIUtil`), `View` (`DesktopDotExe.WindowView`)
- `MouseYLock`: pass as `ref MouseYLock` to slider calls
- `EnableMouseYLock` (virtual, default `true`): override to `false` to turn off the mouse-Y lock
- `GetNavY(Rect p)`: Y position of the footer row
- `ReturnToSebCoreAndClose()`: what the default `BackButtonPressed()` does

```csharp
using SebCore;
using UnityEngine;

namespace MyCart
{
    public sealed class MyCartMenuWindow : CartridgeWindowBase
    {
        protected override void DrawWindow(Rect p)
        {
            Util.Label("Hello", p.x + 8f, p.y + 8f);

            if (Util.NavigationFooter(p, paging: false) == UIUtil.NavigationAction.Back)
            {
                ReturnToSebCoreAndClose();
            }
        }
    }
}
```

If you don't derive from the base class, implement `FrameUpdate`/`BackButtonPressed` yourself and set `UIUtil.M`, `R` and `Nav` from `view` each frame, as `CartridgeWindowBase.FrameUpdate` does.

To get back to the launcher yourself, pass `SebCoreMenuWindow.FileName` and `SebCoreMenuWindow.ListenerData`. `FileName` is a property, not a constant, because users can rename the launcher icon (`DefaultFileName` is `"mods"`).

## Shared UI rows and navigation

`UIUtil` methods are instance methods: call them on a `UIUtil` whose `M`, `R` and `Nav` are set for the current frame, such as `Util` in `CartridgeWindowBase`.

Basic widgets. Names go through `LocalizationDictionary`, except in `ValueLabel` and the `*Raw` variants:

- `Label(name, x, y)`, `LabelOk(name, x, y)`, `LabelErr(name, x, y)`, `ValueLabel(value, x, y)`
- `Button(name, x, y) -> bool`, `SimpleButton(name, x, y) -> bool`, `SimpleButtonRaw(name, x, y) -> bool`, `FancyButton(title, centerX, y, fixedWidthTiles = 0f) -> bool`: `true` on click
- `Toggle(name, state, x, y, enabled = true) -> bool?`: the new state when clicked, otherwise `null`
- `CycleButton(name, value, x, y) -> bool`, `CycleButtonRaw(name, value, x, y) -> bool`: `true` on click; the caller moves to the next value
- `Slider(name, value01, x, y, ref mouseYLock) -> float?`: new 0..1 value, or `null` when idle

`UIUtil.SliderRow(name, value, min, max, display, x, valueX, y, ref mouseYLock)` draws a right-aligned value label and a slider. It returns the changed value in the supplied range, or `null` when idle. Reversed ranges are supported. Callers retain formatting, rounding, and preference writes.

`UIUtil.ChoiceSliderRow(name, value, max, display, x, valueX, y, ref mouseYLock)` returns a rounded, clamped integer from zero through `max`, or `null` when idle. Labels and saving remain with the caller.

`UIUtil.NavigationFooter(rect, paging: true, sideInset: 40f)` draws Prev/Back/Next and returns a `NavigationAction`. Set `paging: false` for Back alone; use `sideInset: 44f` for the launcher/Tweaks layout. Callers handle page wrapping, clamping, and Back transitions. After updating the page, call `UIUtil.PageIndicator(rect, index, count)` with a zero-based index.

## Launch Helpers

`SebCore.DesktopAppLauncher` helps you open launcher programs from code. Each method returns `true` if a window was opened.

- `TryOpen(DesktopDotExe desktop, string fileName) -> bool`: open an existing desktop file/program by name (case-insensitive).
- `TryOpenProgramListener(DesktopDotExe desktop, MiniRenderer renderer, string fileName, string listenerData) -> bool`: open a program window backed by a listener.
  - Use this when you want to return to SebCore from inside a cartridge.
  - `desktop.R` is used when set; otherwise `renderer`. Pass the caller's `view.R` so it works when `desktop.R` is null.
- `TryOpenProgramListener(DesktopDotExe desktop, string fileName, string listenerData) -> bool`: same, with no fallback renderer.

## Listener Helper

`SebCore.CartridgeApps.EnsureListener(DesktopDotExe desktop, SebCore.CartridgeApps.App app)`:

- Creates a `GameObject(app.ListenerName)` under the given `desktop` (if missing) and adds `app.WindowTypeName` as a component.
- Returns `false` if the target plugin isn't loaded (by `PluginGuid`) or the window type can't be resolved.

## Other Helpers

- `SebCore.ModPrefs`: `PlayerPrefs` wrappers that never throw: `GetFloat/SetFloat`, `GetInt/SetInt`, `GetBool/SetBool` (stored as 0/1 ints), `HasKey`, `DeleteKey`, `Save`. Getters return the `fallback` on error; setters do not save on their own.
- `SebCore.ConfigUtil.CopyLegacyConfigIfNeeded(string oldGuid, string newGuid)`: copies `BepInEx/config/<oldGuid>.cfg` to `<newGuid>.cfg` if the new file doesn't exist yet. Best effort; never throws. Call it before you bind any config entries.
- `SebCore.HarmonyUtil.PatchByName(Harmony harmony, Type patchType, string typeName, string methodName, string prefix = null, string postfix = null, Action<string> logDebug = null)`: patches a game method found by name. `prefix`/`postfix` name static methods on `patchType`. If the type or method is missing it logs through `logDebug` and skips the patch. For overloaded methods it picks the one with the fewest parameters.

## Threading

All of this is intended to run on Unity's main thread.
