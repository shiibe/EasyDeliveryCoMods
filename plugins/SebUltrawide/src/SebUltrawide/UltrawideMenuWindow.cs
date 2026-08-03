using SebCore;
using UnityEngine;

namespace SebUltrawide
{
    public class UltrawideMenuWindow : CartridgeWindowBase
    {
        public const string FileName = "wide";
        public const string ListenerName = "UltrawideMenu";
        public const string ListenerData = "listener_UltrawideMenu";

        protected override void DrawWindow(Rect p)
        {
            float center = p.x + p.width / 2f - 16f;
            float y = p.y + 10f;
            float line = 12f;
            float sectionGap = 4f;

            // Bottom nav.
            float cx = p.x + p.width / 2f;
            float navY = p.y + p.height - 18f;

            // Reset button sits above Back.
            float resetY = navY - 12f;
            if (Util.SimpleButtonRaw("Reset Defaults", cx, resetY))
            {
                Plugin.ResetGraphicsDefaults();
                MouseYLock = 0f;
            }

            if (Util.SimpleButtonRaw("Back", cx, navY))
            {
                BackButtonPressed();
                return;
            }

            Util.Label("Graphics", p.x + p.width / 2f, y);
            y += line + sectionGap;

            Util.Label("FOV", p.x + p.width / 2f, y);
            y += line;

            float fovMin;
            float fovMax;
            GetFovRange(out fovMin, out fovMax);

            float currentFov = GetCurrentCameraFov();

            float thirdFov = Mathf.Clamp(Plugin.GetSavedFovOrDefault(firstPerson: false, fallback: currentFov), fovMin, fovMax);
            float thirdValue = Mathf.InverseLerp(fovMin, fovMax, thirdFov);
            Util.ValueLabel($"{thirdFov:0}", p.x + p.width - 12f, y);
            float? newThirdValue = Util.Slider("3rd Person", thirdValue, center, y, ref MouseYLock);
            if (newThirdValue.HasValue)
            {
                float newFov = Mathf.Lerp(fovMin, fovMax, newThirdValue.Value);
                Plugin.SaveFovOverride(firstPerson: false, fov: newFov);
            }

            y += line;

            // The game's default 1st-person FOV is higher than the menu camera's.
            float firstFov = Mathf.Clamp(Plugin.GetSavedFovOrDefault(firstPerson: true, fallback: 90f), fovMin, fovMax);
            float firstValue = Mathf.InverseLerp(fovMin, fovMax, firstFov);
            Util.ValueLabel($"{firstFov:0}", p.x + p.width - 12f, y);
            float? newFirstValue = Util.Slider("1st Person", firstValue, center, y, ref MouseYLock);
            if (newFirstValue.HasValue)
            {
                float newFov = Mathf.Lerp(fovMin, fovMax, newFirstValue.Value);
                Plugin.SaveFovOverride(firstPerson: true, fov: newFov);
            }

            y += line + sectionGap;

            Util.Label("Renderer", p.x + p.width / 2f, y);
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

            y += line;

            int vsMode = Plugin.GetVsyncMode();
            string vsLabel = vsMode == 1 ? "On" : "Off";
            Util.ValueLabel(vsLabel, p.x + p.width - 12f, y);
            float? newVsyncValue = Util.Slider("VSync", vsMode == 1 ? 1f : 0f, center, y, ref MouseYLock);
            if (newVsyncValue.HasValue)
            {
                Plugin.SetVsyncMode(newVsyncValue.Value >= 0.5f ? 1 : 0);
            }

            y += line + sectionGap;

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

        private static void GetFovRange(out float min, out float max)
        {
            min = 50f;
            max = 110f;
        }
    }
}
