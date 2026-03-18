using UnityEngine;

namespace SebBinds
{
    public static class AxisBindingStore
    {
        private static string Key(AxisAction action)
        {
            // Stable numeric key.
            return "SebBinds_Axis_" + (int)action;
        }

        public static BindingInput GetAxisBinding(AxisAction action)
        {
            int raw = PlayerPrefs.GetInt(Key(action), -1);
            return Decode(raw);
        }

        public static void SetAxisBinding(AxisAction action, BindingInput input)
        {
            PlayerPrefs.SetInt(Key(action), Encode(input));
        }

        public static void ClearAxisBinding(AxisAction action)
        {
            PlayerPrefs.DeleteKey(Key(action));
        }

        public static void ClearAll()
        {
            foreach (AxisAction a in System.Enum.GetValues(typeof(AxisAction)))
            {
                PlayerPrefs.DeleteKey(Key(a));
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
