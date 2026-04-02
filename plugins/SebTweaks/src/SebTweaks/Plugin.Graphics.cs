using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SebTweaks
{
    public sealed partial class Plugin
    {
        private enum VsyncMode
        {
            Default = 0,
            On = 1,
            Off = 2
        }

        private static bool _gfxInit;
        private static float _gfxNextPoll;

        private static int _lastPixelMode = -999;
        private static int _lastViewMode = -999;
        private static float _lastAppliedFov = -1f;
        private static bool _lastAppliedFirstPerson;

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
        private static int _pixelLastW;
        private static int _pixelLastH;
        private static int _pixelLastRearW;
        private static int _pixelLastRearH;

        private static bool _insideLookupInit;
        private static Type _ambianceType;
        private static FieldInfo _ambiancePlayerInsideField;
        private static UnityEngine.Object _ambianceInstance;

        private static Type _uwType;

        private void Update()
        {
            // Cheats: optional per-stat freeze enforcement.
            // Run every frame so values stay locked even if other game logic changes them.
            try
            {
                ApplyRefillFreezesIfEnabled();
            }
            catch
            {
                // ignore
            }

            // Poll at a low cadence; the actual heavy work only runs when values change.
            if (Time.unscaledTime < _gfxNextPoll)
            {
                return;
            }
            _gfxNextPoll = Time.unscaledTime + 0.25f;

            EnsureGraphicsInit();
            ApplyVsyncMode();
            ApplySavedFovIfNeeded();
            ApplyGraphicsModesIfChanged();
        }

        private static void ApplyRefillFreezesIfEnabled()
        {
            if (!IsInGameNow())
            {
                return;
            }

            bool freezeEnergy = GetInt(PrefKeyFreezeRefillEnergy, 0) == 1;
            bool freezeFuel = GetInt(PrefKeyFreezeRefillFuel, 0) == 1;
            bool freezeTemp = GetInt(PrefKeyFreezeRefillTemp, 0) == 1;
            if (!freezeEnergy && !freezeFuel && !freezeTemp)
            {
                return;
            }

            var hud = UnityEngine.Object.FindFirstObjectByType<sHUD>();
            if (hud == null)
            {
                return;
            }

            if (freezeEnergy && hud.energyCapacity > 0f)
            {
                float e01 = Mathf.Clamp01(GetFloat(PrefKeyRefillEnergy01, hud.energy / hud.energyCapacity));
                hud.energy = e01 * hud.energyCapacity;
            }

            if (freezeFuel && hud.fuelCapacity > 0f)
            {
                float f01 = Mathf.Clamp01(GetFloat(PrefKeyRefillFuel01, hud.fuel / hud.fuelCapacity));
                hud.fuel = f01 * hud.fuelCapacity;

                try
                {
                    if (hud.LowFuel())
                    {
                        hud.navigation.car.fuelScale = Mathf.Clamp01(hud.fuel / hud.fuelCapacity / 0.25f);
                    }
                    else
                    {
                        hud.navigation.car.fuelScale = 1f;
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (freezeTemp)
            {
                float limit = hud.temperatureLimit;
                if (limit > 0f)
                {
                    float t01 = Mathf.Clamp01(GetFloat(PrefKeyRefillTemp01, hud.temperature / limit));
                    hud.temperature = Mathf.Clamp(t01 * limit, 0f, limit);
                }
            }
        }

        private static void EnsureGraphicsInit()
        {
            if (_gfxInit)
            {
                return;
            }
            _gfxInit = true;

            try
            {
                // Force initial apply even if prefs were set by another mod/menu.
                _lastPixelMode = -999;
                _lastViewMode = -999;
            }
            catch
            {
                // ignore
            }
        }

        internal static void ResetGraphicsDefaults()
        {
            // Shared keys (match SebUltrawide).
            PlayerPrefs.DeleteKey(PrefKeyFovFirstPerson);
            PlayerPrefs.DeleteKey(PrefKeyFovThirdPerson);
            PlayerPrefs.DeleteKey(PrefKeyFovLegacy);

            SavePixelationMode(3); // Default
            SaveViewDistanceMode(1); // Default

            // SebTweaks-only.
            SetInt(PrefKeyGfxVsyncMode, (int)VsyncMode.Default);

            PlayerPrefs.Save();
        }

        internal static int GetVsyncMode()
        {
            return Mathf.Clamp(GetInt(PrefKeyGfxVsyncMode, 0), 0, 2);
        }

        internal static void SetVsyncMode(int mode)
        {
            SetInt(PrefKeyGfxVsyncMode, Mathf.Clamp(mode, 0, 2));
        }

        private static void ApplyVsyncMode()
        {
            int mode = GetVsyncMode();
            try
            {
                if (mode == (int)VsyncMode.On)
                {
                    QualitySettings.vSyncCount = 1;
                }
                else if (mode == (int)VsyncMode.Off)
                {
                    QualitySettings.vSyncCount = 0;
                }
            }
            catch
            {
                // ignore
            }
        }

        internal static int GetViewDistanceMode()
        {
            int raw = PlayerPrefs.GetInt(PrefKeyViewDistanceMode, 1);
            int version = PlayerPrefs.GetInt(PrefKeyViewDistanceModeVersion, 1);

            // v1 mapping: 0 Default, 1 Far, 2 Farther, 3 Distant, 4 Max
            // v2 mapping: 0 Near, 1 Default, 2 Far, 3 Max
            if (version < 2 && PlayerPrefs.HasKey(PrefKeyViewDistanceMode))
            {
                int migrated;
                switch (raw)
                {
                    case 1:
                        migrated = 0; // old Far -> Near
                        break;
                    case 3:
                        migrated = 2; // old Distant -> Far
                        break;
                    case 4:
                        migrated = 3; // old Max -> Max
                        break;
                    case 0:
                    case 2:
                    default:
                        migrated = 1; // old Default/Farther/other -> Default
                        break;
                }

                raw = migrated;
                PlayerPrefs.SetInt(PrefKeyViewDistanceModeVersion, 2);
                PlayerPrefs.SetInt(PrefKeyViewDistanceMode, raw);
            }

            return Mathf.Clamp(raw, 0, 3);
        }

        internal static void SaveViewDistanceMode(int mode)
        {
            mode = Mathf.Clamp(mode, 0, 3);

            if (TryInvokeUltrawideStatic("SaveViewDistanceMode", mode))
            {
                return;
            }

            PlayerPrefs.SetInt(PrefKeyViewDistanceModeVersion, 2);
            PlayerPrefs.SetInt(PrefKeyViewDistanceMode, mode);
            RefreshViewDistance();
        }

        internal static int GetPixelationMode()
        {
            int raw = PlayerPrefs.GetInt(PrefKeyPixelationMode, 3);
            int version = PlayerPrefs.GetInt(PrefKeyPixelationModeVersion, 1);

            // v1 mapping: 0 Disable, 1 Fine, 2 Default, 3 Large
            // v2 mapping: 0 None, 1 Finer, 2 Fine, 3 Default, 4 Large
            if (version < 2 && PlayerPrefs.HasKey(PrefKeyPixelationMode))
            {
                int migrated = raw;
                switch (raw)
                {
                    case 0:
                        migrated = 0;
                        break;
                    case 1:
                        migrated = 2;
                        break;
                    case 2:
                        migrated = 3;
                        break;
                    case 3:
                        migrated = 4;
                        break;
                }
                raw = migrated;
                PlayerPrefs.SetInt(PrefKeyPixelationModeVersion, 2);
                PlayerPrefs.SetInt(PrefKeyPixelationMode, raw);
            }

            return Mathf.Clamp(raw, 0, 4);
        }

        internal static void SavePixelationMode(int mode)
        {
            mode = Mathf.Clamp(mode, 0, 4);

            if (TryInvokeUltrawideStatic("SavePixelationMode", mode))
            {
                return;
            }

            PlayerPrefs.SetInt(PrefKeyPixelationModeVersion, 2);
            PlayerPrefs.SetInt(PrefKeyPixelationMode, mode);
            RefreshPixelation();
        }

        internal static float GetSavedFovOrDefault(bool firstPerson, float fallback)
        {
            return TryGetSavedFov(firstPerson, out float fov) ? fov : fallback;
        }

        internal static void SaveFovOverride(bool firstPerson, float fov)
        {
            if (TryInvokeUltrawideStatic("SaveFovOverride", firstPerson, fov))
            {
                return;
            }

            PlayerPrefs.SetFloat(firstPerson ? PrefKeyFovFirstPerson : PrefKeyFovThirdPerson, fov);
            if (IsFirstPersonViewActive() == firstPerson)
            {
                ApplyFovOverride(fov);
            }
        }

        private static bool TryGetSavedFov(bool firstPerson, out float fov)
        {
            string key = firstPerson ? PrefKeyFovFirstPerson : PrefKeyFovThirdPerson;
            fov = PlayerPrefs.GetFloat(key, -1f);
            if (fov >= 1f)
            {
                return true;
            }

            if (!firstPerson)
            {
                fov = PlayerPrefs.GetFloat(PrefKeyFovLegacy, -1f);
                if (fov >= 1f)
                {
                    return true;
                }
            }

            fov = 0f;
            return false;
        }

        private static bool IsFirstPersonViewActive()
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<sCameraController>();
            return controller != null && controller.firstPerson && !controller.fixedPerspective;
        }

        private static void ApplySavedFovIfNeeded()
        {
            bool firstPerson = IsFirstPersonViewActive();
            if (!TryGetSavedFov(firstPerson, out float savedFov))
            {
                _lastAppliedFov = -1f;
                _lastAppliedFirstPerson = firstPerson;
                return;
            }

            if (_lastAppliedFov >= 1f && Mathf.Abs(_lastAppliedFov - savedFov) < 0.001f && _lastAppliedFirstPerson == firstPerson)
            {
                return;
            }

            _lastAppliedFirstPerson = firstPerson;
            _lastAppliedFov = savedFov;
            ApplyFovOverride(savedFov);
        }

        private static void ApplyFovOverride(float fov)
        {
            if (IsPlayerInsideBuilding())
            {
                return;
            }

            var pauseSystem = PauseSystem.pauseSystem;
            if (pauseSystem != null && pauseSystem.mainCamera != null)
            {
                PauseSystem.FOV = fov;
                pauseSystem.mainCamera.fieldOfView = fov;
                return;
            }

            PauseSystem.FOV = fov;

            var cam = Camera.main;
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

        private static void ApplyGraphicsModesIfChanged()
        {
            int pixelMode;
            int viewMode;
            try
            {
                pixelMode = GetPixelationMode();
                viewMode = GetViewDistanceMode();
            }
            catch
            {
                return;
            }

            if (pixelMode != _lastPixelMode)
            {
                _lastPixelMode = pixelMode;
                RefreshPixelation();
            }

            if (viewMode != _lastViewMode)
            {
                _lastViewMode = viewMode;
                RefreshViewDistance();
            }
        }

        private static void CaptureViewDistanceBaseValues()
        {
            if (_viewDistanceBaseCaptured)
            {
                return;
            }

            _baseShadowDistance = QualitySettings.shadowDistance;
            _baseLodBias = QualitySettings.lodBias;
            _baseMaximumLodLevel = QualitySettings.maximumLODLevel;
            _viewDistanceBaseCaptured = true;
        }

        internal static void RefreshViewDistance()
        {
            try
            {
                CaptureViewDistanceBaseValues();
                ApplyViewDistanceQuality(GetViewDistanceMode());
            }
            catch
            {
                // ignore
            }
        }

        private static void ApplyViewDistanceQuality(int mode)
        {
            if (!_viewDistanceBaseCaptured)
            {
                return;
            }

            if (mode == 1)
            {
                QualitySettings.shadowDistance = _baseShadowDistance;
                QualitySettings.lodBias = _baseLodBias;
                QualitySettings.maximumLODLevel = _baseMaximumLodLevel;
                return;
            }

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
                    shadowDistance = 3000f;
                    lodBias = 6.0f;
                    break;
                default:
                    shadowDistance = _baseShadowDistance;
                    lodBias = _baseLodBias;
                    break;
            }

            QualitySettings.shadowDistance = shadowDistance;
            QualitySettings.lodBias = lodBias;
            QualitySettings.maximumLODLevel = 0;
        }

        internal static void ApplyViewDistanceFarClip(sCameraController controller)
        {
            if (controller == null || controller.cam == null)
            {
                return;
            }

            int mode = GetViewDistanceMode();
            if (mode == 1)
            {
                return;
            }

            float far;
            switch (mode)
            {
                case 0:
                    far = 3000f;
                    break;
                case 2:
                    far = 25000f;
                    break;
                case 3:
                    far = 100000f;
                    break;
                default:
                    far = 10000f;
                    break;
            }

            if (far > controller.cam.nearClipPlane + 1f)
            {
                controller.cam.farClipPlane = far;
            }
        }

        internal static void RefreshPixelation()
        {
            try
            {
                EnsurePixelationTargets();
                ApplyPixelation();
            }
            catch
            {
                // Experimental: never hard-fail due to this feature.
            }
        }

        private static void EnsurePixelationTargets()
        {
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
                            if (candidates != null)
                            {
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

            if (_pixelDefaultRt == null && _pixelMainCamera != null && _pixelMainCamera.targetTexture != null)
            {
                _pixelDefaultRt = _pixelMainCamera.targetTexture;
            }

            if (_pixelDefaultRearRt == null && _pixelRearCamera != null && _pixelRearCamera.targetTexture != null)
            {
                _pixelDefaultRearRt = _pixelRearCamera.targetTexture;
            }
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

            if (mode == 3)
            {
                ReleaseCustomRt(ref _pixelCustomRt, _pixelDefaultRt);
                _pixelMainCamera.targetTexture = _pixelDefaultRt;
                SetMainTexture(_pixelScreenRenderer, _pixelDefaultRt);
                return;
            }

            int fullW = GetFullWidth();
            int fullH = GetFullHeight();

            int targetW;
            int targetH;
            switch (mode)
            {
                case 0:
                    targetW = fullW;
                    targetH = fullH;
                    break;
                case 1:
                    targetW = Mathf.Min(fullW, Mathf.Max(1, Mathf.RoundToInt(_pixelDefaultRt.width * 2.25f)));
                    targetH = Mathf.Min(fullH, Mathf.Max(1, Mathf.RoundToInt(_pixelDefaultRt.height * 2.25f)));
                    break;
                case 2:
                    targetW = Mathf.Min(fullW, Mathf.Max(1, Mathf.RoundToInt(_pixelDefaultRt.width * 1.5f)));
                    targetH = Mathf.Min(fullH, Mathf.Max(1, Mathf.RoundToInt(_pixelDefaultRt.height * 1.5f)));
                    break;
                case 4:
                    targetW = Mathf.Max(1, Mathf.RoundToInt(_pixelDefaultRt.width * 0.75f));
                    targetH = Mathf.Max(1, Mathf.RoundToInt(_pixelDefaultRt.height * 0.75f));
                    break;
                default:
                    targetW = _pixelDefaultRt.width;
                    targetH = _pixelDefaultRt.height;
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

            if (mode == 3)
            {
                ReleaseCustomRt(ref _pixelCustomRearRt, _pixelDefaultRearRt);
                _pixelRearCamera.targetTexture = _pixelDefaultRearRt;
                SetMainTexture(_pixelRearRenderer, _pixelDefaultRearRt);
                return;
            }

            int fullW = GetFullWidth(_pixelRearCamera, _pixelDefaultRearRt);
            int fullH = GetFullHeight(_pixelRearCamera, _pixelDefaultRearRt);

            int baseW = _pixelDefaultRearRt.width;
            int baseH = _pixelDefaultRearRt.height;

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

        private static void SetMainTexture(MeshRenderer renderer, Texture tex)
        {
            if (renderer == null || tex == null)
            {
                return;
            }

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

        private static bool TryInvokeUltrawideStatic(string method, params object[] args)
        {
            try
            {
                _uwType ??= AccessTools.TypeByName("SebUltrawide.Plugin");
                if (_uwType == null)
                {
                    return false;
                }
                var m = _uwType.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (m == null)
                {
                    return false;
                }
                m.Invoke(null, args);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(sCameraController), "Update")]
        private static class SCameraController_Update_Postfix_Graphics
        {
            [HarmonyPostfix]
            private static void Postfix(sCameraController __instance)
            {
                try
                {
                    ApplyViewDistanceFarClip(__instance);
                }
                catch
                {
                    // ignore
                }
            }
        }

    }
}
