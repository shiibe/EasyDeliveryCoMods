using UnityEngine;
using UnityEngine.InputSystem;

namespace SebBinds
{
    public static class BindingStore
    {
        private const int BindingPovOffset = 1000;
        private const int BindingKeyOffset = 2000;
        private const int BindingMouseOffset = 10000;
        private const int BindingGamepadButtonOffset = 11000;
        private const int BindingGamepadDpadOffset = 12000;
        private const int BindingGamepadAxisOffset = 14000;
        private const int BindingWheelAxisOffset = 15000;
        private const int BindingGamepadDpadAxisOffset = 16000;
        private const int BindingWheelDpadAxisOffset = 17000;
        private const string PrefKeyBindModifierController = "ELWS_Bind_Modifier";
        private const string PrefKeyBindModifierKeyboard = "SebBinds_KB_Bind_Modifier";
        private const string PrefKeyBindModifierWheel = "SebBinds_Wheel_Bind_Modifier";

        private static string GetModifierPrefKey(BindingScheme scheme)
        {
            return scheme == BindingScheme.Keyboard
                ? PrefKeyBindModifierKeyboard
                : (scheme == BindingScheme.Wheel ? PrefKeyBindModifierWheel : PrefKeyBindModifierController);
        }

        private static string GetBindPrefKey(BindingScheme scheme, BindingLayer layer, BindAction action)
        {
            // Stable key: do not depend on enum ToString().
            if (scheme == BindingScheme.Keyboard)
            {
                return "SebBinds_KB_Bind_" + (int)layer + "_" + (int)action;
            }
            if (scheme == BindingScheme.Wheel)
            {
                return "SebBinds_Wheel_Bind_" + (int)layer + "_" + (int)action;
            }
            return "ELWS_Bind_" + (int)layer + "_" + (int)action;
        }

        private static string GetLegacyBindPrefKey(BindingScheme scheme, BindingLayer layer, BindAction action)
        {
            // Legacy key format (enum names).
            if (scheme == BindingScheme.Keyboard)
            {
                return "SebBinds_KB_Bind_" + layer + "_" + action;
            }
            if (scheme == BindingScheme.Wheel)
            {
                return "SebBinds_Wheel_Bind_" + layer + "_" + action;
            }
            return "ELWS_Bind_" + layer + "_" + action;
        }

        public static void ClearAll()
        {
            // Clear both stable keys and legacy-name keys.
            foreach (BindAction action in System.Enum.GetValues(typeof(BindAction)))
            {
                foreach (BindingLayer layer in System.Enum.GetValues(typeof(BindingLayer)))
                {
                    foreach (BindingScheme scheme in System.Enum.GetValues(typeof(BindingScheme)))
                    {
                        string key = GetBindPrefKey(scheme, layer, action);
                        PlayerPrefs.DeleteKey(key);
                        PlayerPrefs.DeleteKey(GetLegacyBindPrefKey(scheme, layer, action));
                    }
                }
            }

            PlayerPrefs.DeleteKey(GetModifierPrefKey(BindingScheme.Controller));
            PlayerPrefs.DeleteKey(GetModifierPrefKey(BindingScheme.Keyboard));
            PlayerPrefs.DeleteKey(GetModifierPrefKey(BindingScheme.Wheel));
        }

        public static void ClearScheme(BindingScheme scheme)
        {
            foreach (BindAction action in System.Enum.GetValues(typeof(BindAction)))
            {
                foreach (BindingLayer layer in System.Enum.GetValues(typeof(BindingLayer)))
                {
                    PlayerPrefs.DeleteKey(GetBindPrefKey(scheme, layer, action));
                    PlayerPrefs.DeleteKey(GetLegacyBindPrefKey(scheme, layer, action));
                }
            }
            PlayerPrefs.DeleteKey(GetModifierPrefKey(scheme));
        }

        public static BindingInput GetBinding(BindingLayer layer, BindAction action)
        {
            return GetBinding(BindingScheme.Controller, layer, action);
        }

        public static BindingInput GetBinding(BindingScheme scheme, BindingLayer layer, BindAction action)
        {
            string key = GetBindPrefKey(scheme, layer, action);
            int raw = PlayerPrefs.GetInt(key, -1);
            if (raw < 0)
            {
                raw = PlayerPrefs.GetInt(GetLegacyBindPrefKey(scheme, layer, action), -1);
            }
            return Decode(raw);
        }

        public static void SetBinding(BindingLayer layer, BindAction action, BindingInput input)
        {
            SetBinding(BindingScheme.Controller, layer, action, input);
        }

        public static void SetBinding(BindingScheme scheme, BindingLayer layer, BindAction action, BindingInput input)
        {
            PlayerPrefs.SetInt(GetBindPrefKey(scheme, layer, action), Encode(input));
        }

        public static BindingInput GetModifierBinding()
        {
            return GetModifierBinding(BindingScheme.Controller);
        }

        public static void SetModifierBinding(BindingInput input)
        {
            SetModifierBinding(BindingScheme.Controller, input);
        }

        public static BindingInput GetModifierBinding(BindingScheme scheme)
        {
            int raw = PlayerPrefs.GetInt(GetModifierPrefKey(scheme), -1);
            return Decode(raw);
        }

        public static void SetModifierBinding(BindingScheme scheme, BindingInput input)
        {
            PlayerPrefs.SetInt(GetModifierPrefKey(scheme), Encode(input));
        }

        public static string GetBindingLabel(BindingInput input)
        {
            if (input.Kind == BindingKind.Button)
            {
                return "But. " + (Mathf.Clamp(input.Code, 0, 127) + 1);
            }
            if (input.Kind == BindingKind.Pov)
            {
                switch (Mathf.Clamp(input.Code, 0, 3))
                {
                    case 0:
                        return "DP Up";
                    case 1:
                        return "DP Right";
                    case 2:
                        return "DP Down";
                    default:
                        return "DP Left";
                }
            }
            if (input.Kind == BindingKind.Key)
            {
                try
                {
                    return ((Key)Mathf.Max(0, input.Code)).ToString();
                }
                catch
                {
                    return "Key";
                }
            }
            if (input.Kind == BindingKind.MouseButton)
            {
                switch (input.Code)
                {
                    case 0:
                        return "Mouse L";
                    case 1:
                        return "Mouse R";
                    case 2:
                        return "Mouse M";
                    default:
                        return "Mouse";
                }
            }
            if (input.Kind == BindingKind.GamepadButton)
            {
                return "Pad " + input.Code;
            }
            if (input.Kind == BindingKind.GamepadDpad)
            {
                switch (Mathf.Clamp(input.Code, 0, 3))
                {
                    case 0:
                        return "Dpad Up";
                    case 1:
                        return "Dpad Right";
                    case 2:
                        return "Dpad Down";
                    default:
                        return "Dpad Left";
                }
            }
            if (input.Kind == BindingKind.GamepadAxis)
            {
                switch (input.Code)
                {
                    case 0:
                        return "LT Axis";
                    case 1:
                        return "RT Axis";
                    case 2:
                        return "LS X";
                    case 3:
                        return "LS Y";
                    case 4:
                        return "RS X";
                    case 5:
                        return "RS Y";
                    default:
                        return "Axis";
                }
            }
            if (input.Kind == BindingKind.WheelAxis)
            {
                switch (Mathf.Clamp(input.Code, 0, 7))
                {
                    case 0: return "lX";
                    case 1: return "lY";
                    case 2: return "lZ";
                    case 3: return "lRx";
                    case 4: return "lRy";
                    case 5: return "lRz";
                    case 6: return "slider0";
                    case 7: return "slider1";
                    default: return "lX";
                }
            }
            if (input.Kind == BindingKind.GamepadDpadAxis)
            {
                return input.Code == 0 ? "Dpad X" : "Dpad Y";
            }
            if (input.Kind == BindingKind.WheelDpadAxis)
            {
                return input.Code == 0 ? "Wheel Dpad X" : "Wheel Dpad Y";
            }
            return "None";
        }

        public static string GetChordLabel(BindingInput input, bool modified)
        {
            string baseLabel = GetBindingLabel(input);
            return baseLabel;
        }

        public static string GetActionLabel(BindAction action)
        {
            switch (action)
            {
                case BindAction.InteractOk:
                    return "Interact";
                case BindAction.Back:
                    return "Back";
                case BindAction.MapItems:
                    return "Map/Items";
                case BindAction.Pause:
                    return "Pause";
                case BindAction.Drive:
                    return "Drive";
                case BindAction.Brake:
                    return "Brake";
                case BindAction.Map:
                    return "Map";
                case BindAction.Items:
                    return "Items";
                case BindAction.Jobs:
                    return "Jobs";
                case BindAction.SteerLeft:
                    return "Steer Left";
                case BindAction.SteerRight:
                    return "Steer Right";
                case BindAction.MoveUp:
                    return "Move Up";
                case BindAction.MoveDown:
                    return "Move Down";
                case BindAction.MoveLeft:
                    return "Move Left";
                case BindAction.MoveRight:
                    return "Move Right";
                case BindAction.LookUp:
                    return "Look Up";
                case BindAction.LookDown:
                    return "Look Down";
                case BindAction.LookLeft:
                    return "Look Left";
                case BindAction.LookRight:
                    return "Look Right";
                case BindAction.CameraLookX:
                    return "Cam Look X";
                case BindAction.CameraLookY:
                    return "Cam Look Y";
                case BindAction.SteerAxis:
                    return "Steer Axis";
                case BindAction.Camera:
                    return "Camera";
                case BindAction.ResetVehicle:
                    return "Reset";
                case BindAction.Headlights:
                    return "Lights";
                case BindAction.Horn:
                    return "Horn";
                case BindAction.RadioPower:
                    return "Radio Pwr";
                case BindAction.RadioScanToggle:
                    return "Scan";
                case BindAction.RadioScanLeft:
                    return "Prev Ch";
                case BindAction.RadioScanRight:
                    return "Next Ch";
                case BindAction.ToggleGearbox:
                    return "Gearbox";
                case BindAction.ShiftUp:
                    return "Shift Up";
                case BindAction.ShiftDown:
                    return "Shift Down";
                case BindAction.IgnitionToggle:
                    return "Ignition";
                default:
                    return action.ToString();
            }
        }

        private static int Encode(BindingInput input)
        {
            if (input.Kind == BindingKind.Button)
            {
                return Mathf.Clamp(input.Code, 0, 127);
            }
            if (input.Kind == BindingKind.Pov)
            {
                return BindingPovOffset + Mathf.Clamp(input.Code, 0, 3);
            }
            if (input.Kind == BindingKind.Key)
            {
                return BindingKeyOffset + Mathf.Max(0, input.Code);
            }
            if (input.Kind == BindingKind.MouseButton)
            {
                return BindingMouseOffset + Mathf.Clamp(input.Code, 0, 31);
            }
            if (input.Kind == BindingKind.GamepadButton)
            {
                return BindingGamepadButtonOffset + Mathf.Clamp(input.Code, 0, 255);
            }
            if (input.Kind == BindingKind.GamepadDpad)
            {
                return BindingGamepadDpadOffset + Mathf.Clamp(input.Code, 0, 3);
            }
            if (input.Kind == BindingKind.GamepadAxis)
            {
                return BindingGamepadAxisOffset + Mathf.Clamp(input.Code, 0, 15);
            }
            if (input.Kind == BindingKind.WheelAxis)
            {
                return BindingWheelAxisOffset + Mathf.Clamp(input.Code, 0, 31);
            }
            if (input.Kind == BindingKind.GamepadDpadAxis)
            {
                return BindingGamepadDpadAxisOffset + Mathf.Clamp(input.Code, 0, 1);
            }
            if (input.Kind == BindingKind.WheelDpadAxis)
            {
                return BindingWheelDpadAxisOffset + Mathf.Clamp(input.Code, 0, 1);
            }
            return -1;
        }

        private static BindingInput Decode(int raw)
        {
            if (raw < 0)
            {
                return new BindingInput { Kind = BindingKind.None, Code = 0 };
            }
            if (raw >= BindingWheelDpadAxisOffset)
            {
                return new BindingInput { Kind = BindingKind.WheelDpadAxis, Code = Mathf.Clamp(raw - BindingWheelDpadAxisOffset, 0, 1) };
            }
            if (raw >= BindingGamepadDpadAxisOffset)
            {
                return new BindingInput { Kind = BindingKind.GamepadDpadAxis, Code = Mathf.Clamp(raw - BindingGamepadDpadAxisOffset, 0, 1) };
            }
            if (raw >= BindingWheelAxisOffset)
            {
                return new BindingInput { Kind = BindingKind.WheelAxis, Code = Mathf.Clamp(raw - BindingWheelAxisOffset, 0, 31) };
            }
            if (raw >= BindingGamepadAxisOffset)
            {
                return new BindingInput { Kind = BindingKind.GamepadAxis, Code = Mathf.Clamp(raw - BindingGamepadAxisOffset, 0, 15) };
            }
            if (raw >= BindingGamepadDpadOffset)
            {
                return new BindingInput { Kind = BindingKind.GamepadDpad, Code = Mathf.Clamp(raw - BindingGamepadDpadOffset, 0, 3) };
            }
            if (raw >= BindingGamepadButtonOffset)
            {
                return new BindingInput { Kind = BindingKind.GamepadButton, Code = Mathf.Clamp(raw - BindingGamepadButtonOffset, 0, 255) };
            }
            if (raw >= BindingMouseOffset)
            {
                return new BindingInput { Kind = BindingKind.MouseButton, Code = Mathf.Clamp(raw - BindingMouseOffset, 0, 31) };
            }
            if (raw >= BindingKeyOffset)
            {
                return new BindingInput { Kind = BindingKind.Key, Code = Mathf.Max(0, raw - BindingKeyOffset) };
            }
            if (raw >= BindingPovOffset)
            {
                return new BindingInput { Kind = BindingKind.Pov, Code = Mathf.Clamp(raw - BindingPovOffset, 0, 3) };
            }
            return new BindingInput { Kind = BindingKind.Button, Code = Mathf.Clamp(raw, 0, 127) };
        }
    }
}
