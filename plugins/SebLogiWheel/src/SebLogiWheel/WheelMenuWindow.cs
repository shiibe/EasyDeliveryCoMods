using System;
using System.Text;
using SebCore;
using UnityEngine;

namespace SebLogiWheel
{
    public class WheelMenuWindow : CartridgeWindowBase
    {
        public const string FileName = "wheel";
        public const string ListenerName = "G920Menu";
        public const string ListenerData = "listener_G920Menu";

        private Page _page;
        private CalStep _calStep;

        private enum CalStep
        {
            None = 0,
            SteeringCenter = 1,
            SteeringLeft = 2,
            SteeringRight = 3,
            ThrottleReleased = 4,
            ThrottlePressed = 5,
            BrakeReleased = 6,
            BrakePressed = 7,
            ClutchReleased = 8,
            ClutchPressed = 9
        }

        private enum Page
        {
            Main = 0,
            Ffb = 1,
            Steering = 2,
            Calibration = 4,
            CalibrationWizard = 5,
            Bindings = 6
        }

        public override void FrameUpdate(DesktopDotExe.WindowView view)
        {
            // Allow SebBinds to deep-link into the calibration wizard for pedals.
            if (Plugin.ConsumeOpenCalibrationWizardRequest())
            {
                _page = Page.CalibrationWizard;
            }

            // Allow SebBinds to deep-link into the axis binding page.
            if (Plugin.ConsumeOpenAxisMappingRequest())
            {
                _page = Page.Bindings;
            }

            base.FrameUpdate(view);
        }

        public override void BackButtonPressed()
        {
            if (_page == Page.CalibrationWizard)
            {
                _calStep = CalStep.None;
                _page = Page.Calibration;
                return;
            }

            if (_page != Page.Main)
            {
                _calStep = CalStep.None;
                _page = Page.Main;
                return;
            }

            // From the main page, return to SebCore.
            ReturnToSebCoreAndClose();
        }

        protected override void DrawWindow(Rect p)
        {
            float center = p.x + p.width / 2f - 16f;
            float y = p.y + 10f;
            float line = 12f;
            float sectionGap = 4f;

            Plugin.SetWheelMenuActive(true);
            Plugin.SetFfbPageActive(_page == Page.Ffb);
            Plugin.SetCalibrationWizardActive(_page == Page.CalibrationWizard);

            if (_page == Page.Main)
            {
                Util.Label("Wheel Settings", p.x + p.width / 2f, y);
                y += line + sectionGap;
            }

            if (_page == Page.Main)
            {
                DrawMain(p, center, ref y, line, sectionGap);
            }
            else if (_page == Page.Ffb)
            {
                DrawFfb(p, center, ref y, line, sectionGap);
            }
            else if (_page == Page.Steering)
            {
                DrawSteering(p, center, ref y, line, sectionGap);
            }
            else if (_page == Page.Calibration)
            {
                DrawCalibration(p, center, ref y, line, sectionGap);
            }
            else if (_page == Page.CalibrationWizard)
            {
                DrawCalibrationWizard(p, center, ref y, line, sectionGap);
            }
            else
            {
                DrawBindings(p, center, ref y, line, sectionGap);
            }
        }

        private void DrawMain(Rect p, float center, ref float y, float line, float sectionGap)
        {
            float cx = p.x + p.width / 2f;

            // Main navigation buttons
            float btnGap = 22f;
            if (Util.FancyButton("Button Binds", cx, y))
            {
                SebCore.CartridgeApps.TryOpen(Util.M, Util.R, SebCore.CartridgeApps.Binds);
            }

            y += btnGap;
            if (Util.FancyButton("Axis Mapping", cx, y))
            {
                _page = Page.Bindings;
            }

            y += btnGap;
            if (Util.FancyButton("Force Feedback", cx, y))
            {
                _page = Page.Ffb;
            }

            y += btnGap;
            if (Util.FancyButton("Steering", cx, y))
            {
                _page = Page.Steering;
            }

            y += btnGap;
            if (Util.FancyButton("Calibration", cx, y))
            {
                _page = Page.Calibration;
            }

            // Bottom buttons
            float statusY = p.y + p.height - 66f;
            float retryY = p.y + p.height - 54f;
            float backY = p.y + p.height - 18f;
            Util.Label("Logitech SDK: " + Plugin.GetLogitechStatus(), cx, statusY);
            if (Util.SimpleButton("Retry SDK", cx, retryY))
            {
                Plugin.ForceReinitLogitech(true);
            }

            if (Util.SimpleButton("Back", cx, backY))
            {
                BackButtonPressed();
            }
        }

        private void DrawFfb(Rect p, float center, ref float y, float line, float sectionGap)
        {
            Util.Label("Force Feedback", p.x + p.width / 2f, y);
            y += line;

            float cx = p.x + p.width / 2f;
            float resetY = p.y + p.height - 30f;
            float backY = p.y + p.height - 18f;
            if (Util.SimpleButton("Reset Defaults", cx, resetY))
            {
                Plugin.ResetFfbDefaults();
            }
            if (Util.SimpleButton("Back", cx, backY))
            {
                _page = Page.Main;
                return;
            }

            y += sectionGap;

            bool ffbEnabled = Plugin.GetFfbEnabled();
            bool? newFfbEnabled = Util.Toggle("Enable FFB", ffbEnabled, center, y);
            if (newFfbEnabled.HasValue)
            {
                Plugin.SetFfbEnabled(newFfbEnabled.Value);
            }
            y += line;

            float overall = Plugin.GetFfbOverallGain();
            Util.ValueLabel($"{Mathf.RoundToInt(overall * 100f)}%", p.x + p.width - 12f, y);
            float? newOverall = Util.Slider("Strength", overall, center, y, ref MouseYLock);
            if (newOverall.HasValue)
            {
                Plugin.SetFfbOverallGain(newOverall.Value);
            }
            y += line;

            float spring = Plugin.GetFfbSpringGain();
            Util.ValueLabel($"{Mathf.RoundToInt(spring * 100f)}%", p.x + p.width - 12f, y);
            float? newSpring = Util.Slider("Spring", spring, center, y, ref MouseYLock);
            if (newSpring.HasValue)
            {
                Plugin.SetFfbSpringGain(newSpring.Value);
            }
            y += line;

            float damper = Plugin.GetFfbDamperGain();
            Util.ValueLabel($"{Mathf.RoundToInt(damper * 100f)}%", p.x + p.width - 12f, y);
            float? newDamper = Util.Slider("Damper", damper, center, y, ref MouseYLock);
            if (newDamper.HasValue)
            {
                Plugin.SetFfbDamperGain(newDamper.Value);
            }
        }

        private void DrawSteering(Rect p, float center, ref float y, float line, float sectionGap)
        {
            Util.Label("Steering", p.x + p.width / 2f, y);
            y += line;

            float cx = p.x + p.width / 2f;
            float resetY = p.y + p.height - 30f;
            float backY = p.y + p.height - 18f;
            if (Util.SimpleButton("Reset Defaults", cx, resetY))
            {
                Plugin.ResetSteeringDefaults();
            }
            if (Util.SimpleButton("Back", cx, backY))
            {
                _page = Page.Main;
                return;
            }

            y += sectionGap;

            int range = Plugin.GetWheelRange();
            Util.ValueLabel($"{range} deg", p.x + p.width - 12f, y);
            float rangeValue = Mathf.InverseLerp(180f, 900f, range);
            float? newRangeValue = Util.Slider("Range", rangeValue, center, y, ref MouseYLock);
            if (newRangeValue.HasValue)
            {
                int newRange = Mathf.RoundToInt(Mathf.Lerp(180f, 900f, newRangeValue.Value));
                newRange = Mathf.Clamp(newRange, 180, 900);
                Plugin.SetWheelRange(newRange);
            }
            y += line;

            float steerGain = Plugin.GetSteeringGain();
            Util.ValueLabel($"{steerGain:0.00}x", p.x + p.width - 12f, y);
            float steerGainNorm = Mathf.InverseLerp(0.5f, 3.0f, steerGain);
            float? newSteerGainNorm = Util.Slider("Steer Sens", steerGainNorm, center, y, ref MouseYLock);
            if (newSteerGainNorm.HasValue)
            {
                Plugin.SetSteeringGain(Mathf.Lerp(0.5f, 3.0f, newSteerGainNorm.Value));
            }
            y += line;

            float dz = Plugin.GetSteeringDeadzone();
            Util.ValueLabel($"{Mathf.RoundToInt(dz * 100f)}%", p.x + p.width - 12f, y);
            float dzNorm = Mathf.InverseLerp(0f, 0.12f, dz);
            float? newDzNorm = Util.Slider("Deadzone", dzNorm, center, y, ref MouseYLock);
            if (newDzNorm.HasValue)
            {
                Plugin.SetSteeringDeadzone(Mathf.Lerp(0f, 0.12f, newDzNorm.Value));
            }
        }

        private void DrawBindings(Rect p, float center, ref float y, float line, float sectionGap)
        {
            Util.Label("Axis Mapping", p.x + p.width / 2f, y);
            y += line;

            float cx = p.x + p.width / 2f;
            float navY = p.y + p.height - 18f;

            if (Util.SimpleButton("Back", cx, navY))
            {
                _page = Page.Main;
                return;
            }

            y += sectionGap;

            DrawBindingsAxes(p, center, ref y, line, sectionGap);
        }

        private void DrawBindingsAxes(Rect p, float center, ref float y, float line, float sectionGap)
        {
            if (!Plugin.TryGetCachedWheelState(out var state))
            {
                string status = Plugin.GetLogitechStatus();
                if (status == "No wheel")
                {
                    Util.Label("No wheel detected.", p.x + p.width / 2f, y);
                }
                else
                {
                    Util.Label("Logitech SDK: " + status, p.x + p.width / 2f, y);
                }
                return;
            }

            Plugin.AxisId steerAxis = Plugin.GetSteeringAxis();
            Plugin.AxisId throttleAxis = Plugin.GetThrottleAxis();
            Plugin.AxisId brakeAxis = Plugin.GetBrakeAxis();
            Plugin.AxisId clutchAxis = Plugin.GetClutchAxis();

            if (Util.CycleButtonRaw("Steering", steerAxis.ToString(), center, y))
            {
                Plugin.SetSteeringAxis(NextAxis(steerAxis));
                _calStep = CalStep.None;
            }

            int rawSteer0 = Plugin.GetAxisValue(state, steerAxis);
            float steerNorm0 = Plugin.NormalizeSteering(rawSteer0);
            Util.ValueLabel($"{steerNorm0:+0.00;-0.00;0.00}", p.x + p.width - 12f, y);
            y += line;
            if (Util.CycleButtonRaw("Throttle", throttleAxis.ToString(), center, y))
            {
                Plugin.SetThrottleAxis(NextAxis(throttleAxis));
                _calStep = CalStep.None;
            }

            int rawThr0 = Plugin.GetAxisValue(state, throttleAxis);
            float thrNorm0 = Plugin.NormalizePedal(rawThr0, Plugin.PedalKind.Throttle);
            Util.ValueLabel($"{thrNorm0:0.00}", p.x + p.width - 12f, y);
            y += line;
            if (Util.CycleButtonRaw("Brake", brakeAxis.ToString(), center, y))
            {
                Plugin.SetBrakeAxis(NextAxis(brakeAxis));
                _calStep = CalStep.None;
            }

            int rawBrk0 = Plugin.GetAxisValue(state, brakeAxis);
            float brkNorm0 = Plugin.NormalizePedal(rawBrk0, Plugin.PedalKind.Brake);
            Util.ValueLabel($"{brkNorm0:0.00}", p.x + p.width - 12f, y);

            y += line;
            if (Util.CycleButtonRaw("Clutch", clutchAxis.ToString(), center, y))
            {
                Plugin.SetClutchAxis(NextAxis(clutchAxis));
                _calStep = CalStep.None;
            }

            int rawClu0 = Plugin.GetAxisValue(state, clutchAxis);
            float cluNorm0 = Plugin.NormalizePedal(rawClu0, Plugin.PedalKind.Clutch);
            Util.ValueLabel($"{cluNorm0:0.00}", p.x + p.width - 12f, y);

            y += line + sectionGap;

            Util.Label("Live Axes", p.x + p.width / 2f, y);
            y += line;

            string axes1 = $"lX={state.lX} lY={state.lY} lZ={state.lZ}";
            string axes2 = $"lRx={state.lRx} lRy={state.lRy} lRz={state.lRz}";
            int s0 = state.rglSlider != null && state.rglSlider.Length > 0 ? state.rglSlider[0] : 0;
            int s1 = state.rglSlider != null && state.rglSlider.Length > 1 ? state.rglSlider[1] : 0;
            string axes3 = $"slider0={s0} slider1={s1}";
            Util.Label(axes1, p.x + p.width / 2f, y);
            y += line - 2f;
            Util.Label(axes2, p.x + p.width / 2f, y);
            y += line - 2f;
            Util.Label(axes3, p.x + p.width / 2f, y);
            y += line - 2f;

            Util.Label(GetHeldButtonsLabel(state), p.x + p.width / 2f, y);
            y += line;

            Util.Label($"steer={steerNorm0:+0.00;-0.00;0.00} thr={thrNorm0:0.00}", p.x + p.width / 2f, y);
            y += line - 2f;
            Util.Label($"brk={brkNorm0:0.00} clu={cluNorm0:0.00}", p.x + p.width / 2f, y);
        }

        private void DrawCalibration(Rect p, float center, ref float y, float line, float sectionGap)
        {
            Util.Label("Calibration", p.x + p.width / 2f, y);
            y += line;

            float cx = p.x + p.width / 2f;
            float backY = p.y + p.height - 18f;
            float clearY = p.y + p.height - 30f;
            float wizardY = p.y + p.height - 72f;

            if (Util.FancyButton("Calibrate", cx, wizardY))
            {
                _calStep = CalStep.SteeringCenter;
                _page = Page.CalibrationWizard;
            }
            if (Util.SimpleButton("Clear Calibration", cx, clearY))
            {
                Plugin.ClearCalibration();
                _calStep = CalStep.None;
            }
            if (Util.SimpleButton("Back", cx, backY))
            {
                _calStep = CalStep.None;
                _page = Page.Main;
                return;
            }
            y += line + sectionGap;

            if (!Plugin.TryGetLogiState(out var state))
            {
                string status = Plugin.GetLogitechStatus();
                if (status == "No wheel")
                {
                    Util.Label("No wheel detected.", p.x + p.width / 2f, y);
                }
                else
                {
                    Util.Label("Logitech SDK: " + status, p.x + p.width / 2f, y);
                }
                return;
            }

            Util.Label("Live Axes", p.x + p.width / 2f, y);
            y += line;

            // show a few common axes so it's easy to find where the throttle lives
            string axes1 = $"lX={state.lX} lY={state.lY} lZ={state.lZ}";
            string axes2 = $"lRx={state.lRx} lRy={state.lRy} lRz={state.lRz}";
            int s0 = state.rglSlider != null && state.rglSlider.Length > 0 ? state.rglSlider[0] : 0;
            int s1 = state.rglSlider != null && state.rglSlider.Length > 1 ? state.rglSlider[1] : 0;
            string axes3 = $"slider0={s0} slider1={s1}";
            Util.Label(axes1, p.x + p.width / 2f, y);
            y += line - 2f;
            Util.Label(axes2, p.x + p.width / 2f, y);
            y += line - 2f;
            Util.Label(axes3, p.x + p.width / 2f, y);
            y += line - 2f;
            Util.Label(GetHeldButtonsLabel(state), p.x + p.width / 2f, y);
            y += line + sectionGap;

            // axis selection

            // quick status helpers for pedals
            int rawThr = Plugin.GetAxisValue(state, Plugin.GetThrottleAxis());
            int rawBrk = Plugin.GetAxisValue(state, Plugin.GetBrakeAxis());
            float thr = Plugin.NormalizePedal(rawThr, Plugin.PedalKind.Throttle);
            float brk = Plugin.NormalizePedal(rawBrk, Plugin.PedalKind.Brake);
            int rawClu = Plugin.GetAxisValue(state, Plugin.GetClutchAxis());
            float clu = Plugin.NormalizePedal(rawClu, Plugin.PedalKind.Clutch);
            Util.Label($"Current throttle: {thr:0.00}", p.x + p.width / 2f, y);
            y += line - 2f;
            Util.Label($"Current brake: {brk:0.00}", p.x + p.width / 2f, y);
            y += line - 2f;
            Util.Label($"Current clutch: {clu:0.00}", p.x + p.width / 2f, y);
            y += line;
        }

        private void DrawCalibrationWizard(Rect p, float center, ref float y, float line, float sectionGap)
        {
            Util.Label("Calibration Wizard", p.x + p.width / 2f, y);
            y += line;

            float cx = p.x + p.width / 2f;
            float cancelY = p.y + p.height - 18f;

            if (Util.SimpleButton("Cancel", cx, cancelY))
            {
                _calStep = CalStep.None;
                _page = Page.Calibration;
                return;
            }

            y += sectionGap;

            if (!Plugin.TryGetLogiState(out var state))
            {
                string status = Plugin.GetLogitechStatus();
                if (status == "No wheel")
                {
                    Util.Label("No wheel detected.", p.x + p.width / 2f, y);
                }
                else
                {
                    Util.Label("Logitech SDK: " + status, p.x + p.width / 2f, y);
                }
                return;
            }

            string prompt = GetCalPrompt(_calStep);
            Util.Label(prompt, p.x + p.width / 2f, y);
            y += line + sectionGap;

            int rawSteer = Plugin.GetAxisValue(state, Plugin.GetSteeringAxis());
            int rawThr = Plugin.GetAxisValue(state, Plugin.GetThrottleAxis());
            int rawBrk = Plugin.GetAxisValue(state, Plugin.GetBrakeAxis());
            int rawClu = Plugin.GetAxisValue(state, Plugin.GetClutchAxis());
            Util.Label($"steer={rawSteer}", p.x + p.width / 2f, y);
            y += line - 2f;
            Util.Label($"thr={rawThr}", p.x + p.width / 2f, y);
            y += line - 2f;
            Util.Label($"brk={rawBrk}", p.x + p.width / 2f, y);
            y += line - 2f;
            Util.Label($"clu={rawClu}", p.x + p.width / 2f, y);
            y += line + sectionGap;

            float steerNorm = Plugin.NormalizeSteering(rawSteer);
            float thrNorm = Plugin.NormalizePedal(rawThr, Plugin.PedalKind.Throttle);
            float brkNorm = Plugin.NormalizePedal(rawBrk, Plugin.PedalKind.Brake);
            float cluNorm = Plugin.NormalizePedal(rawClu, Plugin.PedalKind.Clutch);
            Util.Label($"steer norm={steerNorm:0.00}", p.x + p.width / 2f, y);
            y += line - 2f;
            Util.Label($"thr norm={thrNorm:0.00}", p.x + p.width / 2f, y);
            y += line - 2f;
            Util.Label($"brk norm={brkNorm:0.00}", p.x + p.width / 2f, y);
            y += line - 2f;
            Util.Label($"clu norm={cluNorm:0.00}", p.x + p.width / 2f, y);

            y += line + sectionGap;
            float captureY = Mathf.Min(y, cancelY - 34f);
            if (Util.FancyButton("Capture", cx, captureY))
            {
                if (Plugin.TryGetLogiState(out var captureState))
                {
                    CaptureStep(_calStep, captureState);
                    _calStep = NextStep(_calStep);
                    if (_calStep == CalStep.None)
                    {
                        _page = Page.Calibration;
                    }
                }
            }
        }

        private static Plugin.AxisId NextAxis(Plugin.AxisId axis)
        {
            int next = (int)axis + 1;
            if (next > (int)Plugin.AxisId.slider1)
            {
                next = 0;
            }
            return (Plugin.AxisId)next;
        }

        private static string GetHeldButtonsLabel(LogitechGSDK.DIJOYSTATE2ENGINES state)
        {
            if (state.rgbButtons == null || state.rgbButtons.Length == 0)
            {
                return "Buttons held: none";
            }

            StringBuilder sb = new StringBuilder("Buttons held: ");
            int shown = 0;
            int total = 0;
            for (int i = 0; i < state.rgbButtons.Length; i++)
            {
                if (state.rgbButtons[i] < 128)
                {
                    continue;
                }

                total++;
                if (shown >= 8)
                {
                    continue;
                }

                if (shown > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(i);
                shown++;
            }

            if (total == 0)
            {
                return "Buttons held: none";
            }
            if (total > shown)
            {
                sb.Append(" +");
                sb.Append(total - shown);
            }
            return sb.ToString();
        }

        private static string GetCalPrompt(CalStep step)
        {
            switch (step)
            {
                case CalStep.SteeringCenter:
                    return "Center the wheel";
                case CalStep.SteeringLeft:
                    return "Turn fully left";
                case CalStep.SteeringRight:
                    return "Turn fully right";
                case CalStep.ThrottleReleased:
                    return "Release throttle";
                case CalStep.ThrottlePressed:
                    return "Press throttle fully";
                case CalStep.BrakeReleased:
                    return "Release brake";
                case CalStep.BrakePressed:
                    return "Press brake fully";
                case CalStep.ClutchReleased:
                    return "Release clutch";
                case CalStep.ClutchPressed:
                    return "Press clutch fully";
                default:
                    return "";
            }
        }

        private static CalStep NextStep(CalStep step)
        {
            switch (step)
            {
                case CalStep.SteeringCenter:
                    return CalStep.SteeringLeft;
                case CalStep.SteeringLeft:
                    return CalStep.SteeringRight;
                case CalStep.SteeringRight:
                    return CalStep.ThrottleReleased;
                case CalStep.ThrottleReleased:
                    return CalStep.ThrottlePressed;
                case CalStep.ThrottlePressed:
                    return CalStep.BrakeReleased;
                case CalStep.BrakeReleased:
                    return CalStep.BrakePressed;
                case CalStep.BrakePressed:
                    return CalStep.ClutchReleased;
                case CalStep.ClutchReleased:
                    return CalStep.ClutchPressed;
                case CalStep.ClutchPressed:
                    return CalStep.None;
                default:
                    return CalStep.None;
            }
        }

        private static void CaptureStep(CalStep step, LogitechGSDK.DIJOYSTATE2ENGINES state)
        {
            switch (step)
            {
                case CalStep.SteeringCenter:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalSteerCenter, Plugin.GetAxisValue(state, Plugin.GetSteeringAxis()));
                    break;
                case CalStep.SteeringLeft:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalSteerLeft, Plugin.GetAxisValue(state, Plugin.GetSteeringAxis()));
                    break;
                case CalStep.SteeringRight:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalSteerRight, Plugin.GetAxisValue(state, Plugin.GetSteeringAxis()));
                    break;
                case CalStep.ThrottleReleased:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalThrottleReleased, Plugin.GetAxisValue(state, Plugin.GetThrottleAxis()));
                    break;
                case CalStep.ThrottlePressed:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalThrottlePressed, Plugin.GetAxisValue(state, Plugin.GetThrottleAxis()));
                    break;
                case CalStep.BrakeReleased:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalBrakeReleased, Plugin.GetAxisValue(state, Plugin.GetBrakeAxis()));
                    break;
                case CalStep.BrakePressed:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalBrakePressed, Plugin.GetAxisValue(state, Plugin.GetBrakeAxis()));
                    break;
                case CalStep.ClutchReleased:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalClutchReleased, Plugin.GetAxisValue(state, Plugin.GetClutchAxis()));
                    break;
                case CalStep.ClutchPressed:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalClutchPressed, Plugin.GetAxisValue(state, Plugin.GetClutchAxis()));
                    break;
            }
        }
    }
}
