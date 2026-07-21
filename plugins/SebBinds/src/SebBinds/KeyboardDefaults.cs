using UnityEngine.InputSystem;

namespace SebBinds
{
    internal static class KeyboardDefaults
    {
        internal static void EnsureDefaults()
        {
            SetIfUnset(BindAction.InteractOk, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.E });
            SetIfUnset(BindAction.Back, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Space });
            SetIfUnset(BindAction.Brake, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.S });
            SetIfUnset(BindAction.MapItems, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.LeftShift });
            SetIfUnset(BindAction.Jobs, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Tab });
            SetIfUnset(BindAction.Pause, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Escape });
            SetIfUnset(BindAction.MoveUp, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.W });
            SetIfUnset(BindAction.MoveLeft, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.A });
            SetIfUnset(BindAction.MoveDown, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.S });
            SetIfUnset(BindAction.MoveRight, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.D });
            SetIfUnset(BindAction.SteerLeft, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.A });
            SetIfUnset(BindAction.SteerRight, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.D });
            SetIfUnset(BindAction.Drive, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.W });
            SetIfUnset(BindAction.Camera, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.C });
            SetIfUnset(BindAction.LookLeft, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Z });
            SetIfUnset(BindAction.LookRight, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.X });
            SetIfUnset(BindAction.ResetVehicle, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.R });
            SetIfUnset(BindAction.Headlights, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.H });
            SetIfUnset(BindAction.RadioPower, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.DownArrow });
            SetIfUnset(BindAction.RadioScanLeft, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.LeftArrow });
            SetIfUnset(BindAction.RadioScanRight, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.RightArrow });
            SetIfUnset(BindAction.RadioScanToggle, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.UpArrow });
            SetIfUnset(BindAction.Horn, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Q });
            SetIfUnset(BindAction.ShiftUp, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.NumpadPlus });
            SetIfUnset(BindAction.ShiftDown, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.NumpadMinus });
            SetIfUnset(BindAction.Clutch, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Numpad0 });
            SetIfUnset(BindAction.GearReverse, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Numpad7 });
            SetIfUnset(BindAction.GearNeutral, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.NumpadPeriod });
            SetIfUnset(BindAction.Gear1, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Numpad1 });
            SetIfUnset(BindAction.Gear2, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Numpad2 });
            SetIfUnset(BindAction.Gear3, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Numpad3 });
            SetIfUnset(BindAction.Gear4, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Numpad4 });
            SetIfUnset(BindAction.Gear5, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Numpad5 });
            SetIfUnset(BindAction.Gear6, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Numpad6 });
            if (BindingStore.GetModifierBinding(BindingScheme.Keyboard).Kind == BindingKind.None)
            {
                BindingStore.SetModifierBinding(BindingScheme.Keyboard, new BindingInput { Kind = BindingKind.None, Code = 0 });
            }
        }

        private static void SetIfUnset(BindAction action, BindingInput input)
        {
            var existing = BindingStore.GetBinding(BindingScheme.Keyboard, BindingLayer.Normal, action);
            var existing2 = BindingStore.GetBinding(BindingScheme.Keyboard, BindingLayer.Modified, action);
            if (existing.Kind != BindingKind.None || existing2.Kind != BindingKind.None)
            {
                return;
            }

            BindingStore.SetBinding(BindingScheme.Keyboard, BindingLayer.Normal, action, input);
        }
    }
}
