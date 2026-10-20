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
            Tweaks = 5,
            Equipment = 6
        }

        protected override void DrawWindow(Rect p)
        {
            float center = p.x + p.width / 2f - 16f;
            float cx = p.x + p.width / 2f;
            float y = p.y + 10f;
            float line = 12f;
            float sectionGap = 4f;

            const int pageCount = 7;

            Util.Label("Truck", cx, y);
            y += line;

            float navY = p.y + p.height - 18f;
            var navigation = Util.NavigationFooter(p);

            if (navigation == UIUtil.NavigationAction.Previous)
            {
                _page = (Page)(((int)_page + pageCount - 1) % pageCount);
            }
            if (navigation == UIUtil.NavigationAction.Back)
            {
                BackButtonPressed();
                return;
            }
            if (navigation == UIUtil.NavigationAction.Next)
            {
                _page = (Page)(((int)_page + 1) % pageCount);
            }

            Util.PageIndicator(p, (int)_page, pageCount);

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
                else if (_page == Page.Equipment) Plugin.ResetEquipmentDefaults();
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
                float? newPower = Util.SliderRow("Power", power, 0.5f, 2.0f, $"{power:0.00}x", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newPower.HasValue) Plugin.SetHandlingPowerMult(newPower.Value);
                y += line;

                float speed = Plugin.GetHandlingSpeedMult();
                float? newSpeed = Util.SliderRow("Top Speed", speed, 0.5f, 2.0f, $"{speed:0.00}x", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newSpeed.HasValue) Plugin.SetHandlingSpeedMult(newSpeed.Value);
                y += line;

                float steering = Plugin.GetHandlingSteeringMult();
                float? newSteering = Util.SliderRow("Steering", steering, 0.5f, 2.0f, $"{steering:0.00}x", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newSteering.HasValue) Plugin.SetHandlingSteeringMult(newSteering.Value);
                y += line;

                float grip = Plugin.GetHandlingGripMult();
                float? newGrip = Util.SliderRow("Grip", grip, 0.5f, 2.0f, $"{grip:0.00}x", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newGrip.HasValue) Plugin.SetHandlingGripMult(newGrip.Value);
                y += line;

                if (rally)
                {
                    float mass = Plugin.GetHandlingMassMult();
                    float? newMass = Util.SliderRow("Mass", mass, 0.5f, 2.0f, $"{mass:0.00}x", center, p.x + p.width - 12f, y, ref MouseYLock);
                    if (newMass.HasValue) Plugin.SetHandlingMassMult(newMass.Value);
                }
                else
                {
                    float downforce = Plugin.GetHandlingDownforceMult();
                    float? newDownforce = Util.SliderRow("Downforce", downforce, 0.0f, 2.0f, $"{downforce:0.00}x", center, p.x + p.width - 12f, y, ref MouseYLock);
                    if (newDownforce.HasValue) Plugin.SetHandlingDownforceMult(newDownforce.Value);
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
                float? newHold = Util.SliderRow("Ignition Time", holdS, 0.25f, 3.0f, $"{holdS:0.00}s", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newHold.HasValue) Plugin.SetIgnitionHoldSeconds(newHold.Value);
                y += line;

                bool ignSfx = Plugin.GetIgnitionSfxEnabled();
                if (Util.CycleButtonRaw("Ignition SFX", ignSfx ? "On" : "Off", center, y))
                {
                    Plugin.SetIgnitionSfxEnabled(!ignSfx);
                }
                y += line;

                float vol = Plugin.GetIgnitionSfxVolume();
                float? newVol = Util.SliderRow("Ignition Vol.", vol, 0f, 1f, $"{Mathf.RoundToInt(vol * 100f)}%", center, p.x + p.width - 12f, y, ref MouseYLock);
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
                float? newStep = Util.SliderRow("Blink Rate", step, 0.20f, 1.20f, $"{step:0.00}s", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newStep.HasValue) Plugin.SetIndicatorBlinkSeconds(newStep.Value);
                y += line;

                float inten = Plugin.GetTurnSignalLightIntensity();
                float? newInten = Util.SliderRow("Intensity", inten, 0.0f, 1.0f, $"{inten:0.00}", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newInten.HasValue) Plugin.SetTurnSignalLightIntensity(newInten.Value);
                y += line;

                bool indSfx = Plugin.GetIndicatorSfxEnabled();
                if (Util.CycleButtonRaw("Signal SFX", indSfx ? "On" : "Off", center, y))
                {
                    Plugin.SetIndicatorSfxEnabled(!indSfx);
                }
                y += line;

                float vol = Plugin.GetIndicatorSfxVolume();
                float? newVol = Util.SliderRow("Signal Vol.", vol, 0f, 1f, $"{Mathf.RoundToInt(vol * 100f)}%", center, p.x + p.width - 12f, y, ref MouseYLock);
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
                float? newVol = Util.SliderRow("Volume", vol, 0f, 1f, $"{Mathf.RoundToInt(vol * 100f)}%", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newVol.HasValue) Plugin.SetBackupAlarmVolume(newVol.Value);
                y += line;

                float interval = Plugin.GetBackupAlarmInterval();
                float? newInterval = Util.SliderRow("Beep Rate", interval, 1.2f, 0.35f, $"{interval:0.00}s", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newInterval.HasValue) Plugin.SetBackupAlarmInterval(newInterval.Value);
                y += line;

                float tone = Plugin.GetBackupAlarmTone();
                float? newTone = Util.SliderRow("Tone", tone, 450f, 1800f, $"{Mathf.RoundToInt(tone)}hz", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newTone.HasValue) Plugin.SetBackupAlarmTone(newTone.Value);
                y += line;

                return;
            }

            if (_page == Page.Hud)
            {
                if (rally)
                {
                    var rallyStyle = Plugin.GetRallyHudStyle();
                    if (Util.CycleButtonRaw("Style", Plugin.GetHudStyleLabel(rallyStyle), center, y))
                    {
                        Plugin.SetRallyHudStyle(rallyStyle == Plugin.HudStyle.Fancy ? Plugin.HudStyle.Rally : Plugin.HudStyle.Fancy);
                    }
                    y += line;

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

                if (style != Plugin.HudStyle.Off)
                {
                    var storyUnits = Plugin.GetHudSpeedUnit();
                    if (Util.CycleButtonRaw("Units", Plugin.GetHudSpeedUnitLabel(storyUnits), center, y))
                    {
                        Plugin.SetHudSpeedUnit(Plugin.NextHudSpeedUnit(storyUnits));
                    }
                    y += line;

                    bool? newHudSpeed = Util.Toggle("Speedomtr", Plugin.GetHudShowSpeed(), center, y);
                    if (newHudSpeed.HasValue) Plugin.SetHudShowSpeed(newHudSpeed.Value);
                    y += line;

                    if (manual)
                    {
                        bool? newHudTach = Util.Toggle("Tachomtr", Plugin.GetHudShowTach(), center, y);
                        if (newHudTach.HasValue) Plugin.SetHudShowTach(newHudTach.Value);
                        y += line;

                        bool? newHudGear = Util.Toggle("Gear Ind", Plugin.GetHudShowGear(), center, y);
                        if (newHudGear.HasValue) Plugin.SetHudShowGear(newHudGear.Value);
                        y += line;
                    }
                }

                if (style == Plugin.HudStyle.Fancy)
                {
                    var odometer = Plugin.GetHudOdometerMode();
                    if (Util.CycleButtonRaw("Odometer", odometer.ToString(), center, y))
                    {
                        Plugin.SetHudOdometerMode((Plugin.HudOdometerMode)(((int)odometer + 1) % 3));
                    }
                    y += line;
                }

                y += 4f;
                Util.Label("Odometer " + Plugin.FormatStoryOdometer(trip: false), cx, y);
                y += line;
                if (Util.SimpleButtonRaw("Reset Trip " + Plugin.FormatStoryOdometer(trip: true), cx, y))
                {
                    Plugin.ResetTrip();
                }
                return;
            }

            if (_page == Page.Equipment)
            {
                // Greyed out until the upgrade is bought in the game.
                bool hasTires = Plugin.HasSnowTiresUpgrade();
                bool? newTires = Util.Toggle("Snow Tires", hasTires && Plugin.GetMountSnowTires(), center, y, hasTires);
                if (newTires.HasValue)
                {
                    Plugin.SetMountSnowTires(newTires.Value);
                }
                y += line;

                bool hasChains = Plugin.HasIceChainsUpgrade();
                bool? newChains = Util.Toggle("Ice Chains", hasChains && Plugin.GetMountIceChains(), center, y, hasChains);
                if (newChains.HasValue)
                {
                    Plugin.SetMountIceChains(newChains.Value);
                }
                y += line;

                bool hasRamBar = Plugin.HasRamBarUpgrade();
                bool? newRamBar = Util.Toggle("Ram Bar", hasRamBar && Plugin.GetMountRamBar(), center, y, hasRamBar);
                if (newRamBar.HasValue)
                {
                    Plugin.SetMountRamBar(newRamBar.Value);
                }
                y += line * 2f;

                int horn = Plugin.GetHornIndex();
                if (Util.CycleButtonRaw("Horn", Plugin.HornLabels[horn], center, y))
                {
                    Plugin.SetHornIndex((horn + 1) % Plugin.HornLabels.Length);
                }
                y += line * 2f;

                Util.Label("Cosmetics", cx, y);
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
                return;
            }

            // Tweaks
            {
                
                float fwd = Plugin.GetManualSpeedMultForward();
                float? newFwd = Util.SliderRow("Speed Mult.", fwd, 0.5f, 1.5f, $"{Mathf.RoundToInt(fwd * 100f)}%", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newFwd.HasValue)
                {
                    Plugin.SetManualSpeedMultForward(newFwd.Value);
                }
                y += line;

                float rev = Plugin.GetManualSpeedMultReverse();
                float? newRev = Util.SliderRow("Revrs Mult.", rev, 0.5f, 1.5f, $"{Mathf.RoundToInt(rev * 100f)}%", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newRev.HasValue)
                {
                    Plugin.SetManualSpeedMultReverse(newRev.Value);
                }
                y += line;

                float inten = Plugin.GetHeadlightIntensityMult();
                float? newInten = Util.SliderRow("Headlgt Bright", inten, 0.25f, 2.0f, $"{inten:0.00}x", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newInten.HasValue)
                {
                    Plugin.SetHeadlightIntensityMult(newInten.Value);
                }
                y += line;

                float dist = Plugin.GetHeadlightRangeMult();
                float? newDist = Util.SliderRow("Headlght Dist", dist, 0.25f, 1.0f, $"{dist:0.00}x", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newDist.HasValue)
                {
                    Plugin.SetHeadlightRangeMult(newDist.Value);
                }

                y += line;

                y += 2f;
                int mirrorQuality = Plugin.GetMirrorQuality();
                if (Util.CycleButtonRaw("Mirror Quality", Plugin.GetMirrorQualityLabel(mirrorQuality), center, y))
                {
                    Plugin.SetMirrorQuality((mirrorQuality + 1) % 4);
                }
                y += line;

                float wheelScale = Plugin.GetWheelScale();
                float? newWheelScale = Util.SliderRow("Wheel Scale", wheelScale, 0.5f, 2.0f, $"{wheelScale:0.00}x", center, p.x + p.width - 12f, y, ref MouseYLock);
                if (newWheelScale.HasValue)
                {
                    Plugin.SetWheelScale(newWheelScale.Value);
                }
            }
        }
    }
}
