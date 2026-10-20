using UnityEngine;

namespace SebLogiWheel
{
    public partial class Plugin
    {
        // Wheel input exposed to SebBinds. Both BindingKind enums share Button=1, Pov=2.
        private sealed class BindsWheelSource : SebBinds.IWheelSource
        {
            public bool HasCalibration() => Plugin.HasCalibration();
            public void RequestOpenCalibrationWizard() => Plugin.RequestOpenCalibrationWizard();
            public void RequestOpenAxisMapping() => Plugin.RequestOpenAxisMapping();

            public bool IsMenuActive() => IsWheelMenuActive();
            public bool IsBindingCaptureActive() => Plugin.IsBindingCaptureActive();
            public bool IsCalibrationWizardActive() => Plugin.IsCalibrationWizardActive();
            public bool IsInWalkingMode() => _isInWalkingMode;

            public bool TryCaptureNextBinding(out SebBinds.BindingInput input)
            {
                bool ok = Plugin.TryCaptureNextBinding(out BindingInput wheel);
                input = new SebBinds.BindingInput { Kind = (SebBinds.BindingKind)(int)wheel.Kind, Code = wheel.Code };
                return ok;
            }

            public bool IsBindingDown(SebBinds.BindingInput input) => IsBindingDownForCurrentFrame(ToWheel(input));
            public bool IsBindingPressedThisFrame(SebBinds.BindingInput input) => IsBindingPressedThisFrameForCurrentFrame(ToWheel(input));
            public bool IsBindingReleasedThisFrame(SebBinds.BindingInput input) => IsBindingReleasedThisFrameForCurrentFrame(ToWheel(input));

            public bool TryGetPov8Vector(out Vector2 v) => Plugin.TryGetPov8Vector(out v);

            public float GetRawAxisValue(int axisIdCode)
            {
                if (!TryGetCachedWheelState(out var state))
                {
                    return 0f;
                }

                // Typical joystick raw range is 0..65535.
                const float mid = 32767.5f;
                return Mathf.Clamp((GetAxisValue(state, (AxisId)axisIdCode) - mid) / mid, -1f, 1f);
            }

            public bool TryGetLastInput(out float steer, out float accel) => TryGetWheelLastInput(out steer, out accel);

            private static BindingInput ToWheel(SebBinds.BindingInput input)
            {
                return new BindingInput { Kind = (BindingKind)(int)input.Kind, Code = input.Code };
            }
        }
    }
}
