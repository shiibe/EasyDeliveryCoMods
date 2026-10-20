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
                SebCore.ModGraphics.ResetDefaults();
                MouseYLock = 0f;
            }

            if (Util.NavigationFooter(p, paging: false) == UIUtil.NavigationAction.Back)
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

            float thirdFov = Mathf.Clamp(SebCore.ModGraphics.GetSavedFovOrDefault(firstPerson: false, fallback: currentFov), fovMin, fovMax);
            float? newThirdValue = Util.SliderRow("3rd Person", thirdFov, fovMin, fovMax, $"{thirdFov:0}", center, p.x + p.width - 12f, y, ref MouseYLock);
            if (newThirdValue.HasValue)
            {
                float newFov = newThirdValue.Value;
                SebCore.ModGraphics.SaveFovOverride(firstPerson: false, fov: newFov);
            }

            y += line;

            // The game's default 1st-person FOV is higher than the menu camera's.
            float firstFov = Mathf.Clamp(SebCore.ModGraphics.GetSavedFovOrDefault(firstPerson: true, fallback: 90f), fovMin, fovMax);
            float? newFirstValue = Util.SliderRow("1st Person", firstFov, fovMin, fovMax, $"{firstFov:0}", center, p.x + p.width - 12f, y, ref MouseYLock);
            if (newFirstValue.HasValue)
            {
                float newFov = newFirstValue.Value;
                SebCore.ModGraphics.SaveFovOverride(firstPerson: true, fov: newFov);
            }

            y += line + sectionGap;

            Util.Label("Renderer", p.x + p.width / 2f, y);
            y += line;

            int pixelMode = SebCore.ModGraphics.GetPixelationMode();
            string pixelLabel = pixelMode switch
            {
                0 => "None",
                1 => "Finer",
                2 => "Fine",
                3 => "Default",
                4 => "Large",
                _ => "Default"
            };
            int? newPixelValue = Util.ChoiceSliderRow("Pixelation", pixelMode, 4, pixelLabel, center, p.x + p.width - 12f, y, ref MouseYLock);
            if (newPixelValue.HasValue)
            {
                int newMode = newPixelValue.Value;
                if (newMode != pixelMode)
                {
                    SebCore.ModGraphics.SavePixelationMode(newMode);
                }
            }

            y += line;

            int viewMode = SebCore.ModGraphics.GetViewDistanceMode();
            string viewLabel = viewMode switch
            {
                0 => "Near",
                1 => "Default",
                2 => "Far",
                3 => "Max",
                _ => "Default"
            };
            int? newViewValue = Util.ChoiceSliderRow("View Distance", viewMode, 3, viewLabel, center, p.x + p.width - 12f, y, ref MouseYLock);
            if (newViewValue.HasValue)
            {
                int newMode = newViewValue.Value;
                if (newMode != viewMode)
                {
                    SebCore.ModGraphics.SaveViewDistanceMode(newMode);
                }
            }

            y += line;

            bool? newVsync = Util.Toggle("VSync", SebCore.ModGraphics.GetVsyncMode() == 1, center, y);
            if (newVsync.HasValue)
            {
                SebCore.ModGraphics.SetVsyncMode(newVsync.Value ? 1 : 0);
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
