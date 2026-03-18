using UnityEngine;

namespace SebBinds
{
    public static class AxisBindingStore
    {
        private static string Key(BindingScheme scheme, AxisAction action)
        {
            // Stable numeric key.
            if (scheme == BindingScheme.Wheel)
            {
                return "SebBinds_Wheel_Axis_" + (int)action;
            }
            if (scheme == BindingScheme.Keyboard)
            {
                return "SebBinds_KB_Axis_" + (int)action;
            }
            return "SebBinds_Axis_" + (int)action;
        }

        public static BindingInput GetAxisBinding(BindingScheme scheme, AxisAction action)
        {
            string key = Key(scheme, action);
            int raw = PlayerPrefs.GetInt(key, -1);
            var decoded = Decode(raw);

            // Migration from earlier builds where wheel axes were stored in the controller keyspace.
            if (raw < 0 && scheme == BindingScheme.Wheel)
            {
                string legacy = Key(BindingScheme.Controller, action);
                int legacyRaw = PlayerPrefs.GetInt(legacy, -1);
                var legacyDecoded = Decode(legacyRaw);
                if (legacyDecoded.Kind == BindingKind.WheelAxis || legacyDecoded.Kind == BindingKind.WheelDpadAxis)
                {
                    PlayerPrefs.SetInt(key, legacyRaw);
                    PlayerPrefs.DeleteKey(legacy);
                    decoded = legacyDecoded;
                }
            }

            // Keep schemes isolated.
            if (scheme == BindingScheme.Controller && (decoded.Kind == BindingKind.WheelAxis || decoded.Kind == BindingKind.WheelDpadAxis))
            {
                return new BindingInput { Kind = BindingKind.None, Code = 0 };
            }
            if (scheme == BindingScheme.Wheel && (decoded.Kind == BindingKind.GamepadAxis || decoded.Kind == BindingKind.GamepadDpadAxis || decoded.Kind == BindingKind.GamepadDpad))
            {
                return new BindingInput { Kind = BindingKind.None, Code = 0 };
            }

            return decoded;
        }

        public static void SetAxisBinding(BindingScheme scheme, AxisAction action, BindingInput input)
        {
            // Keep schemes isolated.
            if (scheme == BindingScheme.Controller && (input.Kind == BindingKind.WheelAxis || input.Kind == BindingKind.WheelDpadAxis))
            {
                input = new BindingInput { Kind = BindingKind.None, Code = 0 };
            }
            if (scheme == BindingScheme.Wheel && (input.Kind == BindingKind.GamepadAxis || input.Kind == BindingKind.GamepadDpadAxis || input.Kind == BindingKind.GamepadDpad))
            {
                input = new BindingInput { Kind = BindingKind.None, Code = 0 };
            }

            PlayerPrefs.SetInt(Key(scheme, action), Encode(input));
        }

        public static void ClearAxisBinding(BindingScheme scheme, AxisAction action)
        {
            PlayerPrefs.DeleteKey(Key(scheme, action));
        }

        // Back-compat: controller scheme.
        public static BindingInput GetAxisBinding(AxisAction action) => GetAxisBinding(BindingScheme.Controller, action);
        public static void SetAxisBinding(AxisAction action, BindingInput input) => SetAxisBinding(BindingScheme.Controller, action, input);
        public static void ClearAxisBinding(AxisAction action) => ClearAxisBinding(BindingScheme.Controller, action);

        public static void ClearAll()
        {
            foreach (AxisAction a in System.Enum.GetValues(typeof(AxisAction)))
            {
                PlayerPrefs.DeleteKey(Key(BindingScheme.Controller, a));
                PlayerPrefs.DeleteKey(Key(BindingScheme.Keyboard, a));
                PlayerPrefs.DeleteKey(Key(BindingScheme.Wheel, a));
            }
        }

        public static void ClearScheme(BindingScheme scheme)
        {
            foreach (AxisAction a in System.Enum.GetValues(typeof(AxisAction)))
            {
                PlayerPrefs.DeleteKey(Key(scheme, a));
            }
        }

        // Encoding matches BindingStore's scheme but is local to avoid exposing internals.
        private const int BindingPovOffset = 1000;
        private const int BindingKeyOffset = 2000;
        private const int BindingMouseOffset = 10000;
        private const int BindingGamepadButtonOffset = 11000;
        private const int BindingGamepadDpadOffset = 12000;
        private const int BindingGamepadAxisOffset = 14000;
        private const int BindingWheelAxisOffset = 15000;
        private const int BindingGamepadDpadAxisOffset = 16000;
        private const int BindingWheelDpadAxisOffset = 17000;

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
            if (input.Kind == BindingKind.GamepadDpad)
            {
                return BindingGamepadDpadOffset + Mathf.Clamp(input.Code, 0, 3);
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
