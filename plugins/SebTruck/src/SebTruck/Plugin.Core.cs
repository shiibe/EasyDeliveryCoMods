using UnityEngine;

namespace SebTruck
{
    public partial class Plugin
    {
        internal const string PrefKeyManualTransmissionEnabled = "SebTruck_ManualTransmission";
        internal const string PrefKeyManualGearCount = "SebTruck_ManualGearCount";
        internal const string PrefKeyManualSpeedMultForward = "SebTruck_ManualSpeedMultFwd";
        internal const string PrefKeyManualSpeedMultReverse = "SebTruck_ManualSpeedMultRev";

        internal const string PrefKeyIgnitionEnabled = "SebTruck_IgnitionEnabled";
        internal const string PrefKeyIgnitionFeatureEnabled = "SebTruck_IgnitionFeatureEnabled";
        internal const string PrefKeyIgnitionHoldSeconds = "SebTruck_IgnitionHoldSeconds";
        internal const string PrefKeyIgnitionSfxEnabled = "SebTruck_IgnitionSfxEnabled";

        internal const string PrefKeyIndicatorFeatureEnabled = "SebTruck_IndicatorFeatureEnabled";

        internal const string PrefKeyHudShowSpeed = "SebTruck_HudShowSpeed";
        internal const string PrefKeyHudShowTach = "SebTruck_HudShowTach";
        internal const string PrefKeyHudShowGear = "SebTruck_HudShowGear";
        internal const string PrefKeyHudSpeedUnits = "SebTruck_HudSpeedUnits";
        internal const string PrefKeyHudReadoutAnchor = "SebTruck_HudReadoutAnchor";
        internal const string PrefKeyHudSpeedAnchor = "SebTruck_HudSpeedAnchor";
        internal const string PrefKeyHudTachAnchor = "SebTruck_HudTachAnchor";
        internal const string PrefKeyHudGearAnchor = "SebTruck_HudGearAnchor";

        internal const string PrefKeyHeadlightIntensityMult = "SebTruck_HeadlightIntensityMult";
        internal const string PrefKeyHeadlightRangeMult = "SebTruck_HeadlightRangeMult";

        internal enum SpeedUnit
        {
            Kmh = 0,
            Mph = 1
        }

        internal enum HudReadoutAnchor
        {
            BottomLeft = 0,
            BottomRight = 1
        }

        private static bool _manualTransmissionEnabled;
        private static int _manualGear = 1; // -1=R, 0=N, 1..GetManualGearCount()

        internal static int GetManualGearCount()
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyManualGearCount, 5), 3, 6);
        }

        internal static void SetManualGearCount(int count)
        {
            count = Mathf.Clamp(count, 3, 6);
            PlayerPrefs.SetInt(PrefKeyManualGearCount, count);
            if (_manualGear > count)
            {
                _manualGear = count;
            }
        }

        internal static int NextManualGearCount(int count)
        {
            count++;
            if (count > 6) count = 3;
            return count;
        }

        internal static bool GetManualTransmissionEnabled()
        {
            return PlayerPrefs.GetInt(PrefKeyManualTransmissionEnabled, 0) != 0;
        }

        internal static void SetManualTransmissionEnabled(bool enabled)
        {
            bool before = GetManualTransmissionEnabled();
            _manualTransmissionEnabled = enabled;
            PlayerPrefs.SetInt(PrefKeyManualTransmissionEnabled, enabled ? 1 : 0);
            if (!enabled)
            {
                _manualGear = Mathf.Clamp(_manualGear, 1, GetManualGearCount());
            }
            else
            {
                if (_manualGear == 0)
                {
                    _manualGear = 1;
                }
            }

            if (before != enabled)
            {
                LogDebug("Manual transmission: " + (enabled ? "ON" : "OFF"));
            }
        }

        internal static void ToggleManualTransmission()
        {
            SetManualTransmissionEnabled(!GetManualTransmissionEnabled());
        }

        internal static int GetManualGear()
        {
            return _manualGear;
        }

        internal static string GetManualGearLabel()
        {
            if (_manualGear < 0) return "R";
            if (_manualGear == 0) return "N";
            return _manualGear.ToString();
        }

        internal static void ShiftManualGear(int delta)
        {
            if (!GetManualTransmissionEnabled())
            {
                _manualGear = Mathf.Clamp(_manualGear, 1, GetManualGearCount());
                return;
            }

            int before = _manualGear;

            int count = GetManualGearCount();
            int g = _manualGear;

            if (g == 0)
            {
                // From N: up -> 1, down -> R
                g = delta > 0 ? 1 : -1;
            }
            else if (g < 0)
            {
                // From R: up -> N, down -> stay R
                g = delta > 0 ? 0 : -1;
            }
            else
            {
                g += delta;
                if (g > count) g = count;
                if (g < 1) g = 0;
            }

            _manualGear = g;

            if (before != _manualGear)
            {
                LogDebug("Shift: " + before + " -> " + _manualGear);
            }
        }


        internal static bool GetIgnitionEnabled()
        {
            return PlayerPrefs.GetInt(PrefKeyIgnitionEnabled, 1) != 0;
        }

        internal static void SetIgnitionEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(PrefKeyIgnitionEnabled, enabled ? 1 : 0);
        }

        internal static bool GetIgnitionFeatureEnabled()
        {
            return PlayerPrefs.GetInt(PrefKeyIgnitionFeatureEnabled, 1) != 0;
        }

        internal static void SetIgnitionFeatureEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(PrefKeyIgnitionFeatureEnabled, enabled ? 1 : 0);
        }

        internal static float GetIgnitionHoldSeconds()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(PrefKeyIgnitionHoldSeconds, 1.35f), 0.25f, 5.0f);
        }

        internal static void SetIgnitionHoldSeconds(float seconds)
        {
            PlayerPrefs.SetFloat(PrefKeyIgnitionHoldSeconds, Mathf.Clamp(seconds, 0.25f, 5.0f));
        }

        internal static bool GetIgnitionSfxEnabled()
        {
            return PlayerPrefs.GetInt(PrefKeyIgnitionSfxEnabled, 1) != 0;
        }

        internal static void SetIgnitionSfxEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(PrefKeyIgnitionSfxEnabled, enabled ? 1 : 0);
        }

        internal static bool GetIgnitionEnabledEffective()
        {
            if (!GetIgnitionFeatureEnabled())
            {
                return true;
            }

            // If ignition is not bound at all, treat it as always on (opt-in via binding).
            bool bound = false;
            try
            {
                foreach (SebBinds.BindingScheme scheme in (SebBinds.BindingScheme[])System.Enum.GetValues(typeof(SebBinds.BindingScheme)))
                {
                    var n = SebBinds.BindingStore.GetBinding(scheme, SebBinds.BindingLayer.Normal, SebBinds.BindAction.IgnitionToggle);
                    var m = SebBinds.BindingStore.GetBinding(scheme, SebBinds.BindingLayer.Modified, SebBinds.BindAction.IgnitionToggle);
                    if (n.Kind != SebBinds.BindingKind.None || m.Kind != SebBinds.BindingKind.None)
                    {
                        bound = true;
                        break;
                    }
                }
            }
            catch
            {
                bound = true;
            }

            if (!bound)
            {
                return true;
            }

            return GetIgnitionEnabled();
        }


        internal static bool GetIndicatorFeatureEnabled()
        {
            return PlayerPrefs.GetInt(PrefKeyIndicatorFeatureEnabled, 1) != 0;
        }

        internal static void SetIndicatorFeatureEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(PrefKeyIndicatorFeatureEnabled, enabled ? 1 : 0);
        }


        internal static bool GetHudShowSpeed()
        {
            return PlayerPrefs.GetInt(PrefKeyHudShowSpeed, 1) != 0;
        }

        internal static void SetHudShowSpeed(bool enabled)
        {
            PlayerPrefs.SetInt(PrefKeyHudShowSpeed, enabled ? 1 : 0);
        }

        internal static bool GetHudShowTach()
        {
            return PlayerPrefs.GetInt(PrefKeyHudShowTach, 1) != 0;
        }

        internal static void SetHudShowTach(bool enabled)
        {
            PlayerPrefs.SetInt(PrefKeyHudShowTach, enabled ? 1 : 0);
        }

        internal static bool GetHudShowGear()
        {
            return PlayerPrefs.GetInt(PrefKeyHudShowGear, 1) != 0;
        }

        internal static void SetHudShowGear(bool enabled)
        {
            PlayerPrefs.SetInt(PrefKeyHudShowGear, enabled ? 1 : 0);
        }

        internal static SpeedUnit GetHudSpeedUnit()
        {
            return (SpeedUnit)Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyHudSpeedUnits, 0), 0, 1);
        }

        internal static void SetHudSpeedUnit(SpeedUnit unit)
        {
            PlayerPrefs.SetInt(PrefKeyHudSpeedUnits, (int)unit);
        }

        internal static SpeedUnit NextHudSpeedUnit(SpeedUnit unit)
        {
            return unit == SpeedUnit.Kmh ? SpeedUnit.Mph : SpeedUnit.Kmh;
        }

        internal static string GetHudSpeedUnitLabel(SpeedUnit unit)
        {
            return unit == SpeedUnit.Mph ? "mph" : "kmh";
        }

        internal static float ConvertSpeedForHud(float kmh)
        {
            return GetHudSpeedUnit() == SpeedUnit.Mph ? (kmh * 0.621371f) : kmh;
        }

        internal static HudReadoutAnchor GetHudReadoutAnchor()
        {
            return (HudReadoutAnchor)Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyHudReadoutAnchor, 0), 0, 1);
        }

        internal static void SetHudReadoutAnchor(HudReadoutAnchor anchor)
        {
            PlayerPrefs.SetInt(PrefKeyHudReadoutAnchor, (int)anchor);
        }

        internal static HudReadoutAnchor NextHudReadoutAnchor(HudReadoutAnchor anchor)
        {
            return anchor == HudReadoutAnchor.BottomLeft ? HudReadoutAnchor.BottomRight : HudReadoutAnchor.BottomLeft;
        }

        internal static string GetHudReadoutAnchorLabel(HudReadoutAnchor anchor)
        {
            return anchor == HudReadoutAnchor.BottomRight ? "Bottom Right" : "Bottom Left";
        }

        internal static HudReadoutAnchor GetHudSpeedAnchor()
        {
            if (!PlayerPrefs.HasKey(PrefKeyHudSpeedAnchor))
            {
                return GetHudReadoutAnchor();
            }
            return (HudReadoutAnchor)Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyHudSpeedAnchor, (int)GetHudReadoutAnchor()), 0, 1);
        }

        internal static void SetHudSpeedAnchor(HudReadoutAnchor anchor)
        {
            PlayerPrefs.SetInt(PrefKeyHudSpeedAnchor, (int)anchor);
        }

        internal static HudReadoutAnchor GetHudTachAnchor()
        {
            if (!PlayerPrefs.HasKey(PrefKeyHudTachAnchor))
            {
                return GetHudReadoutAnchor();
            }
            return (HudReadoutAnchor)Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyHudTachAnchor, (int)GetHudReadoutAnchor()), 0, 1);
        }

        internal static void SetHudTachAnchor(HudReadoutAnchor anchor)
        {
            PlayerPrefs.SetInt(PrefKeyHudTachAnchor, (int)anchor);
        }

        internal static HudReadoutAnchor GetHudGearAnchor()
        {
            if (!PlayerPrefs.HasKey(PrefKeyHudGearAnchor))
            {
                return GetHudReadoutAnchor();
            }
            return (HudReadoutAnchor)Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyHudGearAnchor, (int)GetHudReadoutAnchor()), 0, 1);
        }

        internal static void SetHudGearAnchor(HudReadoutAnchor anchor)
        {
            PlayerPrefs.SetInt(PrefKeyHudGearAnchor, (int)anchor);
        }


        internal static float GetManualSpeedMultForward()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(PrefKeyManualSpeedMultForward, 1f), 0.25f, 2.0f);
        }

        internal static void SetManualSpeedMultForward(float mult)
        {
            PlayerPrefs.SetFloat(PrefKeyManualSpeedMultForward, Mathf.Clamp(mult, 0.25f, 2.0f));
        }

        internal static float GetManualSpeedMultReverse()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(PrefKeyManualSpeedMultReverse, 1f), 0.25f, 2.0f);
        }

        internal static void SetManualSpeedMultReverse(float mult)
        {
            PlayerPrefs.SetFloat(PrefKeyManualSpeedMultReverse, Mathf.Clamp(mult, 0.25f, 2.0f));
        }

        internal static float GetHeadlightIntensityMult()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(PrefKeyHeadlightIntensityMult, 1f), 0.1f, 3.0f);
        }

        internal static void SetHeadlightIntensityMult(float mult)
        {
            PlayerPrefs.SetFloat(PrefKeyHeadlightIntensityMult, Mathf.Clamp(mult, 0.1f, 3.0f));
        }

        internal static float GetHeadlightRangeMult()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(PrefKeyHeadlightRangeMult, 1f), 0.1f, 3.0f);
        }

        internal static void SetHeadlightRangeMult(float mult)
        {
            PlayerPrefs.SetFloat(PrefKeyHeadlightRangeMult, Mathf.Clamp(mult, 0.1f, 3.0f));
        }


        internal static void ResetVehicleDefaults()
        {
            SetManualTransmissionEnabled(false);
            SetManualGearCount(5);
            SetIgnitionFeatureEnabled(true);
            SetIgnitionEnabled(true);
            SetIgnitionHoldSeconds(1.35f);
            SetIgnitionSfxEnabled(true);
            SetIndicatorFeatureEnabled(true);
            SetManualSpeedMultForward(1.0f);
            SetManualSpeedMultReverse(1.0f);
            SetHeadlightIntensityMult(1.0f);
            SetHeadlightRangeMult(1.0f);
            PlayerPrefs.Save();
        }

        internal static void ResetTransmissionDefaults()
        {
            SetManualTransmissionEnabled(false);
            SetManualGearCount(5);
            PlayerPrefs.Save();
        }

        internal static void ResetIgnitionDefaults()
        {
            SetIgnitionFeatureEnabled(true);
            SetIgnitionEnabled(true);
            SetIgnitionHoldSeconds(1.35f);
            SetIgnitionSfxEnabled(true);
            PlayerPrefs.Save();
        }

        internal static void ResetIndicatorDefaults()
        {
            SetIndicatorFeatureEnabled(true);
            PlayerPrefs.Save();
        }

        internal static void ResetTweaksDefaults()
        {
            SetManualSpeedMultForward(1.0f);
            SetManualSpeedMultReverse(1.0f);
            SetHeadlightIntensityMult(1.0f);
            SetHeadlightRangeMult(1.0f);
            PlayerPrefs.Save();
        }

        internal static void ResetHudDefaults()
        {
            SetHudSpeedUnit(SpeedUnit.Kmh);
            SetHudShowSpeed(true);
            SetHudShowTach(true);
            SetHudShowGear(true);
            SetHudReadoutAnchor(HudReadoutAnchor.BottomLeft);
            PlayerPrefs.DeleteKey(PrefKeyHudSpeedAnchor);
            PlayerPrefs.DeleteKey(PrefKeyHudTachAnchor);
            PlayerPrefs.DeleteKey(PrefKeyHudGearAnchor);
            PlayerPrefs.Save();
        }
    }
}
