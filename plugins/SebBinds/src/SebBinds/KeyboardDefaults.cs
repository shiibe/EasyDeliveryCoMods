using UnityEngine.InputSystem;

namespace SebBinds
{
    internal static class KeyboardDefaults
    {
        internal static void EnsureDefaults()
        {
            // Only fill unset actions.
            SetIfUnset(BindAction.InteractOk, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.E });

            // Handbrake/back
            SetIfUnset(BindAction.Back, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Space });

            // Brake/reverse
            SetIfUnset(BindAction.Brake, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.S });

            // Map/Items (hold)
            SetIfUnset(BindAction.MapItems, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.LeftShift });

            // Job selection
            SetIfUnset(BindAction.Jobs, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Tab });

            // Pause
            SetIfUnset(BindAction.Pause, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Escape });

            // Movement
            SetIfUnset(BindAction.MoveUp, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.W });
            SetIfUnset(BindAction.MoveLeft, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.A });
            SetIfUnset(BindAction.MoveDown, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.S });
            SetIfUnset(BindAction.MoveRight, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.D });

            // Vehicle
            SetIfUnset(BindAction.SteerLeft, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.A });
            SetIfUnset(BindAction.SteerRight, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.D });
            SetIfUnset(BindAction.Drive, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.W });

            // Switch view
            SetIfUnset(BindAction.Camera, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.C });

            // FPV look left/right
            SetIfUnset(BindAction.LookLeft, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Z });
            SetIfUnset(BindAction.LookRight, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.X });

            // Reset (hold)
            SetIfUnset(BindAction.ResetVehicle, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.R });

            // Lights
            SetIfUnset(BindAction.Headlights, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.H });

            // Radio
            SetIfUnset(BindAction.RadioPower, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.DownArrow });
            SetIfUnset(BindAction.RadioScanLeft, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.LeftArrow });
            SetIfUnset(BindAction.RadioScanRight, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.RightArrow });
            SetIfUnset(BindAction.RadioScanToggle, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.UpArrow });

            // Honk
            SetIfUnset(BindAction.Horn, new BindingInput { Kind = BindingKind.Key, Code = (int)Key.Q });

            // Modifier defaults unset.
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
