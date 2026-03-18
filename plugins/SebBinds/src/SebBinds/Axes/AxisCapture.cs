using UnityEngine;

namespace SebBinds
{
    internal static class AxisCapture
    {
        private static bool _wheelBaselineValid;
        private static readonly float[] _wheelBaseline = new float[8];
        private static Vector2 _wheelPovBaseline;

        internal static void BeginCaptureSession(BindingScheme scheme)
        {
            if (scheme != BindingScheme.Wheel)
            {
                _wheelBaselineValid = false;
                return;
            }

            for (int i = 0; i < _wheelBaseline.Length; i++)
            {
                _wheelBaseline[i] = WheelInterop.GetWheelRawAxisValue(i);
            }
            _wheelBaselineValid = true;

            if (WheelInterop.TryGetPov8Vector(out var pov))
            {
                _wheelPovBaseline = -pov;
            }
            else
            {
                _wheelPovBaseline = Vector2.zero;
            }
        }

        internal static bool TryCaptureNextAxis(BindingScheme scheme, out BindingInput input)
        {
            // Wheel axes via providers.
            if (scheme == BindingScheme.Wheel)
            {
                // Pick the axis that changed the most since capture started.
                float bestDelta = 0f;
                int best = -1;

                if (!_wheelBaselineValid)
                {
                    BeginCaptureSession(BindingScheme.Wheel);
                }

                for (int code = 0; code <= 7; code++)
                {
                    float cur = WheelInterop.GetWheelRawAxisValue(code);
                    float prev = _wheelBaseline[code];
                    float delta = Mathf.Abs(cur - prev);
                    if (delta > bestDelta)
                    {
                        bestDelta = delta;
                        best = code;
                    }
                }

                if (best >= 0 && bestDelta > 0.35f)
                {
                    input = new BindingInput { Kind = BindingKind.WheelAxis, Code = best };
                    return true;
                }

                // Dpad as axis (X/Y) from wheel POV.
                if (WheelInterop.TryGetPov8Vector(out var pov))
                {
                    var v = -pov;
                    if (Mathf.Abs(v.x - _wheelPovBaseline.x) > 0.5f && Mathf.Abs(v.x) > 0.5f)
                    {
                        input = new BindingInput { Kind = BindingKind.WheelDpadAxis, Code = 0 };
                        return true;
                    }
                    if (Mathf.Abs(v.y - _wheelPovBaseline.y) > 0.5f && Mathf.Abs(v.y) > 0.5f)
                    {
                        input = new BindingInput { Kind = BindingKind.WheelDpadAxis, Code = 1 };
                        return true;
                    }
                }
            }

            // Controller axes: pick the axis that moved the most.
            if (scheme == BindingScheme.Controller)
            {
                BindingEvaluator.BeginFrame();

                float bestDelta = 0f;
                int best = -1;

                // Codes: 0 LT, 1 RT, 2 LSX, 3 LSY, 4 RSX, 5 RSY
                for (int code = 0; code <= 5; code++)
                {
                    var b = new BindingInput { Kind = BindingKind.GamepadAxis, Code = code };
                    float cur = BindingEvaluator.GetAxisValue(b);
                    float prev = BindingEvaluator.GetPrevAxisValueForCapture(b);
                    float delta = Mathf.Abs(cur - prev);
                    if (delta > bestDelta)
                    {
                        bestDelta = delta;
                        best = code;
                    }
                }

                if (best >= 0 && bestDelta > 0.35f)
                {
                    input = new BindingInput { Kind = BindingKind.GamepadAxis, Code = best };
                    return true;
                }

                // Dpad as axis (X/Y).
                var g = UnityEngine.InputSystem.Gamepad.current;
                if (g != null)
                {
                    var v = g.dpad.ReadValue();
                    if (Mathf.Abs(v.x) > 0.5f)
                    {
                        input = new BindingInput { Kind = BindingKind.GamepadDpadAxis, Code = 0 };
                        return true;
                    }
                    if (Mathf.Abs(v.y) > 0.5f)
                    {
                        input = new BindingInput { Kind = BindingKind.GamepadDpadAxis, Code = 1 };
                        return true;
                    }
                }
            }

            input = default;
            return false;
        }
    }
}
