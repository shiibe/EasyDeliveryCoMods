using HarmonyLib;
using UnityEngine;

namespace SebUltrawide
{
    public partial class Plugin
    {
        private void Awake()
        {
            _log = Logger;

            SebCore.ConfigUtil.CopyLegacyConfigIfNeeded(oldGuid: "shibe.easydeliveryco.ultrawide", newGuid: PluginGuid);
            _aspectRatio = Config.Bind(
                "General",
                "aspect_ratio",
                "auto",
                "Aspect ratio override.\n\n" +
                "auto    - Automatically uses the current window aspect ratio (recommended).\n" +
                "default - Use the game's original behavior (disables ultrawide fixes).\n" +
                "w:h     - Manual ratio such as 21:9 or 32:9.\n" +
                "number  - Decimal ratio such as 2.39 or 1.78.\n\n" +
                "Examples:\n" +
                "21:9\n" +
                "32:9\n" +
                "2.39"
            );

            _debugMode = Config.Bind("Logging", "debug_logging", false, "Log debug information about applied adjustments.");

            var harmony = new Harmony(PluginGuid);
            Patch(harmony, typeof(RaceManager), nameof(RaceManager.StartRace), postfix: nameof(ApplyAllCameras_Postfix));
            Patch(harmony, typeof(PauseSystem), "Start", postfix: nameof(PauseSystem_Start_Postfix));
            Patch(harmony, typeof(PauseSystem), nameof(PauseSystem.SetResolution), postfix: nameof(PresentationResized_Postfix));
            Patch(harmony, typeof(PauseSystem), nameof(PauseSystem.SetFullscreen), postfix: nameof(PresentationResized_Postfix));
            Patch(harmony, typeof(sOptionsMenu), "FrameUpdate", postfix: nameof(PresentationChange_Postfix));
            Patch(harmony, typeof(sEasyRally), "Update", postfix: nameof(PresentationChange_Postfix));
            Patch(harmony, typeof(sFreeCam), "Start", postfix: nameof(ApplyAllCameras_Postfix));
            Patch(harmony, typeof(sFreeCam), nameof(sFreeCam.ToggleFreeCam), postfix: nameof(ApplyAllCameras_Postfix));
            Patch(harmony, typeof(IntroDotExe), nameof(IntroDotExe.Setup), postfix: nameof(IntroDotExe_Setup_Postfix));
            Patch(harmony, typeof(ChooseExe), nameof(ChooseExe.Setup), postfix: nameof(ChooseExe_Setup_Postfix));
            Patch(harmony, typeof(CamDotExe), "Start", postfix: nameof(CamDotExe_Start_Postfix));
            Patch(harmony, typeof(MiniRenderer), "Start", postfix: nameof(MiniRenderer_Start_Postfix));
            Patch(harmony, typeof(pixelPerfectView), "AdjustViewPlane", prefix: nameof(PixelPerfectView_AdjustViewPlane_Prefix));
            Patch(harmony, typeof(sHUD), nameof(sHUD.Init), postfix: nameof(SHud_Init_Postfix));
            Patch(harmony, typeof(sHUD), nameof(sHUD.FadeToBlack), prefix: nameof(SHud_Backdrop_Prefix));
            Patch(harmony, typeof(sHUD), nameof(sHUD.DipToBlack), prefix: nameof(SHud_Backdrop_Prefix));
            Patch(harmony, typeof(ScreenSystem), nameof(ScreenSystem.Init), postfix: nameof(ScreenSystem_Postfix));
            Patch(harmony, typeof(ScreenSystem), "DoTransition", postfix: nameof(ScreenSystem_Postfix));
            Patch(harmony, typeof(SceneTransition), "Start", postfix: nameof(SceneTransition_Start_Postfix));
            Patch(harmony, typeof(sTeleporter), "Teleport", postfix: nameof(STeleporter_Teleport_Postfix));

            // FOV, pixelation, view distance and vsync live in SebCore.
            SebCore.ModGraphics.OutputAspect = GetPixelationOutputAspect;
            SebCore.ModGraphics.FreeCamCamera = ApplyAspect;
            SebCore.ModGraphics.Enable();
        }

        // Missing targets are logged as warnings so a game update can't break a fix silently.
        private static void Patch(Harmony harmony, System.Type type, string methodName, string prefix = null, string postfix = null)
        {
            SebCore.HarmonyUtil.PatchByName(harmony, typeof(Plugin), type.FullName, methodName, prefix, postfix, m => _log.LogWarning(m));
        }

        #pragma warning disable IDE1006
        private static void ApplyAllCameras_Postfix()
        {
            ApplyAllCameras();
        }

        private static void PauseSystem_Start_Postfix(PauseSystem __instance)
        {
            if (__instance != null)
            {
                ApplyCameraAspect(__instance.mainCamera);
            }

            ApplyAllCameras();
            ScaleOverlayBackdrops();
        }

        private static void PresentationResized_Postfix()
        {
            ApplyAllCameras();
            ScaleOverlayBackdrops();
        }

        private static void PresentationChange_Postfix()
        {
            RefreshPresentationIfChanged();
        }

        private static void SHud_Init_Postfix(sHUD __instance, MiniRenderer R)
        {
            UpdateHudRendererAspect(R);
            ScaleSpriteToCamera(__instance != null ? __instance.backdrop : null, Camera.main, "sHUD");
        }

        private static void SHud_Backdrop_Prefix(sHUD __instance)
        {
            ScaleSpriteToCamera(__instance != null ? __instance.backdrop : null, Camera.main, "sHUD");
        }

        private static void ScreenSystem_Postfix(ScreenSystem __instance)
        {
            if (__instance == null)
            {
                return;
            }

            var menuCamera = ResolveMenuCamera(__instance.menuCamera);
            ScaleSpriteToCamera(__instance.backdrop, menuCamera, "ScreenSystem");
            ScaleOverlaySprites(__instance, menuCamera, "ScreenSystem");
        }

        private static void IntroDotExe_Setup_Postfix(IntroDotExe __instance)
        {
            ApplyMenuCameraAspect(__instance != null ? __instance.menuCamera : null);
        }

        private static void ChooseExe_Setup_Postfix(ChooseExe __instance)
        {
            ApplyMenuCameraAspect(__instance != null ? __instance.menuCamera : null);
        }

        private static void CamDotExe_Start_Postfix(sFreeCam ___freecam)
        {
            var cam = ___freecam != null ? ___freecam.cam : null;
            if (cam == null)
            {
                LogDebug("CamDotExe freecam camera not found for aspect fix.");
                return;
            }

            ApplyAspect(cam);
            LogDebug($"Applied CamDotExe freecam aspect fix to {cam.name} ({cam.GetInstanceID()}).");
        }

        private static void SceneTransition_Start_Postfix(SceneTransition __instance, SpriteRenderer ___sr)
        {
            ScaleSpriteToCamera(___sr, Camera.main, "SceneTransition");
            ScaleOverlaySprites(__instance, Camera.main, "SceneTransition");
        }

        private static void STeleporter_Teleport_Postfix()
        {
            var hud = Object.FindFirstObjectByType<sHUD>();
            ScaleSpriteToCamera(hud != null ? hud.backdrop : null, Camera.main, "sHUD");
        }
        #pragma warning restore IDE1006
    }
}
