using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SebBinds;
using UnityEngine;

namespace SebTruck
{
    public partial class Plugin
    {
        private static sCarController _currentCar;
        private static bool _isInWalkingMode;
        private static float _currentSpeedKmh;
        private static float _lastThrottle01;
        private static float _neutralRev01;

        private static float _ignitionHoldStart = -1f;
        private static bool _ignitionHoldConsumed;
        private static bool _ignitionHoldWasDown;
        private static bool _ignitionIgnoreHoldUntilRelease;
        private static float _ignitionOffSince = -1f;
        private static bool _ignitionPrevHeadlightsOn;
        private static bool _ignitionPrevRadioOn;

        private static readonly Dictionary<int, (float maxSpeedScale, float drivePowerScale)> _carScaleDefaults =
            new Dictionary<int, (float maxSpeedScale, float drivePowerScale)>();

        private static readonly Dictionary<int, (float intensity, float range)> _lightDefaults =
            new Dictionary<int, (float intensity, float range)>();

        private static Type _headlightsRuntimeType;
        private static FieldInfo _headlightsHeadLightsField;
        private static FieldInfo _headlightsCarMatField;
        private static FieldInfo _headlightsEmissiveRegularField;
        private static FieldInfo _headlightsHeadlightsOnField;
        private static FieldInfo _headlightsModelField;
        private static Texture2D _ignitionBlackEmissiveTex;

        private static void EnsureHeadlightsRefs(object instance)
        {
            if (instance == null)
            {
                return;
            }

            var t = instance.GetType();
            if (_headlightsRuntimeType == t)
            {
                return;
            }

            _headlightsRuntimeType = t;
            _headlightsHeadLightsField = AccessTools.Field(t, "headLights");
            _headlightsCarMatField = AccessTools.Field(t, "carMat");
            _headlightsEmissiveRegularField = AccessTools.Field(t, "emissiveRegular");
            _headlightsHeadlightsOnField = AccessTools.Field(t, "headlightsOn");
            _headlightsModelField = AccessTools.Field(t, "model");
        }

        private static Texture2D GetIgnitionBlackEmissionTex()
        {
            if (_ignitionBlackEmissiveTex != null)
            {
                return _ignitionBlackEmissiveTex;
            }

            _ignitionBlackEmissiveTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            _ignitionBlackEmissiveTex.SetPixels(new[] { Color.black, Color.black, Color.black, Color.black });
            _ignitionBlackEmissiveTex.Apply(false, true);
            _ignitionBlackEmissiveTex.hideFlags = HideFlags.HideAndDontSave;
            return _ignitionBlackEmissiveTex;
        }

        private static void ForceVehicleLightsOff(sCarController car)
        {
            if (car == null)
            {
                return;
            }

            var hl = car.headlights;
            if (hl == null)
            {
                return;
            }

            EnsureHeadlightsRefs(hl);

            var go = _headlightsHeadLightsField != null ? _headlightsHeadLightsField.GetValue(hl) as GameObject : null;
            if (go != null && go.activeSelf)
            {
                go.SetActive(false);
            }

            try
            {
                hl.headlightsOn = false;
            }
            catch
            {
                // ignore
            }

            var mat = _headlightsCarMatField != null ? _headlightsCarMatField.GetValue(hl) as Material : null;
            if (mat != null)
            {
                mat.SetTexture("_EmissionMap", GetIgnitionBlackEmissionTex());
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        private static void RestoreVehicleLightState(sCarController car, bool wantOn)
        {
            if (car == null)
            {
                return;
            }

            var hl = car.headlights;
            if (hl == null)
            {
                return;
            }

            EnsureHeadlightsRefs(hl);

            var go = _headlightsHeadLightsField != null ? _headlightsHeadLightsField.GetValue(hl) as GameObject : null;
            if (go != null)
            {
                go.SetActive(wantOn);
            }

            try
            {
                hl.headlightsOn = wantOn;
            }
            catch
            {
                // ignore
            }

            var mat = _headlightsCarMatField != null ? _headlightsCarMatField.GetValue(hl) as Material : null;
            var regular = _headlightsEmissiveRegularField != null ? _headlightsEmissiveRegularField.GetValue(hl) as Texture : null;
            if (wantOn && mat != null && regular != null)
            {
                mat.SetTexture("_EmissionMap", regular);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.white);
                }
            }
            if (!wantOn && mat != null)
            {
                mat.SetTexture("_EmissionMap", GetIgnitionBlackEmissionTex());
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        private static void ApplyIgnitionStateChange(bool ignitionOn)
        {
            var car = _currentCar;
            if (car == null || car.GuyActive)
            {
                return;
            }

            var hl = car.headlights;
            var radio = sRadioSystem.instance;
            bool radioIsForCar = radio != null && ReferenceEquals(radio.car, car);

            if (!ignitionOn)
            {
                _ignitionPrevHeadlightsOn = hl != null && hl.headlightsOn;
                _ignitionPrevRadioOn = radioIsForCar && radio.source != null && radio.source.enabled;

                _ignitionOffSince = Time.unscaledTime;

                ForceVehicleLightsOff(car);
                if (radioIsForCar && radio.source != null && radio.source.enabled)
                {
                    radio.ToggleRadio();
                }
                return;
            }

            // Ignition ON sound.
            try
            {
                PlayIgnitionOnSfx(car);
            }
            catch
            {
            }

            _ignitionOffSince = -1f;

            RestoreVehicleLightState(car, _ignitionPrevHeadlightsOn);
            if (radioIsForCar && radio.source != null && _ignitionPrevRadioOn && !radio.source.enabled)
            {
                radio.ToggleRadio();
            }

            if (GetManualTransmissionEnabled())
            {
                _manualGear = 1;
            }
        }

        private static void EnforceIgnitionOffForCurrentCar()
        {
            var car = _currentCar;
            if (car == null || car.GuyActive)
            {
                return;
            }

            if (car.player >= 0 && car.player < sInputManager.players.Length)
            {
                sInputManager.players[car.player].headlightsPressed = false;
                sInputManager.players[car.player].radioPressed = false;
                sInputManager.players[car.player].radioInput = Vector2.zero;
            }

            ForceVehicleLightsOff(car);

            var radio = sRadioSystem.instance;
            if (radio != null && ReferenceEquals(radio.car, car) && radio.source != null && radio.source.enabled)
            {
                radio.ToggleRadio();
            }
        }

        private static void ApplyHeadlightTuning(sCarController car)
        {
            if (car == null)
            {
                return;
            }

            var hl = car.headlights;
            if (hl == null)
            {
                return;
            }

            EnsureHeadlightsRefs(hl);
            var go = _headlightsHeadLightsField != null ? _headlightsHeadLightsField.GetValue(hl) as GameObject : null;
            if (go == null)
            {
                return;
            }

            float intenMul = GetHeadlightIntensityMult();
            float rangeMul = GetHeadlightRangeMult();
            if (!GetIgnitionEnabledEffective())
            {
                intenMul = 0f;
                rangeMul = 0f;
            }

            Light[] lights;
            try
            {
                lights = go.GetComponentsInChildren<Light>(true);
            }
            catch
            {
                lights = null;
            }

            if (lights == null)
            {
                return;
            }

            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null)
                {
                    continue;
                }
                int id = l.GetInstanceID();
                if (!_lightDefaults.TryGetValue(id, out var d))
                {
                    d = (l.intensity, l.range);
                    _lightDefaults[id] = d;
                }
                l.intensity = d.intensity * intenMul;
                l.range = d.range * rangeMul;
            }
        }

        private static void ApplySpeedScaleTuning(sCarController car)
        {
            if (car == null)
            {
                return;
            }

            int id = car.GetInstanceID();
            if (!_carScaleDefaults.ContainsKey(id))
            {
                _carScaleDefaults[id] = (car.maxSpeedScale, car.drivePowerScale);
            }
            var d = _carScaleDefaults[id];

            if (car.GuyActive)
            {
                car.maxSpeedScale = d.maxSpeedScale;
                car.drivePowerScale = d.drivePowerScale;
                return;
            }

            float mult;
            if (GetManualTransmissionEnabled())
            {
                mult = _manualGear < 0 ? GetManualSpeedMultReverse() : GetManualSpeedMultForward();
            }
            else
            {
                float dir = 0f;
                try
                {
                    if (car.player >= 0 && car.player < sInputManager.players.Length)
                    {
                        dir = sInputManager.players[car.player].driveInput.y;
                    }
                }
                catch
                {
                    dir = 0f;
                }
                mult = dir < 0f ? GetManualSpeedMultReverse() : GetManualSpeedMultForward();
            }

            car.maxSpeedScale = d.maxSpeedScale * mult;
            car.drivePowerScale = d.drivePowerScale;
        }

        private static float GetMaxSpeedForGearKmh(int gear)
        {
            int count = GetManualGearCount();
            gear = Mathf.Clamp(gear, 1, count);

            const float baseKmh = 25f;
            const float topKmh = 125f;
            float growth = Mathf.Pow(topKmh / baseKmh, 1f / Mathf.Max(1, count - 1));
            return baseKmh * Mathf.Pow(growth, gear - 1);
        }

        private static float GetMaxSpeedForCurrentGearKmh()
        {
            int g = _manualGear;
            if (g == 0)
            {
                return 1f;
            }
            float baseKmh = GetMaxSpeedForGearKmh(Mathf.Clamp(Mathf.Abs(g), 1, GetManualGearCount()));
            return Mathf.Max(1f, baseKmh);
        }

        internal static float GetEstimatedRpm()
        {
            const float idle = 900f;
            const float redline = 6500f;

            float t;
            if (!GetManualTransmissionEnabled())
            {
                t = Mathf.Clamp01(_currentSpeedKmh / 140f);
                return Mathf.Lerp(idle, redline, t);
            }

            if (_manualGear == 0)
            {
                return Mathf.Lerp(idle, redline, _neutralRev01);
            }

            t = Mathf.Clamp01(_currentSpeedKmh / GetMaxSpeedForCurrentGearKmh());
            return Mathf.Lerp(idle, redline, t);
        }

        internal static float GetEstimatedRpmNormForSound()
        {
            if (!GetManualTransmissionEnabled())
            {
                return Mathf.Clamp01(_currentSpeedKmh / 140f);
            }

            if (_manualGear == 0)
            {
                _neutralRev01 = Mathf.Lerp(_neutralRev01, Mathf.Clamp01(_lastThrottle01), Time.deltaTime * 8f);
                return Mathf.Clamp(_neutralRev01, 0f, 1.2f);
            }

            float t = _currentSpeedKmh / GetMaxSpeedForCurrentGearKmh();
            return Mathf.Clamp(t, 0f, 1.2f);
        }

        internal static float ComputeManualAccel(float gas)
        {
            if (!GetManualTransmissionEnabled())
            {
                return gas;
            }

            if (_manualGear == 0)
            {
                return 0f;
            }

            float speed = Mathf.Max(0f, _currentSpeedKmh);
            float max = GetMaxSpeedForCurrentGearKmh();

            float t = Mathf.Clamp01(speed / Mathf.Max(1f, max));
            float band = 1f - t;
            float shaped = Mathf.Pow(band, 0.55f);
            float torque = Mathf.Lerp(0.55f, 1.20f, shaped);

            float a = Mathf.Clamp01(gas) * torque;
            return _manualGear < 0 ? -a : a;
        }


        [HarmonyPatch(typeof(sCarController), "Update")]
        [HarmonyPrefix]
        private static void SCarController_Update_Prefix(sCarController __instance)
        {
            if (__instance == null)
            {
                return;
            }

            _currentCar = __instance;
            _isInWalkingMode = __instance.GuyActive;

            if (__instance.rb != null)
            {
                _currentSpeedKmh = __instance.rb.linearVelocity.magnitude * 3.6f;
            }

            ApplyHeadlightTuning(__instance);
            ApplySpeedScaleTuning(__instance);

            if (!GetIgnitionEnabledEffective())
            {
                EnforceIgnitionOffForCurrentCar();
            }
        }


        [HarmonyPatch(typeof(sInputManager), "GetInput")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void SInputManager_GetInput_Postfix(sInputManager __instance)
        {
            if (__instance == null)
            {
                return;
            }

            // Only apply while driving.
            var car = UnityEngine.Object.FindFirstObjectByType<sCarController>();
            if (car == null || car.GuyActive)
            {
                _ignitionHoldStart = -1f;
                _ignitionHoldConsumed = false;
                _ignitionHoldWasDown = false;
                _ignitionIgnoreHoldUntilRelease = false;
                return;
            }

            if (PauseSystem.paused || __instance.lockInput)
            {
                return;
            }

            BindingEvaluator.BeginFrame();

            var schemes = new[] { BindingScheme.Controller, BindingScheme.Keyboard, BindingScheme.Wheel };

            bool Pressed(BindingInput b) => b.Kind != BindingKind.None && BindingEvaluator.WasPressedThisFrame(b);
            bool Released(BindingInput b) => b.Kind != BindingKind.None && BindingEvaluator.WasReleasedThisFrame(b);
            bool Down(BindingInput b) => b.Kind != BindingKind.None && BindingEvaluator.IsDown(b);

            BindingLayer LayerFor(BindingScheme s)
            {
                var mod = BindingStore.GetModifierBinding(s);
                bool modifierDown = mod.Kind != BindingKind.None && BindingEvaluator.IsDown(mod);
                return modifierDown ? BindingLayer.Modified : BindingLayer.Normal;
            }

            BindingInput GetBind(BindingScheme s, BindAction action)
            {
                var layer = LayerFor(s);
                var b = BindingStore.GetBinding(s, layer, action);
                if (b.Kind == BindingKind.None && layer == BindingLayer.Modified)
                {
                    b = BindingStore.GetBinding(s, BindingLayer.Normal, action);
                }
                return b;
            }

            bool PressedAny(BindAction a)
            {
                for (int i = 0; i < schemes.Length; i++)
                {
                    if (Pressed(GetBind(schemes[i], a)))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool ReleasedAny(BindAction a)
            {
                for (int i = 0; i < schemes.Length; i++)
                {
                    if (Released(GetBind(schemes[i], a)))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool DownAny(BindAction a)
            {
                for (int i = 0; i < schemes.Length; i++)
                {
                    if (Down(GetBind(schemes[i], a)))
                    {
                        return true;
                    }
                }
                return false;
            }

            // Transmission + ignition binds.
            {
                // Ignition
                bool ignitionFeature = GetIgnitionFeatureEnabled();
                bool ignitionPressed = PressedAny(BindAction.IgnitionToggle);
                bool ignitionDown = DownAny(BindAction.IgnitionToggle);
                bool ignitionReleased = ReleasedAny(BindAction.IgnitionToggle);

                if (!ignitionFeature)
                {
                    _ignitionHoldStart = -1f;
                    _ignitionHoldConsumed = false;
                    _ignitionHoldWasDown = ignitionDown;
                    _ignitionIgnoreHoldUntilRelease = false;
                    ignitionPressed = false;
                    ignitionDown = false;
                    ignitionReleased = false;
                }

                if (ignitionFeature && GetIgnitionEnabled() && ignitionPressed)
                {
                    StopIgnitionHoldSfx();
                    _ignitionHoldStart = -1f;
                    _ignitionHoldConsumed = false;
                    _ignitionIgnoreHoldUntilRelease = true;

                    SetIgnitionEnabled(false);
                    ApplyIgnitionStateChange(false);
                }

                if (ignitionFeature && !GetIgnitionEnabled() && ignitionDown && !_ignitionIgnoreHoldUntilRelease)
                {
                    if (_ignitionHoldStart < 0f)
                    {
                        _ignitionHoldStart = Time.unscaledTime;
                        _ignitionHoldConsumed = false;

                        StartIgnitionHoldSfx(car);
                    }

                    float holdS = GetIgnitionHoldSeconds();
                    if (!_ignitionHoldConsumed && Time.unscaledTime - _ignitionHoldStart >= holdS)
                    {
                        SetIgnitionEnabled(true);
                        ApplyIgnitionStateChange(true);
                        _ignitionHoldConsumed = true;

                        StopIgnitionHoldSfx();
                    }
                }

                if (ignitionReleased)
                {
                    StopIgnitionHoldSfx();
                    _ignitionIgnoreHoldUntilRelease = false;
                    _ignitionHoldStart = -1f;
                    _ignitionHoldConsumed = false;
                }

                _ignitionHoldWasDown = ignitionDown;


                // Toggle manual transmission
                if (PressedAny(BindAction.ToggleGearbox))
                {
                    ToggleManualTransmission();
                }

                // Shift
                if (PressedAny(BindAction.ShiftUp))
                {
                    if (!GetManualTransmissionEnabled())
                    {
                        SetManualTransmissionEnabled(true);
                    }
                    ShiftManualGear(+1);
                }

                if (PressedAny(BindAction.ShiftDown))
                {
                    if (!GetManualTransmissionEnabled())
                    {
                        SetManualTransmissionEnabled(true);
                    }
                    ShiftManualGear(-1);
                }
            }


            // Manual transmission + ignition enforcement (applies to any input source).
            if (!PauseSystem.paused)
            {
                float throttle = Mathf.Clamp01(__instance.driveInput.y);
                if (throttle < 0f) throttle = 0f;

                float brake = __instance.breakPressed ? 1f : 0f;

                // Track rev input for Neutral.
                _lastThrottle01 = throttle;
                if (GetManualTransmissionEnabled() && GetManualGear() == 0)
                {
                    _neutralRev01 = Mathf.Lerp(_neutralRev01, _lastThrottle01, Time.deltaTime * 8f);
                }
                else
                {
                    _neutralRev01 = Mathf.Lerp(_neutralRev01, 0f, Time.deltaTime * 6f);
                }

                float accel;
                if (GetManualTransmissionEnabled())
                {
                    int gear = GetManualGear();
                    float drive = ComputeManualAccel(throttle);

                    float signedKmh = _currentSpeedKmh;
                    try
                    {
                        if (car.rb != null)
                        {
                            signedKmh = Vector3.Dot(car.rb.linearVelocity, car.transform.forward) * 3.6f;
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                    if (gear > 0)
                    {
                        if (signedKmh > 1.0f)
                        {
                            accel = drive - brake;
                        }
                        else
                        {
                            accel = Mathf.Max(0f, drive - brake);
                        }
                    }
                    else if (gear < 0)
                    {
                        if (signedKmh < -1.0f)
                        {
                            accel = drive + brake;
                        }
                        else
                        {
                            accel = Mathf.Min(0f, drive + brake);
                        }
                    }
                    else
                    {
                        accel = 0f;
                    }
                }
                else
                {
                    accel = __instance.driveInput.y;
                }

                accel = Mathf.Clamp(accel, -1f, 1f);
                if (!GetIgnitionEnabledEffective())
                {
                    accel = 0f;
                }

                var v = __instance.driveInput;
                v.y = accel;
                __instance.driveInput = v;
            }
        }


        [HarmonyPatch(typeof(sHUD), "DoFuelMath")]
        [HarmonyPrefix]
        private static bool SHUD_DoFuelMath_Prefix()
        {
            if (!GetIgnitionEnabledEffective())
            {
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(sHUD), "DoTemperature")]
        [HarmonyPostfix]
        private static void SHUD_DoTemperature_Postfix(sHUD __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (__instance.navigation == null || __instance.navigation.car == null || __instance.navigation.car.GuyActive)
            {
                return;
            }

            if (!GetIgnitionFeatureEnabled() || GetIgnitionEnabledEffective())
            {
                return;
            }

            if (_ignitionOffSince < 0f)
            {
                _ignitionOffSince = Time.unscaledTime;
            }
            if (Time.unscaledTime - _ignitionOffSince < 30.0f)
            {
                return;
            }

            __instance.AddWarning("truck temperature low");
            float extra = __instance.temperatureRate * 0.5f;
            __instance.temperature = Mathf.Clamp(__instance.temperature - Time.deltaTime * extra, 0f, __instance.temperatureLimit);
        }


        [HarmonyPatch]
        private static class Headlights_Toggle_Patch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                return AccessTools
                    .GetDeclaredMethods(typeof(Headlights))
                    .Where(m => m != null && string.Equals(m.Name, "Toggle", StringComparison.Ordinal));
            }

            private static bool Prefix()
            {
                return GetIgnitionEnabledEffective();
            }
        }

        [HarmonyPatch]
        private static class Headlights_Break_Patch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                return AccessTools
                    .GetDeclaredMethods(typeof(Headlights))
                    .Where(m => m != null && string.Equals(m.Name, "Break", StringComparison.Ordinal));
            }

            private static bool Prefix()
            {
                return GetIgnitionEnabledEffective();
            }
        }


        [HarmonyPatch(typeof(sHUD), "RadioDisplay")]
        [HarmonyPostfix]
        private static void SHUD_RadioDisplay_Postfix(object __instance)
        {
            var hud = __instance as sHUD;
            if (hud == null)
            {
                return;
            }

            var car = UnityEngine.Object.FindFirstObjectByType<sCarController>();
            if (car == null || car.GuyActive)
            {
                return;
            }

            var rField = AccessTools.Field(typeof(sHUD), "R");
            var R = rField != null ? rField.GetValue(hud) as MiniRenderer : null;
            if (R == null)
            {
                return;
            }

            bool ignitionFeature = GetIgnitionFeatureEnabled();
            bool ignOn = GetIgnitionEnabledEffective();
            bool starting = ignitionFeature && !GetIgnitionEnabled() && _ignitionHoldStart >= 0f && !_ignitionHoldConsumed;

            bool showSpeed = ignOn && GetHudShowSpeed();
            bool manualEnabled = GetManualTransmissionEnabled();
            bool showTach = ignOn && manualEnabled && GetHudShowTach();
            bool showGear = ignOn && manualEnabled && GetHudShowGear();

            bool showOff = ignitionFeature && !ignOn && !starting;
            if (!starting && !showOff && !showSpeed && !showTach && !showGear)
            {
                return;
            }

            float xLeft = 68f;
            float xRight = R.width - 68f;
            float yLeft = R.height - 64f + 22f;
            float yRight = yLeft;

            var aSpeed = GetHudSpeedAnchor();
            var aTach = GetHudTachAnchor();
            var aGear = GetHudGearAnchor();

            void PutLine(HudReadoutAnchor a, string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }
                if (a == HudReadoutAnchor.BottomRight)
                {
                    R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
                    R.fput(text, xRight, yRight);
                    yRight += 10f;
                }
                else
                {
                    R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;
                    R.put(text, xLeft, yLeft);
                    yLeft += 10f;
                }
            }

            if (starting)
            {
                float holdS = Mathf.Max(0.01f, GetIgnitionHoldSeconds());
                float t = Mathf.Clamp01((Time.unscaledTime - _ignitionHoldStart) / holdS);
                PutLine(aSpeed, "START " + Mathf.RoundToInt(t * 100f) + "%");
            }
            else if (showOff)
            {
                PutLine(aSpeed, "ENGN OFF");
            }

            if (showSpeed)
            {
                float spd = ConvertSpeedForHud(_currentSpeedKmh);
                int spdInt = Mathf.Max(0, Mathf.RoundToInt(spd));
                string unit = GetHudSpeedUnitLabel(GetHudSpeedUnit());
                PutLine(aSpeed, spdInt + unit);
            }
            if (showTach)
            {
                float target = GetEstimatedRpm();
                float rpmNorm = Mathf.Clamp(GetEstimatedRpmNormForSound(), 0f, 1.2f);
                bool lastGear = GetManualGear() > 0 && GetManualGear() == GetManualGearCount();
                string suffix = lastGear ? "" : (rpmNorm >= 1.0f ? "!!" : (rpmNorm >= 0.92f ? "!" : ""));
                PutLine(aTach, Mathf.RoundToInt(target) + "rpm" + suffix);
            }
            if (showGear)
            {
                int g = GetManualGear();
                int gearCount = GetManualGearCount();

                string line = "RN";
                for (int i = 1; i <= gearCount; i++)
                {
                    line += i.ToString();
                }

                if (aGear == HudReadoutAnchor.BottomRight)
                {
                    float lineXLeft = xRight - 8f * line.Length;
                    R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;
                    R.put(line, lineXLeft, yRight);
                    int selIndex = g < 0 ? 0 : (g == 0 ? 1 : Mathf.Clamp(g, 1, gearCount) + 1);
                    yRight += 10f;
                    R.put("^", lineXLeft + 8f * selIndex, yRight);
                    yRight += 10f;
                }
                else
                {
                    R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;
                    R.put(line, xLeft, yLeft);
                    int selIndex = g < 0 ? 0 : (g == 0 ? 1 : Mathf.Clamp(g, 1, gearCount) + 1);
                    yLeft += 10f;
                    R.put("^", xLeft + 8f * selIndex, yLeft);
                    yLeft += 10f;
                }
            }
        }


        private static Type _engineSfxRuntimeType;
        private static FieldInfo _engineCarField;
        private static FieldInfo _engineIdleField;
        private static FieldInfo _engineDriveField;
        private static FieldInfo _engineIntenseField;
        private static FieldInfo _engineDistortionField;
        private static readonly Dictionary<int, float> _enginePitchMulApplied = new Dictionary<int, float>();

        private static void EnsureEngineSfxRefs(object instance)
        {
            if (instance == null)
            {
                return;
            }

            var t = instance.GetType();
            if (_engineSfxRuntimeType == t)
            {
                return;
            }

            _engineSfxRuntimeType = t;
            _engineCarField = AccessTools.Field(t, "car");
            _engineIdleField = AccessTools.Field(t, "idle");
            _engineDriveField = AccessTools.Field(t, "drive");
            _engineIntenseField = AccessTools.Field(t, "intense");
            _engineDistortionField = AccessTools.Field(t, "distortionFilter");
        }

        [HarmonyPatch(typeof(sEngineSFX), "Update")]
        [HarmonyPostfix]
        private static void SEngineSFX_Update_Postfix(object __instance)
        {
            EngineSfxPostfix(__instance);
        }

        private static void EngineSfxPostfix(object __instance)
        {
            if (__instance == null)
            {
                return;
            }

            EnsureEngineSfxRefs(__instance);

            var car = _engineCarField != null ? _engineCarField.GetValue(__instance) as sCarController : null;
            if (car == null || car.GuyActive)
            {
                return;
            }
            if (_currentCar != null && !ReferenceEquals(car, _currentCar))
            {
                return;
            }

            if (!GetIgnitionEnabledEffective())
            {
                var idle0 = _engineIdleField != null ? _engineIdleField.GetValue(__instance) as AudioSource : null;
                var drive0 = _engineDriveField != null ? _engineDriveField.GetValue(__instance) as AudioSource : null;
                var intense0 = _engineIntenseField != null ? _engineIntenseField.GetValue(__instance) as AudioSource : null;
                if (idle0 != null) idle0.volume = 0f;
                if (drive0 != null) drive0.volume = 0f;
                if (intense0 != null) intense0.volume = 0f;

                var dist0 = _engineDistortionField != null ? _engineDistortionField.GetValue(__instance) as AudioDistortionFilter : null;
                if (dist0 != null) dist0.distortionLevel = 0f;
                return;
            }

            if (!GetManualTransmissionEnabled())
            {
                return;
            }

            float rpmNorm = GetEstimatedRpmNormForSound();
            float over = Mathf.Clamp01((rpmNorm - 1f) / 0.2f);
            float neutral = (_manualGear == 0) ? Mathf.Clamp01(_neutralRev01) : 0f;
            float warn = Mathf.Clamp01((rpmNorm - 0.92f) / (1.0f - 0.92f));

            float pitchMul = 1f + neutral * 0.85f + over * 0.35f;

            var idle = _engineIdleField != null ? _engineIdleField.GetValue(__instance) as AudioSource : null;
            var drive = _engineDriveField != null ? _engineDriveField.GetValue(__instance) as AudioSource : null;
            var intense = _engineIntenseField != null ? _engineIntenseField.GetValue(__instance) as AudioSource : null;

            void ApplyPitchMul(AudioSource src)
            {
                if (src == null)
                {
                    return;
                }
                int id = src.GetInstanceID();
                float last = 1f;
                if (_enginePitchMulApplied.TryGetValue(id, out float prev) && prev > 0.0001f)
                {
                    last = prev;
                }
                src.pitch = (src.pitch / last) * pitchMul;
                _enginePitchMulApplied[id] = pitchMul;
            }

            ApplyPitchMul(idle);
            ApplyPitchMul(drive);
            ApplyPitchMul(intense);

            if (neutral > 0.01f)
            {
                if (drive != null)
                {
                    drive.volume = Mathf.Max(drive.volume, neutral * 0.55f);
                }
                if (intense != null)
                {
                    intense.volume = Mathf.Max(intense.volume, neutral * 0.35f);
                }
            }

            if (warn > 0.01f)
            {
                float driveBoost = Mathf.Min(1f, warn * 0.35f + over * 0.45f);
                float intenseBoost = Mathf.Min(1f, warn * 0.22f + over * 0.35f);
                if (drive != null)
                {
                    drive.volume = Mathf.Max(drive.volume, driveBoost);
                }
                if (intense != null)
                {
                    intense.volume = Mathf.Max(intense.volume, intenseBoost);
                }
            }

            if (over > 0.01f)
            {
                float freq = Mathf.Lerp(7f, 14f, over);
                float saw = Mathf.Repeat(Time.time * freq, 1f);
                float ramp = 1f - saw;
                float cut = (saw < 0.12f) ? Mathf.Lerp(0.25f, 1f, saw / 0.12f) : 1f;

                float baseMul = Mathf.Lerp(1.0f, 0.72f, over);
                float stutterMul = Mathf.Lerp(1.0f, ramp, over) * cut;
                float volMul = baseMul * stutterMul;
                float pitchJitter = 1f + (Mathf.Sin(Time.time * 55f) * 0.012f * over);

                if (drive != null)
                {
                    drive.volume *= volMul;
                    drive.pitch *= pitchJitter;
                }
                if (intense != null)
                {
                    intense.volume *= volMul;
                    intense.pitch *= pitchJitter;
                }
            }

            var dist = _engineDistortionField != null ? _engineDistortionField.GetValue(__instance) as AudioDistortionFilter : null;
            if (dist != null)
            {
                float target = dist.distortionLevel;
                if (over > 0f)
                {
                    target = Mathf.Max(target, 0.10f + over * 0.55f);
                }
                if (neutral > 0.1f)
                {
                    target = Mathf.Max(target, 0.05f + neutral * 0.10f);
                }
                dist.distortionLevel = target;
            }
        }
    }
}
