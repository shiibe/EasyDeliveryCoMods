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
        private static bool _isInWalkingMode;
        private static float _currentSpeedKmh;
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

        private static Type _headlightsRuntimeType;
        private static FieldInfo _headlightsHeadLightsField;
        private static FieldInfo _headlightsCarMatField;
        private static FieldInfo _headlightsEmissiveRegularField;
        private static FieldInfo _headlightsHeadlightsOnField;
        private static FieldInfo _headlightsModelField;
        private static Texture2D _ignitionBlackEmissiveTex;

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

            if (_indicatorMode == IndicatorMode.Off)
            {
                _indicatorBlinkOn = false;
                SetIndicators(car, IndicatorMode.Off, blinkOn: false, forceOff: false);
                return;
            }

            bool blinkWasOn = _indicatorBlinkOn;

            float now = Time.unscaledTime;
            if (_indicatorNextBlinkTime <= 0f)
            {
                _indicatorBlinkOn = true;
                _indicatorNextBlinkTime = now + 0.45f;
            }
            else if (now >= _indicatorNextBlinkTime)
            {
                _indicatorBlinkOn = !_indicatorBlinkOn;
                _indicatorNextBlinkTime = now + 0.45f;
            }

            // Click sound on each blink edge.
            if (_indicatorBlinkOn != blinkWasOn)
            {
                PlayIndicatorClick(car, highPitch: _indicatorBlinkOn);
            }

            SetIndicators(car, _indicatorMode, _indicatorBlinkOn, forceOff: false);
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
                mat.SetTexture("_EmissionMap", regular);
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
                if (_ignitionPrevHeadlightsOn && hl != null)
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

            EnsureHeadlightsRefs(hl);
            var go = _headlightsHeadLightsField != null ? _headlightsHeadLightsField.GetValue(hl) as GameObject : null;
            if (go == null)
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

            Light[] lights;
            try
            {
                lights = go.GetComponentsInChildren<Light>(true);
            }
            catch
            {
                lights = null;
            }

            if (lights == null)
            {
                return;
            }

            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
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
        }

        private static float GetMaxSpeedForGearKmh(int gear)
        {
            int count = GetManualGearCount();
            gear = Mathf.Clamp(gear, 1, count);

            const float baseKmh = 25f;
            const float topKmh = 125f;

            // Use a linear distribution so the last gear isn't a huge jump.
            // (With 5 gears, this yields 25/50/75/100/125 instead of an exponential ramp.)
            float t = count <= 1 ? 1f : (gear - 1f) / (count - 1f);
            return Mathf.Lerp(baseKmh, topKmh, Mathf.Clamp01(t));
        }

        private static float GetMaxSpeedForCurrentGearKmh()
        {
            int g = _manualGear;
            if (g == 0)
            {
                return 1f;
            }
            float baseKmh = GetMaxSpeedForGearKmh(Mathf.Clamp(Mathf.Abs(g), 1, GetManualGearCount()));
            return Mathf.Max(1f, baseKmh);
        }

        internal static float GetEstimatedRpm()
        {
            const float idle = 900f;
            const float redline = 6500f;

            float t;
            if (!GetManualTransmissionEnabled())
            {
                t = Mathf.Clamp01(_currentSpeedKmh / 140f);
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
                return Mathf.Clamp01(_currentSpeedKmh / 140f);
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

            if (__instance.rb != null)
            {
                _currentSpeedKmh = __instance.rb.linearVelocity.magnitude * 3.6f;
            }

            ApplyHeadlightTuning(__instance);
            ApplySpeedScaleTuning(__instance);

            if (!GetIgnitionEnabledEffective())
            {
                EnforceIgnitionOffForCurrentCar();
            }
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
            var car = UnityEngine.Object.FindFirstObjectByType<sCarController>();
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

            var schemes = new[] { BindingScheme.Controller, BindingScheme.Keyboard, BindingScheme.Wheel };

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
                if (GetIgnitionEnabledEffective())
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

                UpdateIndicators(car);

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
                float throttle = Mathf.Clamp01(__instance.driveInput.y);
                if (throttle < 0f) throttle = 0f;

                float brake = __instance.breakPressed ? 1f : 0f;

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
            if (Time.unscaledTime - _ignitionOffSince < 30.0f)
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

            if (!GetIgnitionFeatureEnabled() || GetIgnitionEnabledEffective())
            {
                return;
            }

            // Mute idle/engine loops while ignition is off.
            try
            {
                var idle = AccessTools.Field(typeof(sEngineSFX), "idle")?.GetValue(__instance) as Component;
                var idleLow = AccessTools.Field(typeof(sEngineSFX), "idleLowFuel")?.GetValue(__instance) as Component;
                var drive = AccessTools.Field(typeof(sEngineSFX), "drive")?.GetValue(__instance) as Component;
                var intense = AccessTools.Field(typeof(sEngineSFX), "intense")?.GetValue(__instance) as Component;

                SetProp(idle, "volume", 0f);
                SetProp(idleLow, "volume", 0f);
                SetProp(drive, "volume", 0f);
                SetProp(intense, "volume", 0f);
            }
            catch
            {
            }
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

            var car = UnityEngine.Object.FindFirstObjectByType<sCarController>();
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

        // Note: engine SFX patching removed (requires UnityEngine.AudioModule).
    }
}
