using System;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace SebBinds
{
    internal static class WheelInterop
    {
        // Current wheel plugin GUID (pre-refactor). Keep this until we rename the wheel plugin.
        private const string WheelPluginGuid = "shibe.easydeliveryco.logiwheel";

        private static Type _wheelPluginType;
        private static Type _wheelBindingInputType;
        private static FieldInfo _wheelBindKindField;
        private static FieldInfo _wheelBindCodeField;
        private static MethodInfo _tryCaptureNextBinding;
        private static MethodInfo _isBindingDownForCurrentFrame;
        private static MethodInfo _isBindingPressedThisFrame;
        private static MethodInfo _isBindingReleasedThisFrame;
        private static MethodInfo _tryGetPov8Vector;
        private static MethodInfo _isWheelMenuActive;
        private static MethodInfo _isWheelBindingCaptureActive;
        private static MethodInfo _isWheelCalibrationWizardActive;
        private static MethodInfo _hasCalibration;
        private static MethodInfo _requestOpenCalibrationWizard;
        private static FieldInfo _isInWalkingModeField;

        private static void SetWheelBindingFields(object boxedWheelBindingInput, BindingInput input)
        {
            if (boxedWheelBindingInput == null || _wheelBindKindField == null || _wheelBindCodeField == null)
            {
                return;
            }

            object kindValue;
            Type kindType = _wheelBindKindField.FieldType;
            if (kindType.IsEnum)
            {
                kindValue = Enum.ToObject(kindType, (int)input.Kind);
            }
            else
            {
                kindValue = Convert.ChangeType((int)input.Kind, kindType);
            }

            _wheelBindKindField.SetValue(boxedWheelBindingInput, kindValue);
            _wheelBindCodeField.SetValue(boxedWheelBindingInput, input.Code);
        }

        private static Type GetWheelPluginType()
        {
            if (_wheelPluginType != null)
            {
                return _wheelPluginType;
            }

            if (!Chainloader.PluginInfos.TryGetValue(WheelPluginGuid, out var info) || info == null)
            {
                return null;
            }
            var inst = info.Instance;
            _wheelPluginType = inst != null ? inst.GetType() : null;
            return _wheelPluginType;
        }

        internal static bool IsWheelPluginPresent()
        {
            return Chainloader.PluginInfos.ContainsKey(WheelPluginGuid);
        }

        private static bool EnsureWheelReflection()
        {
            Type t = GetWheelPluginType();
            if (t == null)
            {
                return false;
            }

            if (_tryCaptureNextBinding != null &&
                _isBindingDownForCurrentFrame != null &&
                _isBindingPressedThisFrame != null &&
                _isBindingReleasedThisFrame != null &&
                _tryGetPov8Vector != null &&
                _wheelBindingInputType != null)
            {
                return true;
            }

            _wheelBindingInputType = t.GetNestedType("BindingInput", BindingFlags.NonPublic | BindingFlags.Public);
            if (_wheelBindingInputType == null)
            {
                return false;
            }

            _wheelBindKindField = _wheelBindingInputType.GetField("Kind", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _wheelBindCodeField = _wheelBindingInputType.GetField("Code", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_wheelBindKindField == null || _wheelBindCodeField == null)
            {
                return false;
            }

            _tryCaptureNextBinding = t.GetMethod("TryCaptureNextBinding", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            _isBindingDownForCurrentFrame = t.GetMethod("IsBindingDownForCurrentFrame", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            _isBindingPressedThisFrame = t.GetMethod("IsBindingPressedThisFrameForCurrentFrame", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            _isBindingReleasedThisFrame = t.GetMethod("IsBindingReleasedThisFrameForCurrentFrame", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            _tryGetPov8Vector = t.GetMethod("TryGetPov8Vector", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

            _hasCalibration = t.GetMethod("HasCalibration", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            _requestOpenCalibrationWizard = t.GetMethod("RequestOpenCalibrationWizard", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

            // Private/internal helpers for gating input while wheel UI is active.
            _isWheelMenuActive = t.GetMethod("IsWheelMenuActive", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            _isWheelBindingCaptureActive = t.GetMethod("IsBindingCaptureActive", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            _isWheelCalibrationWizardActive = t.GetMethod("IsCalibrationWizardActive", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

            _isInWalkingModeField = t.GetField("_isInWalkingMode", BindingFlags.NonPublic | BindingFlags.Static);

            return _tryCaptureNextBinding != null &&
                   _isBindingDownForCurrentFrame != null &&
                   _isBindingPressedThisFrame != null &&
                   _isBindingReleasedThisFrame != null &&
                   _tryGetPov8Vector != null;
        }

        internal static bool HasCalibration()
        {
            if (!EnsureWheelReflection() || _hasCalibration == null)
            {
                // Fail open: don't lock out binding if older wheel builds don't expose this.
                return true;
            }
            try
            {
                return (bool)_hasCalibration.Invoke(null, null);
            }
            catch
            {
                return true;
            }
        }

        internal static void RequestOpenCalibrationWizard()
        {
            if (!EnsureWheelReflection() || _requestOpenCalibrationWizard == null)
            {
                return;
            }
            try
            {
                _requestOpenCalibrationWizard.Invoke(null, null);
            }
            catch
            {
            }
        }

        internal static float GetWheelAxisValue(int code)
        {
            // Minimal mapping for SebBinds axis evaluation/capture.
            // 0=steer, 1=throttle, 2=brake, 3=clutch
            try
            {
                Type t = GetWheelPluginType();
                if (t == null)
                {
                    return 0f;
                }

                // Call into the wheel plugin's normalization helpers.
                var tryGetState = t.GetMethod("TryGetCachedWheelState", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                var getAxis = t.GetMethod("GetAxisValue", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                var getSteerAxis = t.GetMethod("GetSteeringAxis", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                var getThrottleAxis = t.GetMethod("GetThrottleAxis", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                var getBrakeAxis = t.GetMethod("GetBrakeAxis", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                var getClutchAxis = t.GetMethod("GetClutchAxis", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                var normSteer = t.GetMethod("NormalizeSteering", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                var normPedal = t.GetMethod("NormalizePedal", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

                if (tryGetState == null || getAxis == null || normSteer == null || normPedal == null)
                {
                    return 0f;
                }

                var args = new object[] { null };
                bool ok;
                try
                {
                    ok = (bool)tryGetState.Invoke(null, args);
                }
                catch
                {
                    ok = false;
                }
                if (!ok || args[0] == null)
                {
                    return 0f;
                }

                var state = args[0];

                if (code == 0)
                {
                    if (getSteerAxis == null)
                    {
                        return 0f;
                    }
                    var axisId = getSteerAxis.Invoke(null, null);
                    int raw = (int)getAxis.Invoke(null, new[] { state, axisId });
                    return (float)normSteer.Invoke(null, new object[] { raw });
                }

                MethodInfo which = code == 1 ? getThrottleAxis : (code == 2 ? getBrakeAxis : getClutchAxis);
                if (which == null)
                {
                    return 0f;
                }
                var pedAxisId = which.Invoke(null, null);
                int rawPedal = (int)getAxis.Invoke(null, new[] { state, pedAxisId });

                // PedalKind enum lives in wheel plugin; pass int then coerce if needed.
                int pkInt = code == 1 ? 0 : (code == 2 ? 1 : 2);
                object pk = pkInt;
                try
                {
                    return (float)normPedal.Invoke(null, new object[] { rawPedal, pk });
                }
                catch
                {
                    var pkType = normPedal.GetParameters()[1].ParameterType;
                    if (pkType.IsEnum)
                    {
                        pk = Enum.ToObject(pkType, pkInt);
                    }
                    return (float)normPedal.Invoke(null, new object[] { rawPedal, pk });
                }
            }
            catch
            {
                return 0f;
            }
        }

        internal static float GetWheelRawAxisValue(int axisIdCode)
        {
            // AxisId codes match the wheel calibration tool (lX, lY, lZ, lRx, lRy, lRz, slider0, slider1).
            try
            {
                Type t = GetWheelPluginType();
                if (t == null)
                {
                    return 0f;
                }

                var tryGetState = t.GetMethod("TryGetCachedWheelState", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                var getAxis = t.GetMethod("GetAxisValue", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (tryGetState == null || getAxis == null)
                {
                    return 0f;
                }

                var args = new object[] { null };
                bool ok;
                try
                {
                    ok = (bool)tryGetState.Invoke(null, args);
                }
                catch
                {
                    ok = false;
                }
                if (!ok || args[0] == null)
                {
                    return 0f;
                }

                object state = args[0];

                // AxisId enum type is the second param of GetAxisValue.
                var axisParamType = getAxis.GetParameters()[1].ParameterType;
                object axisId = axisParamType.IsEnum
                    ? Enum.ToObject(axisParamType, Mathf.Clamp(axisIdCode, 0, 7))
                    : (object)Mathf.Clamp(axisIdCode, 0, 7);

                int raw = (int)getAxis.Invoke(null, new[] { state, axisId });

                // Typical joystick raw range is 0..65535.
                const float mid = 32767.5f;
                float v = (raw - mid) / mid;
                return Mathf.Clamp(v, -1f, 1f);
            }
            catch
            {
                return 0f;
            }
        }

        internal static bool IsWheelMenuActive()
        {
            if (!EnsureWheelReflection() || _isWheelMenuActive == null)
            {
                return false;
            }
            try
            {
                return (bool)_isWheelMenuActive.Invoke(null, null);
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsWheelBindingCaptureActive()
        {
            if (!EnsureWheelReflection() || _isWheelBindingCaptureActive == null)
            {
                return false;
            }
            try
            {
                return (bool)_isWheelBindingCaptureActive.Invoke(null, null);
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsWheelCalibrationWizardActive()
        {
            if (!EnsureWheelReflection() || _isWheelCalibrationWizardActive == null)
            {
                return false;
            }
            try
            {
                return (bool)_isWheelCalibrationWizardActive.Invoke(null, null);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryCaptureNextBinding(out BindingInput input)
        {
            input = default;
            if (!EnsureWheelReflection())
            {
                return false;
            }

            object boxed = Activator.CreateInstance(_wheelBindingInputType);
            object[] args = { boxed };
            bool ok;
            try
            {
                ok = (bool)_tryCaptureNextBinding.Invoke(null, args);
            }
            catch
            {
                return false;
            }

            if (!ok)
            {
                return false;
            }

            if (args.Length < 1 || args[0] == null)
            {
                return false;
            }

            object wheelBi = args[0];
            int kind = (int)Convert.ChangeType(_wheelBindKindField.GetValue(wheelBi), typeof(int));
            int code = (int)Convert.ChangeType(_wheelBindCodeField.GetValue(wheelBi), typeof(int));

            input = new BindingInput { Kind = (BindingKind)kind, Code = code };
            return true;
        }

        internal static bool IsBindingDownForCurrentFrame(BindingInput input)
        {
            if (!EnsureWheelReflection())
            {
                return false;
            }

            object boxed = Activator.CreateInstance(_wheelBindingInputType);
            SetWheelBindingFields(boxed, input);
            try
            {
                return (bool)_isBindingDownForCurrentFrame.Invoke(null, new object[] { boxed });
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsBindingPressedThisFrameForCurrentFrame(BindingInput input)
        {
            if (!EnsureWheelReflection())
            {
                return false;
            }

            object boxed = Activator.CreateInstance(_wheelBindingInputType);
            SetWheelBindingFields(boxed, input);
            try
            {
                return (bool)_isBindingPressedThisFrame.Invoke(null, new object[] { boxed });
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsBindingReleasedThisFrameForCurrentFrame(BindingInput input)
        {
            if (!EnsureWheelReflection())
            {
                return false;
            }

            object boxed = Activator.CreateInstance(_wheelBindingInputType);
            SetWheelBindingFields(boxed, input);
            try
            {
                return (bool)_isBindingReleasedThisFrame.Invoke(null, new object[] { boxed });
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryGetPov8Vector(out Vector2 v)
        {
            v = Vector2.zero;
            if (!EnsureWheelReflection())
            {
                return false;
            }

            object[] args = { v };
            bool ok;
            try
            {
                ok = (bool)_tryGetPov8Vector.Invoke(null, args);
            }
            catch
            {
                return false;
            }

            if (!ok)
            {
                return false;
            }

            if (args.Length > 0 && args[0] is Vector2 vec)
            {
                v = vec;
            }
            return true;
        }

        internal static bool TryGetIsInWalkingMode(out bool isWalking)
        {
            isWalking = false;
            if (!EnsureWheelReflection() || _isInWalkingModeField == null)
            {
                return false;
            }

            try
            {
                object v = _isInWalkingModeField.GetValue(null);
                if (v is bool b)
                {
                    isWalking = b;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}
