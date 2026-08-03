using SebCore;
using UnityEngine;

namespace SebTruck
{
    public class TruckMenuWindow : CartridgeWindowBase
    {
        public const string FileName = "truck";
        public const string ListenerName = "SebTruckMenu";
        public const string ListenerData = "listener_SebTruckMenu";

        private string _toastMsg;
        private float _toastUntil;

        private Page _page;

        private enum Page
        {
            Handling = 0,
            Ignition = 1,
            TurnSignals = 2,
            BackupAlarm = 3,
            Hud = 4,
            Tweaks = 5
        }

        protected override void DrawWindow(Rect p)
        {
            float center = p.x + p.width / 2f - 16f;
            float cx = p.x + p.width / 2f;
            float y = p.y + 10f;
            float line = 12f;
            float sectionGap = 4f;

            const int pageCount = 6;

            Util.Label("Truck", cx, y);
            y += line;

            float navY = p.y + p.height - 18f;
            float prevX = p.x + 40f;
            float nextX = p.x + p.width - 40f;

            if (Util.SimpleButtonRaw("Prev", prevX, navY))
            {
                _page = (Page)(((int)_page + pageCount - 1) % pageCount);
            }
            if (Util.SimpleButtonRaw("Back", cx, navY))
            {
                BackButtonPressed();
                return;
            }
            if (Util.SimpleButtonRaw("Next", nextX, navY))
            {
                _page = (Page)(((int)_page + 1) % pageCount);
            }

            Util.Label(((int)_page + 1) + "/" + pageCount, p.x + p.width - 18f, p.y + 10f);

            y += sectionGap;
            string pageLabel = _page == Page.Hud ? "HUD" : _page.ToString();
            if (_page == Page.TurnSignals)
            {
                pageLabel = "Turn Signals";
            }
            else if (_page == Page.BackupAlarm)
            {
                pageLabel = "Backup Alarm";
            }
            Util.Label(pageLabel, cx, y);
            y += line;

            // Reset button sits above Back.
            float resetY = navY - 12f;

            // Toast sits above Reset.
            float toastY = resetY - 12f;
            if (!string.IsNullOrWhiteSpace(_toastMsg) && Time.unscaledTime < _toastUntil)
            {
                Util.Label(_toastMsg, cx, toastY);
            }

            if (Util.SimpleButtonRaw("Reset Defaults", cx, resetY))
            {
                if (_page == Page.Handling)
                {
                    if (Plugin.IsRallyModeActive()) Plugin.ResetRallyTransmissionDefaults();
                    else Plugin.ResetTransmissionDefaults();
                    Plugin.ResetHandlingDefaults();
                }
                else if (_page == Page.Ignition) Plugin.ResetIgnitionDefaults();
                else if (_page == Page.TurnSignals) Plugin.ResetIndicatorDefaults();
                else if (_page == Page.BackupAlarm) Plugin.ResetBackupAlarmDefaults();
                else if (_page == Page.Hud) Plugin.ResetHudDefaults();
                else Plugin.ResetTweaksDefaults();
            }

            y += sectionGap;

            DrawPage(p, center, cx, ref y, line);
        }

        private void DrawPage(Rect p, float center, float cx, ref float y, float line)
        {
            bool rally = Plugin.IsRallyModeActive();
            bool manual = Plugin.IsManualTransmissionEnabledEffective();

            if (_page == Page.Handling)
            {
                if (rally)
                {
                    bool rallyManual = Plugin.GetRallyManualTransmissionEnabled();
                    if (Util.CycleButtonRaw("Transmission", rallyManual ? "Manual" : "Auto", center, y))
                    {
                        Plugin.SetRallyManualTransmissionEnabled(!rallyManual);
                    }
                    y += line;
                }
                else
                {
                    if (Util.CycleButtonRaw("Transmission", manual ? "Manual" : "Auto", center, y))
                    {
                        Plugin.ToggleManualTransmission();
                    }
                    y += line;

                    if (manual)
                    {
                        int gears = Plugin.GetManualGearCount();
                        if (Util.CycleButtonRaw("Max Gears", gears.ToString(), center, y))
                        {
                            Plugin.SetManualGearCount(Plugin.NextManualGearCount(gears));
                        }
                        y += line;
                    }
                }

                var shiftMode = SebBinds.SebBindsApi.GetShiftMode();
                if (Util.CycleButtonRaw("Shift Mode", SebBinds.SebBindsApi.GetShiftModeLabel(shiftMode), center, y))
                {
                    SebBinds.SebBindsApi.SetShiftMode(SebBinds.SebBindsApi.NextShiftMode(shiftMode));
                }
                y += line;

                y += 2f;
                Util.Label(rally ? Plugin.GetCurrentRallyVariantLabel() : "Story Truck", cx, y);
                y += line;

                float power = Plugin.GetHandlingPowerMult();
                Util.ValueLabel($"{power:0.00}x", p.x + p.width - 12f, y);
                float? newPowerNorm = Util.Slider("Power", Mathf.InverseLerp(0.5f, 2.0f, power), center, y, ref MouseYLock);
                if (newPowerNorm.HasValue) Plugin.SetHandlingPowerMult(Mathf.Lerp(0.5f, 2.0f, newPowerNorm.Value));
                y += line;

                float speed = Plugin.GetHandlingSpeedMult();
                Util.ValueLabel($"{speed:0.00}x", p.x + p.width - 12f, y);
                float? newSpeedNorm = Util.Slider("Top Speed", Mathf.InverseLerp(0.5f, 2.0f, speed), center, y, ref MouseYLock);
                if (newSpeedNorm.HasValue) Plugin.SetHandlingSpeedMult(Mathf.Lerp(0.5f, 2.0f, newSpeedNorm.Value));
                y += line;

                float steering = Plugin.GetHandlingSteeringMult();
                Util.ValueLabel($"{steering:0.00}x", p.x + p.width - 12f, y);
                float? newSteeringNorm = Util.Slider("Steering", Mathf.InverseLerp(0.5f, 2.0f, steering), center, y, ref MouseYLock);
                if (newSteeringNorm.HasValue) Plugin.SetHandlingSteeringMult(Mathf.Lerp(0.5f, 2.0f, newSteeringNorm.Value));
                y += line;

                float grip = Plugin.GetHandlingGripMult();
                Util.ValueLabel($"{grip:0.00}x", p.x + p.width - 12f, y);
                float? newGripNorm = Util.Slider("Grip", Mathf.InverseLerp(0.5f, 2.0f, grip), center, y, ref MouseYLock);
                if (newGripNorm.HasValue) Plugin.SetHandlingGripMult(Mathf.Lerp(0.5f, 2.0f, newGripNorm.Value));
                y += line;

                if (rally)
                {
                    float mass = Plugin.GetHandlingMassMult();
                    Util.ValueLabel($"{mass:0.00}x", p.x + p.width - 12f, y);
                    float? newMassNorm = Util.Slider("Mass", Mathf.InverseLerp(0.5f, 2.0f, mass), center, y, ref MouseYLock);
                    if (newMassNorm.HasValue) Plugin.SetHandlingMassMult(Mathf.Lerp(0.5f, 2.0f, newMassNorm.Value));
                }
                else
                {
                    float downforce = Plugin.GetHandlingDownforceMult();
                    Util.ValueLabel($"{downforce:0.00}x", p.x + p.width - 12f, y);
                    float? newDownforceNorm = Util.Slider("Downforce", Mathf.InverseLerp(0.0f, 2.0f, downforce), center, y, ref MouseYLock);
                    if (newDownforceNorm.HasValue) Plugin.SetHandlingDownforceMult(Mathf.Lerp(0.0f, 2.0f, newDownforceNorm.Value));
                }
                y += line;

                return;
            }

            if (_page == Page.Ignition)
            {
                bool ignFeature = Plugin.GetIgnitionFeatureEnabled();
                if (Util.CycleButtonRaw("Ignition", ignFeature ? "Enabled" : "Disabled", center, y))
                {
                    Plugin.SetIgnitionFeatureEnabled(!ignFeature);
                }
                y += line;

                float holdS = Plugin.GetIgnitionHoldSeconds();
                Util.ValueLabel($"{holdS:0.00}s", p.x + p.width - 12f, y);
                float? newHoldNorm = Util.Slider("Ignition Time", Mathf.InverseLerp(0.25f, 3.0f, holdS), center, y, ref MouseYLock);
                if (newHoldNorm.HasValue) Plugin.SetIgnitionHoldSeconds(Mathf.Lerp(0.25f, 3.0f, newHoldNorm.Value));
                y += line;

                bool ignSfx = Plugin.GetIgnitionSfxEnabled();
                if (Util.CycleButtonRaw("Ignition SFX", ignSfx ? "On" : "Off", center, y))
                {
                    Plugin.SetIgnitionSfxEnabled(!ignSfx);
                }
                y += line;

                float vol = Plugin.GetIgnitionSfxVolume();
                Util.ValueLabel($"{Mathf.RoundToInt(vol * 100f)}%", p.x + p.width - 12f, y);
                float? newVol = Util.Slider("Ignition Vol.", vol, center, y, ref MouseYLock);
                if (newVol.HasValue) Plugin.SetIgnitionSfxVolume(newVol.Value);
                y += line;

                return;
            }

            if (_page == Page.TurnSignals)
            {
                bool indFeature = Plugin.GetIndicatorFeatureEnabled();
                if (Util.CycleButtonRaw("Turn Signals", indFeature ? "Enabled" : "Disabled", center, y))
                {
                    Plugin.SetIndicatorFeatureEnabled(!indFeature);
                }
                y += line;

                float step = Plugin.GetIndicatorBlinkSeconds();
                Util.ValueLabel($"{step:0.00}s", p.x + p.width - 12f, y);
                float? newStepNorm = Util.Slider("Blink Rate", Mathf.InverseLerp(0.20f, 1.20f, step), center, y, ref MouseYLock);
                if (newStepNorm.HasValue) Plugin.SetIndicatorBlinkSeconds(Mathf.Lerp(0.20f, 1.20f, newStepNorm.Value));
                y += line;

                float inten = Plugin.GetTurnSignalLightIntensity();
                Util.ValueLabel($"{inten:0.00}", p.x + p.width - 12f, y);
                float? newIntenNorm = Util.Slider("Intensity", Mathf.InverseLerp(0.0f, 1.0f, inten), center, y, ref MouseYLock);
                if (newIntenNorm.HasValue) Plugin.SetTurnSignalLightIntensity(Mathf.Lerp(0.0f, 1.0f, newIntenNorm.Value));
                y += line;

                bool indSfx = Plugin.GetIndicatorSfxEnabled();
                if (Util.CycleButtonRaw("Signal SFX", indSfx ? "On" : "Off", center, y))
                {
                    Plugin.SetIndicatorSfxEnabled(!indSfx);
                }
                y += line;

                float vol = Plugin.GetIndicatorSfxVolume();
                Util.ValueLabel($"{Mathf.RoundToInt(vol * 100f)}%", p.x + p.width - 12f, y);
                float? newVol = Util.Slider("Signal Vol.", vol, center, y, ref MouseYLock);
                if (newVol.HasValue) Plugin.SetIndicatorSfxVolume(newVol.Value);
                y += line;

                return;
            }

            if (_page == Page.BackupAlarm)
            {
                bool backupEnabled = Plugin.GetBackupAlarmEnabled();
                if (Util.CycleButtonRaw("Backup Alarm", backupEnabled ? "On" : "Off", center, y))
                {
                    Plugin.SetBackupAlarmEnabled(!backupEnabled);
                }
                y += line;

                bool hazards = Plugin.GetBackupAlarmHazardsEnabled();
                if (Util.CycleButtonRaw("4-Ways", hazards ? "On" : "Off", center, y))
                {
                    Plugin.SetBackupAlarmHazardsEnabled(!hazards);
                }
                y += line;

                float vol = Plugin.GetBackupAlarmVolume();
                Util.ValueLabel($"{Mathf.RoundToInt(vol * 100f)}%", p.x + p.width - 12f, y);
                float? newVol = Util.Slider("Volume", vol, center, y, ref MouseYLock);
                if (newVol.HasValue) Plugin.SetBackupAlarmVolume(newVol.Value);
                y += line;

                float interval = Plugin.GetBackupAlarmInterval();
                Util.ValueLabel($"{interval:0.00}s", p.x + p.width - 12f, y);
                float? newInterval = Util.Slider("Beep Rate", Mathf.InverseLerp(1.2f, 0.35f, interval), center, y, ref MouseYLock);
                if (newInterval.HasValue) Plugin.SetBackupAlarmInterval(Mathf.Lerp(1.2f, 0.35f, newInterval.Value));
                y += line;

                float tone = Plugin.GetBackupAlarmTone();
                Util.ValueLabel($"{Mathf.RoundToInt(tone)}hz", p.x + p.width - 12f, y);
                float? newTone = Util.Slider("Tone", Mathf.InverseLerp(450f, 1800f, tone), center, y, ref MouseYLock);
                if (newTone.HasValue) Plugin.SetBackupAlarmTone(Mathf.Lerp(450f, 1800f, newTone.Value));
                y += line;

                return;
            }

            if (_page == Page.Hud)
            {
                if (rally)
                {
                    var rallyUnits = Plugin.GetRallyHudSpeedUnit();
                    if (Util.CycleButtonRaw("Units", Plugin.GetHudSpeedUnitLabel(rallyUnits), center, y))
                    {
                        Plugin.SetRallyHudSpeedUnit(Plugin.NextHudSpeedUnit(rallyUnits));
                    }
                    y += line;

                    Util.Label("Rally HUD always shown", cx, y);
                    y += line;
                    return;
                }

                var style = Plugin.GetHudStyle();
                if (Util.CycleButtonRaw("Style", Plugin.GetHudStyleLabel(style), center, y))
                {
                    Plugin.SetHudStyle(Plugin.NextHudStyle(style));
                }
                y += line;

                var storyUnits = Plugin.GetHudSpeedUnit();
                if (Util.CycleButtonRaw("Units", Plugin.GetHudSpeedUnitLabel(storyUnits), center, y))
                {
                    Plugin.SetHudSpeedUnit(Plugin.NextHudSpeedUnit(storyUnits));
                }
                y += line;

                bool hudSpeed = Plugin.GetHudShowSpeed();
                bool? newHudSpeed = Util.Toggle("Speedomtr", hudSpeed, center, y);
                if (newHudSpeed.HasValue) Plugin.SetHudShowSpeed(newHudSpeed.Value);
                y += line;

                if (manual)
                {
                    bool hudTach = Plugin.GetHudShowTach();
                    bool? newHudTach = Util.Toggle("Tachomtr", hudTach, center, y);
                    if (newHudTach.HasValue) Plugin.SetHudShowTach(newHudTach.Value);
                    y += line;

                    bool hudGear = Plugin.GetHudShowGear();
                    bool? newHudGear = Util.Toggle("Gear Ind", hudGear, center, y);
                    if (newHudGear.HasValue) Plugin.SetHudShowGear(newHudGear.Value);
                    y += line;
                }

                if (style == Plugin.HudStyle.Classic)
                {
                    var spPos = Plugin.GetHudSpeedAnchor();
                    if (Util.CycleButtonRaw("Speedomtr Pos", Plugin.GetHudReadoutAnchorLabel(spPos), center, y))
                    {
                        Plugin.SetHudSpeedAnchor(Plugin.NextHudReadoutAnchor(spPos));
                    }
                    y += line;

                    if (manual)
                    {
                        var tPos = Plugin.GetHudTachAnchor();
                        if (Util.CycleButtonRaw("Tachomtr Pos", Plugin.GetHudReadoutAnchorLabel(tPos), center, y))
                        {
                            Plugin.SetHudTachAnchor(Plugin.NextHudReadoutAnchor(tPos));
                        }
                        y += line;

                        var gPos = Plugin.GetHudGearAnchor();
                        if (Util.CycleButtonRaw("Gear Ind. Pos", Plugin.GetHudReadoutAnchorLabel(gPos), center, y))
                        {
                            Plugin.SetHudGearAnchor(Plugin.NextHudReadoutAnchor(gPos));
                        }
                    }
                }

                return;
            }

            // Tweaks
            {
                
                float fwd = Plugin.GetManualSpeedMultForward();
                Util.ValueLabel($"{Mathf.RoundToInt(fwd * 100f)}%", p.x + p.width - 12f, y);
                float fwdNorm = Mathf.InverseLerp(0.5f, 1.5f, fwd);
                float? newFwdNorm = Util.Slider("Speed Mult.", fwdNorm, center, y, ref MouseYLock);
                if (newFwdNorm.HasValue)
                {
                    Plugin.SetManualSpeedMultForward(Mathf.Lerp(0.5f, 1.5f, newFwdNorm.Value));
                }
                y += line;

                float rev = Plugin.GetManualSpeedMultReverse();
                Util.ValueLabel($"{Mathf.RoundToInt(rev * 100f)}%", p.x + p.width - 12f, y);
                float revNorm = Mathf.InverseLerp(0.5f, 1.5f, rev);
                float? newRevNorm = Util.Slider("Revrs Mult.", revNorm, center, y, ref MouseYLock);
                if (newRevNorm.HasValue)
                {
                    Plugin.SetManualSpeedMultReverse(Mathf.Lerp(0.5f, 1.5f, newRevNorm.Value));
                }
                y += line;

                float inten = Plugin.GetHeadlightIntensityMult();
                Util.ValueLabel($"{inten:0.00}x", p.x + p.width - 12f, y);
                float intenNorm = Mathf.InverseLerp(0.25f, 2.0f, inten);
                float? newIntenNorm = Util.Slider("Headlgt Bright", intenNorm, center, y, ref MouseYLock);
                if (newIntenNorm.HasValue)
                {
                    Plugin.SetHeadlightIntensityMult(Mathf.Lerp(0.25f, 2.0f, newIntenNorm.Value));
                }
                y += line;

                float dist = Plugin.GetHeadlightRangeMult();
                Util.ValueLabel($"{dist:0.00}x", p.x + p.width - 12f, y);
                float distNorm = Mathf.InverseLerp(0.25f, 1.0f, dist);
                float? newDistNorm = Util.Slider("Headlght Dist", distNorm, center, y, ref MouseYLock);
                if (newDistNorm.HasValue)
                {
                    Plugin.SetHeadlightRangeMult(Mathf.Lerp(0.25f, 1.0f, newDistNorm.Value));
                }

                y += line;

                y += 2f;
                Util.Label("Cosmetics", cx, y);
                y += line;

                float wheelScale = Plugin.GetWheelScale();
                Util.ValueLabel($"{wheelScale:0.00}x", p.x + p.width - 12f, y);
                float wheelScaleNorm = Mathf.InverseLerp(0.5f, 2.0f, wheelScale);
                float? newWheelScaleNorm = Util.Slider("Wheel Scale", wheelScaleNorm, center, y, ref MouseYLock);
                if (newWheelScaleNorm.HasValue)
                {
                    Plugin.SetWheelScale(Mathf.Lerp(0.5f, 2.0f, newWheelScaleNorm.Value));
                }
                y += line;

                int bobble = Plugin.GetSelectedBobbleIndexUnlockedOrNone();
                if (Util.CycleButtonRaw("Bobblehead", Plugin.GetBobbleLabel(bobble), center, y))
                {
                    if (!Plugin.HasAnyUnlockedBobbleheads())
                    {
                        _toastMsg = "No Bobbleheads unlocked.";
                        _toastUntil = Time.unscaledTime + 2.0f;
                    }
                    else
                    {
                        Plugin.CycleBobbleheadSelection();
                    }
                }
                y += line;

                int paint = Plugin.GetSelectedPaintIndexUnlockedOrDefault();
                if (Util.CycleButtonRaw("Truck Paint", Plugin.GetPaintLabel(paint), center, y))
                {
                    if (!Plugin.HasAnyUnlockedPaints())
                    {
                        _toastMsg = "No Paints unlocked.";
                        _toastUntil = Time.unscaledTime + 2.0f;
                    }
                    else
                    {
                        Plugin.CyclePaintSelection();
                    }
                }
            }
        }
    }
}
