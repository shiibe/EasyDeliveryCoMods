using System;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace SebBinds
{
    internal static class WheelInterop
    {
        // Current wheel plugin GUID (pre-refactor). Keep this until we rename the wheel plugin.
        private const string WheelPluginGuid = "shibe.easydeliveryco.seblogiwheel";

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
        private static MethodInfo _requestOpenAxisMapping;
        private static FieldInfo _isInWalkingModeField;

        // Cached wheel state accessors (avoid reflection lookups/allocations in hot paths).
        private static MethodInfo _tryGetCachedWheelState;
        private static MethodInfo _getAxisValue;
        private static MethodInfo _getSteeringAxis;
        private static MethodInfo _getThrottleAxis;
        private static MethodInfo _getBrakeAxis;
        private static MethodInfo _getClutchAxis;
        private static MethodInfo _normalizeSteering;
        private static MethodInfo _normalizePedal;
        private static Type _axisIdType;
        private static readonly object[] _stateArgs = new object[1];
        private static readonly object[] _axisArgs = new object[2];
        private static readonly object[] _normSteerArgs = new object[1];
        private static readonly object[] _normPedalArgs = new object[2];
        private static readonly object[] _axisIdCache = new object[8];

        private static object _tmpWheelBindingBoxed;
        private static readonly object[] _tmpWheelBindingArgs = new object[1];

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

        private static bool EnsureWheelStateAccessors()
        {
            if (_tryGetCachedWheelState != null && _getAxisValue != null)
            {
                return true;
            }

            if (!IsWheelPluginPresent())
            {
                return false;
            }

            if (!EnsureWheelReflection())
            {
                return false;
            }

            Type t = GetWheelPluginType();
            if (t == null)
            {
                return false;
            }

            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            _tryGetCachedWheelState = t.GetMethod("TryGetCachedWheelState", flags);
            _getAxisValue = t.GetMethod("GetAxisValue", flags);
            _getSteeringAxis = t.GetMethod("GetSteeringAxis", flags);
            _getThrottleAxis = t.GetMethod("GetThrottleAxis", flags);
            _getBrakeAxis = t.GetMethod("GetBrakeAxis", flags);
            _getClutchAxis = t.GetMethod("GetClutchAxis", flags);
            _normalizeSteering = t.GetMethod("NormalizeSteering", flags);
            _normalizePedal = t.GetMethod("NormalizePedal", flags);

            if (_getAxisValue != null)
            {
                try
                {
                    _axisIdType = _getAxisValue.GetParameters()[1].ParameterType;
                }
                catch
                {
                    _axisIdType = null;
                }
            }

            return _tryGetCachedWheelState != null && _getAxisValue != null;
        }

        private static object GetAxisIdObject(int axisIdCode)
        {
            axisIdCode = Mathf.Clamp(axisIdCode, 0, 7);
            var cached = _axisIdCache[axisIdCode];
            if (cached != null)
            {
                return cached;
            }

            object v;
            if (_axisIdType != null && _axisIdType.IsEnum)
            {
                v = Enum.ToObject(_axisIdType, axisIdCode);
            }
            else
            {
                v = axisIdCode;
            }
            _axisIdCache[axisIdCode] = v;
            return v;
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
            _requestOpenAxisMapping = t.GetMethod("RequestOpenAxisMapping", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

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

        internal static void RequestOpenAxisMapping()
        {
            if (!EnsureWheelReflection() || _requestOpenAxisMapping == null)
            {
                return;
            }
            try
            {
                _requestOpenAxisMapping.Invoke(null, null);
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
                if (!EnsureWheelStateAccessors() || _normalizeSteering == null || _normalizePedal == null)
                {
                    return 0f;
                }

                _stateArgs[0] = null;
                bool ok;
                try
                {
                    ok = (bool)_tryGetCachedWheelState.Invoke(null, _stateArgs);
                }
                catch
                {
                    ok = false;
                }
                if (!ok || _stateArgs[0] == null)
                {
                    return 0f;
                }

                var state = _stateArgs[0];

                if (code == 0)
                {
                    if (_getSteeringAxis == null)
                    {
                        return 0f;
                    }
                    var axisId = _getSteeringAxis.Invoke(null, null);
                    _axisArgs[0] = state;
                    _axisArgs[1] = axisId;
                    int raw = (int)_getAxisValue.Invoke(null, _axisArgs);
                    _normSteerArgs[0] = raw;
                    return (float)_normalizeSteering.Invoke(null, _normSteerArgs);
                }

                MethodInfo which = code == 1 ? _getThrottleAxis : (code == 2 ? _getBrakeAxis : _getClutchAxis);
                if (which == null)
                {
                    return 0f;
                }
                var pedAxisId = which.Invoke(null, null);

                _axisArgs[0] = state;
                _axisArgs[1] = pedAxisId;
                int rawPedal = (int)_getAxisValue.Invoke(null, _axisArgs);

                // PedalKind enum lives in wheel plugin; pass int then coerce if needed.
                int pkInt = code == 1 ? 0 : (code == 2 ? 1 : 2);
                object pk = pkInt;
                try
                {
                    _normPedalArgs[0] = rawPedal;
                    _normPedalArgs[1] = pk;
                    return (float)_normalizePedal.Invoke(null, _normPedalArgs);
                }
                catch
                {
                    var pkType = _normalizePedal.GetParameters()[1].ParameterType;
                    if (pkType.IsEnum)
                    {
                        pk = Enum.ToObject(pkType, pkInt);
                    }
                    _normPedalArgs[0] = rawPedal;
                    _normPedalArgs[1] = pk;
                    return (float)_normalizePedal.Invoke(null, _normPedalArgs);
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
                if (!EnsureWheelStateAccessors())
                {
                    return 0f;
                }

                _stateArgs[0] = null;
                bool ok;
                try
                {
                    ok = (bool)_tryGetCachedWheelState.Invoke(null, _stateArgs);
                }
                catch
                {
                    ok = false;
                }
                if (!ok || _stateArgs[0] == null)
                {
                    return 0f;
                }

                object state = _stateArgs[0];


                object axisId = GetAxisIdObject(axisIdCode);

                _axisArgs[0] = state;
                _axisArgs[1] = axisId;
                int raw = (int)_getAxisValue.Invoke(null, _axisArgs);

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
            if (!IsWheelPluginPresent())
            {
                return false;
            }
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
            if (!IsWheelPluginPresent())
            {
                return false;
            }
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
            if (!IsWheelPluginPresent())
            {
                return false;
            }
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
            if (!IsWheelPluginPresent())
            {
                return false;
            }
            if (!EnsureWheelReflection())
            {
                return false;
            }

            if (_tmpWheelBindingBoxed == null || _wheelBindingInputType == null || _tmpWheelBindingBoxed.GetType() != _wheelBindingInputType)
            {
                _tmpWheelBindingBoxed = Activator.CreateInstance(_wheelBindingInputType);
            }
            SetWheelBindingFields(_tmpWheelBindingBoxed, input);
            try
            {
                _tmpWheelBindingArgs[0] = _tmpWheelBindingBoxed;
                return (bool)_isBindingDownForCurrentFrame.Invoke(null, _tmpWheelBindingArgs);
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsBindingPressedThisFrameForCurrentFrame(BindingInput input)
        {
            if (!IsWheelPluginPresent())
            {
                return false;
            }
            if (!EnsureWheelReflection())
            {
                return false;
            }

            if (_tmpWheelBindingBoxed == null || _wheelBindingInputType == null || _tmpWheelBindingBoxed.GetType() != _wheelBindingInputType)
            {
                _tmpWheelBindingBoxed = Activator.CreateInstance(_wheelBindingInputType);
            }
            SetWheelBindingFields(_tmpWheelBindingBoxed, input);
            try
            {
                _tmpWheelBindingArgs[0] = _tmpWheelBindingBoxed;
                return (bool)_isBindingPressedThisFrame.Invoke(null, _tmpWheelBindingArgs);
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsBindingReleasedThisFrameForCurrentFrame(BindingInput input)
        {
            if (!IsWheelPluginPresent())
            {
                return false;
            }
            if (!EnsureWheelReflection())
            {
                return false;
            }

            if (_tmpWheelBindingBoxed == null || _wheelBindingInputType == null || _tmpWheelBindingBoxed.GetType() != _wheelBindingInputType)
            {
                _tmpWheelBindingBoxed = Activator.CreateInstance(_wheelBindingInputType);
            }
            SetWheelBindingFields(_tmpWheelBindingBoxed, input);
            try
            {
                _tmpWheelBindingArgs[0] = _tmpWheelBindingBoxed;
                return (bool)_isBindingReleasedThisFrame.Invoke(null, _tmpWheelBindingArgs);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryGetPov8Vector(out Vector2 v)
        {
            v = Vector2.zero;
            if (!IsWheelPluginPresent())
            {
                return false;
            }
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
            if (!IsWheelPluginPresent())
            {
                return false;
            }
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
