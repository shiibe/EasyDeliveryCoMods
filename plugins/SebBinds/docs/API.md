# SebBinds API

This is the developer-facing API for integrating with SebBinds (bindings storage, input evaluation, and UI extension points).

## Dependency

```csharp
[BepInDependency("shibe.easydeliveryco.sebbinds", BepInDependency.DependencyFlags.HardDependency)]
```

If you only *optionally* integrate with SebBinds, use a soft dependency and wrap calls in a try/catch.

## Concepts

- `BindingScheme`: `Controller`, `Keyboard`, `Wheel`
- `BindingLayer`: `Normal`, `Modified` (a per-scheme modifier binding toggles the layer)
- `BindingInput`: `{ Kind, Code }`
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

Maintenance helpers (these delete PlayerPrefs keys):

- `ClearScheme(BindingScheme scheme)`
- `ClearAll()`

Axis binds (`AxisAction`) are stored via `SebBinds.AxisBindingStore`:

- `GetAxisBinding(BindingScheme scheme, AxisAction action) -> BindingInput`
- `SetAxisBinding(BindingScheme scheme, AxisAction action, BindingInput input)`

Maintenance helpers (these delete PlayerPrefs keys):

- `ClearScheme(BindingScheme scheme)`
- `ClearAll()`

Recommended "effective bind" pattern (use Modified layer when modifier is held; fall back to Normal if Modified is unbound):

```csharp
using SebBinds;

static BindingLayer GetActiveLayer(BindingScheme scheme)
{
    var mod = BindingStore.GetModifierBinding(scheme);
    return (mod.Kind != BindingKind.None && BindingEvaluator.IsDown(mod))
        ? BindingLayer.Modified
        : BindingLayer.Normal;
}

static BindingInput GetEffectiveBind(BindingScheme scheme, BindAction action)
{
    var layer = GetActiveLayer(scheme);
    var b = BindingStore.GetBinding(scheme, layer, action);
    if (b.Kind == BindingKind.None && layer == BindingLayer.Modified)
    {
        b = BindingStore.GetBinding(scheme, BindingLayer.Normal, action);
    }
    return b;
}
```

## Evaluating Input

Input evaluation helpers live in `SebBinds.BindingEvaluator`:

- `BeginFrame()`: updates internal cached axis values; safe to call multiple times per frame.
- `IsDown(BindingInput input) -> bool`
- `WasPressedThisFrame(BindingInput input) -> bool`
- `WasReleasedThisFrame(BindingInput input) -> bool`
- `GetAxisValue(BindingInput input) -> float`

Typical per-frame usage:

```csharp
using SebBinds;

BindingEvaluator.BeginFrame();

var b = BindingStore.GetBinding(BindingScheme.Controller, BindingLayer.Normal, BindAction.Horn);
if (b.Kind != BindingKind.None && BindingEvaluator.WasPressedThisFrame(b))
{
    // Do horn action.
}
```

Notes:

- `GetAxisValue(...)` supports `BindingKind.GamepadAxis`, `BindingKind.GamepadDpadAxis`, `BindingKind.WheelAxis`, and `BindingKind.WheelDpadAxis`.
- `IsDown(...)` treats axes as "pressed" once they pass an internal threshold.

## Adding Pages To The SebBinds UI

Other mods can add a custom actions page to the binds menu:

API: `SebBinds.SebBindsApi.RegisterActionsPage(string id, string title, params BindAction[] actions)`

- `id`: unique ID (case-insensitive). If a page with the same ID already exists, it will be replaced.
- `title`: shown in the UI.
- `actions`: the `BindAction` entries to show on the page.

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
