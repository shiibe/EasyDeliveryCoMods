using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace SebBinds
{
    public static class BindingEvaluator
    {
        private const float AxisPressThreshold = 0.35f;

        private static int _axisCacheFrame = -1;
        private static Gamepad _axisCacheGamepad;

        private static float _lt;
        private static float _rt;
        private static float _lsx;
        private static float _lsy;
        private static float _rsx;
        private static float _rsy;

        private static float _plt;
        private static float _prt;
        private static float _plsx;
        private static float _plsy;
        private static float _prsx;
        private static float _prsy;

        public static void BeginFrame()
        {
            int f = Time.frameCount;
            var g = Gamepad.current;

            if (_axisCacheFrame == f && ReferenceEquals(_axisCacheGamepad, g))
            {
                return;
            }

            // Shift current to previous.
            _plt = _lt;
            _prt = _rt;
            _plsx = _lsx;
            _plsy = _lsy;
            _prsx = _rsx;
            _prsy = _rsy;

            _axisCacheGamepad = g;
            if (g == null)
            {
                _lt = _rt = _lsx = _lsy = _rsx = _rsy = 0f;
                _axisCacheFrame = f;
                return;
            }

            _lt = SafeReadAxis(g.leftTrigger);
            _rt = SafeReadAxis(g.rightTrigger);
            _lsx = SafeReadAxis(g.leftStick.x);
            _lsy = SafeReadAxis(g.leftStick.y);
            _rsx = SafeReadAxis(g.rightStick.x);
            _rsy = SafeReadAxis(g.rightStick.y);
            _axisCacheFrame = f;
        }

        private static float SafeReadAxis(InputControl<float> c)
        {
            if (c == null)
            {
                return 0f;
            }
            try
            {
                return c.ReadValue();
            }
            catch
            {
                return 0f;
            }
        }

        public static float GetAxisValue(BindingInput input)
        {
            if (input.Kind == BindingKind.GamepadDpadAxis)
            {
                var g = Gamepad.current;
                if (g == null)
                {
                    return 0f;
                }
                var v = g.dpad.ReadValue();
                return input.Code == 0 ? v.x : v.y;
            }

            if (input.Kind == BindingKind.WheelDpadAxis)
            {
                if (!WheelInterop.TryGetPov8Vector(out var pov))
                {
                    return 0f;
                }
                // Wheel POV is inverted relative to normal screen coords.
                var v = -pov;
                return input.Code == 0 ? v.x : v.y;
            }

            if (input.Kind == BindingKind.WheelAxis)
            {
                return WheelInterop.GetWheelAxisValue(input.Code);
            }

            if (input.Kind != BindingKind.GamepadAxis)
            {
                return 0f;
            }

            BeginFrame();

            switch (input.Code)
            {
                case 0:
                    return _lt;
                case 1:
                    return _rt;
                case 2:
                    return _lsx;
                case 3:
                    return _lsy;
                case 4:
                    return _rsx;
                case 5:
                    return _rsy;
                default:
                    return 0f;
            }
        }

        private static float GetPrevAxisValue(BindingInput input)
        {
            if (input.Kind != BindingKind.GamepadAxis)
            {
                return 0f;
            }

            BeginFrame();

            switch (input.Code)
            {
                case 0:
                    return _plt;
                case 1:
                    return _prt;
                case 2:
                    return _plsx;
                case 3:
                    return _plsy;
                case 4:
                    return _prsx;
                case 5:
                    return _prsy;
                default:
                    return 0f;
            }
        }

        // For capture heuristics.
        internal static float GetPrevAxisValueForCapture(BindingInput input)
        {
            return GetPrevAxisValue(input);
        }

        public static bool IsDown(BindingInput input)
        {
            if (input.Kind == BindingKind.None)
            {
                return false;
            }

            if (input.Kind == BindingKind.Button || input.Kind == BindingKind.Pov)
            {
                return WheelInterop.IsBindingDownForCurrentFrame(input);
            }

            if (input.Kind == BindingKind.Key)
            {
                var kb = Keyboard.current;
                if (kb == null)
                {
                    return false;
                }
                var key = (Key)Mathf.Max(0, input.Code);
                return kb[key] != null && kb[key].isPressed;
            }

            if (input.Kind == BindingKind.MouseButton)
            {
                var m = Mouse.current;
                if (m == null)
                {
                    return false;
                }
                switch (input.Code)
                {
                    case 0:
                        return m.leftButton.isPressed;
                    case 1:
                        return m.rightButton.isPressed;
                    case 2:
                        return m.middleButton.isPressed;
                    default:
                        return false;
                }
            }

            if (input.Kind == BindingKind.GamepadButton)
            {
                return TryGetGamepadButtonControl(Gamepad.current, input.Code, out var c) && c.isPressed;
            }

            if (input.Kind == BindingKind.GamepadAxis)
            {
                return GetAxisValue(input) > AxisPressThreshold;
            }

            if (input.Kind == BindingKind.WheelAxis)
            {
                return Mathf.Abs(GetAxisValue(input)) > AxisPressThreshold;
            }

            if (input.Kind == BindingKind.WheelDpadAxis)
            {
                return Mathf.Abs(GetAxisValue(input)) > AxisPressThreshold;
            }

            if (input.Kind == BindingKind.GamepadDpad)
            {
                var g = Gamepad.current;
                if (g == null)
                {
                    return false;
                }
                switch (Mathf.Clamp(input.Code, 0, 3))
                {
                    case 0:
                        return g.dpad.up.isPressed;
                    case 1:
                        return g.dpad.right.isPressed;
                    case 2:
                        return g.dpad.down.isPressed;
                    default:
                        return g.dpad.left.isPressed;
                }
            }

            return false;
        }

        public static bool WasPressedThisFrame(BindingInput input)
        {
            if (input.Kind == BindingKind.None)
            {
                return false;
            }

            if (input.Kind == BindingKind.Button || input.Kind == BindingKind.Pov)
            {
                return WheelInterop.IsBindingPressedThisFrameForCurrentFrame(input);
            }

            if (input.Kind == BindingKind.Key)
            {
                var kb = Keyboard.current;
                if (kb == null)
                {
                    return false;
                }
                var key = (Key)Mathf.Max(0, input.Code);
                return kb[key] != null && kb[key].wasPressedThisFrame;
            }

            if (input.Kind == BindingKind.MouseButton)
            {
                var m = Mouse.current;
                if (m == null)
                {
                    return false;
                }
                switch (input.Code)
                {
                    case 0:
                        return m.leftButton.wasPressedThisFrame;
                    case 1:
                        return m.rightButton.wasPressedThisFrame;
                    case 2:
                        return m.middleButton.wasPressedThisFrame;
                    default:
                        return false;
                }
            }

            if (input.Kind == BindingKind.GamepadButton)
            {
                return TryGetGamepadButtonControl(Gamepad.current, input.Code, out var c) && c.wasPressedThisFrame;
            }

            if (input.Kind == BindingKind.GamepadAxis)
            {
                float prev = GetPrevAxisValue(input);
                float cur = GetAxisValue(input);
                return prev <= AxisPressThreshold && cur > AxisPressThreshold;
            }

            if (input.Kind == BindingKind.GamepadDpad)
            {
                var g = Gamepad.current;
                if (g == null)
                {
                    return false;
                }
                switch (Mathf.Clamp(input.Code, 0, 3))
                {
                    case 0:
                        return g.dpad.up.wasPressedThisFrame;
                    case 1:
                        return g.dpad.right.wasPressedThisFrame;
                    case 2:
                        return g.dpad.down.wasPressedThisFrame;
                    default:
                        return g.dpad.left.wasPressedThisFrame;
                }
            }

            if (input.Kind == BindingKind.GamepadAxis)
            {
                float prev = GetPrevAxisValue(input);
                float cur = GetAxisValue(input);
                return prev <= AxisPressThreshold && cur > AxisPressThreshold;
            }


            return false;
        }

        public static bool WasReleasedThisFrame(BindingInput input)
        {
            if (input.Kind == BindingKind.None)
            {
                return false;
            }

            if (input.Kind == BindingKind.Button || input.Kind == BindingKind.Pov)
            {
                return WheelInterop.IsBindingReleasedThisFrameForCurrentFrame(input);
            }

            if (input.Kind == BindingKind.Key)
            {
                var kb = Keyboard.current;
                if (kb == null)
                {
                    return false;
                }
                var key = (Key)Mathf.Max(0, input.Code);
                return kb[key] != null && kb[key].wasReleasedThisFrame;
            }

            if (input.Kind == BindingKind.MouseButton)
            {
                var m = Mouse.current;
                if (m == null)
                {
                    return false;
                }
                switch (input.Code)
                {
                    case 0:
                        return m.leftButton.wasReleasedThisFrame;
                    case 1:
                        return m.rightButton.wasReleasedThisFrame;
                    case 2:
                        return m.middleButton.wasReleasedThisFrame;
                    default:
                        return false;
                }
            }

            if (input.Kind == BindingKind.GamepadButton)
            {
                return TryGetGamepadButtonControl(Gamepad.current, input.Code, out var c) && c.wasReleasedThisFrame;
            }

            if (input.Kind == BindingKind.GamepadAxis)
            {
                float prev = GetPrevAxisValue(input);
                float cur = GetAxisValue(input);
                return prev > AxisPressThreshold && cur <= AxisPressThreshold;
            }

            if (input.Kind == BindingKind.GamepadDpad)
            {
                var g = Gamepad.current;
                if (g == null)
                {
                    return false;
                }
                switch (Mathf.Clamp(input.Code, 0, 3))
                {
                    case 0:
                        return g.dpad.up.wasReleasedThisFrame;
                    case 1:
                        return g.dpad.right.wasReleasedThisFrame;
                    case 2:
                        return g.dpad.down.wasReleasedThisFrame;
                    default:
                        return g.dpad.left.wasReleasedThisFrame;
                }
            }

            if (input.Kind == BindingKind.GamepadAxis)
            {
                float prev = GetPrevAxisValue(input);
                float cur = GetAxisValue(input);
                return prev > AxisPressThreshold && cur <= AxisPressThreshold;
            }


            return false;
        }

        // Code is stored as: (int)GamepadButton (Unity InputSystem).
        internal static bool TryGetGamepadButtonControl(Gamepad g, int code, out ButtonControl control)
        {
            control = null;
            if (g == null)
            {
                return false;
            }

            var b = (GamepadButton)code;
            switch (b)
            {
                case GamepadButton.South:
                    control = g.buttonSouth;
                    return true;
                case GamepadButton.North:
                    control = g.buttonNorth;
                    return true;
                case GamepadButton.West:
                    control = g.buttonWest;
                    return true;
                case GamepadButton.East:
                    control = g.buttonEast;
                    return true;

                case GamepadButton.Start:
                    control = g.startButton;
                    return true;
                case GamepadButton.Select:
                    control = g.selectButton;
                    return true;

                case GamepadButton.LeftShoulder:
                    control = g.leftShoulder;
                    return true;
                case GamepadButton.RightShoulder:
                    control = g.rightShoulder;
                    return true;

                case GamepadButton.LeftTrigger:
                    control = g.leftTrigger;
                    return true;
                case GamepadButton.RightTrigger:
                    control = g.rightTrigger;
                    return true;

                case GamepadButton.LeftStick:
                    control = g.leftStickButton;
                    return true;
                case GamepadButton.RightStick:
                    control = g.rightStickButton;
                    return true;

                default:
                    return false;
            }
        }
    }
}
