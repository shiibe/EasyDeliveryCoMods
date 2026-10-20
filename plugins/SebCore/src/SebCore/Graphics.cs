using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SebCore
{
    // Shared FOV / pixelation / view distance / vsync / rear mirror quality.
    // Inactive until a mod calls Enable() or EnableMirrorQuality(), so SebCore alone changes nothing.
    public static partial class ModGraphics
    {
        // Output aspect for full-resolution pixelation (SebUltrawide). Null = screen aspect.
        public static Func<float> OutputAspect;

        // Called on each refresh with the active free cam (SebUltrawide forces its aspect).
        public static Action<Camera> FreeCamCamera;

        private static bool _patched;
        private static bool _full;
        private static bool _mirror;
        private static float _nextPoll;

        private static bool _viewDistanceBaseCaptured;
        private static float _baseShadowDistance;
        private static float _baseLodBias;
        private static int _baseMaximumLodLevel;

        private static Camera _pixelMainCamera;
        private static MeshRenderer _pixelScreenRenderer;
        private static RenderTexture _pixelDefaultRt;
        private static RenderTexture _pixelCustomRt;

        private static Camera _pixelRearCamera;
        private static MeshRenderer _pixelRearRenderer;
        private static RenderTexture _pixelDefaultRearRt;
        private static RenderTexture _pixelCustomRearRt;

        private static int _pixelLastMode = -1;
        private static int _pixelBaseW;
        private static int _pixelBaseH;
        private static int _pixelRearBaseW;
        private static int _pixelRearBaseH;

        private static bool _insideLookupInit;
        private static Type _ambianceType;
        private static FieldInfo _ambiancePlayerInsideField;
        private static UnityEngine.Object _ambianceInstance;

        private static Type _mirrorModeType;
        private static FieldInfo _mirrorModeMirroredField;
        private static bool _mirrorModeLookupDone;

        // Turns on FOV, pixelation, view distance and vsync. Safe to call from several mods.
        public static void Enable()
        {
            _full = true;
            EnsurePatched();
            ApplySaved();
        }

        // Turns rear-view mirror quality on/off (SebTruck). Works without Enable().
        public static void EnableMirrorQuality(bool enabled)
        {
            _mirror = enabled;
            EnsurePatched();
            RefreshPixelation();
        }

        public static void SetMirrorQuality(int quality)
        {
            ModPrefs.SetInt(PrefKeyMirrorQuality, Mathf.Clamp(quality, 0, 3));
            RefreshPixelation();
        }

        public static void SavePixelationMode(int mode)
        {
            ModPrefs.SetInt(PrefKeyPixelationModeVersion, 2);
            ModPrefs.SetInt(PrefKeyPixelationMode, Mathf.Clamp(mode, 0, 4));
            RefreshPixelation();
        }

        public static void SaveViewDistanceMode(int mode)
        {
            ModPrefs.SetInt(PrefKeyViewDistanceModeVersion, 2);
            ModPrefs.SetInt(PrefKeyViewDistanceMode, Mathf.Clamp(mode, 0, 3));
            RefreshViewDistance();
        }

        public static void SetVsyncMode(int mode)
        {
            ModPrefs.SetInt(PrefKeyGfxVsyncModeVersion, 2);
            ModPrefs.SetInt(PrefKeyGfxVsyncMode, Mathf.Clamp(mode, 0, 1));
            ApplyVsyncMode();
        }

        public static bool TryGetSavedFov(bool firstPerson, out float fov)
        {
            fov = ModPrefs.GetFloat(firstPerson ? PrefKeyFovFirstPerson : PrefKeyFovThirdPerson, -1f);
            if (fov >= 1f)
            {
                return true;
            }

            if (!firstPerson)
            {
                fov = ModPrefs.GetFloat(PrefKeyFovLegacy, -1f);
                if (fov >= 1f)
                {
                    return true;
                }
            }

            fov = 0f;
            return false;
        }

        public static float GetSavedFovOrDefault(bool firstPerson, float fallback)
        {
            return TryGetSavedFov(firstPerson, out float fov) ? fov : fallback;
        }

        public static void SaveFovOverride(bool firstPerson, float fov)
        {
            ModPrefs.SetFloat(firstPerson ? PrefKeyFovFirstPerson : PrefKeyFovThirdPerson, fov);
            if (_full && IsFirstPersonViewActive() == firstPerson)
            {
                ApplyFovOverride(fov);
            }
        }

        public static void ResetDefaults()
        {
            ModPrefs.DeleteKey(PrefKeyFovFirstPerson);
            ModPrefs.DeleteKey(PrefKeyFovThirdPerson);
            ModPrefs.DeleteKey(PrefKeyFovLegacy);
            SavePixelationMode(3); // Default
            SaveViewDistanceMode(1); // Default
            ModPrefs.DeleteKey(PrefKeyGfxVsyncMode);
            ModPrefs.DeleteKey(PrefKeyGfxVsyncModeVersion);
            ApplyVsyncMode();
            ModPrefs.Save();
        }

        // Re-applies every saved setting (after resolution, scene or camera changes).
        public static void ApplySaved()
        {
            if (_full)
            {
                try
                {
                    if (TryGetSavedFov(IsFirstPersonViewActive(), out float savedFov))
                    {
                        ApplyFovOverride(savedFov);
                    }
                }
                catch
                {
                    // ignore
                }

                RefreshViewDistance();
                ApplyVsyncMode();
            }

            RefreshPixelation();
        }

        private static void EnsurePatched()
        {
            if (_patched)
            {
                return;
            }
            _patched = true;

            try
            {
                var harmony = new Harmony(Plugin.PluginGuid + ".graphics");
                Patch(harmony, "sCameraController", "Update", postfix: nameof(SCameraController_Update_Postfix));
                Patch(harmony, "sCameraController", "LateUpdate", postfix: nameof(SCameraController_LateUpdate_Postfix));
                Patch(harmony, "sHUD", "WorldToHUDPoint", prefix: nameof(SHud_WorldToHUDPoint_Prefix));
                Patch(harmony, "sEasyRally", "Update", postfix: nameof(RefreshPixelation));
                Patch(harmony, "sEasyCinema", "Update", postfix: nameof(RefreshPixelation));
                Patch(harmony, "RaceManager", "StartRace", postfix: nameof(RefreshPixelation));
                foreach (var target in new[]
                {
                    "sFreeCam.Start", "sFreeCam.ToggleFreeCam", "ScreenSystem.Init", "ScreenSystem.DoTransition", "sTeleporter.Teleport",
                    "PauseSystem.Start", "PauseSystem.SetResolution", "PauseSystem.SetFullscreen", "PauseSystem.TogglePaused"
                })
                {
                    string[] parts = target.Split('.');
                    Patch(harmony, parts[0], parts[1], postfix: nameof(ApplySaved));
                }
            }
            catch (Exception e)
            {
                Plugin.LogDebug("Graphics patching failed: " + e);
            }
        }

        private static void Patch(Harmony harmony, string typeName, string methodName, string prefix = null, string postfix = null)
        {
            HarmonyUtil.PatchByName(harmony, typeof(ModGraphics), typeName, methodName, prefix, postfix, Plugin.LogDebug);
        }

        #pragma warning disable IDE1006
        private static void SCameraController_Update_Postfix(sCameraController __instance)
        {
            try
            {
                if (_full)
                {
                    ApplyViewDistanceFarClip(__instance);
                }

                // Low-cadence catch-all: other code can reset vsync, and the car/mirror can respawn.
                if (Time.unscaledTime < _nextPoll)
                {
                    return;
                }
                _nextPoll = Time.unscaledTime + 0.5f;
                if (_full)
                {
                    ApplyVsyncMode();
                }
                RefreshPixelation();
            }
            catch
            {
                // ignore
            }
        }

        private static void SCameraController_LateUpdate_Postfix(sCameraController __instance)
        {
            if (!_full || __instance == null || __instance.cam == null || IsPlayerInsideBuilding())
            {
                return;
            }

            bool firstPerson = __instance.firstPerson && !__instance.fixedPerspective;
            if (TryGetSavedFov(firstPerson, out float savedFov))
            {
                __instance.cam.fieldOfView = savedFov;
            }
        }

        // Maps through the camera's actual pixel size so markers stay aligned at any render size.
        private static bool SHud_WorldToHUDPoint_Prefix(ref Vector2 __result, sHUD __instance, ref Vector3 worldPoint, ref MiniRenderer ___R)
        {
            if (!_full)
            {
                return true;
            }

            try
            {
                if (__instance == null || ___R == null || __instance.navigation == null || __instance.navigation.car == null || __instance.navigation.car.carCamera == null)
                {
                    return true;
                }

                var cam = __instance.navigation.car.carCamera;
                var camTransform = cam.transform;
                if (Vector3.Dot(camTransform.forward, worldPoint - camTransform.position) < 0f)
                {
                    __result = new Vector2(-100f, -100f);
                    return false;
                }

                float camWidth = cam.pixelWidth;
                float camHeight = cam.pixelHeight;
                if (camWidth <= 0.01f || camHeight <= 0.01f || ___R.width <= 0 || ___R.height <= 0)
                {
                    return true;
                }

                var screenPoint = cam.WorldToScreenPoint(worldPoint);
                screenPoint.y = camHeight - screenPoint.y;
                screenPoint.x -= camWidth * 0.5f;
                screenPoint.y -= camHeight * 0.5f;

                __result = new Vector2(
                    screenPoint.x * (___R.width / camWidth) + ___R.width * 0.5f,
                    screenPoint.y * (___R.height / camHeight) + ___R.height * 0.5f
                );

                if (IsMirrorModeMirrored())
                {
                    __result.x = ___R.width - __result.x;
                }

                return false;
            }
            catch
            {
                return true;
            }
        }
        #pragma warning restore IDE1006

        private static bool IsMirrorModeMirrored()
        {
            try
            {
                if (!_mirrorModeLookupDone)
                {
                    _mirrorModeLookupDone = true;
                    _mirrorModeType = AccessTools.TypeByName("MirrorMode");
                    _mirrorModeMirroredField = _mirrorModeType != null ? AccessTools.Field(_mirrorModeType, "mirrored") : null;
                }

                return _mirrorModeMirroredField != null && Convert.ToBoolean(_mirrorModeMirroredField.GetValue(null));
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyVsyncMode()
        {
            if (!_full)
            {
                return;
            }

            try
            {
                QualitySettings.vSyncCount = GetVsyncMode() == 1 ? 1 : 0;
            }
            catch
            {
                // ignore
            }
        }

        private static bool IsFirstPersonViewActive()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<sCameraController>();
            return controller != null && controller.firstPerson && !controller.fixedPerspective;
        }

        private static void ApplyFovOverride(float fov)
        {
            if (IsPlayerInsideBuilding())
            {
                return;
            }

            PauseSystem.FOV = fov;
            var pauseSystem = PauseSystem.pauseSystem;
            var cam = pauseSystem != null && pauseSystem.mainCamera != null ? pauseSystem.mainCamera : Camera.main;
            if (cam != null)
            {
                cam.fieldOfView = fov;
            }
        }

        private static bool IsPlayerInsideBuilding()
        {
            try
            {
                if (!_insideLookupInit)
                {
                    _insideLookupInit = true;
                    _ambianceType = AccessTools.TypeByName("sAmbiance");
                    _ambiancePlayerInsideField = _ambianceType != null ? AccessTools.Field(_ambianceType, "playerInside") : null;
                }

                if (_ambianceType == null || _ambiancePlayerInsideField == null)
                {
                    return false;
                }

                if (_ambianceInstance == null)
                {
                    _ambianceInstance = UnityEngine.Object.FindFirstObjectByType(_ambianceType);
                }

                if (_ambianceInstance == null)
                {
                    return false;
                }

                var insideGo = _ambiancePlayerInsideField.GetValue(_ambianceInstance) as GameObject;
                return insideGo != null && insideGo.activeSelf;
            }
            catch
            {
                return false;
            }
        }

        private static void RefreshViewDistance()
        {
            if (!_full)
            {
                return;
            }

            try
            {
                if (!_viewDistanceBaseCaptured)
                {
                    _baseShadowDistance = QualitySettings.shadowDistance;
                    _baseLodBias = QualitySettings.lodBias;
                    _baseMaximumLodLevel = QualitySettings.maximumLODLevel;
                    _viewDistanceBaseCaptured = true;
                }

                ApplyViewDistanceQuality(GetViewDistanceMode());
            }
            catch
            {
                // ignore
            }
        }

        private static void ApplyViewDistanceQuality(int mode)
        {
            float shadowDistance;
            float lodBias;
            switch (mode)
            {
                case 0:
                    shadowDistance = 150f;
                    lodBias = 0.75f;
                    break;
                case 2:
                    shadowDistance = 1200f;
                    lodBias = 3.0f;
                    break;
                case 3:
                    shadowDistance = 8000f;
                    lodBias = 12.0f;
                    break;
                default:
                    QualitySettings.shadowDistance = _baseShadowDistance;
                    QualitySettings.lodBias = _baseLodBias;
                    QualitySettings.maximumLODLevel = _baseMaximumLodLevel;
                    return;
            }

            QualitySettings.shadowDistance = shadowDistance;
            QualitySettings.lodBias = lodBias;
            QualitySettings.maximumLODLevel = 0;
        }

        private static void ApplyViewDistanceFarClip(sCameraController controller)
        {
            if (controller == null || controller.cam == null)
            {
                return;
            }

            float far;
            switch (GetViewDistanceMode())
            {
                case 0:
                    far = 3000f;
                    break;
                case 2:
                    far = 25000f;
                    break;
                case 3:
                    far = 250000f;
                    break;
                default:
                    return; // Default leaves the game's far clip alone.
            }

            if (far > controller.cam.nearClipPlane + 1f)
            {
                controller.cam.farClipPlane = far;
            }
        }

        public static void RefreshPixelation()
        {
            // A leftover custom mirror target still needs restoring after mirror quality is turned off.
            if (!_full && !_mirror && _pixelCustomRearRt == null)
            {
                return;
            }

            try
            {
                EnsurePixelationTargets();
                int mode = _full ? GetPixelationMode() : 3;
                if (_full)
                {
                    ApplyPixelationMain(mode);
                }
                ApplyPixelationRear(mode);
                _pixelLastMode = mode;
            }
            catch
            {
                // Experimental: never hard-fail due to this feature.
            }
        }

        private static void EnsurePixelationTargets()
        {
            if (_full)
            {
                EnsureMainPixelationTargets();
            }

            if (_pixelRearCamera == null || _pixelRearRenderer == null)
            {
                var car = UnityEngine.Object.FindFirstObjectByType<sCarController>();
                if (car != null)
                {
                    if (_pixelRearRenderer == null)
                    {
                        // Scene path is carInt/InvertMirrored/RearViewMirror; keep the old path as a fallback.
                        var mirror = car.transform.Find("carInt/InvertMirrored/RearViewMirror") ?? car.transform.Find("carInt/RearViewMirror");
                        if (mirror != null)
                        {
                            _pixelRearRenderer = mirror.GetComponent<MeshRenderer>();
                        }
                    }

                    if (_pixelRearCamera == null)
                    {
                        var rearCam = car.transform.Find("RearViewCam");
                        if (rearCam != null)
                        {
                            _pixelRearCamera = rearCam.GetComponent<Camera>();
                        }
                    }
                }
            }

            if (_pixelDefaultRearRt == null && _pixelRearCamera != null && _pixelRearCamera.targetTexture != null && _pixelRearCamera.targetTexture != _pixelCustomRearRt)
            {
                _pixelDefaultRearRt = _pixelRearCamera.targetTexture;
            }

            if (_pixelDefaultRearRt == null && TryGetStableRenderTexture(_pixelRearRenderer != null ? _pixelRearRenderer.material.mainTexture : null, _pixelCustomRearRt, out RenderTexture rearRt))
            {
                _pixelDefaultRearRt = rearRt;
            }

            CaptureRearPixelationBaseDimensions();
        }

        private static void EnsureMainPixelationTargets()
        {
            bool hasActiveFreeCam = TryGetActiveFreeCam(out Camera freeCam);
            if (hasActiveFreeCam && freeCam != _pixelMainCamera)
            {
                SwitchMainPixelCamera(freeCam);
            }

            if (hasActiveFreeCam)
            {
                FreeCamCamera?.Invoke(freeCam);
            }

            if (!hasActiveFreeCam && TryGetRaceCamera(out Camera raceCamera) && raceCamera != _pixelMainCamera)
            {
                SwitchMainPixelCamera(raceCamera);
            }

            if (!hasActiveFreeCam && TryGetGameplayCamera(out Camera gameplayCamera) && gameplayCamera != _pixelMainCamera && IsInactiveCamera(_pixelMainCamera))
            {
                SwitchMainPixelCamera(gameplayCamera);
            }

            if (_pixelMainCamera == null)
            {
                if (TryGetGameplayCamera(out Camera controllerCam))
                {
                    _pixelMainCamera = controllerCam;
                }
                if (_pixelMainCamera == null)
                {
                    var pauseSystem = PauseSystem.pauseSystem;
                    _pixelMainCamera = pauseSystem != null ? pauseSystem.mainCamera : null;
                }
                if (_pixelMainCamera == null)
                {
                    _pixelMainCamera = Camera.main;
                }
            }

            if (_pixelScreenRenderer == null)
            {
                var screenSystemType = AccessTools.TypeByName("ScreenSystem");
                var screenSystem = screenSystemType != null ? UnityEngine.Object.FindFirstObjectByType(screenSystemType) as Component : null;
                if (screenSystem != null)
                {
                    var screen = screenSystem.transform.Find("Camera Persp/ScreenPivot/Screen");
                    if (screen != null)
                    {
                        _pixelScreenRenderer = screen.GetComponent<MeshRenderer>();
                    }

                    if (_pixelScreenRenderer == null)
                    {
                        foreach (var mr in screenSystem.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            if (mr != null && string.Equals(mr.name, "Screen", StringComparison.OrdinalIgnoreCase))
                            {
                                _pixelScreenRenderer = mr;
                                break;
                            }
                        }
                    }
                }
            }

            if (_pixelDefaultRt == null && _pixelMainCamera != null && _pixelMainCamera.targetTexture != null && _pixelMainCamera.targetTexture != _pixelCustomRt)
            {
                _pixelDefaultRt = _pixelMainCamera.targetTexture;
            }

            if (_pixelDefaultRt == null && TryGetStableRenderTexture(_pixelScreenRenderer != null ? _pixelScreenRenderer.material.mainTexture : null, _pixelCustomRt, out RenderTexture screenRt))
            {
                _pixelDefaultRt = screenRt;
            }

            CaptureMainPixelationBaseDimensions();
        }

        private static bool TryGetActiveFreeCam(out Camera camera)
        {
            var freeCam = UnityEngine.Object.FindFirstObjectByType<sFreeCam>();
            camera = freeCam != null && freeCam.freeCamEnabled ? freeCam.cam : null;
            return camera != null;
        }

        private static bool TryGetGameplayCamera(out Camera camera)
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<sCameraController>();
            camera = controller != null ? controller.cam : null;
            return camera != null;
        }

        private static bool IsInactiveCamera(Camera camera)
        {
            return camera == null || !camera.gameObject.activeInHierarchy;
        }

        private static bool TryGetRaceCamera(out Camera camera)
        {
            camera = null;
            var raceManager = UnityEngine.Object.FindFirstObjectByType<RaceManager>();
            if (raceManager == null || raceManager.players == null || raceManager.players.Count == 0)
            {
                return false;
            }

            var player = raceManager.players[0];
            camera = player != null ? player.cam : null;
            return camera != null;
        }

        private static void SwitchMainPixelCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            RenderTexture oldCustom = _pixelCustomRt;
            if (_pixelMainCamera != null && _pixelCustomRt != null && _pixelMainCamera.targetTexture == _pixelCustomRt && _pixelDefaultRt != null)
            {
                _pixelMainCamera.targetTexture = _pixelDefaultRt;
                SetMainTexture(_pixelScreenRenderer, _pixelDefaultRt);
            }
            ReleaseCustomRt(ref _pixelCustomRt, _pixelDefaultRt);
            _pixelMainCamera = camera;
            if (camera.targetTexture != null && camera.targetTexture != oldCustom)
            {
                _pixelDefaultRt = camera.targetTexture;
            }
            _pixelLastMode = -1;
            CaptureMainPixelationBaseDimensions();
            Plugin.LogDebug($"Switched pixelation source camera to {camera.name} ({camera.GetInstanceID()}).");
        }

        private static void CaptureMainPixelationBaseDimensions()
        {
            if (_pixelBaseW > 0 && _pixelBaseH > 0)
            {
                return;
            }

            if (TryGetTextureSize(_pixelScreenRenderer != null ? _pixelScreenRenderer.material.mainTexture : null, out _pixelBaseW, out _pixelBaseH))
            {
                return;
            }

            if (_pixelDefaultRt != null && _pixelDefaultRt != _pixelCustomRt)
            {
                _pixelBaseW = _pixelDefaultRt.width;
                _pixelBaseH = _pixelDefaultRt.height;
                return;
            }

            _pixelBaseW = 456;
            _pixelBaseH = 256;
        }

        private static void CaptureRearPixelationBaseDimensions()
        {
            if (_pixelRearBaseW > 0 && _pixelRearBaseH > 0)
            {
                return;
            }

            if (TryGetTextureSize(_pixelRearRenderer != null ? _pixelRearRenderer.material.mainTexture : null, out _pixelRearBaseW, out _pixelRearBaseH))
            {
                return;
            }

            if (_pixelDefaultRearRt != null && _pixelDefaultRearRt != _pixelCustomRearRt)
            {
                _pixelRearBaseW = _pixelDefaultRearRt.width;
                _pixelRearBaseH = _pixelDefaultRearRt.height;
                return;
            }

            _pixelRearBaseW = 110;
            _pixelRearBaseH = 32;
        }

        private static bool TryGetTextureSize(Texture texture, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (texture == null || texture == _pixelCustomRt || texture == _pixelCustomRearRt || texture.width <= 0 || texture.height <= 0)
            {
                return false;
            }

            width = texture.width;
            height = texture.height;
            return true;
        }

        private static bool TryGetStableRenderTexture(Texture texture, RenderTexture custom, out RenderTexture rt)
        {
            rt = texture as RenderTexture;
            return rt != null && rt != custom && rt.width > 0 && rt.height > 0;
        }

        private static void GetScaledTarget(int mode, int baseW, int baseH, int fullW, int fullH, out int targetW, out int targetH)
        {
            switch (mode)
            {
                case 0:
                    targetW = fullW;
                    targetH = fullH;
                    break;
                case 1:
                    targetW = Mathf.Min(fullW, Mathf.Max(1, Mathf.RoundToInt(baseW * 2.25f)));
                    targetH = Mathf.Min(fullH, Mathf.Max(1, Mathf.RoundToInt(baseH * 2.25f)));
                    break;
                case 2:
                    targetW = Mathf.Min(fullW, Mathf.Max(1, Mathf.RoundToInt(baseW * 1.5f)));
                    targetH = Mathf.Min(fullH, Mathf.Max(1, Mathf.RoundToInt(baseH * 1.5f)));
                    break;
                case 4:
                    targetW = Mathf.Max(1, Mathf.RoundToInt(baseW * 0.75f));
                    targetH = Mathf.Max(1, Mathf.RoundToInt(baseH * 0.75f));
                    break;
                default:
                    targetW = baseW;
                    targetH = baseH;
                    break;
            }
        }

        private static void ApplyPixelationMain(int mode)
        {
            if (_pixelMainCamera == null || _pixelScreenRenderer == null || _pixelDefaultRt == null)
            {
                return;
            }

            if (mode == 3)
            {
                ReleaseCustomRt(ref _pixelCustomRt, _pixelDefaultRt);
                _pixelMainCamera.targetTexture = _pixelDefaultRt;
                SetMainTexture(_pixelScreenRenderer, _pixelDefaultRt);
                return;
            }

            int baseW = _pixelBaseW > 0 ? _pixelBaseW : _pixelDefaultRt.width;
            int baseH = _pixelBaseH > 0 ? _pixelBaseH : _pixelDefaultRt.height;
            GetMaxRtSizeForCurrentPresentation(out int fullW, out int fullH);
            GetScaledTarget(mode, baseW, baseH, fullW, fullH, out int targetW, out int targetH);

            if (_pixelCustomRt != null && _pixelCustomRt.width == targetW && _pixelCustomRt.height == targetH && _pixelLastMode == mode)
            {
                _pixelMainCamera.targetTexture = _pixelCustomRt;
                SetMainTexture(_pixelScreenRenderer, _pixelCustomRt);
                return;
            }

            _pixelCustomRt = CreateLike(_pixelCustomRt, _pixelDefaultRt, targetW, targetH);
            _pixelMainCamera.targetTexture = _pixelCustomRt;
            SetMainTexture(_pixelScreenRenderer, _pixelCustomRt);
        }

        private static void ApplyPixelationRear(int mode)
        {
            if (_pixelRearCamera == null || _pixelRearRenderer == null || _pixelDefaultRearRt == null)
            {
                return;
            }

            int mirrorQuality = _mirror ? GetMirrorQuality() : 0;
            if (mode == 3 && mirrorQuality == 0)
            {
                ReleaseCustomRt(ref _pixelCustomRearRt, _pixelDefaultRearRt);
                _pixelRearCamera.targetTexture = _pixelDefaultRearRt;
                SetMainTexture(_pixelRearRenderer, _pixelDefaultRearRt);
                return;
            }

            int baseW = _pixelRearBaseW > 0 ? _pixelRearBaseW : _pixelDefaultRearRt.width;
            int baseH = _pixelRearBaseH > 0 ? _pixelRearBaseH : _pixelDefaultRearRt.height;
            int targetW;
            int targetH;
            if (mirrorQuality > 0)
            {
                targetW = baseW * (1 << mirrorQuality);
                targetH = baseH * (1 << mirrorQuality);
            }
            else
            {
                int fullW = GetFullSize(_pixelRearCamera.pixelWidth, _pixelDefaultRearRt.width, GetScreenWidth());
                int fullH = GetFullSize(_pixelRearCamera.pixelHeight, _pixelDefaultRearRt.height, GetScreenHeight());
                // None caps the mirror at 4x its base instead of the full camera size.
                GetScaledTarget(mode, baseW, baseH, mode == 0 ? Mathf.Min(fullW, baseW * 4) : fullW, mode == 0 ? Mathf.Min(fullH, baseH * 4) : fullH, out targetW, out targetH);
            }

            if (_pixelCustomRearRt != null && _pixelCustomRearRt.width == targetW && _pixelCustomRearRt.height == targetH && _pixelLastMode == mode)
            {
                _pixelRearCamera.targetTexture = _pixelCustomRearRt;
                SetMainTexture(_pixelRearRenderer, _pixelCustomRearRt);
                return;
            }

            _pixelCustomRearRt = CreateLike(_pixelCustomRearRt, _pixelDefaultRearRt, targetW, targetH);
            _pixelRearCamera.targetTexture = _pixelCustomRearRt;
            SetMainTexture(_pixelRearRenderer, _pixelCustomRearRt);
        }

        private static int GetScreenWidth()
        {
            return Mathf.Max(1, Screen.width > 0 ? Screen.width : Screen.currentResolution.width);
        }

        private static int GetScreenHeight()
        {
            return Mathf.Max(1, Screen.height > 0 ? Screen.height : Screen.currentResolution.height);
        }

        private static int GetFullSize(int cameraPixels, int fallbackPixels, int screenPixels)
        {
            return cameraPixels > 0 ? cameraPixels : fallbackPixels > 0 ? fallbackPixels : screenPixels;
        }

        private static void GetMaxRtSizeForCurrentPresentation(out int width, out int height)
        {
            int screenW = GetScreenWidth();
            int screenH = GetScreenHeight();
            width = screenW;
            height = screenH;

            float targetAspect = OutputAspect != null ? OutputAspect() : screenW / (float)screenH;
            if (targetAspect <= 0.01f)
            {
                return;
            }

            if (screenW / (float)screenH >= targetAspect)
            {
                width = Mathf.Max(1, Mathf.RoundToInt(height * targetAspect));
            }
            else
            {
                height = Mathf.Max(1, Mathf.RoundToInt(width / targetAspect));
            }

            width = Mathf.Clamp(width, 1, 16384);
            height = Mathf.Clamp(height, 1, 16384);
        }

        private static void SetMainTexture(MeshRenderer renderer, Texture tex)
        {
            if (renderer == null || tex == null)
            {
                return;
            }

            // Use .material to match base game behavior.
            var mat = renderer.material;
            if (mat != null)
            {
                mat.mainTexture = tex;
            }
        }

        private static RenderTexture CreateLike(RenderTexture existing, RenderTexture like, int width, int height)
        {
            if (like == null)
            {
                return existing;
            }

            if (existing != null && existing.width == width && existing.height == height)
            {
                return existing;
            }

            if (existing != null && existing != like)
            {
                existing.Release();
            }

            var rt = new RenderTexture(width, height, like.depth)
            {
                format = like.format,
                antiAliasing = like.antiAliasing,
                filterMode = like.filterMode
            };
            rt.Create();
            return rt;
        }

        private static void ReleaseCustomRt(ref RenderTexture custom, RenderTexture defaultRt)
        {
            if (custom != null && custom != defaultRt)
            {
                custom.Release();
            }
            custom = null;
        }
    }
}
