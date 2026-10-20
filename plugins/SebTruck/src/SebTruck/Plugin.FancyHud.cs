using System;
using HarmonyLib;
using UnityEngine;

namespace SebTruck
{
    public partial class Plugin
    {
        internal const string PrefKeyRallyHudStyle = "SebTruck_RallyHudStyle";
        internal const string PrefKeyHudOdometer = "SebTruck_HudOdometer";
        // In the game's save file, so mileage is per save slot.
        private const string SaveKeyOdometerKm = "SebTruck_OdometerKm";
        private const string SaveKeyTripKm = "SebTruck_TripKm";

        private const float SpeedoRadius = 22f;
        private const float TachRadius = 18f;
        private const float DialGap = 6f;

        internal enum HudOdometerMode
        {
            Off = 0,
            Total = 1,
            Trip = 2
        }

        private const int RadioLift = 34;
        private static MiniRenderer _radioLiftedRenderer;

        private static double _odometerPendingKm;
        private static float _odometerNextFlush;
        private static int _odometerFrame = -1;
        private static sEasyRally _easyRally;

        internal static HudStyle GetRallyHudStyle() => PlayerPrefs.GetInt(PrefKeyRallyHudStyle, (int)HudStyle.Rally) == (int)HudStyle.Fancy ? HudStyle.Fancy : HudStyle.Rally;
        internal static void SetRallyHudStyle(HudStyle style) => PlayerPrefs.SetInt(PrefKeyRallyHudStyle, (int)style);

        internal static HudOdometerMode GetHudOdometerMode() => (HudOdometerMode)Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyHudOdometer, (int)HudOdometerMode.Total), 0, 2);
        internal static void SetHudOdometerMode(HudOdometerMode mode) => PlayerPrefs.SetInt(PrefKeyHudOdometer, (int)mode);

        // Speed-based, so teleports add nothing.
        private static void AccumulateOdometer(float speedKmh)
        {
            if (_odometerFrame == Time.frameCount || PauseSystem.paused || IsRallyModeActive())
            {
                return;
            }

            _odometerFrame = Time.frameCount;
            _odometerPendingKm += FancyGauge.DistanceKm(speedKmh, Time.deltaTime);
            if (Time.unscaledTime >= _odometerNextFlush)
            {
                _odometerNextFlush = Time.unscaledTime + 5f;
                FlushOdometer();
            }
        }

        // Memory only; the game writes the file when it saves.
        private static void FlushOdometer()
        {
            if (_odometerPendingKm <= 0.0 || sSaveSystem.instance == null)
            {
                return;
            }

            sSaveSystem.SetFloat(SaveKeyOdometerKm, (float)(sSaveSystem.GetFloat(SaveKeyOdometerKm, 0f) + _odometerPendingKm));
            sSaveSystem.SetFloat(SaveKeyTripKm, (float)(sSaveSystem.GetFloat(SaveKeyTripKm, 0f) + _odometerPendingKm));
            _odometerPendingKm = 0.0;
        }

        internal static double GetOdometerKm(bool trip)
        {
            if (sSaveSystem.instance == null)
            {
                return _odometerPendingKm;
            }
            return sSaveSystem.GetFloat(trip ? SaveKeyTripKm : SaveKeyOdometerKm, 0f) + _odometerPendingKm;
        }

        internal static void ResetTrip()
        {
            FlushOdometer();
            if (sSaveSystem.instance != null)
            {
                sSaveSystem.SetFloat(SaveKeyTripKm, 0f);
            }
        }

        internal static string FormatStoryOdometer(bool trip)
        {
            bool miles = GetHudSpeedUnit() == SpeedUnit.Mph;
            return FancyGauge.FormatOdometer(GetOdometerKm(trip), miles, trip) + (miles ? "mi" : "km");
        }

        // Negative when Easy Rally isn't running.
        private static double GetRallyOdometerKm()
        {
            if (_easyRally == null)
            {
                _easyRally = UnityEngine.Object.FindFirstObjectByType<sEasyRally>();
            }
            return _easyRally != null ? _easyRally.distanceTraveled / 1000.0 : -1.0;
        }

        private static void DrawStoryFancyHud(MiniRenderer R, bool showSpeed, bool showTach, bool showGear, bool engineOn, string status)
        {
            float width = (showSpeed ? SpeedoRadius * 2f : 0f) + (showTach || showGear ? DialGap + TachRadius * 2f : 0f);
            float x = R.width - 72f - width;
            float y = R.height - 70f;

            var mode = GetHudOdometerMode();
            bool trip = mode == HudOdometerMode.Trip;
            double odometerKm = mode == HudOdometerMode.Off || !showSpeed ? -1.0 : GetOdometerKm(trip);
            float speed = engineOn ? ConvertSpeedForHud(_currentSpeedKmh) : 0f;
            float rpmNorm = engineOn ? GetEstimatedRpmNormForHud() : 0f;
            DrawFancyHudParts(R, x, y, showSpeed, showTach, showGear, GetManualGear(), rpmNorm, engineOn && IsAtStoryRevLimiter(), speed, GetHudSpeedUnit(), status, odometerKm, trip);
        }

        // (x, y) is the cluster's top-left. odometerKm < 0 hides the odometer.
        private static void DrawFancyHudParts(MiniRenderer R, float x, float y, bool showSpeed, bool showTach, bool showGear, int gear, float rpmNorm, bool atLimiter, float speed, SpeedUnit units, string status, double odometerKm, bool trip)
        {
            float cy = y + SpeedoRadius;
            float cx = x;
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;

            if (showSpeed)
            {
                cx = x + SpeedoRadius;
                float maxSpeed = units == SpeedUnit.Mph ? 100f : 160f;
                int ticks = units == SpeedUnit.Mph ? 10 : 8;
                DrawDial(R, cx, cy, SpeedoRadius, ticks, 2f, speed / maxSpeed, jitter: false);
                PutCentered(R, Mathf.RoundToInt(Mathf.Max(0f, speed)).ToString(), cx, cy + 6f);
                cx += SpeedoRadius + DialGap;
            }

            if (showTach || showGear)
            {
                float tcx = cx + TachRadius;
                if (showTach)
                {
                    DrawDial(R, tcx, cy, TachRadius, 8, 0.85f, rpmNorm, jitter: atLimiter);
                }
                if (showGear)
                {
                    PutCentered(R, gear < 0 ? "R" : gear == 0 ? "N" : gear.ToString(), tcx, cy + 5f);
                }
            }

            float lineY = y + SpeedoRadius * 2f + 3f;
            if (!string.IsNullOrEmpty(status))
            {
                R.put(status, x, lineY);
            }
            else if (odometerKm >= 0.0)
            {
                DrawRollingOdometer(R, x, lineY, odometerKm, units == SpeedUnit.Mph, trip);
            }
        }

        private static readonly int[] OdometerDigits = new int[6];
        private static readonly double[] OdometerRolls = new double[6];

        private static void DrawRollingOdometer(MiniRenderer R, float x, float y, double km, bool miles, bool trip)
        {
            double distance = miles ? km * FancyGauge.MilesPerKm : km;
            int wheels = trip ? 4 : 6;
            FancyGauge.OdometerWheels(trip ? distance * 10.0 : distance, OdometerDigits, OdometerRolls);
            for (int k = wheels - 1; k >= 0; k--)
            {
                if (trip && k == 0)
                {
                    R.put(".", x, y);
                    x += 8f;
                }
                DrawOdometerWheel(R, x, y, OdometerDigits[k], OdometerRolls[k]);
                x += 8f;
            }
            R.put(miles ? "mi" : "km", x, y);
        }

        // Font cells are 8x8, starting at ' '.
        private static void DrawOdometerWheel(MiniRenderer R, float x, float y, int digit, double roll)
        {
            int offset = Mathf.Clamp(Mathf.RoundToInt((float)roll * 8f), 0, 8);
            float sx = ('0' + digit - 32) * 8f;
            float nextSx = ('0' + (digit + 1) % 10 - 32) * 8f;
            if (offset < 8)
            {
                R.spr(R.fontSheet, sx, offset, x, y, 8f, 8f - offset, flip: false, 8f, 8f - offset);
            }
            if (offset > 0)
            {
                R.spr(R.fontSheet, nextSx, 0f, x, y + 8f - offset, 8f, offset, flip: false, 8f, offset);
            }
        }

        // The pixel matrix is set before the HUD draws, so lowering R.height here moves only the radio.
        [HarmonyPatch(typeof(sHUD), "RadioDisplay")]
        [HarmonyPrefix]
        private static void SHUD_RadioDisplay_Prefix(MiniRenderer ___R)
        {
            if (___R == null || _radioLiftedRenderer != null || IsRallyModeActive() || GetHudStyle() != HudStyle.Fancy)
            {
                return;
            }

            ___R.height -= RadioLift;
            _radioLiftedRenderer = ___R;
        }

        // Runs even if RadioDisplay throws.
        [HarmonyPatch(typeof(sHUD), "RadioDisplay")]
        [HarmonyFinalizer]
        private static Exception SHUD_RadioDisplay_Finalizer(Exception __exception)
        {
            RestoreRadioLift();
            return __exception;
        }

        private static void RestoreRadioLift()
        {
            if (_radioLiftedRenderer != null)
            {
                _radioLiftedRenderer.height += RadioLift;
                _radioLiftedRenderer = null;
            }
        }

        // The game's own 1px HUD dot, so colors match the HUD.
        private static void DrawDial(MiniRenderer R, float cx, float cy, float radius, int majorTicks, float redFrom01, float value01, bool jitter)
        {
            int ringDots = Mathf.CeilToInt(radius * 4.71f);
            for (int i = 0; i <= ringDots; i++)
            {
                DialDot(R, cx, cy, radius, i / (float)ringDots);
            }

            for (int t = 0; t <= majorTicks; t++)
            {
                float v = t / (float)majorTicks;
                // Longer ticks stand in for a red band.
                float length = v >= redFrom01 ? 5f : 3f;
                for (float r = radius - length; r < radius; r += 1f)
                {
                    DialDot(R, cx, cy, r, v);
                }
            }

            // Bounces off the rev limiter.
            float needle = Mathf.Clamp01(value01) - (jitter ? UnityEngine.Random.value * 0.05f : 0f);
            for (float r = 2f; r <= radius - 3f; r += 1f)
            {
                DialDot(R, cx, cy, r, needle);
            }
            R.spr(48f, 32f, Mathf.Round(cx) - 1f, Mathf.Round(cy) - 1f, 2f, 2f);
        }

        private static void DialDot(MiniRenderer R, float cx, float cy, float r, float value01)
        {
            double a = FancyGauge.DialAngle(value01);
            R.spr(49f, 33f, Mathf.Round(cx + (float)System.Math.Cos(a) * r), Mathf.Round(cy - (float)System.Math.Sin(a) * r), 1f, 1f);
        }

        private static void PutCentered(MiniRenderer R, string text, float cx, float y)
        {
            R.put(text, Mathf.Round(cx - text.Length * 4f), y);
        }
    }
}
