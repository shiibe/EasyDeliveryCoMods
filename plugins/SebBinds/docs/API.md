# SebBinds API

This is the developer-facing API for integrating with SebBinds (bindings storage, input evaluation, and UI extension points).

## Dependency

```csharp
[BepInDependency("shibe.easydeliveryco.sebbinds", BepInDependency.DependencyFlags.HardDependency)]
```

If you only *optionally* integrate with SebBinds, use a soft dependency, check `BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("shibe.easydeliveryco.sebbinds")`, and keep every SebBinds call in a separate method marked `[MethodImpl(MethodImplOptions.NoInlining)]`. A missing assembly fails when the calling method is JIT-compiled, so a try/catch inside that same method never runs.

```csharp
using System.Runtime.CompilerServices;

static bool HornPressed() =>
    BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("shibe.easydeliveryco.sebbinds") && HornPressedSebBinds();

[MethodImpl(MethodImplOptions.NoInlining)]
static bool HornPressedSebBinds() => SebBinds.SebBindsApi.WasActionPressed(SebBinds.BindAction.Horn);
```

## Concepts

- `BindingScheme`: `Controller`, `Keyboard`, `Wheel`
- `BindingLayer`: `Normal`, `Modified` (a per-scheme modifier binding toggles the layer)
- `BindingInput`: `{ Kind, Code }`
- `BindingKind`: `None`, `Button`, `Pov` (wheel), `Key`, `MouseButton`, `GamepadButton`, `GamepadDpad`, `GamepadAxis`, `WheelAxis`, `GamepadDpadAxis`, `WheelDpadAxis`
- `ShiftMode`: `Toggle`, `Hold`
- `BindAction`: the action enum used for button-style binds
- `AxisAction`: the action enum used for axis binds

`BindAction`/`AxisAction` are not extensible at runtime. If you need a new action ID for your mod, it has to be added to SebBinds.

## Reading And Writing Stored Bindings

Button-style binds (`BindAction`) are stored via `SebBinds.BindingStore`:

- `GetBinding(BindingScheme scheme, BindingLayer layer, BindAction action) -> BindingInput`
- `SetBinding(BindingScheme scheme, BindingLayer layer, BindAction action, BindingInput input)`
- `GetModifierBinding(BindingScheme scheme) -> BindingInput`
- `SetModifierBinding(BindingScheme scheme, BindingInput input)`
- `GetBindingLabel(BindingInput input) -> string`
- `GetActionLabel(BindAction action) -> string`: display name for an action

Overloads without a `BindingScheme` (`GetBinding(layer, action)`, `SetBinding(layer, action, input)`, `GetModifierBinding()`, `SetModifierBinding(input)`) are kept for back-compat and always use `BindingScheme.Controller`.

Setters write `PlayerPrefs` but do not call `PlayerPrefs.Save()`; call it yourself if the change must survive a crash.

Store `BindingKind.None` to keep an action unbound, including after a restart.
Defaults and legacy migration only fill missing preferences.

Maintenance helpers (these delete PlayerPrefs keys):

- `ClearScheme(BindingScheme scheme)`
- `ClearAll()`

Axis binds (`AxisAction`) are stored via `SebBinds.AxisBindingStore`:

- `GetAxisBinding(BindingScheme scheme, AxisAction action) -> BindingInput`
- `SetAxisBinding(BindingScheme scheme, AxisAction action, BindingInput input)`
- `ClearAxisBinding(BindingScheme scheme, AxisAction action)` stores an explicit unbound axis.

The scheme-less overloads (`GetAxisBinding(action)`, `SetAxisBinding(action, input)`, `ClearAxisBinding(action)`) use `BindingScheme.Controller`.

Maintenance helpers (these delete PlayerPrefs keys):

- `ClearScheme(BindingScheme scheme)`
- `ClearAll()`

`SebBinds.Plugin.ResetBindings()` clears every scheme and runtime binding cache,
then restores controller and keyboard defaults. SebCore uses this optional hook
for its global preference reset.

To get the bind that is in effect right now (Modified layer while the modifier is held), use `SebBindsApi.GetActiveBinding(scheme, action, normalFallback: true)` instead of reading layers yourself. See [Action-level helpers](#action-level-helpers).

## Evaluating Input

Input evaluation helpers live in `SebBinds.BindingEvaluator`:

- `BeginFrame()`: updates the cached gamepad axis values. Safe to call more than once per frame. The evaluator calls it itself on every gamepad-axis read, so calling it is optional.
- `IsDown(BindingInput input) -> bool`
- `WasPressedThisFrame(BindingInput input) -> bool`
- `WasReleasedThisFrame(BindingInput input) -> bool`
- `GetAxisValue(BindingInput input) -> float`

Typical per-frame usage:

```csharp
using SebBinds;

var b = BindingStore.GetBinding(BindingScheme.Controller, BindingLayer.Normal, BindAction.Horn);
if (b.Kind != BindingKind.None && BindingEvaluator.WasPressedThisFrame(b))
{
    // Do horn action.
}
```

Notes:

- `GetAxisValue(...)` supports `BindingKind.GamepadAxis`, `BindingKind.GamepadDpadAxis`, `BindingKind.WheelAxis`, and `BindingKind.WheelDpadAxis`.
- `IsDown(...)` treats axes as "pressed" once they pass an internal threshold. `WasPressedThisFrame`/`WasReleasedThisFrame` report those threshold crossings for every axis kind.
- `WheelAxis`, `WheelDpadAxis` and `GamepadDpadAxis` have no native edge events, so SebBinds finds their edges by comparing this frame's state with last frame's. Poll them **every frame**: after a frame with no poll, no edge is reported (this stops a held input from firing again after a pause). The same applies to `SebBindsApi.WasActionReleased`.

### Action-level helpers

Most mods only need to ask about an action across all schemes. `SebBinds.SebBindsApi` handles scheme selection (Wheel only when SebLogiWheel is installed) and the modifier layer:

- `IsActionDown(BindAction action, bool normalFallback = false) -> bool`
- `WasActionPressed(BindAction action, bool normalFallback = false) -> bool`
- `WasActionReleased(BindAction action, bool normalFallback = false) -> bool`: tracked per action, so releasing or pressing the modifier mid-hold still reports the release
- `IsActionBound(BindAction action) -> bool`: any bind on either layer of any active scheme
- `GetActiveLayer(BindingScheme scheme) -> BindingLayer`
- `GetActiveBinding(BindingScheme scheme, BindAction action, bool normalFallback = false) -> BindingInput`

`normalFallback: true` uses the Normal bind while the modifier is held if the action has no Modified bind.

```csharp
if (SebBindsApi.WasActionPressed(BindAction.Horn))
{
    // Do horn action.
}
```

### Shift mode

The gearbox shift mode preference (`SebBinds_ShiftMode`, default `Toggle`) is shared through `SebBindsApi`:

- `GetShiftMode() -> ShiftMode`
- `SetShiftMode(ShiftMode mode)`
- `NextShiftMode(ShiftMode mode) -> ShiftMode`: cycles Toggle and Hold
- `GetShiftModeLabel(ShiftMode mode) -> string`

## Adding Pages To The SebBinds UI

Other mods can add a custom actions page to the binds menu:

API: `SebBinds.SebBindsApi.RegisterActionsPage(string id, string title, params BindAction[] actions)`

- `id`: unique ID (trimmed, case-insensitive). If a page with the same ID already exists, it will be replaced.
- `title`: shown in the UI.
- `actions`: the `BindAction` entries to show on the page.

Throws `ArgumentException` if `id` or `title` is blank, and `ArgumentNullException` if `actions` is null.

Example (from SebTruck):

```csharp
SebBinds.SebBindsApi.RegisterActionsPage(
    id: "sebtruck",
    title: "Truck",
    SebBinds.BindAction.IgnitionToggle,
    SebBinds.BindAction.IndicatorLeft,
    SebBinds.BindAction.IndicatorRight,
    SebBinds.BindAction.IndicatorHazards,
    SebBinds.BindAction.ToggleGearbox,
    SebBinds.BindAction.ShiftUp,
    SebBinds.BindAction.ShiftDown
);
```

## Custom Axis Providers

If you have a custom device (or want to provide axis capture for something SebBinds doesn't natively understand), implement `SebBinds.IAxisProvider` and register it:

API: `SebBinds.SebBindsApi.RegisterAxisProvider(IAxisProvider provider)`

Registering the same instance twice does nothing. Throws `ArgumentNullException` for null.

```csharp
using SebBinds;

public sealed class MyAxisProvider : IAxisProvider
{
    public bool IsAvailable() => true;
    public bool SupportsClutch() => false;

    public bool TryGetAxisValue(BindingInput input, out float value)
    {
        value = 0f;
        return false;
    }

    public bool TryGetAxisLabel(BindingInput input, out string label)
    {
        label = null;
        return false;
    }

    public bool TryCaptureNextAxis(out BindingInput input)
    {
        input = default;
        return false;
    }
}

// During Awake():
SebBindsApi.RegisterAxisProvider(new MyAxisProvider());
```

## Persistence / Compatibility

- SebBinds stores bindings in `PlayerPrefs` using stable numeric IDs.
- Do not change the numeric values of `BindAction` / `AxisAction` if you want existing user bindings to keep working.

## Threading

All API calls should be made from Unity's main thread.
