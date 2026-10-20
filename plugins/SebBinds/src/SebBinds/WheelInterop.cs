using BepInEx.Bootstrap;
using UnityEngine;

namespace SebBinds
{
    internal static class WheelInterop
    {
        private const string WheelPluginGuid = "shibe.easydeliveryco.seblogiwheel";

        internal static IWheelSource Source;

        internal static bool IsWheelPluginPresent()
        {
            return Chainloader.PluginInfos.ContainsKey(WheelPluginGuid);
        }

        // Fail open: don't lock out binding before the wheel registers.
        internal static bool HasCalibration() => Source == null || Source.HasCalibration();

        internal static void RequestOpenCalibrationWizard() => Source?.RequestOpenCalibrationWizard();

        internal static void RequestOpenAxisMapping() => Source?.RequestOpenAxisMapping();

        internal static bool IsWheelMenuActive() => Source != null && Source.IsMenuActive();

        internal static bool IsWheelBindingCaptureActive() => Source != null && Source.IsBindingCaptureActive();

        internal static bool IsWheelCalibrationWizardActive() => Source != null && Source.IsCalibrationWizardActive();

        internal static float GetWheelRawAxisValue(int axisIdCode)
        {
            return Source != null ? Source.GetRawAxisValue(Mathf.Clamp(axisIdCode, 0, 7)) : 0f;
        }

        internal static bool TryCaptureNextBinding(out BindingInput input)
        {
            input = default;
            return Source != null && Source.TryCaptureNextBinding(out input);
        }

        internal static bool IsBindingDownForCurrentFrame(BindingInput input) => Source != null && Source.IsBindingDown(input);

        internal static bool IsBindingPressedThisFrameForCurrentFrame(BindingInput input) => Source != null && Source.IsBindingPressedThisFrame(input);

        internal static bool IsBindingReleasedThisFrameForCurrentFrame(BindingInput input) => Source != null && Source.IsBindingReleasedThisFrame(input);

        internal static bool TryGetPov8Vector(out Vector2 v)
        {
            v = Vector2.zero;
            return Source != null && Source.TryGetPov8Vector(out v);
        }

        internal static bool TryGetIsInWalkingMode(out bool isWalking)
        {
            isWalking = Source != null && Source.IsInWalkingMode();
            return Source != null;
        }

        internal static bool TryGetWheelLastInput(out float steer, out float accel)
        {
            steer = 0f;
            accel = 0f;
            return Source != null && Source.TryGetLastInput(out steer, out accel);
        }
    }
}
