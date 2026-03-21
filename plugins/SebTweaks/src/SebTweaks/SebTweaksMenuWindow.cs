using SebCore;
using UnityEngine;

namespace SebTweaks
{
    // This is the window type referenced by SebCore.CartridgeApps.App.WindowTypeName.
    public sealed class SebTweaksMenuWindow : CartridgeWindowBase
    {
        public const string ListenerName = "SebTweaksMenu";
        public const string ListenerData = "listener_SebTweaksMenu";

        private Page _page;

        private enum Page
        {
            Gameplay = 0,
            Atmosphere = 1,
            Graphics = 2,
            TimeWeather = 3,
            Cheats = 4,
            GodMode = 5
        }

        // (Cheats page has fixed increment buttons.)

        protected override void DrawWindow(Rect p)
        {
            float cx = p.x + p.width / 2f;
            float center = cx - 16f;
            float y = p.y + 10f;
            float line = 12f;
            float sectionGap = 4f;

            // Show a simple placeholder screen on non-gameplay scenes.
            if (!Plugin.IsInGameNow())
            {
                float backY = GetNavY(p);
                Util.Label("Use in-game only", cx, y + 24f);
                if (Util.SimpleButtonRaw("Back", cx, backY))
                {
                    BackButtonPressed();
                }
                return;
            }

            // Bottom nav.
            float navY = GetNavY(p);
            float prevX = p.x + 44f;
            float nextX = p.x + p.width - 44f;

            if (_page == Page.Gameplay || _page == Page.Atmosphere || _page == Page.Graphics || _page == Page.TimeWeather)
            {
                // Reset button sits above Back.
                float resetY = navY - 12f;
                if (Util.SimpleButtonRaw("Reset Defaults", cx, resetY))
                {
                    if (_page == Page.Gameplay)
                    {
                        ResetGameplayDefaults();
                    }
                    else if (_page == Page.Atmosphere)
                    {
                        ResetAtmosphereDefaults();
                    }
                    else if (_page == Page.Graphics)
                    {
                        Plugin.ResetGraphicsDefaults();
                    }
                    else
                    {
                        ResetTimeWeatherDefaults();
                    }

                    MouseYLock = 0f;
                }
            }

            if (Util.SimpleButtonRaw("Prev", prevX, navY))
            {
                int pageCount = (int)Page.GodMode + 1;
                _page = (Page)(((int)_page + pageCount - 1) % pageCount);
            }
            if (Util.SimpleButtonRaw("Back", cx, navY))
            {
                BackButtonPressed();
                return;
            }
            if (Util.SimpleButtonRaw("Next", nextX, navY))
            {
                int pageCount = (int)Page.GodMode + 1;
                _page = (Page)(((int)_page + 1) % pageCount);
            }

            // Page indicator.
            int pageNum = (int)_page + 1;
            int pageTotal = (int)Page.GodMode + 1;
            Util.Label(pageNum + "/" + pageTotal, p.x + p.width - 18f, p.y + 10f);

            Util.Label("Tweaks", cx, y);
            y += line;
            Util.Label(PageLabel(_page), cx, y);
            y += line + sectionGap;

            if (_page == Page.Gameplay)
            {
                DrawGameplay(p, center, ref y, line);
                return;
            }
            if (_page == Page.Atmosphere)
            {
                DrawAtmosphere(p, center, ref y, line);
                return;
            }
            if (_page == Page.TimeWeather)
            {
                DrawTimeWeather(p, center, ref y, line);
                return;
            }

            if (_page == Page.Graphics)
            {
                DrawGraphics(p, center, ref y, line);
                return;
            }

            if (_page == Page.GodMode)
            {
                DrawGodMode(p, center, ref y, line);
                return;
            }

            DrawCheats(p, center, ref y, line);
        }

        private static void ResetGameplayDefaults()
        {
            Plugin.SetFloat(Plugin.PrefKeyJobPayoutMult, 1f);
            Plugin.SetFloat(Plugin.PrefKeyGasPriceMult, 1f);
            Plugin.SetFloat(Plugin.PrefKeyGasConsumptionMult, 1f);
            Plugin.SetFloat(Plugin.PrefKeyEnergyLossMult, 1f);
            Plugin.SetFloat(Plugin.PrefKeyTempLossMult, 1f);
            Plugin.SetInt(Plugin.PrefKeyIceCrackEnabled, 1);
        }

        private static void ResetWorldDefaults()
        {
            // Kept for binary compatibility; no longer used by the menu.
            ResetAtmosphereDefaults();
            ResetTimeWeatherDefaults();
        }

        private static void ResetAtmosphereDefaults()
        {
            Plugin.SetFloat(Plugin.PrefKeyFogMult, 1f);
            Plugin.SetFloat(Plugin.PrefKeyWorldLightMult, 1f);
            Plugin.SetFloat(Plugin.PrefKeyWorldLightColorR, 1f);
            Plugin.SetFloat(Plugin.PrefKeyWorldLightColorG, 1f);
            Plugin.SetFloat(Plugin.PrefKeyWorldLightColorB, 1f);
        }

        private static void ResetTimeWeatherDefaults()
        {
            Plugin.SetFloat(Plugin.PrefKeyTimeOfDay, 0.25f);
            Plugin.SetInt(Plugin.PrefKeyFreezeTime, 0);
            Plugin.SetInt(Plugin.PrefKeyWeatherMode, 0);
            Plugin.SetFloat(Plugin.PrefKeyWeatherIntensity, 0.4f);
        }

        private static string PageLabel(Page p)
        {
            return p switch
            {
                Page.Gameplay => "Gameplay",
                Page.Atmosphere => "Atmosphere",
                Page.Graphics => "Graphics",
                Page.TimeWeather => "Time & Weather",
                Page.Cheats => "Cheats",
                Page.GodMode => "God Mode",
                _ => ""
            };
        }

        private void DrawGraphics(Rect p, float center, ref float y, float line)
        {
            float cx = p.x + p.width / 2f;
            float sectionGap = 4f;

            const float fovMin = 50f;
            const float fovMax = 110f;
            float currentFov = GetCurrentCameraFov();

            float thirdFov = Mathf.Clamp(Plugin.GetSavedFovOrDefault(firstPerson: false, fallback: currentFov), fovMin, fovMax);
            float thirdValue = Mathf.InverseLerp(fovMin, fovMax, thirdFov);
            Util.ValueLabel($"{thirdFov:0}", p.x + p.width - 12f, y);
            float? newThirdValue = Util.Slider("3rd Per. FOV", thirdValue, center, y, ref MouseYLock);
            if (newThirdValue.HasValue)
            {
                float newFov = Mathf.Lerp(fovMin, fovMax, newThirdValue.Value);
                Plugin.SaveFovOverride(firstPerson: false, fov: newFov);
            }
            y += line;

            float firstFov = Mathf.Clamp(Plugin.GetSavedFovOrDefault(firstPerson: true, fallback: 90f), fovMin, fovMax);
            float firstValue = Mathf.InverseLerp(fovMin, fovMax, firstFov);
            Util.ValueLabel($"{firstFov:0}", p.x + p.width - 12f, y);
            float? newFirstValue = Util.Slider("1st Per. FOV", firstValue, center, y, ref MouseYLock);
            if (newFirstValue.HasValue)
            {
                float newFov = Mathf.Lerp(fovMin, fovMax, newFirstValue.Value);
                Plugin.SaveFovOverride(firstPerson: true, fov: newFov);
            }

            y += line;

            int pixelMode = Plugin.GetPixelationMode();
            string pixelLabel = pixelMode switch
            {
                0 => "None",
                1 => "Finer",
                2 => "Fine",
                3 => "Default",
                4 => "Large",
                _ => "Default"
            };
            Util.ValueLabel(pixelLabel, p.x + p.width - 12f, y);
            float pixelValue = Mathf.Clamp01(pixelMode / 4f);
            float? newPixelValue = Util.Slider("Pixelation", pixelValue, center, y, ref MouseYLock);
            if (newPixelValue.HasValue)
            {
                int newMode = Mathf.Clamp(Mathf.RoundToInt(newPixelValue.Value * 4f), 0, 4);
                if (newMode != pixelMode)
                {
                    Plugin.SavePixelationMode(newMode);
                }
            }
            y += line;

            int viewMode = Plugin.GetViewDistanceMode();
            string viewLabel = viewMode switch
            {
                0 => "Near",
                1 => "Default",
                2 => "Far",
                3 => "Max",
                _ => "Default"
            };
            Util.ValueLabel(viewLabel, p.x + p.width - 12f, y);
            float viewValue = Mathf.Clamp01(viewMode / 3f);
            float? newViewValue = Util.Slider("View Distance", viewValue, center, y, ref MouseYLock);
            if (newViewValue.HasValue)
            {
                int newMode = Mathf.Clamp(Mathf.RoundToInt(newViewValue.Value * 3f), 0, 3);
                if (newMode != viewMode)
                {
                    Plugin.SaveViewDistanceMode(newMode);
                }
            }

            y += line + sectionGap;

            int vsMode = Plugin.GetVsyncMode();
            string vsLabel = vsMode switch
            {
                1 => "On",
                2 => "Off",
                _ => "Default"
            };
            if (Util.CycleButtonRaw("VSync", vsLabel, center, y))
            {
                Plugin.SetVsyncMode((vsMode + 1) % 3);
            }
            y += line;
        }

        private static float GetCurrentCameraFov()
        {
            var pauseSystem = PauseSystem.pauseSystem;
            if (pauseSystem != null && pauseSystem.mainCamera != null)
            {
                return pauseSystem.mainCamera.fieldOfView;
            }

            var cam = Camera.main;
            if (cam != null)
            {
                return cam.fieldOfView;
            }

            return 70f;
        }

        private void DrawGodMode(Rect p, float center, ref float y, float line)
        {
            float cx = p.x + p.width / 2f;

            float labelX = p.x + 12f;
            float valueX = p.x + p.width - 12f;

            bool DrawToggleRow(string label, bool state, float rowY)
            {
                label = LocalizationDictionary.Translate(label);
                string text = "[" + (state ? "on" : "off") + "]";
                int rowW = (int)(valueX - labelX);
                bool hovered = Util.M.MouseOver((int)labelX, (int)rowY, rowW, 8);
                bool clicked = false;

                if (hovered)
                {
                    Util.M.mouseIcon = 128;
                    if (Util.M.mouseButton)
                    {
                        Util.M.mouseIcon = 160;
                    }
                    if (Util.M.mouseButtonUp)
                    {
                        clicked = true;
                    }
                    label = ">" + label;
                }

                Util.R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;
                Util.R.fput(label, labelX, rowY);
                Util.R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
                Util.R.fput(text, valueX, rowY);
                return clicked;
            }

            bool noEnergy = Plugin.GetInt(Plugin.PrefKeyGodNoEnergyLoss, 0) == 1;
            if (DrawToggleRow("No Energy Loss", noEnergy, y))
            {
                Plugin.SetInt(Plugin.PrefKeyGodNoEnergyLoss, noEnergy ? 0 : 1);
            }
            y += line;

            bool noGas = Plugin.GetInt(Plugin.PrefKeyGodNoGasLoss, 0) == 1;
            if (DrawToggleRow("No Gas Loss", noGas, y))
            {
                Plugin.SetInt(Plugin.PrefKeyGodNoGasLoss, noGas ? 0 : 1);
            }
            y += line;

            bool noTemp = Plugin.GetInt(Plugin.PrefKeyGodNoTempLoss, 0) == 1;
            if (DrawToggleRow("No Temp Loss", noTemp, y))
            {
                Plugin.SetInt(Plugin.PrefKeyGodNoTempLoss, noTemp ? 0 : 1);
            }
            y += line;

            bool invTruck = Plugin.GetInt(Plugin.PrefKeyGodInvincibleTruck, 0) == 1;
            if (DrawToggleRow("Invincible Truck", invTruck, y))
            {
                Plugin.SetInt(Plugin.PrefKeyGodInvincibleTruck, invTruck ? 0 : 1);
            }
        }

        private void DrawGameplay(Rect p, float center, ref float y, float line)
        {
            DrawMultSlider(p, center, ref y, line, "Job Payout", Plugin.PrefKeyJobPayoutMult, 0.1f, 5f);
            DrawMultSlider(p, center, ref y, line, "Gas Price", Plugin.PrefKeyGasPriceMult, 0.1f, 3f);
            DrawMultSlider(p, center, ref y, line, "Gas Use", Plugin.PrefKeyGasConsumptionMult, 0.1f, 3f);
            DrawMultSlider(p, center, ref y, line, "Energy Loss", Plugin.PrefKeyEnergyLossMult, 0.1f, 3f);
            DrawMultSlider(p, center, ref y, line, "Temp Loss", Plugin.PrefKeyTempLossMult, 0.1f, 3f);

            bool iceCrack = Plugin.GetInt(Plugin.PrefKeyIceCrackEnabled, 1) == 1;
            bool? newIceCrack = Util.Toggle("Ice Cracking", iceCrack, center, y);
            if (newIceCrack.HasValue)
            {
                Plugin.SetInt(Plugin.PrefKeyIceCrackEnabled, newIceCrack.Value ? 1 : 0);
            }
            y += line;
        }

        private void DrawAtmosphere(Rect p, float center, ref float y, float line)
        {
            // These systems typically don't exist on the main menu scene.
            // The settings still save, and will apply when you're in-game.
            if (Object.FindFirstObjectByType<sDayNightCycle>() == null && sWeatherSystem.instance == null)
            {
                float cx = p.x + p.width / 2f;
                Util.Label("(world settings apply in-game)", cx, y);
                y += line + 2f;
            }

            // Fog
            float fog = Plugin.GetFloat(Plugin.PrefKeyFogMult, 1f);
            float fog01 = Mathf.InverseLerp(0f, 3f, fog);
            Util.ValueLabel($"x{fog:0.00}", p.x + p.width - 12f, y);
            float? newFog01 = Util.Slider("Fog", fog01, center, y, ref MouseYLock);
            if (newFog01.HasValue)
            {
                Plugin.SetFloat(Plugin.PrefKeyFogMult, Mathf.Lerp(0f, 3f, newFog01.Value));
            }
            y += line;

            // World light
            float light = Plugin.GetFloat(Plugin.PrefKeyWorldLightMult, 1f);
            float light01 = Mathf.InverseLerp(0f, 2f, light);
            Util.ValueLabel($"x{light:0.00}", p.x + p.width - 12f, y);
            float? newLight01 = Util.Slider("World Light", light01, center, y, ref MouseYLock);
            if (newLight01.HasValue)
            {
                Plugin.SetFloat(Plugin.PrefKeyWorldLightMult, Mathf.Lerp(0f, 2f, newLight01.Value));
            }
            y += line;

            float r = Plugin.GetFloat(Plugin.PrefKeyWorldLightColorR, 1f);
            float r01 = Mathf.InverseLerp(0f, 2f, r);
            Util.ValueLabel($"x{r:0.00}", p.x + p.width - 12f, y);
            float? newR01 = Util.Slider("Light Red", r01, center, y, ref MouseYLock);
            if (newR01.HasValue)
            {
                Plugin.SetFloat(Plugin.PrefKeyWorldLightColorR, Mathf.Lerp(0f, 2f, newR01.Value));
            }
            y += line;

            float g = Plugin.GetFloat(Plugin.PrefKeyWorldLightColorG, 1f);
            float g01 = Mathf.InverseLerp(0f, 2f, g);
            Util.ValueLabel($"x{g:0.00}", p.x + p.width - 12f, y);
            float? newG01 = Util.Slider("Light Green", g01, center, y, ref MouseYLock);
            if (newG01.HasValue)
            {
                Plugin.SetFloat(Plugin.PrefKeyWorldLightColorG, Mathf.Lerp(0f, 2f, newG01.Value));
            }
            y += line;

            float b = Plugin.GetFloat(Plugin.PrefKeyWorldLightColorB, 1f);
            float b01 = Mathf.InverseLerp(0f, 2f, b);
            Util.ValueLabel($"x{b:0.00}", p.x + p.width - 12f, y);
            float? newB01 = Util.Slider("Light Blue", b01, center, y, ref MouseYLock);
            if (newB01.HasValue)
            {
                Plugin.SetFloat(Plugin.PrefKeyWorldLightColorB, Mathf.Lerp(0f, 2f, newB01.Value));
            }
            y += line;
        }

        private void DrawTimeWeather(Rect p, float center, ref float y, float line)
        {
            // These systems typically don't exist on the main menu scene.
            // The settings still save, and will apply when you're in-game.
            if (Object.FindFirstObjectByType<sDayNightCycle>() == null && sWeatherSystem.instance == null)
            {
                float cx = p.x + p.width / 2f;
                Util.Label("(time & weather apply in-game)", cx, y);
                y += line + 2f;
            }

            // Time
            bool freezeTime = Plugin.GetInt(Plugin.PrefKeyFreezeTime, 0) == 1;

            float t;
            if (!freezeTime)
            {
                var cycle = Object.FindFirstObjectByType<sDayNightCycle>();
                t = cycle != null ? Mathf.Repeat(cycle.time, 1f) : Mathf.Repeat(Plugin.GetFloat(Plugin.PrefKeyTimeOfDay, 0.25f), 1f);
            }
            else
            {
                t = Mathf.Repeat(Plugin.GetFloat(Plugin.PrefKeyTimeOfDay, 0.25f), 1f);
            }

            Util.ValueLabel(TimeLabel(t), p.x + p.width - 12f, y);
            float? newT = Util.Slider("Time", t, center, y, ref MouseYLock);
            if (newT.HasValue)
            {
                Plugin.SetFloat(Plugin.PrefKeyTimeOfDay, newT.Value);
                Plugin.MarkTimeUser();
            }
            y += line;

            bool? newFreeze = Util.Toggle("Freeze Time", freezeTime, center, y);
            if (newFreeze.HasValue)
            {
                Plugin.SetInt(Plugin.PrefKeyFreezeTime, newFreeze.Value ? 1 : 0);

                // When enabling freeze, seed the stored time from the current world time.
                if (newFreeze.Value)
                {
                    var cycle = Object.FindFirstObjectByType<sDayNightCycle>();
                    if (cycle != null)
                    {
                        Plugin.SetFloat(Plugin.PrefKeyTimeOfDay, Mathf.Repeat(cycle.time, 1f));
                    }
                }

                Plugin.MarkTimeUser();
            }
            y += line;

            // Spacing between Time and Weather sections.
            y += line * 2f;

            // Weather
            bool weatherManual = Plugin.GetInt(Plugin.PrefKeyWeatherMode, 0) == 1;
            if (Util.CycleButtonRaw("Weather", weatherManual ? "Manual" : "Auto", center, y))
            {
                Plugin.SetInt(Plugin.PrefKeyWeatherMode, weatherManual ? 0 : 1);
            }
            y += line;

            if (Plugin.GetInt(Plugin.PrefKeyWeatherMode, 0) == 1)
            {
                float w = Mathf.Clamp01(Plugin.GetFloat(Plugin.PrefKeyWeatherIntensity, 0.4f));
                Util.ValueLabel($"{Mathf.RoundToInt(w * 100f)}%", p.x + p.width - 12f, y);
                float? newW = Util.Slider("Intensity", w, center, y, ref MouseYLock);
                if (newW.HasValue)
                {
                    Plugin.SetFloat(Plugin.PrefKeyWeatherIntensity, newW.Value);
                }
                y += line;

                if (Util.CycleButtonRaw("Preset", WeatherPresetLabel(w), center, y))
                {
                    float next = NextWeatherPreset(w);
                    Plugin.SetFloat(Plugin.PrefKeyWeatherIntensity, next);
                }
                y += line;
            }
        }

        private void DrawCheats(Rect p, float center, ref float y, float line)
        {
            float cx = p.x + p.width / 2f;
            float colGap = 72f;
            float leftX = cx - colGap;
            float rightX = cx + colGap;
            var hud = Object.FindFirstObjectByType<sHUD>();

            Util.Label("Money", cx, y);
            y += line;

            if (Util.SimpleButtonRaw("Add $10", leftX, y))
            {
                hud?.ReceivePayment(10f);
            }
            if (Util.SimpleButtonRaw("Remove $10", rightX, y))
            {
                if (hud != null)
                {
                    hud.money = Mathf.Max(0f, hud.money - 10f);
                }
            }
            y += line;

            if (Util.SimpleButtonRaw("Add $20", leftX, y))
            {
                hud?.ReceivePayment(20f);
            }
            if (Util.SimpleButtonRaw("Remove $20", rightX, y))
            {
                if (hud != null)
                {
                    hud.money = Mathf.Max(0f, hud.money - 20f);
                }
            }
            y += line;

            if (Util.SimpleButtonRaw("Add $50", leftX, y))
            {
                hud?.ReceivePayment(50f);
            }
            if (Util.SimpleButtonRaw("Remove $50", rightX, y))
            {
                if (hud != null)
                {
                    hud.money = Mathf.Max(0f, hud.money - 50f);
                }
            }
            y += line;

            if (Util.SimpleButtonRaw("Add $100", leftX, y))
            {
                hud?.ReceivePayment(100f);
            }
            if (Util.SimpleButtonRaw("Remove $100", rightX, y))
            {
                if (hud != null)
                {
                    hud.money = Mathf.Max(0f, hud.money - 100f);
                }
            }
            y += line + 4f;

            Util.Label("Refill", cx, y);
            y += line;

            if (hud == null)
            {
                Util.Label("(in-game only)", cx, y);
                return;
            }

            // Energy slider
            float energy01 = hud.energyCapacity > 0f ? Mathf.Clamp01(hud.energy / hud.energyCapacity) : 0f;
            Util.ValueLabel($"{Mathf.RoundToInt(energy01 * 100f)}%", p.x + p.width - 12f, y);
            float? newEnergy01 = Util.Slider("Energy", energy01, center, y, ref MouseYLock);
            if (newEnergy01.HasValue)
            {
                hud.energy = newEnergy01.Value * hud.energyCapacity;
            }
            y += line;

            // Fuel slider
            float fuel01 = hud.fuelCapacity > 0f ? Mathf.Clamp01(hud.fuel / hud.fuelCapacity) : 0f;
            Util.ValueLabel($"{Mathf.RoundToInt(fuel01 * 100f)}%", p.x + p.width - 12f, y);
            float? newFuel01 = Util.Slider("Fuel", fuel01, center, y, ref MouseYLock);
            if (newFuel01.HasValue)
            {
                hud.fuel = newFuel01.Value * hud.fuelCapacity;
            }
        }

        private void DrawMultSlider(Rect p, float center, ref float y, float line, string label, string key, float min, float max)
        {
            float v = Mathf.Clamp(Plugin.GetFloat(key, 1f), min, max);
            float v01 = Mathf.InverseLerp(min, max, v);
            Util.ValueLabel($"x{v:0.00}", p.x + p.width - 12f, y);
            float? new01 = Util.Slider(label, v01, center, y, ref MouseYLock);
            if (new01.HasValue)
            {
                Plugin.SetFloat(key, Mathf.Lerp(min, max, new01.Value));
            }
            y += line;
        }

        private static string TimeLabel(float t01)
        {
            int totalMinutes = Mathf.RoundToInt(t01 * 24f * 60f) % (24 * 60);
            int h = totalMinutes / 60;
            int m = totalMinutes % 60;
            return h.ToString("00") + ":" + m.ToString("00");
        }

        private static float NextWeatherPreset(float cur)
        {
            // Cycle: Clear -> Snow -> Storm -> Clear
            if (cur < 0.2f) return 0.4f;
            if (cur < 0.8f) return 1.0f;
            return 0.0f;
        }

        private static string WeatherPresetLabel(float w)
        {
            if (w < 0.2f) return "Clear";
            if (w < 0.8f) return "Snow";
            return "Storm";
        }
    }
}

