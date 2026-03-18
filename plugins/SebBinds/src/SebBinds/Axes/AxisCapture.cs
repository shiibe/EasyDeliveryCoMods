using UnityEngine;

namespace SebBinds
{
    internal static class AxisCapture
    {
        internal static bool TryCaptureNextAxis(BindingScheme scheme, out BindingInput input)
        {
            // Wheel axes via providers.
            if (scheme == BindingScheme.Wheel)
            {
                // Prefer axis movement from the wheel device directly.
                float steer = WheelInterop.GetWheelAxisValue(0);
                float throttle = WheelInterop.GetWheelAxisValue(1);
                float brake = WheelInterop.GetWheelAxisValue(2);
                float clutch = WheelInterop.GetWheelAxisValue(3);

                // Simple capture: pick the largest magnitude delta.
                float best = 0f;
                int bestCode = -1;
                void Consider(int code, float v)
                {
                    float a = Mathf.Abs(v);
                    if (a > best)
                    {
                        best = a;
                        bestCode = code;
                    }
                }
                Consider(0, steer);
                Consider(1, throttle);
                Consider(2, brake);
                Consider(3, clutch);

                if (bestCode >= 0 && best > 0.35f)
                {
                    input = new BindingInput { Kind = BindingKind.WheelAxis, Code = bestCode };
                    return true;
                }

                // Dpad as axis (X/Y) from wheel POV.
                if (WheelInterop.TryGetPov8Vector(out var pov))
                {
                    var v = -pov;
                    if (Mathf.Abs(v.x) > 0.5f)
                    {
                        input = new BindingInput { Kind = BindingKind.WheelDpadAxis, Code = 0 };
                        return true;
                    }
                    if (Mathf.Abs(v.y) > 0.5f)
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
