using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace SebUltrawide
{
    public partial class Plugin
    {
        private const float DefaultAspect = 16f / 9f;

        private static ConfigEntry<bool> _debugMode;
        private static ConfigEntry<string> _aspectRatio;

        private static ManualLogSource _log;
        private static readonly Dictionary<int, Vector2> OverlayTargetSizes = new Dictionary<int, Vector2>();
        private static readonly Dictionary<int, float> PixelViewAspects = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> PixelViewSkipAspects = new Dictionary<int, float>();
        private static readonly Dictionary<int, PixelPerfectState> PixelPerfectStates = new Dictionary<int, PixelPerfectState>();
        private static readonly Dictionary<int, Vector3> HudDisplayOriginalScales = new Dictionary<int, Vector3>();
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int AlphaTexId = Shader.PropertyToID("_AlphaTex");
        private static int _lastScreenWidth = -1;
        private static int _lastScreenHeight = -1;
        private static bool _lastScreenFullscreen;

        private static AspectRatioSetting.Kind ParseAspectSetting(out float aspect)
        {
            return AspectRatioSetting.Parse(_aspectRatio != null ? _aspectRatio.Value : "auto", out aspect);
        }

        private static bool IsVanillaPresentation()
        {
            return ParseAspectSetting(out _) == AspectRatioSetting.Kind.Vanilla;
        }

        private static void RefreshPresentationIfChanged()
        {
            int width = Screen.width;
            int height = Screen.height;
            bool fullscreen = Screen.fullScreen;

            if (_lastScreenWidth == width && _lastScreenHeight == height && _lastScreenFullscreen == fullscreen)
            {
                return;
            }

            _lastScreenWidth = width;
            _lastScreenHeight = height;
            _lastScreenFullscreen = fullscreen;

            ApplyAllCameras();
            ScaleOverlayBackdrops();
            RefreshHudRendererAspect();
            SebCore.ModGraphics.ApplySaved();
            LogDebug($"Refreshed presentation after resolution/fullscreen change: {width}x{height}, fullscreen={fullscreen}.");
        }

        // Full-resolution pixelation matches the presented (letterboxed) aspect.
        private static float GetPixelationOutputAspect()
        {
            if (IsVanillaPresentation())
            {
                return DefaultAspect;
            }

            float a = GetTargetAspect();
            return a > 0.01f ? a : GetWindowAspect();
        }

        private static float GetTargetAspect()
        {
            switch (ParseAspectSetting(out float aspect))
            {
                case AspectRatioSetting.Kind.Vanilla:
                    return DefaultAspect;
                case AspectRatioSetting.Kind.Auto:
                    return Mathf.Round(GetWindowAspect() * 100f) / 100f;
                case AspectRatioSetting.Kind.Window:
                    return GetWindowAspect();
                case AspectRatioSetting.Kind.Fixed:
                    return aspect;
                default:
                    return GetDisplayAspect();
            }
        }

        private static float GetDisplayAspect()
        {
            if (Screen.currentResolution.height == 0)
            {
                return DefaultAspect;
            }

            return (float)Screen.currentResolution.width / Screen.currentResolution.height;
        }

        private static float GetWindowAspect()
        {
            if (Screen.height == 0)
            {
                return DefaultAspect;
            }

            return (float)Screen.width / Screen.height;
        }

        private static bool IsUltrawide()
        {
            if (IsVanillaPresentation())
            {
                return false;
            }

            return GetWindowAspect() > DefaultAspect + 0.01f;
        }

        private static bool IsPrimaryGameplayCamera(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }

            // Not RearViewCam: the mirror renders into its own fixed-aspect texture.
            if (string.Equals(camera.name, "Camera Persp", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return camera == Camera.main;
        }

        private static void ApplyCameraAspect(Camera camera)
        {
            // Split-screen views keep the game's own aspect.
            if (!IsPrimaryGameplayCamera(camera) || camera.rect.width < 0.99f || camera.rect.height < 0.99f)
            {
                return;
            }

            ApplyAspect(camera);
        }

        private static void ApplyAspect(Camera camera)
        {
            if (camera == null || !IsUltrawide())
            {
                return;
            }

            float targetAspect = GetTargetAspect();
            if (targetAspect <= 0.01f || Mathf.Abs(camera.aspect - targetAspect) < 0.0001f)
            {
                return;
            }

            camera.aspect = targetAspect;
            LogDebug($"Forced aspect for {camera.name} ({camera.GetInstanceID()}) to {targetAspect:0.###}.");
        }

        private static void ApplyMenuCameraAspect(GameObject menuCameraObject)
        {
            if (menuCameraObject == null)
            {
                return;
            }

            ApplyAspect(menuCameraObject.GetComponentInChildren<Camera>(true));
        }

        private static void ApplyAllCameras()
        {
            if (!IsUltrawide())
            {
                return;
            }

            foreach (var camera in Camera.allCameras)
            {
                ApplyCameraAspect(camera);
            }
        }
    }
}
