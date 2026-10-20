using UnityEngine;

namespace SebBinds
{
    // Implemented by the wheel plugin and registered with SebBindsApi.RegisterWheelSource.
    public interface IWheelSource
    {
        bool HasCalibration();
        void RequestOpenCalibrationWizard();
        void RequestOpenAxisMapping();

        bool IsMenuActive();
        bool IsBindingCaptureActive();
        bool IsCalibrationWizardActive();
        bool IsInWalkingMode();

        // Kind is Button or Pov.
        bool TryCaptureNextBinding(out BindingInput input);
        bool IsBindingDown(BindingInput input);
        bool IsBindingPressedThisFrame(BindingInput input);
        bool IsBindingReleasedThisFrame(BindingInput input);

        bool TryGetPov8Vector(out Vector2 v);
        // AxisId codes 0-7 (lX, lY, lZ, lRx, lRy, lRz, slider0, slider1), normalized to -1..1.
        float GetRawAxisValue(int axisIdCode);
        bool TryGetLastInput(out float steer, out float accel);
    }
}
