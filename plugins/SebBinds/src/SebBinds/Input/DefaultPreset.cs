using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SebBinds
{
    internal static class DefaultPreset
    {
        private const string PrefKeyPresetInstalled = "SebBinds_PresetInstalled";

        internal static void MarkInstalled()
        {
            PlayerPrefs.SetInt(PrefKeyPresetInstalled, 1);
            PlayerPrefs.Save();
        }

        internal static bool TryInstallFromInputManager(sInputManager input)
        {
            if (input == null)
            {
                return false;
            }

            var pi = input.GetComponent<PlayerInput>();
            if (pi == null || pi.actions == null)
            {
                return false;
            }

            bool onController = false;
            try
            {
                onController = input.OnController();
            }
            catch
            {
                // ignore
            }

            bool anySet = false;

            // Primary buttons.
            anySet |= SetFromActionIfUnset(pi, onController, "Click", BindAction.InteractOk);
            anySet |= SetFromActionIfUnset(pi, onController, "Break", BindAction.Back);
            // Brake uses the same default as Back (game uses the same action).
            anySet |= SetFromActionIfUnset(pi, onController, "Break", BindAction.Brake);

            anySet |= SetFromActionIfUnset(pi, onController, "Pause", BindAction.Pause);
            anySet |= SetFromActionIfUnset(pi, onController, "Map", BindAction.Map);
            anySet |= SetFromActionIfUnset(pi, onController, "Map", BindAction.Jobs);
            anySet |= SetFromActionIfUnset(pi, onController, "Inventory", BindAction.Items);
            anySet |= SetFromActionIfUnset(pi, onController, "ChangeCamera", BindAction.Camera);
            anySet |= SetFromActionIfUnset(pi, onController, "Reset", BindAction.ResetVehicle);
            anySet |= SetFromActionIfUnset(pi, onController, "Headlights", BindAction.Headlights);
            anySet |= SetFromActionIfUnset(pi, onController, "Horn", BindAction.Horn);

            // Radio is a 2D composite. Map d-pad or keys to the 4 radio functions.
            if (TrySetRadioFromAction(pi, onController))
            {
                anySet = true;
            }
            else
            {
                // Best-effort fallback for controller.
                if (onController)
                {
                    anySet |= SetIfUnset(BindAction.RadioScanToggle, new BindingInput { Kind = BindingKind.GamepadDpad, Code = 0 });
                    anySet |= SetIfUnset(BindAction.RadioScanRight, new BindingInput { Kind = BindingKind.GamepadDpad, Code = 1 });
                    anySet |= SetIfUnset(BindAction.RadioPower, new BindingInput { Kind = BindingKind.GamepadDpad, Code = 2 });
                    anySet |= SetIfUnset(BindAction.RadioScanLeft, new BindingInput { Kind = BindingKind.GamepadDpad, Code = 3 });
                }
            }

            // Modifier starts unset.
            if (BindingStore.GetModifierBinding().Kind == BindingKind.None)
            {
                BindingStore.SetModifierBinding(new BindingInput { Kind = BindingKind.None, Code = 0 });
            }

            if (anySet)
            {
                MarkInstalled();
                return true;
            }

            return false;
        }

        private static bool IsUnset(BindAction action)
        {
            var b0 = BindingStore.GetBinding(BindingLayer.Normal, action);
            var b1 = BindingStore.GetBinding(BindingLayer.Modified, action);
            return b0.Kind == BindingKind.None && b1.Kind == BindingKind.None;
        }

        private static bool SetIfUnset(BindAction target, BindingInput input)
        {
            if (!IsUnset(target))
            {
                return false;
            }
            BindingStore.SetBinding(BindingLayer.Normal, target, input);
            return true;
        }

        private static bool SetFromActionIfUnset(PlayerInput pi, bool onController, string actionName, BindAction target)
        {
            if (!IsUnset(target))
            {
                return false;
            }
            if (pi == null || pi.actions == null)
            {
                return false;
            }

            var action = pi.actions[actionName];
            if (action == null)
            {
                return false;
            }

            if (TryPickBindingInput(action, onController, out var binding))
            {
                BindingStore.SetBinding(BindingLayer.Normal, target, binding);
                return true;
            }

            return false;
        }

        private static bool TrySetRadioFromAction(PlayerInput pi, bool onController)
        {
            var action = pi.actions["Radio"];
            if (action == null)
            {
                return false;
            }

            bool any = false;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (!b.isPartOfComposite)
                {
                    continue;
                }

                string path = b.effectivePath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (onController && !path.StartsWith("<Gamepad>/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!onController && !(path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("<Gamepad>/", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (!TryParsePathToInput(path, out var input))
                {
                    continue;
                }

                string part = (b.name ?? string.Empty).ToLowerInvariant();
                if (part == "up")
                {
                    any |= SetIfUnset(BindAction.RadioScanToggle, input);
                    any = true;
                }
                else if (part == "right")
                {
                    any |= SetIfUnset(BindAction.RadioScanRight, input);
                    any = true;
                }
                else if (part == "down")
                {
                    any |= SetIfUnset(BindAction.RadioPower, input);
                    any = true;
                }
                else if (part == "left")
                {
                    any |= SetIfUnset(BindAction.RadioScanLeft, input);
                    any = true;
                }
            }

            return any;
        }

        private static bool TryPickBindingInput(InputAction action, bool onController, out BindingInput input)
        {
            // Prefer the binding for the current control scheme.
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isComposite || b.isPartOfComposite)
                {
                    continue;
                }
                string path = b.effectivePath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (onController)
                {
                    if (!path.StartsWith("<Gamepad>/", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
                else
                {
                    // Prefer keyboard/mouse.
                    if (!(path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                }

                if (TryParsePathToInput(path, out input))
                {
                    return true;
                }
            }

            // Fallback: any path we can parse.
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (b.isComposite || b.isPartOfComposite)
                {
                    continue;
                }
                string path = b.effectivePath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }
                if (TryParsePathToInput(path, out input))
                {
                    return true;
                }
            }

            input = default;
            return false;
        }

        private static bool TryParsePathToInput(string path, out BindingInput input)
        {
            input = default;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string p = path.Trim();

            if (p.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
            {
                string keyName = p.Substring("<Keyboard>/".Length);
                if (TryParseKeyName(keyName, out var key))
                {
                    input = new BindingInput { Kind = BindingKind.Key, Code = (int)key };
                    return true;
                }
                return false;
            }

            if (p.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase))
            {
                string btn = p.Substring("<Mouse>/".Length).ToLowerInvariant();
                if (btn == "leftbutton")
                {
                    input = new BindingInput { Kind = BindingKind.MouseButton, Code = 0 };
                    return true;
                }
                if (btn == "rightbutton")
                {
                    input = new BindingInput { Kind = BindingKind.MouseButton, Code = 1 };
                    return true;
                }
                if (btn == "middlebutton")
                {
                    input = new BindingInput { Kind = BindingKind.MouseButton, Code = 2 };
                    return true;
                }
                return false;
            }

            if (LooksLikeGamepadPath(p) && p.IndexOf("/dpad/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string dir = p.Substring(p.IndexOf("/dpad/", StringComparison.OrdinalIgnoreCase) + "/dpad/".Length).ToLowerInvariant();
                int code = dir switch
                {
                    "up" => 0,
                    "right" => 1,
                    "down" => 2,
                    _ => 3
                };
                input = new BindingInput { Kind = BindingKind.GamepadDpad, Code = code };
                return true;
            }

            if (LooksLikeGamepadPath(p))
            {
                int idx = p.IndexOf(">/", StringComparison.Ordinal);
                if (idx < 0)
                {
                    return false;
                }
                string btn = p.Substring(idx + 2);
                if (TryParseGamepadButtonName(btn, out var b))
                {
                    input = new BindingInput { Kind = BindingKind.GamepadButton, Code = (int)b };
                    return true;
                }
                return false;
            }

            return false;
        }

        private static bool TryParseGamepadButtonName(string name, out GamepadButton button)
        {
            // InputSystem uses names like "buttonSouth", "start", "select", "leftShoulder"...
            button = default;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string n = name.Trim().ToLowerInvariant();
            return n switch
            {
                "buttonsouth" => Set(GamepadButton.South, out button),
                "buttoneast" => Set(GamepadButton.East, out button),
                "buttonwest" => Set(GamepadButton.West, out button),
                "buttonnorth" => Set(GamepadButton.North, out button),
                "start" => Set(GamepadButton.Start, out button),
                "select" => Set(GamepadButton.Select, out button),
                "leftshoulder" => Set(GamepadButton.LeftShoulder, out button),
                "rightshoulder" => Set(GamepadButton.RightShoulder, out button),
                "lefttrigger" => Set(GamepadButton.LeftTrigger, out button),
                "righttrigger" => Set(GamepadButton.RightTrigger, out button),
                "leftstickpress" => Set(GamepadButton.LeftStick, out button),
                "rightstickpress" => Set(GamepadButton.RightStick, out button),
                _ => false
            };
        }

        private static bool LooksLikeGamepadPath(string path)
        {
            // Examples:
            // <Gamepad>/buttonSouth
            // <XInputController>/start
            // <DualShockGamepad>/dpad/up
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            int lt = path.IndexOf('<');
            int gt = path.IndexOf('>');
            if (lt < 0 || gt < 0 || gt <= lt)
            {
                return false;
            }

            string device = path.Substring(lt + 1, gt - lt - 1);
            if (device.IndexOf("gamepad", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // Common controller device names.
            if (string.Equals(device, "XInputController", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (device.IndexOf("controller", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private static bool TryParseKeyName(string name, out Key key)
        {
            key = Key.None;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string n = name.Trim();
            string lower = n.ToLowerInvariant();

            // Common key aliases.
            if (lower == "space") return Set(Key.Space, out key);
            if (lower == "escape" || lower == "esc") return Set(Key.Escape, out key);
            if (lower == "enter" || lower == "return") return Set(Key.Enter, out key);
            if (lower == "tab") return Set(Key.Tab, out key);
            if (lower == "backspace") return Set(Key.Backspace, out key);
            if (lower == "leftshift") return Set(Key.LeftShift, out key);
            if (lower == "rightshift") return Set(Key.RightShift, out key);
            if (lower == "leftctrl" || lower == "leftcontrol") return Set(Key.LeftCtrl, out key);
            if (lower == "rightctrl" || lower == "rightcontrol") return Set(Key.RightCtrl, out key);
            if (lower == "leftalt") return Set(Key.LeftAlt, out key);
            if (lower == "rightalt") return Set(Key.RightAlt, out key);

            // Single letters.
            if (lower.Length == 1 && lower[0] >= 'a' && lower[0] <= 'z')
            {
                if (Enum.TryParse(lower.ToUpperInvariant(), out Key parsed))
                {
                    return Set(parsed, out key);
                }
            }

            // Digits 0-9.
            if (lower.Length == 1 && lower[0] >= '0' && lower[0] <= '9')
            {
                string enumName = "Digit" + lower;
                if (Enum.TryParse(enumName, out Key parsed))
                {
                    return Set(parsed, out key);
                }
            }

            // Arrows.
            if (lower == "uparrow") return Set(Key.UpArrow, out key);
            if (lower == "downarrow") return Set(Key.DownArrow, out key);
            if (lower == "leftarrow") return Set(Key.LeftArrow, out key);
            if (lower == "rightarrow") return Set(Key.RightArrow, out key);

            // F-keys: f1..f12.
            if (lower.StartsWith("f", StringComparison.Ordinal) && lower.Length <= 3)
            {
                if (Enum.TryParse(lower.ToUpperInvariant(), out Key parsed))
                {
                    return Set(parsed, out key);
                }
            }

            // Best-effort parse.
            if (Enum.TryParse(n, ignoreCase: true, out Key parsedKey))
            {
                return Set(parsedKey, out key);
            }

            return false;
        }

        private static bool Set<T>(T v, out T output)
        {
            output = v;
            return true;
        }
    }
}
