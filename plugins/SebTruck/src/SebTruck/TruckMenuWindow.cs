using SebCore;
using UnityEngine;

namespace SebTruck
{
    public class TruckMenuWindow : MonoBehaviour
    {
        public const string FileName = "truck";
        public const string ListenerName = "SebTruckMenu";
        public const string ListenerData = "listener_SebTruckMenu";

        private float _mouseYLock;
        private UIUtil _util;
        private DesktopDotExe.WindowView _view;

        private string _toastMsg;
        private float _toastUntil;

        private Page _page;

        private enum Page
        {
            Transmission = 0,
            Ignition = 1,
            TurnSignals = 2,
            Hud = 3,
            Tweaks = 4
        }

        public void FrameUpdate(DesktopDotExe.WindowView view)
        {
            if (view == null)
            {
                return;
            }

            _view = view;

            _util ??= new UIUtil();
            _util.M = view.M;
            _util.R = view.R;
            _util.Nav = view.M.nav;

            Rect p = new Rect(view.position * 8f, view.size * 8f);
            p.position += new Vector2(8f, 8f);

            if (_util.M.mouseButtonUp)
            {
                _mouseYLock = 0f;
            }
            if (_mouseYLock > 0f)
            {
                _util.M.mouse.y = _mouseYLock;
            }

            DrawMenu(p);
        }

        public void BackButtonPressed()
        {
            SebCore.DesktopAppLauncher.TryOpenProgramListener(_util?.M, _util?.R, SebCore.SebCoreMenuWindow.FileName, SebCore.SebCoreMenuWindow.ListenerData);
            _view?.Kill();
        }

        private void DrawMenu(Rect p)
        {
            float center = p.x + p.width / 2f - 16f;
            float cx = p.x + p.width / 2f;
            float y = p.y + 10f;
            float line = 12f;
            float sectionGap = 4f;

            const int pageCount = 5;

            _util.Label("Truck", cx, y);
            y += line;

            float navY = p.y + p.height - 18f;
            float prevX = p.x + 40f;
            float nextX = p.x + p.width - 40f;

            if (_util.SimpleButtonRaw("Prev", prevX, navY))
            {
                _page = (Page)(((int)_page + pageCount - 1) % pageCount);
            }
            if (_util.SimpleButtonRaw("Back", cx, navY))
            {
                SebCore.DesktopAppLauncher.TryOpenProgramListener(_util.M, _util.R, SebCore.SebCoreMenuWindow.FileName, SebCore.SebCoreMenuWindow.ListenerData);
                _view?.Kill();
                return;
            }
            if (_util.SimpleButtonRaw("Next", nextX, navY))
            {
                _page = (Page)(((int)_page + 1) % pageCount);
            }

            _util.Label(((int)_page + 1) + "/" + pageCount, p.x + p.width - 18f, p.y + 10f);

            y += sectionGap;
            string pageLabel = _page == Page.Hud ? "HUD" : _page.ToString();
            if (_page == Page.TurnSignals)
            {
                pageLabel = "Turn Signals";
            }
            _util.Label(pageLabel, cx, y);
            y += line;

            // Reset button sits above Back.
            float resetY = navY - 12f;

            // Toast sits above Reset.
            float toastY = resetY - 12f;
            if (!string.IsNullOrWhiteSpace(_toastMsg) && Time.unscaledTime < _toastUntil)
            {
                _util.Label(_toastMsg, cx, toastY);
            }

                if (_util.SimpleButtonRaw("Reset Defaults", cx, resetY))
            {
                if (_page == Page.Transmission) Plugin.ResetTransmissionDefaults();
                else if (_page == Page.Ignition) Plugin.ResetIgnitionDefaults();
                else if (_page == Page.TurnSignals) Plugin.ResetIndicatorDefaults();
                else if (_page == Page.Hud) Plugin.ResetHudDefaults();
                else Plugin.ResetTweaksDefaults();
            }

            y += sectionGap;

            DrawPage(p, center, cx, ref y, line);
        }

        private void DrawPage(Rect p, float center, float cx, ref float y, float line)
        {
            bool manual = Plugin.GetManualTransmissionEnabled();

            if (_page == Page.Transmission)
            {
                string modeLabel = manual ? "Manual" : "Auto";
                if (_util.CycleButtonRaw("Transmission", modeLabel, center, y))
                {
                    Plugin.ToggleManualTransmission();
                }
                y += line;

                if (manual)
                {
                    int gears = Plugin.GetManualGearCount();
                    if (_util.CycleButtonRaw("Max Gears", gears.ToString(), center, y))
                    {
                        Plugin.SetManualGearCount(Plugin.NextManualGearCount(gears));
                    }
                    y += line;
                }

                return;
            }

            if (_page == Page.Ignition)
            {
                
                bool ignFeature = Plugin.GetIgnitionFeatureEnabled();
                string ignLabel = ignFeature ? "Enabled" : "Disabled";
                if (_util.CycleButtonRaw("Ignition", ignLabel, center, y))
                {
                    Plugin.SetIgnitionFeatureEnabled(!ignFeature);
                }
                y += line;

                float holdS = Plugin.GetIgnitionHoldSeconds();
                _util.ValueLabel($"{holdS:0.00}s", p.x + p.width - 12f, y);
                float holdNorm = Mathf.InverseLerp(0.25f, 3.0f, holdS);
                float? newHoldNorm = _util.Slider("Ignition Time", holdNorm, center, y, ref _mouseYLock);
                if (newHoldNorm.HasValue)
                {
                    Plugin.SetIgnitionHoldSeconds(Mathf.Lerp(0.25f, 3.0f, newHoldNorm.Value));
                }
                y += line;

                bool ignSfx = Plugin.GetIgnitionSfxEnabled();
                string ignSfxLabel = ignSfx ? "On" : "Off";
                if (_util.CycleButtonRaw("Ignition SFX", ignSfxLabel, center, y))
                {
                    Plugin.SetIgnitionSfxEnabled(!ignSfx);
                }
                y += line;

                float vol = Plugin.GetIgnitionSfxVolume();
                _util.ValueLabel($"{Mathf.RoundToInt(vol * 100f)}%", p.x + p.width - 12f, y);
                float? newVol = _util.Slider("Ignition Vol.", vol, center, y, ref _mouseYLock);
                if (newVol.HasValue)
                {
                    Plugin.SetIgnitionSfxVolume(newVol.Value);
                }
                y += line;

                return;
            }

            if (_page == Page.TurnSignals)
            {
                bool indFeature = Plugin.GetIndicatorFeatureEnabled();
                string indLabel = indFeature ? "Enabled" : "Disabled";
                if (_util.CycleButtonRaw("Turn Signals", indLabel, center, y))
                {
                    Plugin.SetIndicatorFeatureEnabled(!indFeature);
                }
                y += line;

                float step = Plugin.GetIndicatorBlinkSeconds();
                _util.ValueLabel($"{step:0.00}s", p.x + p.width - 12f, y);
                float stepNorm = Mathf.InverseLerp(0.20f, 1.20f, step);
                float? newStepNorm = _util.Slider("Blink Rate", stepNorm, center, y, ref _mouseYLock);
                if (newStepNorm.HasValue)
                {
                    Plugin.SetIndicatorBlinkSeconds(Mathf.Lerp(0.20f, 1.20f, newStepNorm.Value));
                }
                y += line;

                float inten = Plugin.GetTurnSignalLightIntensity();
                _util.ValueLabel($"{inten:0.00}", p.x + p.width - 12f, y);
                float intenNorm = Mathf.InverseLerp(0.0f, 1.0f, inten);
                float? newIntenNorm = _util.Slider("Intensity", intenNorm, center, y, ref _mouseYLock);
                if (newIntenNorm.HasValue)
                {
                    Plugin.SetTurnSignalLightIntensity(Mathf.Lerp(0.0f, 1.0f, newIntenNorm.Value));
                }
                y += line;

                bool indSfx = Plugin.GetIndicatorSfxEnabled();
                string indSfxLabel = indSfx ? "On" : "Off";
                if (_util.CycleButtonRaw("Signal SFX", indSfxLabel, center, y))
                {
                    Plugin.SetIndicatorSfxEnabled(!indSfx);
                }
                y += line;

                float vol = Plugin.GetIndicatorSfxVolume();
                _util.ValueLabel($"{Mathf.RoundToInt(vol * 100f)}%", p.x + p.width - 12f, y);
                float? newVol = _util.Slider("Signal Vol.", vol, center, y, ref _mouseYLock);
                if (newVol.HasValue)
                {
                    Plugin.SetIndicatorSfxVolume(newVol.Value);
                }
                y += line;

                return;
            }

            if (_page == Page.Hud)
            {
                var units = Plugin.GetHudSpeedUnit();
                if (_util.CycleButtonRaw("Units", Plugin.GetHudSpeedUnitLabel(units), center, y))
                {
                    Plugin.SetHudSpeedUnit(Plugin.NextHudSpeedUnit(units));
                }
                y += line;

                bool hudSpeed = Plugin.GetHudShowSpeed();
                bool? newHudSpeed = _util.Toggle("Speedomtr", hudSpeed, center, y);
                if (newHudSpeed.HasValue)
                {
                    Plugin.SetHudShowSpeed(newHudSpeed.Value);
                }
                y += line;

                if (manual)
                {
                    bool hudTach = Plugin.GetHudShowTach();
                    bool? newHudTach = _util.Toggle("Tachomtr", hudTach, center, y);
                    if (newHudTach.HasValue)
                    {
                        Plugin.SetHudShowTach(newHudTach.Value);
                    }
                    y += line;

                    bool hudGear = Plugin.GetHudShowGear();
                    bool? newHudGear = _util.Toggle("Gear Ind", hudGear, center, y);
                    if (newHudGear.HasValue)
                    {
                        Plugin.SetHudShowGear(newHudGear.Value);
                    }
                    y += line;
                }

                var spPos = Plugin.GetHudSpeedAnchor();
                if (_util.CycleButtonRaw("Speedomtr Pos", Plugin.GetHudReadoutAnchorLabel(spPos), center, y))
                {
                    Plugin.SetHudSpeedAnchor(Plugin.NextHudReadoutAnchor(spPos));
                }
                y += line;

                if (manual)
                {
                    var tPos = Plugin.GetHudTachAnchor();
                    if (_util.CycleButtonRaw("Tachomtr Pos", Plugin.GetHudReadoutAnchorLabel(tPos), center, y))
                    {
                        Plugin.SetHudTachAnchor(Plugin.NextHudReadoutAnchor(tPos));
                    }
                    y += line;

                    var gPos = Plugin.GetHudGearAnchor();
                    if (_util.CycleButtonRaw("Gear Ind. Pos", Plugin.GetHudReadoutAnchorLabel(gPos), center, y))
                    {
                        Plugin.SetHudGearAnchor(Plugin.NextHudReadoutAnchor(gPos));
                    }
                }

                return;
            }

            // Tweaks
            {
                
                float fwd = Plugin.GetManualSpeedMultForward();
                _util.ValueLabel($"{Mathf.RoundToInt(fwd * 100f)}%", p.x + p.width - 12f, y);
                float fwdNorm = Mathf.InverseLerp(0.5f, 1.5f, fwd);
                float? newFwdNorm = _util.Slider("Speed Mult.", fwdNorm, center, y, ref _mouseYLock);
                if (newFwdNorm.HasValue)
                {
                    Plugin.SetManualSpeedMultForward(Mathf.Lerp(0.5f, 1.5f, newFwdNorm.Value));
                }
                y += line;

                float rev = Plugin.GetManualSpeedMultReverse();
                _util.ValueLabel($"{Mathf.RoundToInt(rev * 100f)}%", p.x + p.width - 12f, y);
                float revNorm = Mathf.InverseLerp(0.5f, 1.5f, rev);
                float? newRevNorm = _util.Slider("Revrs Mult.", revNorm, center, y, ref _mouseYLock);
                if (newRevNorm.HasValue)
                {
                    Plugin.SetManualSpeedMultReverse(Mathf.Lerp(0.5f, 1.5f, newRevNorm.Value));
                }
                y += line;

                float inten = Plugin.GetHeadlightIntensityMult();
                _util.ValueLabel($"{inten:0.00}x", p.x + p.width - 12f, y);
                float intenNorm = Mathf.InverseLerp(0.25f, 2.0f, inten);
                float? newIntenNorm = _util.Slider("Headlgt Bright", intenNorm, center, y, ref _mouseYLock);
                if (newIntenNorm.HasValue)
                {
                    Plugin.SetHeadlightIntensityMult(Mathf.Lerp(0.25f, 2.0f, newIntenNorm.Value));
                }
                y += line;

                float dist = Plugin.GetHeadlightRangeMult();
                _util.ValueLabel($"{dist:0.00}x", p.x + p.width - 12f, y);
                float distNorm = Mathf.InverseLerp(0.25f, 1.0f, dist);
                float? newDistNorm = _util.Slider("Headlght Dist", distNorm, center, y, ref _mouseYLock);
                if (newDistNorm.HasValue)
                {
                    Plugin.SetHeadlightRangeMult(Mathf.Lerp(0.25f, 1.0f, newDistNorm.Value));
                }

                y += line;

                y += 2f;
                _util.Label("Cosmetics", cx, y);
                y += line;

                int bobble = Plugin.GetSelectedBobbleIndexUnlockedOrNone();
                if (_util.CycleButtonRaw("Bobblehead", Plugin.GetBobbleLabel(bobble), center, y))
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
                if (_util.CycleButtonRaw("Truck Paint", Plugin.GetPaintLabel(paint), center, y))
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
