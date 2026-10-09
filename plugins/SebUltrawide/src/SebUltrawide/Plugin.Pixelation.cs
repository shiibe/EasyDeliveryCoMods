using System;
using HarmonyLib;
using UnityEngine;

namespace SebUltrawide
{
    public partial class Plugin
    {
        private static Camera _pixelMainCamera;
        private static MeshRenderer _pixelScreenRenderer;
        private static RenderTexture _pixelDefaultRt;
        private static RenderTexture _pixelCustomRt;

        private static Camera _pixelRearCamera;
        private static MeshRenderer _pixelRearRenderer;
        private static RenderTexture _pixelDefaultRearRt;
        private static RenderTexture _pixelCustomRearRt;

        private static int _pixelLastMode = -1;
        private static int _pixelLastW;
        private static int _pixelLastH;
        private static int _pixelLastRearW;
        private static int _pixelLastRearH;
        private static int _pixelBaseW;
        private static int _pixelBaseH;
        private static int _pixelRearBaseW;
        private static int _pixelRearBaseH;

        internal static void RefreshPixelation()
        {
            if (!ShouldApply())
            {
                return;
            }

            try
            {
                EnsurePixelationTargets();
                ApplyPixelation();
            }
            catch
            {
                // Experimental: never hard-fail the mod due to this feature.
            }
        }

        private static void EnsurePixelationTargets()
        {
            bool hasActiveFreeCam = TryGetActiveFreeCam(out Camera freeCam);
            if (hasActiveFreeCam && freeCam != _pixelMainCamera)
            {
                SwitchMainPixelCamera(freeCam);
            }

            if (hasActiveFreeCam)
            {
                ApplyFreeCamAspect(freeCam);
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
                var controller = UnityEngine.Object.FindFirstObjectByType<sCameraController>();
                if (controller != null)
                {
                    _pixelMainCamera = controller.cam;
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
                if (screenSystemType != null)
                {
                    var screenSystem = UnityEngine.Object.FindFirstObjectByType(screenSystemType) as Component;
                    if (screenSystem != null)
                    {
                        var screen = screenSystem.transform.Find("Camera Persp/ScreenPivot/Screen");
                        if (screen != null)
                        {
                            _pixelScreenRenderer = screen.GetComponent<MeshRenderer>();
                        }

                        if (_pixelScreenRenderer == null)
                        {
                            var candidates = screenSystem.GetComponentsInChildren<MeshRenderer>(true);
                            foreach (var mr in candidates)
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
            }

            if (_pixelRearCamera == null || _pixelRearRenderer == null)
            {
                var car = UnityEngine.Object.FindFirstObjectByType<sCarController>();
                if (car != null)
                {
                    if (_pixelRearRenderer == null)
                    {
                        var mirror = car.transform.Find("carInt/RearViewMirror");
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

            if (_pixelDefaultRt == null && _pixelMainCamera != null && _pixelMainCamera.targetTexture != null && _pixelMainCamera.targetTexture != _pixelCustomRt)
            {
                _pixelDefaultRt = _pixelMainCamera.targetTexture;
            }

            if (_pixelDefaultRt == null && TryGetStableRenderTexture(_pixelScreenRenderer != null ? _pixelScreenRenderer.material.mainTexture : null, _pixelCustomRt, out RenderTexture screenRt))
            {
                _pixelDefaultRt = screenRt;
            }

            if (_pixelDefaultRearRt == null && _pixelRearCamera != null && _pixelRearCamera.targetTexture != null && _pixelRearCamera.targetTexture != _pixelCustomRearRt)
            {
                _pixelDefaultRearRt = _pixelRearCamera.targetTexture;
            }

            if (_pixelDefaultRearRt == null && TryGetStableRenderTexture(_pixelRearRenderer != null ? _pixelRearRenderer.material.mainTexture : null, _pixelCustomRearRt, out RenderTexture rearRt))
            {
                _pixelDefaultRearRt = rearRt;
            }

            CapturePixelationBaseDimensions();
        }

        private static bool TryGetActiveFreeCam(out Camera camera)
        {
            camera = null;

            var freeCam = UnityEngine.Object.FindFirstObjectByType<sFreeCam>();
            if (freeCam == null || !freeCam.freeCamEnabled || freeCam.cam == null)
            {
                return false;
            }

            camera = freeCam.cam;
            return true;
        }

        private static bool TryGetGameplayCamera(out Camera camera)
        {
            camera = null;

            var controller = UnityEngine.Object.FindFirstObjectByType<sCameraController>();
            if (controller == null || controller.cam == null)
            {
                return false;
            }

            camera = controller.cam;
            return true;
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
            if (player == null || player.cam == null)
            {
                return false;
            }

            camera = player.cam;
            return true;
        }

        private static void SwitchMainPixelCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            RenderTexture oldCustom = _pixelCustomRt;
            RestoreMainPixelTarget();
            ReleaseCustomRt(ref _pixelCustomRt, _pixelDefaultRt);
            _pixelMainCamera = camera;
            if (camera.targetTexture != null && camera.targetTexture != oldCustom)
            {
                _pixelDefaultRt = camera.targetTexture;
            }
            _pixelLastMode = -1;
            _pixelLastW = 0;
            _pixelLastH = 0;
            CapturePixelationBaseDimensions();
            LogDebug($"Switched pixelation source camera to {camera.name} ({camera.GetInstanceID()}).");
        }

        private static void RestoreMainPixelTarget()
        {
            if (_pixelMainCamera != null && _pixelCustomRt != null && _pixelMainCamera.targetTexture == _pixelCustomRt && _pixelDefaultRt != null)
            {
                _pixelMainCamera.targetTexture = _pixelDefaultRt;
                if (_pixelScreenRenderer != null)
                {
                    SetMainTexture(_pixelScreenRenderer, _pixelDefaultRt);
                }
            }
        }

        private static void CapturePixelationBaseDimensions()
        {
            CaptureMainPixelationBaseDimensions();
            CaptureRearPixelationBaseDimensions();
        }

        private static void CaptureMainPixelationBaseDimensions()
        {
            if (_pixelBaseW > 0 && _pixelBaseH > 0)
            {
                return;
            }

            if (TryGetTextureSize(_pixelScreenRenderer != null ? _pixelScreenRenderer.material.mainTexture : null, out int w, out int h))
            {
                _pixelBaseW = w;
                _pixelBaseH = h;
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

            if (TryGetTextureSize(_pixelRearRenderer != null ? _pixelRearRenderer.material.mainTexture : null, out int w, out int h))
            {
                _pixelRearBaseW = w;
                _pixelRearBaseH = h;
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

        private static void ApplyPixelation()
        {
            int mode = GetPixelationMode();
            ApplyPixelationMain(mode);
            ApplyPixelationRear(mode);
            _pixelLastMode = mode;
        }

        private static void ApplyPixelationMain(int mode)
        {
            if (_pixelMainCamera == null || _pixelScreenRenderer == null || _pixelDefaultRt == null)
            {
                return;
            }

            CaptureMainPixelationBaseDimensions();
            int baseW = _pixelBaseW > 0 ? _pixelBaseW : _pixelDefaultRt.width;
            int baseH = _pixelBaseH > 0 ? _pixelBaseH : _pixelDefaultRt.height;

            if (mode == 3)
            {
                ReleaseCustomRt(ref _pixelCustomRt, _pixelDefaultRt);
                _pixelMainCamera.targetTexture = _pixelDefaultRt;
                SetMainTexture(_pixelScreenRenderer, _pixelDefaultRt);
                return;
            }

            GetMaxRtSizeForCurrentPresentation(out int fullW, out int fullH);

            int targetW;
            int targetH;
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

            if (_pixelCustomRt != null && _pixelCustomRt.width == targetW && _pixelCustomRt.height == targetH && _pixelLastMode == mode)
            {
                _pixelMainCamera.targetTexture = _pixelCustomRt;
                SetMainTexture(_pixelScreenRenderer, _pixelCustomRt);
                return;
            }

            if (_pixelLastW == targetW && _pixelLastH == targetH && _pixelLastMode == mode && _pixelMainCamera.targetTexture != null)
            {
                _pixelMainCamera.targetTexture = _pixelMainCamera.targetTexture;
            }

            _pixelLastW = targetW;
            _pixelLastH = targetH;

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

            CaptureRearPixelationBaseDimensions();

            int mirrorQuality = BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("shibe.easydeliveryco.sebtruck", out var truck) &&
                truck.Instance != null && truck.Instance.isActiveAndEnabled
                ? Mathf.Clamp(SebCore.ModPrefs.GetInt("SebTruck_MirrorQuality", 0), 0, 3) : 0;

            if (mode == 3 && mirrorQuality == 0)
            {
                ReleaseCustomRt(ref _pixelCustomRearRt, _pixelDefaultRearRt);
                _pixelRearCamera.targetTexture = _pixelDefaultRearRt;
                SetMainTexture(_pixelRearRenderer, _pixelDefaultRearRt);
                return;
            }

            int fullW = GetFullWidth(_pixelRearCamera, _pixelDefaultRearRt);
            int fullH = GetFullHeight(_pixelRearCamera, _pixelDefaultRearRt);

            int baseW = _pixelRearBaseW > 0 ? _pixelRearBaseW : _pixelDefaultRearRt.width;
            int baseH = _pixelRearBaseH > 0 ? _pixelRearBaseH : _pixelDefaultRearRt.height;

            int targetW;
            int targetH;
            switch (mode)
            {
                case 0:
                    targetW = Mathf.Min(fullW, baseW * 4);
                    targetH = Mathf.Min(fullH, baseH * 4);
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

            if (mirrorQuality > 0)
            {
                targetW = baseW * (1 << mirrorQuality);
                targetH = baseH * (1 << mirrorQuality);
            }

            if (_pixelCustomRearRt != null && _pixelCustomRearRt.width == targetW && _pixelCustomRearRt.height == targetH && _pixelLastMode == mode)
            {
                _pixelRearCamera.targetTexture = _pixelCustomRearRt;
                SetMainTexture(_pixelRearRenderer, _pixelCustomRearRt);
                return;
            }

            _pixelLastRearW = targetW;
            _pixelLastRearH = targetH;

            _pixelCustomRearRt = CreateLike(_pixelCustomRearRt, _pixelDefaultRearRt, targetW, targetH);
            _pixelRearCamera.targetTexture = _pixelCustomRearRt;
            SetMainTexture(_pixelRearRenderer, _pixelCustomRearRt);
        }

        private static int GetFullWidth()
        {
            return Mathf.Max(1, Screen.width > 0 ? Screen.width : Screen.currentResolution.width);
        }

        private static int GetFullHeight()
        {
            return Mathf.Max(1, Screen.height > 0 ? Screen.height : Screen.currentResolution.height);
        }

        private static int GetFullWidth(Camera cam, RenderTexture fallback)
        {
            if (cam != null && cam.pixelWidth > 0)
            {
                return cam.pixelWidth;
            }

            if (fallback != null && fallback.width > 0)
            {
                return fallback.width;
            }

            return GetFullWidth();
        }

        private static int GetFullHeight(Camera cam, RenderTexture fallback)
        {
            if (cam != null && cam.pixelHeight > 0)
            {
                return cam.pixelHeight;
            }

            if (fallback != null && fallback.height > 0)
            {
                return fallback.height;
            }

            return GetFullHeight();
        }

        private static void GetMaxRtSizeForCurrentPresentation(out int width, out int height)
        {
            int screenW = GetFullWidth();
            int screenH = GetFullHeight();
            width = screenW;
            height = screenH;

            float targetAspect = GetPixelationOutputAspect();
            if (screenW <= 0 || screenH <= 0 || targetAspect <= 0.01f)
            {
                return;
            }

            float screenAspect = screenW / (float)screenH;
            if (screenAspect >= targetAspect)
            {
                height = screenH;
                width = Mathf.Max(1, Mathf.RoundToInt(height * targetAspect));
            }
            else
            {
                width = screenW;
                height = Mathf.Max(1, Mathf.RoundToInt(width / targetAspect));
            }

            width = Mathf.Clamp(width, 1, 16384);
            height = Mathf.Clamp(height, 1, 16384);
        }

        private static float GetPixelationOutputAspect()
        {
            if (IsVanillaPresentation())
            {
                return DefaultAspect;
            }

            float a = GetTargetAspect();
            if (a > 0.01f)
            {
                return a;
            }

            return GetWindowAspect();
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
                antiAliasing = like.antiAliasing
            };
            rt.filterMode = like.filterMode;
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
