using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SebBinds
{
    internal static class InputCapture
    {
        internal static bool TryCaptureNextBinding(BindingScheme scheme, BindAction forAction, BindingLayer forLayer, out BindingInput input)
        {
            // Wheel scheme: only listen to the Logitech wrapper device.
            if (scheme == BindingScheme.Wheel)
            {
                return WheelInterop.TryCaptureNextBinding(out input);
            }

            // Gamepad.
            var g = Gamepad.current;
            if (scheme == BindingScheme.Controller && g != null)
            {
                BindingEvaluator.BeginFrame();

                // Dpad directions.
                if (g.dpad.up.wasPressedThisFrame)
                {
                    input = new BindingInput { Kind = BindingKind.GamepadDpad, Code = 0 };
                    return true;
                }
                if (g.dpad.right.wasPressedThisFrame)
                {
                    input = new BindingInput { Kind = BindingKind.GamepadDpad, Code = 1 };
                    return true;
                }
                if (g.dpad.down.wasPressedThisFrame)
                {
                    input = new BindingInput { Kind = BindingKind.GamepadDpad, Code = 2 };
                    return true;
                }
                if (g.dpad.left.wasPressedThisFrame)
                {
                    input = new BindingInput { Kind = BindingKind.GamepadDpad, Code = 3 };
                    return true;
                }

                // Common face/shoulder/start/select/stick buttons.
                var buttons = new[]
                {
                    GamepadButton.South,
                    GamepadButton.East,
                    GamepadButton.West,
                    GamepadButton.North,
                    GamepadButton.LeftTrigger,
                    GamepadButton.RightTrigger,
                    GamepadButton.LeftShoulder,
                    GamepadButton.RightShoulder,
                    GamepadButton.LeftStick,
                    GamepadButton.RightStick,
                    GamepadButton.Select,
                    GamepadButton.Start
                };

                foreach (var b in buttons)
                {
                    if (BindingEvaluator.TryGetGamepadButtonControl(g, (int)b, out var c) && c.wasPressedThisFrame)
                    {
                        input = new BindingInput { Kind = BindingKind.GamepadButton, Code = (int)b };
                        return true;
                    }
                }
            }

            // Mouse buttons (only when explicitly in KeyboardMouse mode).
            var m = Mouse.current;
            if (scheme == BindingScheme.Keyboard && m != null)
            {
                if (m.leftButton.wasPressedThisFrame)
                {
                    input = new BindingInput { Kind = BindingKind.MouseButton, Code = 0 };
                    return true;
                }
                if (m.rightButton.wasPressedThisFrame)
                {
                    input = new BindingInput { Kind = BindingKind.MouseButton, Code = 1 };
                    return true;
                }
                if (m.middleButton.wasPressedThisFrame)
                {
                    input = new BindingInput { Kind = BindingKind.MouseButton, Code = 2 };
                    return true;
                }
            }

            // Keyboard keys.
            var kb = Keyboard.current;
            if (scheme == BindingScheme.Keyboard && kb != null)
            {
                foreach (var key in kb.allKeys)
                {
                    if (key != null && key.wasPressedThisFrame)
                    {
                        input = new BindingInput { Kind = BindingKind.Key, Code = (int)key.keyCode };
                        return true;
                    }
                }
            }

            input = default;
            return false;
        }
    }
}
