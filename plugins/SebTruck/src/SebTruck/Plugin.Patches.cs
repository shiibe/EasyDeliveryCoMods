using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SebBinds;
using UnityEngine;

namespace SebTruck
{
    public partial class Plugin
    {
        private static sCarController _currentCar;
        private static int _currentCarFrame = -1;
        private static bool _isInWalkingMode;
        private static float _currentSpeedKmh;
        private static float _currentSpeedMult = 1f;
        private static float _lastThrottle01;
        private static float _neutralRev01;

        private static float _ignitionHoldStart = -1f;
        private static bool _ignitionHoldConsumed;
        private static bool _ignitionHoldWasDown;
        private static bool _ignitionIgnoreHoldUntilRelease;
        private static float _ignitionOffSince = -1f;
        private static bool _ignitionPrevHeadlightsOn;
        private static bool _ignitionPrevRadioOn;

        private enum IndicatorMode
        {
            Off = 0,
            Left = 1,
            Right = 2,
            Hazards = 3
        }

        private static IndicatorMode _indicatorMode;
        private static bool _indicatorBlinkOn;
        private static float _indicatorNextBlinkTime;
        private static float _indicatorPauseStart = -1f;

        private const float IndicatorCancelArmThreshold = 0.35f;
        private const float IndicatorCancelCenterThreshold = 0.12f;
        private static bool _indicatorCancelArmed;
        private static IndicatorMode _indicatorCancelArmedForMode;

        private static readonly BindingScheme[] _bindingSchemes = { BindingScheme.Controller, BindingScheme.Keyboard, BindingScheme.Wheel };

        private sealed class IndicatorCache
        {
            public readonly List<Light> Left = new List<Light>();
            public readonly List<Light> Right = new List<Light>();
            public readonly Dictionary<int, (float intensity, bool enabled)> Defaults = new Dictionary<int, (float intensity, bool enabled)>();

            public readonly List<Material> LeftMats = new List<Material>();
            public readonly List<Material> RightMats = new List<Material>();
            public readonly Dictionary<int, (Color emissionColor, Color emissiveColor, bool emissionKeyword)> MatDefaults =
                new Dictionary<int, (Color emissionColor, Color emissiveColor, bool emissionKeyword)>();
        }

        private static readonly Dictionary<int, IndicatorCache> _indicatorCaches = new Dictionary<int, IndicatorCache>();

        private static readonly Dictionary<int, bool> _truckBraking = new Dictionary<int, bool>();

        private static readonly Dictionary<int, bool> _isTruckCarById = new Dictionary<int, bool>();

        private sealed class TurnSignalLightRig
        {
            public GameObject Root;
            public Light FL;
            public Light FR;
            public Light RL;
            public Light RR;

            public bool BaseComputed;
            public Vector3 BaseFL;
            public Vector3 BaseFR;
            public Vector3 BaseRL;
            public Vector3 BaseRR;
        }

        private static readonly Dictionary<int, TurnSignalLightRig> _turnSignalLightRigs = new Dictionary<int, TurnSignalLightRig>();

        private sealed class TailLightCache
        {
            public readonly List<Light> Lights = new List<Light>();
            public readonly Dictionary<int, (float intensity, bool enabled)> Defaults = new Dictionary<int, (float intensity, bool enabled)>();

            public readonly List<Component> Flares = new List<Component>();
            public readonly Dictionary<int, (float intensity, bool enabled)> FlareDefaults = new Dictionary<int, (float intensity, bool enabled)>();
        }

        private static readonly Dictionary<int, TailLightCache> _tailLightCaches = new Dictionary<int, TailLightCache>();

        private static readonly Dictionary<int, (float maxSpeedScale, float drivePowerScale)> _carScaleDefaults =
            new Dictionary<int, (float maxSpeedScale, float drivePowerScale)>();

        private static readonly Dictionary<int, (float intensity, float range)> _lightDefaults =
            new Dictionary<int, (float intensity, float range)>();

        private static Type _engineSfxRuntimeType;
        private static FieldInfo _engineCarField;
        private static FieldInfo _engineIdleField;
        private static FieldInfo _engineDriveField;
        private static FieldInfo _engineIntenseField;
        private static FieldInfo _engineDistortionField;

        private static FieldInfo _radioTargetVolumeField;
        private static FieldInfo _radioRadioVolumeField;
        private static FieldInfo _radioSourceField;
        private static FieldInfo _radioNoiseField;

        private static readonly Dictionary<int, float> _enginePitchMulApplied = new Dictionary<int, float>();

        private static Type _headlightsRuntimeType;
        private static FieldInfo _headlightsHeadLightsField;
        private static FieldInfo _headlightsCarMatField;
        private static FieldInfo _headlightsEmissiveRegularField;
        private static FieldInfo _headlightsHeadlightsOnField;
        private static FieldInfo _headlightsModelField;
        private static Texture2D _ignitionBlackEmissiveTex;

        private static readonly Dictionary<string, Material> _paintMaterialCache = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        private struct PaintDefaults
        {
            public Texture baseMap;
            public Texture mainTex;
        }

        private static readonly Dictionary<int, PaintDefaults> _paintDefaultsByCarId = new Dictionary<int, PaintDefaults>();
        private static readonly Dictionary<int, float> _paintNextTailgateRescanTimeByCarId = new Dictionary<int, float>();
        private static FieldInfo _carDamageCarMatField;
        private static int _paintLastCarId;
        private static int _paintLastIndex = int.MinValue;

        private sealed class HeadlightTuningCache
        {
            public readonly List<Light> Lights = new List<Light>(16);
            public float NextRescanTime;
        }

        private static readonly Dictionary<int, HeadlightTuningCache> _headlightTuningCaches = new Dictionary<int, HeadlightTuningCache>();
        private static readonly HashSet<int> _tmpHeadlightLightIds = new HashSet<int>();

        private static void EnsureHeadlightsRefs(object instance)
        {
            if (instance == null)
            {
                return;
            }

            var t = instance.GetType();
            if (_headlightsRuntimeType == t)
            {
                return;
            }

            _headlightsRuntimeType = t;
            _headlightsHeadLightsField = AccessTools.Field(t, "headLights");
            _headlightsCarMatField = AccessTools.Field(t, "carMat");
            _headlightsEmissiveRegularField = AccessTools.Field(t, "emissiveRegular");
            _headlightsHeadlightsOnField = AccessTools.Field(t, "headlightsOn");
            _headlightsModelField = AccessTools.Field(t, "model");
        }

        private static void EnsureRadioRefs()
        {
            if (_radioTargetVolumeField != null)
            {
                return;
            }

            _radioTargetVolumeField = AccessTools.Field(typeof(sRadioSystem), "targetVolume");
            _radioRadioVolumeField = AccessTools.Field(typeof(sRadioSystem), "radioVolume");
            _radioSourceField = AccessTools.Field(typeof(sRadioSystem), "source");
            _radioNoiseField = AccessTools.Field(typeof(sRadioSystem), "noise");
        }

        private static void EnsureCarDamageRefs()
        {
            if (_carDamageCarMatField != null)
            {
                return;
            }
            try
            {
                _carDamageCarMatField = AccessTools.Field(typeof(CarDamage), "carMat");
            }
            catch
            {
                _carDamageCarMatField = null;
            }
        }

        private static Material FindMaterialByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string key = name.Trim();
            if (_paintMaterialCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            try
            {
                var all = Resources.FindObjectsOfTypeAll<Material>();
                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        var m = all[i];
                        if (m != null && string.Equals(m.name, key, StringComparison.OrdinalIgnoreCase))
                        {
                            _paintMaterialCache[key] = m;
                            return m;
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            _paintMaterialCache[key] = null;
            return null;
        }

        private static string DeriveTruckPaintMaterialName(string cosmeticName)
        {
            if (string.IsNullOrWhiteSpace(cosmeticName))
            {
                return string.Empty;
            }

            string n = cosmeticName.Trim();
            if (n.Length == 0)
            {
                return string.Empty;
            }

            // If the cosmetic name already matches a material name, keep it.
            if (n.StartsWith("truck_texture", StringComparison.OrdinalIgnoreCase))
            {
                return n;
            }

            // Cosmetics are named like "Blue Paint" but materials are like "truck_texture_blue".
            if (n.EndsWith(" paint", StringComparison.OrdinalIgnoreCase))
            {
                n = n.Substring(0, n.Length - 5).Trim();
            }

            string token = n.ToLowerInvariant().Replace(' ', '_');

            // Default/white paint uses the base material.
            if (token == "default" || token == "white")
            {
                return "truck_texture";
            }

            return "truck_texture_" + token;
        }

        private static bool IsTailgateRenderer(MeshRenderer mr)
        {
            if (mr == null)
            {
                return false;
            }
            try
            {
                string n = mr.name;
                if (!string.IsNullOrWhiteSpace(n) && n.IndexOf("tailgate", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            catch
            {
                // ignore
            }
            return false;
        }

        private static int ComputeTailgateSignature(sCarController car)
        {
            if (car == null)
            {
                return 0;
            }

            int hash = 17;
            try
            {
                var mrs = car.GetComponentsInChildren<MeshRenderer>(true);
                if (mrs != null)
                {
                    for (int i = 0; i < mrs.Length; i++)
                    {
                        var mr = mrs[i];
                        if (!IsTailgateRenderer(mr))
                        {
                            continue;
                        }
                        hash = unchecked(hash * 31 + mr.GetInstanceID());
                        hash = unchecked(hash * 31 + (mr.gameObject != null && mr.gameObject.activeInHierarchy ? 1 : 0));
                    }
                }
            }
            catch
            {
                // ignore
            }
            return hash;
        }

        private static bool IsTruckPaintMaterial(Material m)
        {
            if (m == null)
            {
                return false;
            }
            try
            {
                return m.name != null && m.name.IndexOf("truck_texture", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyTailgatePaintMaterialIfPresent(sCarController car, Material paintMat)
        {
            if (car == null || paintMat == null)
            {
                return;
            }

            try
            {
                var mrs = car.GetComponentsInChildren<MeshRenderer>(true);
                if (mrs == null)
                {
                    return;
                }

                for (int i = 0; i < mrs.Length; i++)
                {
                    var mr = mrs[i];
                    if (!IsTailgateRenderer(mr))
                    {
                        continue;
                    }

                    var sms = mr.sharedMaterials;
                    if (sms == null || sms.Length == 0)
                    {
                        continue;
                    }

                    bool changed = false;
                    for (int j = 0; j < sms.Length; j++)
                    {
                        if (IsTruckPaintMaterial(sms[j]))
                        {
                            if (!ReferenceEquals(sms[j], paintMat))
                            {
                                sms[j] = paintMat;
                                changed = true;
                            }
                        }
                    }

                    if (changed)
                    {
                        mr.sharedMaterials = sms;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private static void ApplyTruckPaintIfNeeded(sCarController car)
        {
            if (car == null)
            {
                return;
            }
            if (!IsTruckCar(car))
            {
                return;
            }

            int carId = car.GetInstanceID();
            int index = GetSelectedPaintIndex();

            bool paintChanged = carId != _paintLastCarId || index != _paintLastIndex;
            float now = Time.unscaledTime;
            bool tailgateRescanDue = paintChanged;
            if (!tailgateRescanDue)
            {
                if (!_paintNextTailgateRescanTimeByCarId.TryGetValue(carId, out float next) || now >= next)
                {
                    tailgateRescanDue = true;
                }
            }

            if (!paintChanged && !tailgateRescanDue)
            {
                return;
            }

            _paintLastCarId = carId;
            _paintLastIndex = index;
            _paintNextTailgateRescanTimeByCarId[carId] = now + 1.0f;

            try
            {
                var hl = car.headlights;
                if (hl == null)
                {
                    return;
                }

                EnsureHeadlightsRefs(hl);
                var modelGo = _headlightsModelField != null ? _headlightsModelField.GetValue(hl) as GameObject : null;
                if (modelGo == null)
                {
                    return;
                }

                var mr = modelGo.GetComponent<MeshRenderer>();
                if (mr == null)
                {
                    return;
                }

                // Use .material so we get an instance local to this truck.
                var mat = mr.material;
                if (mat == null)
                {
                    return;
                }

                // Capture defaults once for restore.
                if (!_paintDefaultsByCarId.ContainsKey(carId))
                {
                    var defs = new PaintDefaults();
                    try
                    {
                        if (mat.HasProperty("_BaseMap")) defs.baseMap = mat.GetTexture("_BaseMap");
                    }
                    catch { }
                    try
                    {
                        if (mat.HasProperty("_MainTex")) defs.mainTex = mat.GetTexture("_MainTex");
                    }
                    catch { }
                    _paintDefaultsByCarId[carId] = defs;
                }

                bool applied = false;

                if (index < 0)
                {
                    // Restore default.
                    if (_paintDefaultsByCarId.TryGetValue(carId, out var defs))
                    {
                        try { if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", defs.baseMap); } catch { }
                        try { if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", defs.mainTex); } catch { }
                        applied = true;
                    }
                }
                else
                {
                    // Apply by texture first (works even if color-specific materials aren't loaded).
                    if (TryGetPaintCosmeticTexture(index, out var tex) && tex != null)
                    {
                        try { if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex); } catch { }
                        try { if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex); } catch { }
                        applied = true;
                    }
                }

                if (!applied)
                {
                    // Fallback: try swapping to a named material if present.
                    string matName = index < 0 ? "truck_texture" : null;
                    if (index >= 0 && !TryGetPaintCosmeticName(index, out matName))
                    {
                        return;
                    }

                    var src = FindMaterialByName(matName);
                    if (src == null)
                    {
                        string alt = DeriveTruckPaintMaterialName(matName);
                        src = FindMaterialByName(alt);
                        if (src == null && string.Equals(alt, "truck_texture", StringComparison.OrdinalIgnoreCase))
                        {
                            src = FindMaterialByName("truck_texture_white");
                        }
                        if (src == null)
                        {
                            LogDebug("Paint material not found: " + matName + " (derived=" + alt + ")");
                            return;
                        }
                    }

                    mr.material = src;
                    mat = mr.material;
                }

                // Keep emission/damage systems pointing at the active truck material.
                try { _headlightsCarMatField?.SetValue(hl, mat); } catch { }

                // Tailgate is a separate animated object; keep it using the same paint material instance.
                if (tailgateRescanDue)
                {
                    ApplyTailgatePaintMaterialIfPresent(car, mat);
                }

                EnsureCarDamageRefs();
                if (_carDamageCarMatField != null)
                {
                    var dmg = car.GetComponentInChildren<CarDamage>(true);
                    if (dmg != null)
                    {
                        try { _carDamageCarMatField.SetValue(dmg, mat); } catch { }
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private static void MuteRadioProximityIfIgnitionOff(sRadioSystem radio)
        {
            if (radio == null || radio.car == null)
            {
                return;
            }

            // Only mute the proximity/outside sound (player is out of the vehicle).
            if (!radio.car.GuyActive)
            {
                return;
            }

            if (!GetIgnitionFeatureEnabled() || GetIgnitionEnabledEffective())
            {
                return;
            }

            EnsureRadioRefs();
            try
            {
                _radioTargetVolumeField?.SetValue(radio, 0f);
                _radioRadioVolumeField?.SetValue(radio, 0f);

                var src = _radioSourceField != null ? _radioSourceField.GetValue(radio) as Component : null;
                if (src != null) SetProp(src, "volume", 0f);

                var noise = _radioNoiseField != null ? _radioNoiseField.GetValue(radio) as Component : null;
                if (noise != null) SetProp(noise, "volume", 0f);
            }
            catch
            {
                // ignore
            }
        }

        private static bool IsTruckCar(sCarController car)
        {
            if (car == null)
            {
                return false;
            }

            int id;
            try { id = car.GetInstanceID(); }
            catch { return false; }

            if (_isTruckCarById.TryGetValue(id, out bool cached))
            {
                return cached;
            }

            try
            {
                bool isTruck = car.GetComponentInChildren<sTruckSFX>(true) != null;
                _isTruckCarById[id] = isTruck;
                return isTruck;
            }
            catch
            {
                _isTruckCarById[id] = false;
                return false;
            }
        }

        private static bool GetTruckBraking(sCarController car)
        {
            if (car == null)
            {
                return false;
            }
            int id = car.GetInstanceID();
            if (_truckBraking.TryGetValue(id, out bool v))
            {
                return v;
            }
            return false;
        }

        private static void SetTruckBraking(sCarController car, bool braking)
        {
            if (car == null)
            {
                return;
            }
            _truckBraking[car.GetInstanceID()] = braking;
        }

        private static void ApplyTruckEmissionMap(sCarController car)
        {
            if (car == null)
            {
                return;
            }

            if (!GetIgnitionEnabledEffective())
            {
                return;
            }

            if (!IsTruckCar(car))
            {
                return;
            }

            var hl = car.headlights;
            if (hl == null)
            {
                return;
            }

            EnsureHeadlightsRefs(hl);
            var mat = _headlightsCarMatField != null ? _headlightsCarMatField.GetValue(hl) as Material : null;
            if (mat == null)
            {
                return;
            }

            bool braking = GetTruckBraking(car);
            bool blinkOn = _indicatorMode != IndicatorMode.Off && _indicatorBlinkOn;
            var tex = GetTurnSignalEmissive(braking, (int)_indicatorMode, blinkOn);
            if (tex != null)
            {
                mat.SetTexture("_EmissionMap", tex);
                try
                {
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", Color.white);
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        private static bool TryComputeModelBoundsInCarLocal(sCarController car, out Vector3 min, out Vector3 max)
        {
            min = Vector3.zero;
            max = Vector3.zero;
            if (car == null)
            {
                return false;
            }

            var hl = car.headlights;
            if (hl == null)
            {
                return false;
            }

            EnsureHeadlightsRefs(hl);
            var model = _headlightsModelField != null ? _headlightsModelField.GetValue(hl) as GameObject : null;
            if (model == null)
            {
                return false;
            }

            bool any = false;
            Vector3 vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            void AddPoint(Vector3 p)
            {
                if (!any)
                {
                    any = true;
                    vMin = p;
                    vMax = p;
                    return;
                }
                vMin = Vector3.Min(vMin, p);
                vMax = Vector3.Max(vMax, p);
            }

            try
            {
                var mfs = model.GetComponentsInChildren<MeshFilter>(true);
                if (mfs != null)
                {
                    for (int i = 0; i < mfs.Length; i++)
                    {
                        var mf = mfs[i];
                        if (mf == null || mf.sharedMesh == null)
                        {
                            continue;
                        }

                        Bounds b = mf.sharedMesh.bounds;
                        Vector3 c = b.center;
                        Vector3 e = b.extents;
                        var t = mf.transform;

                        for (int xi = -1; xi <= 1; xi += 2)
                        for (int yi = -1; yi <= 1; yi += 2)
                        for (int zi = -1; zi <= 1; zi += 2)
                        {
                            Vector3 lp = c + new Vector3(e.x * xi, e.y * yi, e.z * zi);
                            Vector3 wp = t.TransformPoint(lp);
                            Vector3 cp = car.transform.InverseTransformPoint(wp);
                            AddPoint(cp);
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            if (!any)
            {
                try
                {
                    var rs = model.GetComponentsInChildren<Renderer>(true);
                    if (rs != null)
                    {
                        for (int i = 0; i < rs.Length; i++)
                        {
                            var r = rs[i];
                            if (r == null)
                            {
                                continue;
                            }
                            Bounds b = r.bounds;
                            Vector3 c = b.center;
                            Vector3 e = b.extents;
                            for (int xi = -1; xi <= 1; xi += 2)
                            for (int yi = -1; yi <= 1; yi += 2)
                            for (int zi = -1; zi <= 1; zi += 2)
                            {
                                Vector3 wp = c + new Vector3(e.x * xi, e.y * yi, e.z * zi);
                                Vector3 cp = car.transform.InverseTransformPoint(wp);
                                AddPoint(cp);
                            }
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (!any)
            {
                return false;
            }

            min = vMin;
            max = vMax;
            return true;
        }

        private static void EnsureTurnSignalLightRig(sCarController car)
        {
            if (car == null)
            {
                return;
            }

            int id = car.GetInstanceID();
            if (_turnSignalLightRigs.TryGetValue(id, out var rig) && rig != null && rig.Root != null)
            {
                if (rig.Root.transform.parent != car.transform)
                {
                    rig.Root.transform.SetParent(car.transform, false);
                }
                return;
            }

            rig = new TurnSignalLightRig();
            _turnSignalLightRigs[id] = rig;

            var root = new GameObject("SebTruck_TurnSignalLights");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.transform.SetParent(car.transform, false);
            root.transform.localPosition = Vector3.zero;
            rig.Root = root;

            Light NewLight(string name)
            {
                var go = new GameObject(name);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Vector3.zero;

                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.shadows = LightShadows.None;
                l.color = new Color(1.0f, 0.55f, 0.12f);
                l.spotAngle = 95f;
                l.innerSpotAngle = 45f;
                l.enabled = false;
                return l;
            }

            rig.FL = NewLight("FL");
            rig.FR = NewLight("FR");
            rig.RL = NewLight("RL");
            rig.RR = NewLight("RR");
            rig.BaseComputed = false;
        }

        private static void ApplyTurnSignalLightParams(TurnSignalLightRig rig)
        {
            if (rig == null)
            {
                return;
            }

            const float fyaw = 20f;
            const float fpitch = 45f;
            const float fspot = 110f;
            const float finner = 15f;

            const float ryaw = 5f;
            const float rpitch = 30f;
            const float rspot = 145f;
            const float rinner = 30f;

            void ApplyFront(Light l, float y)
            {
                if (l == null)
                {
                    return;
                }
                l.spotAngle = fspot;
                l.innerSpotAngle = finner;
                l.transform.localRotation = Quaternion.Euler(fpitch, y, 0f);
            }

            void ApplyRear(Light l, float y)
            {
                if (l == null)
                {
                    return;
                }
                l.spotAngle = rspot;
                l.innerSpotAngle = rinner;
                l.transform.localRotation = Quaternion.Euler(rpitch, y, 0f);
            }

            ApplyFront(rig.FL, -fyaw);
            ApplyFront(rig.FR, fyaw);
            ApplyRear(rig.RL, 180f - ryaw);
            ApplyRear(rig.RR, 180f + ryaw);
        }

        private static void EnsureTurnSignalLightBasePositions(sCarController car)
        {
            if (car == null)
            {
                return;
            }
            int id = car.GetInstanceID();
            if (!_turnSignalLightRigs.TryGetValue(id, out var rig) || rig == null)
            {
                return;
            }
            if (rig.BaseComputed)
            {
                return;
            }

            // Try to anchor to the truck body model bounds in car-local space.
            if (TryComputeModelBoundsInCarLocal(car, out Vector3 min, out Vector3 max))
            {
                float xPad = 0.08f;
                float zPad = 0.12f;
                float xL = min.x - xPad;
                float xR = max.x + xPad;
                float zF = max.z + zPad;
                float zB = min.z - zPad;
                float y = Mathf.Lerp(min.y, max.y, 0.42f);

                rig.BaseFL = new Vector3(xL, y, zF);
                rig.BaseFR = new Vector3(xR, y, zF);
                rig.BaseRL = new Vector3(xL, y, zB);
                rig.BaseRR = new Vector3(xR, y, zB);
                rig.BaseComputed = true;
                return;
            }

            // Fallback: rough truck-like dimensions.
            rig.BaseFL = new Vector3(-0.75f, 0.55f, 1.45f);
            rig.BaseFR = new Vector3(0.75f, 0.55f, 1.45f);
            rig.BaseRL = new Vector3(-0.80f, 0.55f, -1.55f);
            rig.BaseRR = new Vector3(0.80f, 0.55f, -1.55f);
            rig.BaseComputed = true;
        }

        private static void SetLightOn(Light l, bool on, float intensity, float range)
        {
            if (l == null)
            {
                return;
            }
            l.range = range;
            l.intensity = intensity;
            l.enabled = on && intensity > 0.0001f && range > 0.0001f;
        }

        private static void UpdateTurnSignalWorldLights(sCarController car, bool leftOn, bool rightOn, bool forceOff)
        {
            if (car == null)
            {
                return;
            }
            if (!IsTruckCar(car))
            {
                return;
            }

            // Keep signals forced off while ignition is effectively off.
            if (forceOff || !GetIgnitionEnabledEffective())
            {
                int id = car.GetInstanceID();
                if (_turnSignalLightRigs.TryGetValue(id, out var rig0) && rig0 != null)
                {
                    SetLightOn(rig0.FL, false, 0f, 0f);
                    SetLightOn(rig0.FR, false, 0f, 0f);
                    SetLightOn(rig0.RL, false, 0f, 0f);
                    SetLightOn(rig0.RR, false, 0f, 0f);
                }
                return;
            }

            EnsureTurnSignalLightRig(car);
            EnsureTurnSignalLightBasePositions(car);

            int carId = car.GetInstanceID();
            if (!_turnSignalLightRigs.TryGetValue(carId, out var rig) || rig == null)
            {
                return;
            }

            const float range = 6.0f;
            float intensity = GetTurnSignalLightIntensity();
            float side = 0f;
            Vector3 fOff = TurnSignalLightFrontOffset;
            Vector3 rOff = TurnSignalLightRearOffset;

            ApplyTurnSignalLightParams(rig);

            Vector3 fl = rig.BaseFL + fOff + new Vector3(-side, 0f, 0f);
            Vector3 fr = rig.BaseFR + fOff + new Vector3(+side, 0f, 0f);
            Vector3 rl = rig.BaseRL + rOff + new Vector3(-side, 0f, 0f);
            Vector3 rr = rig.BaseRR + rOff + new Vector3(+side, 0f, 0f);

            if (rig.FL != null) rig.FL.transform.localPosition = fl;
            if (rig.FR != null) rig.FR.transform.localPosition = fr;
            if (rig.RL != null) rig.RL.transform.localPosition = rl;
            if (rig.RR != null) rig.RR.transform.localPosition = rr;

            SetLightOn(rig.FL, leftOn, intensity, range);
            SetLightOn(rig.RL, leftOn, intensity, range);
            SetLightOn(rig.FR, rightOn, intensity, range);
            SetLightOn(rig.RR, rightOn, intensity, range);
        }

        private static void UpdateIndicatorAutoCancel(float steerX)
        {
            if (_indicatorMode != IndicatorMode.Left && _indicatorMode != IndicatorMode.Right)
            {
                _indicatorCancelArmed = false;
                _indicatorCancelArmedForMode = _indicatorMode;
                return;
            }

            if (_indicatorCancelArmedForMode != _indicatorMode)
            {
                _indicatorCancelArmedForMode = _indicatorMode;
                _indicatorCancelArmed = false;
            }

            if (!_indicatorCancelArmed)
            {
                if (_indicatorMode == IndicatorMode.Left && steerX <= -IndicatorCancelArmThreshold)
                {
                    _indicatorCancelArmed = true;
                }
                else if (_indicatorMode == IndicatorMode.Right && steerX >= IndicatorCancelArmThreshold)
                {
                    _indicatorCancelArmed = true;
                }
                return;
            }

            if (Mathf.Abs(steerX) <= IndicatorCancelCenterThreshold)
            {
                _indicatorMode = IndicatorMode.Off;
                _indicatorBlinkOn = false;
                _indicatorNextBlinkTime = 0f;
                _indicatorCancelArmed = false;
            }
        }


        private static Texture2D GetIgnitionBlackEmissionTex()
        {
            if (_ignitionBlackEmissiveTex != null)
            {
                return _ignitionBlackEmissiveTex;
            }

            _ignitionBlackEmissiveTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            _ignitionBlackEmissiveTex.SetPixels(new[] { Color.black, Color.black, Color.black, Color.black });
            _ignitionBlackEmissiveTex.Apply(false, true);
            _ignitionBlackEmissiveTex.hideFlags = HideFlags.HideAndDontSave;
            return _ignitionBlackEmissiveTex;
        }

        private static void ForceVehicleLightsOff(sCarController car)
        {
            if (car == null)
            {
                return;
            }

            var hl = car.headlights;
            if (hl == null)
            {
                return;
            }

            EnsureHeadlightsRefs(hl);

            var go = _headlightsHeadLightsField != null ? _headlightsHeadLightsField.GetValue(hl) as GameObject : null;
            if (go != null && go.activeSelf)
            {
                go.SetActive(false);
            }

            try
            {
                hl.headlightsOn = false;
            }
            catch
            {
                // ignore
            }

            var mat = _headlightsCarMatField != null ? _headlightsCarMatField.GetValue(hl) as Material : null;
            if (mat != null)
            {
                mat.SetTexture("_EmissionMap", GetIgnitionBlackEmissionTex());
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.black);
                }
            }

            ForceTailLightsOff(car);
            SetIndicators(car, IndicatorMode.Off, blinkOn: false, forceOff: true);
        }

        private static void ForceTailLightsOff(sCarController car)
        {
            if (car == null)
            {
                return;
            }

            var cache = GetTailLightCache(car);
            for (int i = 0; i < cache.Lights.Count; i++)
            {
                var l = cache.Lights[i];
                if (l == null)
                {
                    continue;
                }
                int id = l.GetInstanceID();
                if (!cache.Defaults.ContainsKey(id))
                {
                    cache.Defaults[id] = (l.intensity, l.enabled);
                }
                l.intensity = 0f;
                l.enabled = false;
            }

            // Also shut off rear lens flare components (tail flares).
            for (int i = 0; i < cache.Flares.Count; i++)
            {
                var f = cache.Flares[i];
                if (f == null)
                {
                    continue;
                }
                int id = f.GetInstanceID();
                if (!cache.FlareDefaults.ContainsKey(id))
                {
                    float inten = 0f;
                    bool en = true;
                    try
                    {
                        var pI = f.GetType().GetProperty("intensity");
                        if (pI != null)
                        {
                            inten = (float)pI.GetValue(f, null);
                        }
                    }
                    catch { }
                    try
                    {
                        var pE = f.GetType().GetProperty("enabled");
                        if (pE != null)
                        {
                            en = (bool)pE.GetValue(f, null);
                        }
                    }
                    catch { }
                    cache.FlareDefaults[id] = (inten, en);
                }
                try { SetProp(f, "intensity", 0f); } catch { }
                try { SetProp(f, "enabled", false); } catch { }
            }
        }

        private static void RestoreTailLights(sCarController car)
        {
            if (car == null)
            {
                return;
            }

            int carId = car.GetInstanceID();
            if (!_tailLightCaches.TryGetValue(carId, out var cache) || cache == null)
            {
                return;
            }

            for (int i = 0; i < cache.Lights.Count; i++)
            {
                var l = cache.Lights[i];
                if (l == null)
                {
                    continue;
                }
                int id = l.GetInstanceID();
                if (cache.Defaults.TryGetValue(id, out var d))
                {
                    l.enabled = d.enabled;
                    l.intensity = d.intensity;
                }
            }

            for (int i = 0; i < cache.Flares.Count; i++)
            {
                var f = cache.Flares[i];
                if (f == null)
                {
                    continue;
                }
                int id = f.GetInstanceID();
                if (cache.FlareDefaults.TryGetValue(id, out var d))
                {
                    try { SetProp(f, "enabled", d.enabled); } catch { }
                    try { SetProp(f, "intensity", d.intensity); } catch { }
                }
            }
        }

        private static TailLightCache GetTailLightCache(sCarController car)
        {
            int carId = car.GetInstanceID();
            if (_tailLightCaches.TryGetValue(carId, out var existing) && existing != null)
            {
                return existing;
            }

            var cache = new TailLightCache();
            _tailLightCaches[carId] = cache;

            // Heuristic: find rear/tail/brake lights outside the Headlights.headLights object.
            GameObject headLightsGo = null;
            try
            {
                var hl = car.headlights;
                if (hl != null)
                {
                    EnsureHeadlightsRefs(hl);
                    headLightsGo = _headlightsHeadLightsField != null ? _headlightsHeadLightsField.GetValue(hl) as GameObject : null;
                }
            }
            catch
            {
                headLightsGo = null;
            }

            Light[] lights;
            try
            {
                lights = car.GetComponentsInChildren<Light>(true);
            }
            catch
            {
                lights = null;
            }

            if (lights == null)
            {
                return cache;
            }

            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null)
                {
                    continue;
                }

                if (headLightsGo != null && l.transform != null && l.transform.IsChildOf(headLightsGo.transform))
                {
                    continue;
                }

                string n = l.name ?? string.Empty;
                bool nameMatch = n.IndexOf("tail", StringComparison.OrdinalIgnoreCase) >= 0
                                 || n.IndexOf("brake", StringComparison.OrdinalIgnoreCase) >= 0
                                 || n.IndexOf("rear", StringComparison.OrdinalIgnoreCase) >= 0;

                bool redish = l.color.r > 0.7f && l.color.g < 0.45f && l.color.b < 0.45f;

                bool isRear = false;
                try
                {
                    Vector3 lp = car.transform.InverseTransformPoint(l.transform.position);
                    isRear = lp.z < -0.2f;
                }
                catch
                {
                    isRear = false;
                }

                if ((nameMatch || (redish && isRear)) && !cache.Lights.Contains(l))
                {
                    cache.Lights.Add(l);
                }
            }

            // Lens flare components (SRP) for tail lights.
            try
            {
                var comps = car.GetComponentsInChildren<Component>(true);
                if (comps != null)
                {
                    for (int i = 0; i < comps.Length; i++)
                    {
                        var c = comps[i];
                        if (c == null)
                        {
                            continue;
                        }
                        string tn = c.GetType().Name;
                        if (tn == null || tn.IndexOf("LensFlareComponentSRP", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        bool isRear = false;
                        try
                        {
                            Vector3 lp = car.transform.InverseTransformPoint(c.transform.position);
                            isRear = lp.z < -0.2f;
                        }
                        catch
                        {
                            isRear = false;
                        }

                        if (!isRear)
                        {
                            continue;
                        }

                        string n = c.name ?? string.Empty;
                        bool nameMatch = n.IndexOf("tail", StringComparison.OrdinalIgnoreCase) >= 0
                                         || n.IndexOf("rear", StringComparison.OrdinalIgnoreCase) >= 0
                                         || n.IndexOf("brake", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!nameMatch)
                        {
                            continue;
                        }

                        if (!cache.Flares.Contains(c))
                        {
                            cache.Flares.Add(c);
                        }
                    }
                }
            }
            catch
            {
            }

            return cache;
        }

        private static IndicatorCache GetIndicatorCache(sCarController car)
        {
            int carId = car.GetInstanceID();
            if (_indicatorCaches.TryGetValue(carId, out var existing) && existing != null)
            {
                return existing;
            }

            var cache = new IndicatorCache();
            _indicatorCaches[carId] = cache;

            GameObject headLightsGo = null;
            try
            {
                var hl = car.headlights;
                if (hl != null)
                {
                    EnsureHeadlightsRefs(hl);
                    headLightsGo = _headlightsHeadLightsField != null ? _headlightsHeadLightsField.GetValue(hl) as GameObject : null;
                }
            }
            catch
            {
                headLightsGo = null;
            }

            Light[] lights;
            try
            {
                lights = car.GetComponentsInChildren<Light>(true);
            }
            catch
            {
                lights = null;
            }

            if (lights == null)
            {
                // Still try renderer/material indicators.
            }

            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null)
                {
                    continue;
                }

                if (headLightsGo != null && l.transform != null && l.transform.IsChildOf(headLightsGo.transform))
                {
                    // Headlights themselves are not indicators.
                    continue;
                }

                string n = l.name ?? string.Empty;
                bool nameMatch = n.IndexOf("indicator", StringComparison.OrdinalIgnoreCase) >= 0
                                 || n.IndexOf("blinker", StringComparison.OrdinalIgnoreCase) >= 0
                                 || n.IndexOf("turn", StringComparison.OrdinalIgnoreCase) >= 0
                                 || n.IndexOf("signal", StringComparison.OrdinalIgnoreCase) >= 0;

                bool amber = l.color.r > 0.7f && l.color.g > 0.25f && l.color.b < 0.35f;

                bool isFront = false;
                float lx = 0f;
                try
                {
                    Vector3 lp = car.transform.InverseTransformPoint(l.transform.position);
                    lx = lp.x;
                    isFront = lp.z > 0.2f;
                }
                catch
                {
                    isFront = false;
                }

                if (!(nameMatch || (amber && isFront)))
                {
                    continue;
                }

                bool left = n.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("_l", StringComparison.OrdinalIgnoreCase) >= 0;
                bool right = n.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("_r", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!left && !right)
                {
                    left = lx < 0f;
                    right = !left;
                }

                if (left && !cache.Left.Contains(l))
                {
                    cache.Left.Add(l);
                }
                if (right && !cache.Right.Contains(l))
                {
                    cache.Right.Add(l);
                }
            }

            // Material emissive indicators (the truck uses emissive materials, not actual Light components).
            try
            {
                var renderers = car.GetComponentsInChildren<Renderer>(true);
                if (renderers != null)
                {
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        var r = renderers[i];
                        if (r == null)
                        {
                            continue;
                        }

                        float lx = 0f;
                        float lz = 0f;
                        try
                        {
                            Vector3 lp = car.transform.InverseTransformPoint(r.transform.position);
                            lx = lp.x;
                            lz = lp.z;
                        }
                        catch
                        {
                            lz = 0f;
                        }

                        // Some meshes have their pivot near center; be generous.
                        if (lz < -0.35f)
                        {
                            continue;
                        }

                        string rn = r.name ?? string.Empty;
                        bool rNameHint = rn.IndexOf("indicator", StringComparison.OrdinalIgnoreCase) >= 0
                                         || rn.IndexOf("blinker", StringComparison.OrdinalIgnoreCase) >= 0
                                         || rn.IndexOf("blink", StringComparison.OrdinalIgnoreCase) >= 0
                                         || rn.IndexOf("turn", StringComparison.OrdinalIgnoreCase) >= 0
                                         || rn.IndexOf("signal", StringComparison.OrdinalIgnoreCase) >= 0
                                         || rn.IndexOf("lamp", StringComparison.OrdinalIgnoreCase) >= 0
                                         || rn.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0;

                        Material[] mats = null;
                        try { mats = r.materials; } catch { mats = null; }
                        if (mats == null || mats.Length == 0)
                        {
                            continue;
                        }

                        for (int mi = 0; mi < mats.Length; mi++)
                        {
                            var m = mats[mi];
                            if (m == null)
                            {
                                continue;
                            }

                            if (!m.HasProperty("_EmissionColor"))
                            {
                                continue;
                            }

                            string mn = m.name ?? string.Empty;
                            bool mNameHint = mn.IndexOf("indicator", StringComparison.OrdinalIgnoreCase) >= 0
                                             || mn.IndexOf("blinker", StringComparison.OrdinalIgnoreCase) >= 0
                                             || mn.IndexOf("blink", StringComparison.OrdinalIgnoreCase) >= 0
                                             || mn.IndexOf("turn", StringComparison.OrdinalIgnoreCase) >= 0
                                             || mn.IndexOf("signal", StringComparison.OrdinalIgnoreCase) >= 0
                                             || mn.IndexOf("lamp", StringComparison.OrdinalIgnoreCase) >= 0
                                             || mn.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0;

                            Color baseC = Color.white;
                            bool hasBase = false;
                            try { if (m.HasProperty("_BaseColor")) { baseC = m.GetColor("_BaseColor"); hasBase = true; } } catch { }
                            try { if (!hasBase && m.HasProperty("_Color")) { baseC = m.GetColor("_Color"); hasBase = true; } } catch { }
                            bool amberBase = baseC.r > 0.7f && baseC.g > 0.25f && baseC.b < 0.35f;

                            if (!(amberBase || rNameHint || mNameHint))
                            {
                                continue;
                            }

                            bool left = rn.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0 || rn.IndexOf("_l", StringComparison.OrdinalIgnoreCase) >= 0;
                            bool right = rn.IndexOf("right", StringComparison.OrdinalIgnoreCase) >= 0 || rn.IndexOf("_r", StringComparison.OrdinalIgnoreCase) >= 0;
                            if (!left && !right)
                            {
                                left = lx < 0f;
                                right = !left;
                            }

                            if (left && !cache.LeftMats.Contains(m))
                            {
                                cache.LeftMats.Add(m);
                            }
                            if (right && !cache.RightMats.Contains(m))
                            {
                                cache.RightMats.Add(m);
                            }

                            int id = m.GetInstanceID();
                            if (!cache.MatDefaults.ContainsKey(id))
                            {
                                Color e = Color.black;
                                Color ee = Color.black;
                                bool kw = false;
                                try { if (m.HasProperty("_EmissionColor")) { e = m.GetColor("_EmissionColor"); } } catch { }
                                try { if (m.HasProperty("_EmissiveColor")) { ee = m.GetColor("_EmissiveColor"); } } catch { }
                                try { kw = m.IsKeywordEnabled("_EMISSION"); } catch { }
                                cache.MatDefaults[id] = (e, ee, kw);
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            if (_debugLogging != null && _debugLogging.Value)
            {
                LogDebug("Indicators: lights left=" + cache.Left.Count + " right=" + cache.Right.Count + " mats left=" + cache.LeftMats.Count + " right=" + cache.RightMats.Count);
            }

            return cache;
        }

        private static void SetIndicators(sCarController car, IndicatorMode mode, bool blinkOn, bool forceOff)
        {
            if (car == null)
            {
                return;
            }

            var cache = GetIndicatorCache(car);

            bool leftOn = blinkOn && (mode == IndicatorMode.Left || mode == IndicatorMode.Hazards);
            bool rightOn = blinkOn && (mode == IndicatorMode.Right || mode == IndicatorMode.Hazards);

            void Apply(List<Light> ls, bool on)
            {
                for (int i = 0; i < ls.Count; i++)
                {
                    var l = ls[i];
                    if (l == null)
                    {
                        continue;
                    }

                    int id = l.GetInstanceID();
                    if (!cache.Defaults.ContainsKey(id))
                    {
                        cache.Defaults[id] = (l.intensity, l.enabled);
                    }

                    if (on)
                    {
                        var d = cache.Defaults[id];
                        l.enabled = d.enabled;
                        l.intensity = d.intensity;
                    }
                    else
                    {
                        l.intensity = 0f;
                        l.enabled = false;
                    }
                }
            }

            Apply(cache.Left, leftOn);
            Apply(cache.Right, rightOn);

            void ApplyMat(List<Material> ms, bool on)
            {
                for (int i = 0; i < ms.Count; i++)
                {
                    var m = ms[i];
                    if (m == null)
                    {
                        continue;
                    }

                    int id = m.GetInstanceID();
                    if (!cache.MatDefaults.TryGetValue(id, out var d))
                    {
                        d = (Color.black, Color.black, false);
                    }

                    if (on)
                    {
                        Color e = d.emissionColor;
                        Color ee = d.emissiveColor;
                        if (e.maxColorComponent <= 0.01f && ee.maxColorComponent <= 0.01f)
                        {
                            e = new Color(1.0f, 0.55f, 0.12f) * 2.0f;
                            ee = e;
                        }
                        try { m.EnableKeyword("_EMISSION"); } catch { }
                        try { if (m.HasProperty("_EmissionColor")) { m.SetColor("_EmissionColor", e); } } catch { }
                        try { if (m.HasProperty("_EmissiveColor")) { m.SetColor("_EmissiveColor", ee); } } catch { }
                    }
                    else
                    {
                        if (forceOff)
                        {
                            try { m.DisableKeyword("_EMISSION"); } catch { }
                            try { if (m.HasProperty("_EmissionColor")) { m.SetColor("_EmissionColor", Color.black); } } catch { }
                            try { if (m.HasProperty("_EmissiveColor")) { m.SetColor("_EmissiveColor", Color.black); } } catch { }
                        }
                        else
                        {
                            // Restore defaults when off.
                            try { if (!d.emissionKeyword) { m.DisableKeyword("_EMISSION"); } else { m.EnableKeyword("_EMISSION"); } } catch { }
                            try { if (m.HasProperty("_EmissionColor")) { m.SetColor("_EmissionColor", d.emissionColor); } } catch { }
                            try { if (m.HasProperty("_EmissiveColor")) { m.SetColor("_EmissiveColor", d.emissiveColor); } } catch { }
                        }
                    }
                }
            }

            ApplyMat(cache.LeftMats, leftOn);
            ApplyMat(cache.RightMats, rightOn);
        }

        private static void UpdateIndicators(sCarController car)
        {
            if (car == null)
            {
                return;
            }

            // Feature toggle: if disabled, restore defaults and bail.
            if (!GetIndicatorFeatureEnabled())
            {
                _indicatorMode = IndicatorMode.Off;
                _indicatorBlinkOn = false;
                _indicatorNextBlinkTime = 0f;
                _indicatorPauseStart = -1f;
                SetIndicators(car, IndicatorMode.Off, blinkOn: false, forceOff: false);
                ApplyTruckEmissionMap(car);
                UpdateTurnSignalWorldLights(car, leftOn: false, rightOn: false, forceOff: true);
                return;
            }

            // Keep indicators forced off while ignition is effectively off.
            if (!GetIgnitionEnabledEffective())
            {
                _indicatorMode = IndicatorMode.Off;
                _indicatorBlinkOn = false;
                _indicatorNextBlinkTime = 0f;
                _indicatorPauseStart = -1f;
                SetIndicators(car, IndicatorMode.Off, blinkOn: false, forceOff: true);
                UpdateTurnSignalWorldLights(car, leftOn: false, rightOn: false, forceOff: true);
                return;
            }

            if (_indicatorMode == IndicatorMode.Off)
            {
                _indicatorBlinkOn = false;
                _indicatorPauseStart = -1f;
                SetIndicators(car, IndicatorMode.Off, blinkOn: false, forceOff: false);
                ApplyTruckEmissionMap(car);
                UpdateTurnSignalWorldLights(car, leftOn: false, rightOn: false, forceOff: false);
                return;
            }

            // Pause menu: freeze indicator blink timing/state.
            if (PauseSystem.paused)
            {
                if (_indicatorPauseStart < 0f)
                {
                    _indicatorPauseStart = Time.unscaledTime;
                }

                bool pLeftOn = _indicatorBlinkOn && (_indicatorMode == IndicatorMode.Left || _indicatorMode == IndicatorMode.Hazards);
                bool pRightOn = _indicatorBlinkOn && (_indicatorMode == IndicatorMode.Right || _indicatorMode == IndicatorMode.Hazards);
                UpdateTurnSignalWorldLights(car, pLeftOn, pRightOn, forceOff: false);
                return;
            }
            if (_indicatorPauseStart >= 0f)
            {
                float pausedFor = Time.unscaledTime - _indicatorPauseStart;
                if (_indicatorNextBlinkTime > 0f)
                {
                    _indicatorNextBlinkTime += pausedFor;
                }
                _indicatorPauseStart = -1f;
            }

            bool blinkWasOn = _indicatorBlinkOn;

            float now = Time.unscaledTime;
            float step = GetIndicatorBlinkSeconds();
            if (_indicatorNextBlinkTime <= 0f)
            {
                _indicatorBlinkOn = true;
                _indicatorNextBlinkTime = now + step;
            }
            else if (now >= _indicatorNextBlinkTime)
            {
                _indicatorBlinkOn = !_indicatorBlinkOn;
                _indicatorNextBlinkTime = now + step;
            }

            // Click sound on each blink edge.
            if (_indicatorBlinkOn != blinkWasOn)
            {
                PlayIndicatorClick(car, highPitch: _indicatorBlinkOn);
            }

            SetIndicators(car, _indicatorMode, _indicatorBlinkOn, forceOff: false);
            ApplyTruckEmissionMap(car);

            bool leftOn = _indicatorBlinkOn && (_indicatorMode == IndicatorMode.Left || _indicatorMode == IndicatorMode.Hazards);
            bool rightOn = _indicatorBlinkOn && (_indicatorMode == IndicatorMode.Right || _indicatorMode == IndicatorMode.Hazards);
            UpdateTurnSignalWorldLights(car, leftOn, rightOn, forceOff: false);
        }

        private static void RestoreVehicleLightState(sCarController car, bool wantOn)
        {
            if (car == null)
            {
                return;
            }

            var hl = car.headlights;
            if (hl == null)
            {
                return;
            }

            EnsureHeadlightsRefs(hl);

            var go = _headlightsHeadLightsField != null ? _headlightsHeadLightsField.GetValue(hl) as GameObject : null;
            if (go != null)
            {
                go.SetActive(wantOn);
            }

            try
            {
                hl.headlightsOn = wantOn;
            }
            catch
            {
                // ignore
            }

            // Always restore the vehicle emissive map while ignition is on.
            // Headlights themselves are controlled by the headLights GameObject.
            var mat = _headlightsCarMatField != null ? _headlightsCarMatField.GetValue(hl) as Material : null;
            var regular = _headlightsEmissiveRegularField != null ? _headlightsEmissiveRegularField.GetValue(hl) as Texture : null;
            if (mat != null && regular != null)
            {
                Texture want = regular;
                if (IsTruckCar(car))
                {
                    bool braking = GetTruckBraking(car);
                    var custom = GetTurnSignalEmissive(braking, indicatorMode: 0, blinkOn: false);
                    if (custom != null)
                    {
                        want = custom;
                    }
                }

                mat.SetTexture("_EmissionMap", want);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.white);
                }
            }
        }

        private static void ApplyIgnitionStateChange(bool ignitionOn)
        {
            var car = _currentCar;
            if (car == null || car.GuyActive)
            {
                return;
            }

            LogDebug("Ignition: " + (ignitionOn ? "ON" : "OFF"));

            var hl = car.headlights;
            var radio = sRadioSystem.instance;
            bool radioIsForCar = radio != null && ReferenceEquals(radio.car, car);
            object radioSource = radioIsForCar ? GetRadioSource(radio) : null;
            bool radioOn = radioSource != null && GetBehaviourEnabled(radioSource);

            if (!ignitionOn)
            {
                _ignitionPrevHeadlightsOn = hl != null && hl.headlightsOn;
                _ignitionPrevRadioOn = radioIsForCar && radioOn;

                _ignitionOffSince = Time.unscaledTime;

                ForceVehicleLightsOff(car);

                // Play the headlight toggle click when shutting the engine off.
                if (hl != null)
                {
                    try
                    {
                        AccessTools.Method(hl.GetType(), "PlaySound", new[] { typeof(bool) })?.Invoke(hl, new object[] { false });
                    }
                    catch
                    {
                    }
                }
                if (radioIsForCar && radioOn)
                {
                    radio.ToggleRadio();
                }
                return;
            }

            _ignitionOffSince = -1f;

            RestoreVehicleLightState(car, _ignitionPrevHeadlightsOn);
            RestoreTailLights(car);
            if (radioIsForCar && _ignitionPrevRadioOn && !radioOn)
            {
                radio.ToggleRadio();
            }

            if (GetManualTransmissionEnabled())
            {
                _manualGear = 1;
            }
        }

        private static void EnforceIgnitionOffForCurrentCar()
        {
            var car = _currentCar;
            if (car == null || car.GuyActive)
            {
                return;
            }

            if (car.player >= 0 && car.player < sInputManager.players.Length)
            {
                sInputManager.players[car.player].headlightsPressed = false;
                sInputManager.players[car.player].radioPressed = false;
                sInputManager.players[car.player].radioInput = Vector2.zero;
            }

            ForceVehicleLightsOff(car);

            // Keep indicators off while ignition is off.
            _indicatorMode = IndicatorMode.Off;
            _indicatorBlinkOn = false;

            var radio = sRadioSystem.instance;
            if (radio != null && ReferenceEquals(radio.car, car))
            {
                object src = GetRadioSource(radio);
                if (src != null && GetBehaviourEnabled(src))
                {
                    radio.ToggleRadio();
                }
            }
        }

        private static object GetRadioSource(sRadioSystem radio)
        {
            if (radio == null)
            {
                return null;
            }
            try
            {
                return AccessTools.Field(radio.GetType(), "source")?.GetValue(radio);
            }
            catch
            {
                return null;
            }
        }

        private static bool GetBehaviourEnabled(object behaviour)
        {
            if (behaviour == null)
            {
                return false;
            }
            try
            {
                var p = behaviour.GetType().GetProperty("enabled");
                if (p != null)
                {
                    return (bool)p.GetValue(behaviour, null);
                }
            }
            catch
            {
            }
            return false;
        }

        private static void ApplyHeadlightTuning(sCarController car)
        {
            if (car == null)
            {
                return;
            }

            var hl = car.headlights;
            if (hl == null)
            {
                return;
            }

            int carId = car.GetInstanceID();
            if (!_headlightTuningCaches.TryGetValue(carId, out var cache) || cache == null)
            {
                cache = new HeadlightTuningCache();
                _headlightTuningCaches[carId] = cache;
            }

            // Rescan at low cadence; applying tuning each frame is cheap, scanning isn't.
            float now = Time.unscaledTime;
            if (cache.Lights.Count == 0 || now >= cache.NextRescanTime)
            {
                cache.Lights.Clear();
                cache.NextRescanTime = now + 2.0f;

                EnsureHeadlightsRefs(hl);
                var go = _headlightsHeadLightsField != null ? _headlightsHeadLightsField.GetValue(hl) as GameObject : null;

                _tmpHeadlightLightIds.Clear();
                var seen = _tmpHeadlightLightIds;

                void AddLightsFrom(GameObject root)
                {
                    if (root == null)
                    {
                        return;
                    }
                    try
                    {
                        var ls = root.GetComponentsInChildren<Light>(true);
                        if (ls == null)
                        {
                            return;
                        }
                        for (int i = 0; i < ls.Length; i++)
                        {
                            var l = ls[i];
                            if (l == null)
                            {
                                continue;
                            }
                            int id = l.GetInstanceID();
                            if (seen.Add(id))
                            {
                                cache.Lights.Add(l);
                            }
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }

                // Primary: Headlights.headLights object.
                AddLightsFrom(go);

                // TrueNight-style: some prefabs put the main light objects under child(0) instead.
                try
                {
                    var t0 = hl.transform != null && hl.transform.childCount > 0 ? hl.transform.GetChild(0) : null;
                    AddLightsFrom(t0 != null ? t0.gameObject : null);
                }
                catch
                {
                    // ignore
                }

                // Fallback: grab anything under the headlights component.
                try
                {
                    var ls = hl.GetComponentsInChildren<Light>(true);
                    if (ls != null)
                    {
                        for (int i = 0; i < ls.Length; i++)
                        {
                            var l = ls[i];
                            if (l == null)
                            {
                                continue;
                            }
                            int id = l.GetInstanceID();
                            if (seen.Add(id))
                            {
                                cache.Lights.Add(l);
                            }
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            var list = cache.Lights;
            if (list.Count == 0)
            {
                return;
            }

            float intenMul = GetHeadlightIntensityMult();
            float rangeMul = GetHeadlightRangeMult();
            if (!GetIgnitionEnabledEffective())
            {
                intenMul = 0f;
                rangeMul = 0f;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var l = list[i];
                if (l == null)
                {
                    continue;
                }
                int id = l.GetInstanceID();
                if (!_lightDefaults.TryGetValue(id, out var d))
                {
                    d = (l.intensity, l.range);
                    _lightDefaults[id] = d;
                }
                l.intensity = d.intensity * intenMul;
                l.range = d.range * rangeMul;
            }
        }

        private static void EnsureEngineSfxRefs(object instance)
        {
            if (instance == null)
            {
                return;
            }

            var t = instance.GetType();
            if (_engineSfxRuntimeType == t)
            {
                return;
            }

            _engineSfxRuntimeType = t;
            _engineCarField = AccessTools.Field(t, "car");
            _engineIdleField = AccessTools.Field(t, "idle");
            _engineDriveField = AccessTools.Field(t, "drive");
            _engineIntenseField = AccessTools.Field(t, "intense");
            _engineDistortionField = AccessTools.Field(t, "distortionFilter");
        }

        private static void ApplySpeedScaleTuning(sCarController car)
        {
            if (car == null)
            {
                return;
            }

            int id = car.GetInstanceID();
            if (!_carScaleDefaults.ContainsKey(id))
            {
                _carScaleDefaults[id] = (car.maxSpeedScale, car.drivePowerScale);
            }
            var d = _carScaleDefaults[id];

            if (car.GuyActive)
            {
                car.maxSpeedScale = d.maxSpeedScale;
                car.drivePowerScale = d.drivePowerScale;
                _currentSpeedMult = 1f;
                return;
            }

            float mult;
            if (GetManualTransmissionEnabled())
            {
                mult = _manualGear < 0 ? GetManualSpeedMultReverse() : GetManualSpeedMultForward();
            }
            else
            {
                float dir = 0f;
                try
                {
                    if (car.player >= 0 && car.player < sInputManager.players.Length)
                    {
                        dir = sInputManager.players[car.player].driveInput.y;
                    }
                }
                catch
                {
                    dir = 0f;
                }
                mult = dir < 0f ? GetManualSpeedMultReverse() : GetManualSpeedMultForward();
            }

            car.maxSpeedScale = d.maxSpeedScale * mult;
            car.drivePowerScale = d.drivePowerScale;
            _currentSpeedMult = mult;
        }

        private static float GetMaxSpeedForGearKmh(int gear)
        {
            int count = GetManualGearCount();
            gear = Mathf.Clamp(gear, 1, count);

            const float baseKmh = 25f;
            const float topKmh = 125f;

            // Virtual gearbox curve: lower gears top out earlier.
            // This is an exponential/geometric progression in max speed per gear, which corresponds to a
            // geometric decay in effective ratio (big early drop, then flatter as gears go up).
            float growth = Mathf.Pow(topKmh / baseKmh, 1f / Mathf.Max(1, count - 1));
            return baseKmh * Mathf.Pow(growth, gear - 1);
        }

        private static float GetMaxSpeedForCurrentGearKmh()
        {
            int g = _manualGear;
            if (g == 0)
            {
                return 1f;
            }
            float baseKmh = GetMaxSpeedForGearKmh(Mathf.Clamp(Mathf.Abs(g), 1, GetManualGearCount()));
            return Mathf.Max(1f, baseKmh * Mathf.Max(0.01f, _currentSpeedMult));
        }

        internal static float GetEstimatedRpm()
        {
            const float idle = 900f;
            const float redline = 6500f;

            float t;
            if (!GetManualTransmissionEnabled())
            {
                t = Mathf.Clamp01(_currentSpeedKmh / (140f * Mathf.Max(0.01f, _currentSpeedMult)));
                return Mathf.Lerp(idle, redline, t);
            }

            if (_manualGear == 0)
            {
                return Mathf.Lerp(idle, redline, _neutralRev01);
            }

            t = Mathf.Clamp01(_currentSpeedKmh / GetMaxSpeedForCurrentGearKmh());
            return Mathf.Lerp(idle, redline, t);
        }

        internal static float GetEstimatedRpmNormForSound()
        {
            if (!GetManualTransmissionEnabled())
            {
                return Mathf.Clamp01(_currentSpeedKmh / (140f * Mathf.Max(0.01f, _currentSpeedMult)));
            }

            if (_manualGear == 0)
            {
                _neutralRev01 = Mathf.Lerp(_neutralRev01, Mathf.Clamp01(_lastThrottle01), Time.deltaTime * 8f);
                return Mathf.Clamp(_neutralRev01, 0f, 1.2f);
            }

            float t = _currentSpeedKmh / GetMaxSpeedForCurrentGearKmh();
            return Mathf.Clamp(t, 0f, 1.2f);
        }

        internal static float ComputeManualAccel(float gas)
        {
            if (!GetManualTransmissionEnabled())
            {
                return gas;
            }

            if (_manualGear == 0)
            {
                return 0f;
            }

            float speed = Mathf.Max(0f, _currentSpeedKmh);
            float max = GetMaxSpeedForCurrentGearKmh();

            float t = Mathf.Clamp01(speed / Mathf.Max(1f, max));
            float band = 1f - t;
            float shaped = Mathf.Pow(band, 0.55f);
            float torque = Mathf.Lerp(0.55f, 1.20f, shaped);

            // Higher gears should not add a large acceleration "kick".
            // Give lower gears more pull; taper off toward top gear.
            int count = GetManualGearCount();
            int gAbs = Mathf.Clamp(Mathf.Abs(_manualGear), 1, count);
            float gt = count <= 1 ? 1f : (gAbs - 1f) / (count - 1f);
            float gearTorqueScale = Mathf.Lerp(1.25f, 0.85f, Mathf.Clamp01(gt));
            torque *= gearTorqueScale;

            float a = Mathf.Clamp01(gas) * torque;
            return _manualGear < 0 ? -a : a;
        }


        [HarmonyPatch(typeof(sCarController), "Update")]
        [HarmonyPrefix]
        private static void SCarController_Update_Prefix(sCarController __instance)
        {
            if (__instance == null)
            {
                return;
            }

            _currentCar = __instance;
            _isInWalkingMode = __instance.GuyActive;
            _currentCarFrame = Time.frameCount;

            if (__instance.rb != null)
            {
                _currentSpeedKmh = __instance.rb.linearVelocity.magnitude * 3.6f;
            }

            ApplyHeadlightTuning(__instance);
            ApplySpeedScaleTuning(__instance);

            ApplyTruckPaintIfNeeded(__instance);

            if (!GetIgnitionEnabledEffective())
            {
                EnforceIgnitionOffForCurrentCar();
            }

            // Indicator blink/update should run continuously while driving,
            // not only when input is queried.
            UpdateIndicators(__instance);
        }


        [HarmonyPatch(typeof(sInputManager), "GetInput")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void SInputManager_GetInput_Postfix(sInputManager __instance)
        {
            if (__instance == null)
            {
                return;
            }

            // Only apply while driving.
            var car = _currentCar;
            if (car == null)
            {
                // Fallback for early frames / unexpected call order.
                car = UnityEngine.Object.FindFirstObjectByType<sCarController>();
            }
            if (car == null || car.GuyActive)
            {
                _ignitionHoldStart = -1f;
                _ignitionHoldConsumed = false;
                _ignitionHoldWasDown = false;
                _ignitionIgnoreHoldUntilRelease = false;
                return;
            }

            if (PauseSystem.paused || __instance.lockInput)
            {
                return;
            }

            BindingEvaluator.BeginFrame();

            var schemes = _bindingSchemes;

            bool Pressed(BindingInput b) => b.Kind != BindingKind.None && BindingEvaluator.WasPressedThisFrame(b);
            bool Released(BindingInput b) => b.Kind != BindingKind.None && BindingEvaluator.WasReleasedThisFrame(b);
            bool Down(BindingInput b) => b.Kind != BindingKind.None && BindingEvaluator.IsDown(b);

            BindingLayer LayerFor(BindingScheme s)
            {
                var mod = BindingStore.GetModifierBinding(s);
                bool modifierDown = mod.Kind != BindingKind.None && BindingEvaluator.IsDown(mod);
                return modifierDown ? BindingLayer.Modified : BindingLayer.Normal;
            }

            BindingInput GetBind(BindingScheme s, BindAction action)
            {
                var layer = LayerFor(s);
                var b = BindingStore.GetBinding(s, layer, action);
                if (b.Kind == BindingKind.None && layer == BindingLayer.Modified)
                {
                    b = BindingStore.GetBinding(s, BindingLayer.Normal, action);
                }
                return b;
            }

            bool PressedAny(BindAction a)
            {
                for (int i = 0; i < schemes.Length; i++)
                {
                    if (Pressed(GetBind(schemes[i], a)))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool ReleasedAny(BindAction a)
            {
                for (int i = 0; i < schemes.Length; i++)
                {
                    if (Released(GetBind(schemes[i], a)))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool DownAny(BindAction a)
            {
                for (int i = 0; i < schemes.Length; i++)
                {
                    if (Down(GetBind(schemes[i], a)))
                    {
                        return true;
                    }
                }
                return false;
            }

            // Transmission + ignition binds.
            {
                // Ignition
                bool ignitionFeature = GetIgnitionFeatureEnabled();
                bool ignitionPressed = PressedAny(BindAction.IgnitionToggle);
                bool ignitionDown = DownAny(BindAction.IgnitionToggle);
                bool ignitionReleased = ReleasedAny(BindAction.IgnitionToggle);

                bool indicatorFeature = GetIndicatorFeatureEnabled();
                bool indicatorLeftPressed = PressedAny(BindAction.IndicatorLeft);
                bool indicatorRightPressed = PressedAny(BindAction.IndicatorRight);
                bool indicatorHazardsPressed = PressedAny(BindAction.IndicatorHazards);

                if (!ignitionFeature)
                {
                    _ignitionHoldStart = -1f;
                    _ignitionHoldConsumed = false;
                    _ignitionHoldWasDown = ignitionDown;
                    _ignitionIgnoreHoldUntilRelease = false;
                    ignitionPressed = false;
                    ignitionDown = false;
                    ignitionReleased = false;
                }

                if (ignitionFeature && GetIgnitionEnabled() && ignitionPressed)
                {
                    StopIgnitionHoldSfx();
                    _ignitionHoldStart = -1f;
                    _ignitionHoldConsumed = false;
                    _ignitionIgnoreHoldUntilRelease = true;

                    SetIgnitionEnabled(false);
                    ApplyIgnitionStateChange(false);

                    LogDebug("Ignition toggled OFF");
                }

                if (ignitionFeature && !GetIgnitionEnabled() && ignitionDown && !_ignitionIgnoreHoldUntilRelease)
                {
                if (_ignitionHoldStart < 0f)
                {
                    _ignitionHoldStart = Time.unscaledTime;
                    _ignitionHoldConsumed = false;

                    LogDebug("Ignition hold started");

                    StartIgnitionHoldSfx(car);
                }

                    float holdS = GetIgnitionHoldSeconds();
                    if (!_ignitionHoldConsumed && Time.unscaledTime - _ignitionHoldStart >= holdS)
                    {
                        SetIgnitionEnabled(true);
                        ApplyIgnitionStateChange(true);
                        _ignitionHoldConsumed = true;

                        LogDebug("Ignition hold complete");

                        // Let the ignition SFX finish naturally (do not cut it off early).
                    }
                }

                if (ignitionReleased)
                {
                    // If the player releases before ignition succeeds, stop the SFX.
                    // If ignition already turned on, let the one-shot SFX finish naturally.
                    if (ignitionFeature && !GetIgnitionEnabled())
                    {
                        StopIgnitionHoldSfx();
                    }
                    _ignitionIgnoreHoldUntilRelease = false;
                    _ignitionHoldStart = -1f;
                    _ignitionHoldConsumed = false;

                    if (ignitionFeature && !GetIgnitionEnabled())
                    {
                        LogDebug("Ignition hold cancelled");
                    }
                }

                // Indicators (only while ignition is effectively on).
                if (indicatorFeature && GetIgnitionEnabledEffective())
                {
                    if (indicatorHazardsPressed)
                    {
                        _indicatorMode = _indicatorMode == IndicatorMode.Hazards ? IndicatorMode.Off : IndicatorMode.Hazards;
                        _indicatorNextBlinkTime = 0f;
                    }
                    if (indicatorLeftPressed)
                    {
                        _indicatorMode = _indicatorMode == IndicatorMode.Left ? IndicatorMode.Off : IndicatorMode.Left;
                        _indicatorNextBlinkTime = 0f;
                    }
                    if (indicatorRightPressed)
                    {
                        _indicatorMode = _indicatorMode == IndicatorMode.Right ? IndicatorMode.Off : IndicatorMode.Right;
                        _indicatorNextBlinkTime = 0f;
                    }
                }
                else
                {
                    _indicatorMode = IndicatorMode.Off;
                }

                UpdateIndicatorAutoCancel(__instance.driveInput.x);

                _ignitionHoldWasDown = ignitionDown;


                // Toggle manual transmission
                if (PressedAny(BindAction.ToggleGearbox))
                {
                    ToggleManualTransmission();
                }

                // Shift
                if (PressedAny(BindAction.ShiftUp))
                {
                    if (!GetManualTransmissionEnabled())
                    {
                        SetManualTransmissionEnabled(true);
                    }
                    ShiftManualGear(+1);
                }

                if (PressedAny(BindAction.ShiftDown))
                {
                    if (!GetManualTransmissionEnabled())
                    {
                        SetManualTransmissionEnabled(true);
                    }
                    ShiftManualGear(-1);
                }
            }


            // Manual transmission + ignition enforcement (applies to any input source).
            if (!PauseSystem.paused)
            {
                float y = __instance.driveInput.y;

                // Vanilla uses a combined axis for brake/reverse (negative y).
                // Also keep supporting the separate handbrake/back flag.
                float throttle = Mathf.Clamp01(y);
                if (throttle < 0f) throttle = 0f;

                float brake = Mathf.Clamp01(-y);
                float handbrake = __instance.breakPressed ? 1f : 0f;
                brake = Mathf.Max(brake, handbrake);

                // Track rev input for Neutral.
                _lastThrottle01 = throttle;
                if (GetManualTransmissionEnabled() && GetManualGear() == 0)
                {
                    _neutralRev01 = Mathf.Lerp(_neutralRev01, _lastThrottle01, Time.deltaTime * 8f);
                }
                else
                {
                    _neutralRev01 = Mathf.Lerp(_neutralRev01, 0f, Time.deltaTime * 6f);
                }

                float accel;
                if (GetManualTransmissionEnabled())
                {
                    int gear = GetManualGear();
                    float drive = ComputeManualAccel(throttle);

                    float signedKmh = _currentSpeedKmh;
                    try
                    {
                        if (car.rb != null)
                        {
                            signedKmh = Vector3.Dot(car.rb.linearVelocity, car.transform.forward) * 3.6f;
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                    if (gear > 0)
                    {
                        if (signedKmh > 1.0f)
                        {
                            accel = drive - brake;
                        }
                        else
                        {
                            accel = Mathf.Max(0f, drive - brake);
                        }
                    }
                    else if (gear < 0)
                    {
                        if (signedKmh < -1.0f)
                        {
                            accel = drive + brake;
                        }
                        else
                        {
                            accel = Mathf.Min(0f, drive + brake);
                        }
                    }
                    else
                    {
                        accel = 0f;
                    }
                }
                else
                {
                    accel = __instance.driveInput.y;
                }

                accel = Mathf.Clamp(accel, -1f, 1f);
                if (!GetIgnitionEnabledEffective())
                {
                    accel = 0f;
                }

                var v = __instance.driveInput;
                v.y = accel;
                __instance.driveInput = v;
            }
        }


        [HarmonyPatch(typeof(sRadioSystem), "Update")]
        [HarmonyPostfix]
        private static void SRadioSystem_Update_Postfix(sRadioSystem __instance)
        {
            MuteRadioProximityIfIgnitionOff(__instance);
        }


        [HarmonyPatch(typeof(Headlights), "Break")]
        [HarmonyPostfix]
        private static void Headlights_Break_Postfix(Headlights __instance, bool breaking)
        {
            if (__instance == null)
            {
                return;
            }

            sCarController car = null;
            try
            {
                car = __instance.GetComponentInParent<sCarController>();
            }
            catch
            {
                // ignore
            }
            if (car == null)
            {
                return;
            }

            SetTruckBraking(car, breaking);
            ApplyTruckEmissionMap(car);
        }


        [HarmonyPatch(typeof(sHUD), "DoFuelMath")]
        [HarmonyPrefix]
        private static bool SHUD_DoFuelMath_Prefix()
        {
            if (!GetIgnitionEnabledEffective())
            {
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(sHUD), "DoTemperature")]
        [HarmonyPostfix]
        private static void SHUD_DoTemperature_Postfix(sHUD __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (__instance.navigation == null || __instance.navigation.car == null || __instance.navigation.car.GuyActive)
            {
                return;
            }

            if (!GetIgnitionFeatureEnabled() || GetIgnitionEnabledEffective())
            {
                return;
            }

            if (_ignitionOffSince < 0f)
            {
                _ignitionOffSince = Time.unscaledTime;
            }
            if (Time.unscaledTime - _ignitionOffSince < 15.0f)
            {
                return;
            }

            __instance.AddWarning("truck temperature low");
            float extra = __instance.temperatureRate * 0.5f;
            __instance.temperature = Mathf.Clamp(__instance.temperature - Time.deltaTime * extra, 0f, __instance.temperatureLimit);
        }


        [HarmonyPatch]
        private static class Headlights_Toggle_Patch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                return AccessTools
                    .GetDeclaredMethods(typeof(Headlights))
                    .Where(m => m != null && string.Equals(m.Name, "Toggle", StringComparison.Ordinal));
            }

            private static bool Prefix()
            {
                return GetIgnitionEnabledEffective();
            }

            private static void Postfix(Headlights __instance)
            {
                try
                {
                    if (__instance == null)
                    {
                        return;
                    }
                    var car = __instance.GetComponentInParent<sCarController>();
                    if (car == null)
                    {
                        return;
                    }
                    ApplyHeadlightTuning(car);
                }
                catch
                {
                    // ignore
                }
            }
        }

        [HarmonyPatch]
        private static class Headlights_Break_Patch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                return AccessTools
                    .GetDeclaredMethods(typeof(Headlights))
                    .Where(m => m != null && string.Equals(m.Name, "Break", StringComparison.Ordinal));
            }

            private static bool Prefix()
            {
                return GetIgnitionEnabledEffective();
            }
        }


        [HarmonyPatch(typeof(sEngineSFX), "Update")]
        [HarmonyPostfix]
        private static void SEngineSFX_Update_Postfix(sEngineSFX __instance)
        {
            if (__instance == null)
            {
                return;
            }

            EnsureEngineSfxRefs(__instance);

            var car = _engineCarField != null ? _engineCarField.GetValue(__instance) as sCarController : null;
            if (car == null)
            {
                return;
            }

            // Only adjust the active car.
            if (_currentCar != null && !ReferenceEquals(car, _currentCar))
            {
                return;
            }

            // Ignition off: hard-mute engine loops.
            if (GetIgnitionFeatureEnabled() && !GetIgnitionEnabledEffective())
            {
                try
                {
                    var idle0 = _engineIdleField != null ? _engineIdleField.GetValue(__instance) as Component : null;
                    var drive0 = _engineDriveField != null ? _engineDriveField.GetValue(__instance) as Component : null;
                    var intense0 = _engineIntenseField != null ? _engineIntenseField.GetValue(__instance) as Component : null;
                    if (idle0 != null) SetProp(idle0, "volume", 0f);
                    if (drive0 != null) SetProp(drive0, "volume", 0f);
                    if (intense0 != null) SetProp(intense0, "volume", 0f);

                    var dist0 = _engineDistortionField != null ? _engineDistortionField.GetValue(__instance) as Component : null;
                    if (dist0 != null) SetProp(dist0, "distortionLevel", 0f);
                }
                catch
                {
                    // ignore
                }
                return;
            }

            // Don't touch engine proximity audio while walking (ignition-on case).
            if (car.GuyActive)
            {
                return;
            }

            // Manual-mode-only sound tweaks.
            if (!GetManualTransmissionEnabled())
            {
                return;
            }

            float rpmNorm = GetEstimatedRpmNormForSound();
            float over = Mathf.Clamp01((rpmNorm - 1f) / 0.2f);
            float neutral = (_manualGear == 0) ? Mathf.Clamp01(_neutralRev01) : 0f;

            // Warning ramp for high RPM (aligns with HUD suffix thresholds).
            float warn = Mathf.Clamp01((rpmNorm - 0.92f) / (1.0f - 0.92f));

            // Apply pitch boost to all loops so revving in Neutral is audible.
            float pitchMul = 1f + neutral * 0.85f + over * 0.35f;

            var idle = _engineIdleField != null ? _engineIdleField.GetValue(__instance) as Component : null;
            var drive = _engineDriveField != null ? _engineDriveField.GetValue(__instance) as Component : null;
            var intense = _engineIntenseField != null ? _engineIntenseField.GetValue(__instance) as Component : null;

            void ApplyPitchMul(Component src)
            {
                if (src == null)
                {
                    return;
                }

                int id = src.GetInstanceID();
                float last = 1f;
                if (_enginePitchMulApplied.TryGetValue(id, out float prev) && prev > 0.0001f)
                {
                    last = prev;
                }

                // Avoid compounding multipliers frame-to-frame.
                float cur = GetFloatProp(src, "pitch", 1f);
                SetProp(src, "pitch", (cur / last) * pitchMul);
                _enginePitchMulApplied[id] = pitchMul;
            }

            ApplyPitchMul(idle);
            ApplyPitchMul(drive);
            ApplyPitchMul(intense);

            // In neutral, also push volume up so it actually sounds like revving.
            if (neutral > 0.01f)
            {
                if (drive != null)
                {
                    float v = GetFloatProp(drive, "volume", 0f);
                    SetProp(drive, "volume", Mathf.Max(v, neutral * 0.55f));
                }
                if (intense != null)
                {
                    float v = GetFloatProp(intense, "volume", 0f);
                    SetProp(intense, "volume", Mathf.Max(v, neutral * 0.35f));
                }
            }

            // Overrev: ramp up the same "revving" feel as Neutral (warn -> !, over -> !!).
            if (warn > 0.01f)
            {
                float driveBoost = Mathf.Min(1f, warn * 0.35f + over * 0.45f);
                float intenseBoost = Mathf.Min(1f, warn * 0.22f + over * 0.35f);
                if (drive != null)
                {
                    float v = GetFloatProp(drive, "volume", 0f);
                    SetProp(drive, "volume", Mathf.Max(v, driveBoost));
                }
                if (intense != null)
                {
                    float v = GetFloatProp(intense, "volume", 0f);
                    SetProp(intense, "volume", Mathf.Max(v, intenseBoost));
                }
            }

            // Over-rev: make it sound like a strained neutral rev with sputter.
            if (over > 0.01f)
            {
                float freq = Mathf.Lerp(7f, 14f, over);
                float saw = Mathf.Repeat(Time.time * freq, 1f);
                float ramp = 1f - saw; // 1..0
                float cut = (saw < 0.12f) ? Mathf.Lerp(0.25f, 1f, saw / 0.12f) : 1f;

                float baseMul = Mathf.Lerp(1.0f, 0.72f, over);
                float stutterMul = Mathf.Lerp(1.0f, ramp, over) * cut;
                float volMul = baseMul * stutterMul;
                float pitchJitter = 1f + (Mathf.Sin(Time.time * 55f) * 0.012f * over);

                if (drive != null)
                {
                    SetProp(drive, "volume", GetFloatProp(drive, "volume", 0f) * volMul);
                    SetProp(drive, "pitch", GetFloatProp(drive, "pitch", 1f) * pitchJitter);
                }
                if (intense != null)
                {
                    SetProp(intense, "volume", GetFloatProp(intense, "volume", 0f) * volMul);
                    SetProp(intense, "pitch", GetFloatProp(intense, "pitch", 1f) * pitchJitter);
                }
            }

            // Extra distortion when over-revving (simulated).
            var dist = _engineDistortionField != null ? _engineDistortionField.GetValue(__instance) as Component : null;
            if (dist != null)
            {
                float target = GetFloatProp(dist, "distortionLevel", 0f);
                if (over > 0f)
                {
                    target = Mathf.Max(target, 0.10f + over * 0.55f);
                }
                if (neutral > 0.1f)
                {
                    target = Mathf.Max(target, 0.05f + neutral * 0.10f);
                }
                SetProp(dist, "distortionLevel", target);
            }
        }


        [HarmonyPatch(typeof(sPathFinder), "DoResetCar")]
        [HarmonyPostfix]
        private static void SPathFinder_DoResetCar_Postfix()
        {
            if (!GetManualTransmissionEnabled())
            {
                return;
            }

            // Any reset path (manual reset, ice crack, explosion) should leave you in 1st gear.
            _manualGear = 1;
            _neutralRev01 = 0f;
        }


        [HarmonyPatch(typeof(sHUD), "RadioDisplay")]
        [HarmonyPostfix]
        private static void SHUD_RadioDisplay_Postfix(object __instance)
        {
            var hud = __instance as sHUD;
            if (hud == null)
            {
                return;
            }

            var car = _currentCar;
            if (car == null)
            {
                car = UnityEngine.Object.FindFirstObjectByType<sCarController>();
            }
            if (car == null || car.GuyActive)
            {
                return;
            }

            var rField = AccessTools.Field(typeof(sHUD), "R");
            var R = rField != null ? rField.GetValue(hud) as MiniRenderer : null;
            if (R == null)
            {
                return;
            }

            bool ignitionFeature = GetIgnitionFeatureEnabled();
            bool ignOn = GetIgnitionEnabledEffective();
            bool starting = ignitionFeature && !GetIgnitionEnabled() && _ignitionHoldStart >= 0f && !_ignitionHoldConsumed;

            bool showSpeed = ignOn && GetHudShowSpeed();
            bool manualEnabled = GetManualTransmissionEnabled();
            bool showTach = ignOn && manualEnabled && GetHudShowTach();
            bool showGear = ignOn && manualEnabled && GetHudShowGear();

            bool showOff = ignitionFeature && !ignOn && !starting;
            if (!starting && !showOff && !showSpeed && !showTach && !showGear)
            {
                return;
            }

            float xLeft = 68f;
            float xRight = R.width - 68f;
            float yLeft = R.height - 64f + 22f;
            float yRight = yLeft;

            var aSpeed = GetHudSpeedAnchor();
            var aTach = GetHudTachAnchor();
            var aGear = GetHudGearAnchor();

            void PutLine(HudReadoutAnchor a, string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }
                if (a == HudReadoutAnchor.BottomRight)
                {
                    R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
                    R.fput(text, xRight, yRight);
                    yRight += 10f;
                }
                else
                {
                    R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;
                    R.put(text, xLeft, yLeft);
                    yLeft += 10f;
                }
            }

            if (starting)
            {
                float holdS = Mathf.Max(0.01f, GetIgnitionHoldSeconds());
                float t = Mathf.Clamp01((Time.unscaledTime - _ignitionHoldStart) / holdS);
                PutLine(aSpeed, "START " + Mathf.RoundToInt(t * 100f) + "%");
            }
            else if (showOff)
            {
                PutLine(aSpeed, "ENGN OFF");
            }

            if (showSpeed)
            {
                float spd = ConvertSpeedForHud(_currentSpeedKmh);
                int spdInt = Mathf.Max(0, Mathf.RoundToInt(spd));
                string unit = GetHudSpeedUnitLabel(GetHudSpeedUnit());
                PutLine(aSpeed, spdInt + unit);
            }
            if (showTach)
            {
                float target = GetEstimatedRpm();
                float rpmNorm = Mathf.Clamp(GetEstimatedRpmNormForSound(), 0f, 1.2f);
                bool lastGear = GetManualGear() > 0 && GetManualGear() == GetManualGearCount();
                string suffix = lastGear ? "" : (rpmNorm >= 1.0f ? "!!" : (rpmNorm >= 0.92f ? "!" : ""));
                PutLine(aTach, Mathf.RoundToInt(target) + "rpm" + suffix);
            }
            if (showGear)
            {
                int g = GetManualGear();
                int gearCount = GetManualGearCount();

                string line = "RN";
                for (int i = 1; i <= gearCount; i++)
                {
                    line += i.ToString();
                }

                if (aGear == HudReadoutAnchor.BottomRight)
                {
                    float lineXLeft = xRight - 8f * line.Length;
                    R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;
                    R.put(line, lineXLeft, yRight);
                    int selIndex = g < 0 ? 0 : (g == 0 ? 1 : Mathf.Clamp(g, 1, gearCount) + 1);
                    yRight += 10f;
                    R.put("^", lineXLeft + 8f * selIndex, yRight);
                    yRight += 10f;
                }
                else
                {
                    R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;
                    R.put(line, xLeft, yLeft);
                    int selIndex = g < 0 ? 0 : (g == 0 ? 1 : Mathf.Clamp(g, 1, gearCount) + 1);
                    yLeft += 10f;
                    R.put("^", xLeft + 8f * selIndex, yLeft);
                    yLeft += 10f;
                }
            }
        }

        // Note: engine SFX patching uses reflection to avoid AudioModule reference.
    }
}
