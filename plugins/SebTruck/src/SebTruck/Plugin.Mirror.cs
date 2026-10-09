using System.Collections.Generic;
using System.Reflection;
using SebCore;
using UnityEngine;

namespace SebTruck
{
    public partial class Plugin
    {
        internal const string PrefKeyMirrorQuality = "SebTruck_MirrorQuality";
        private readonly Dictionary<RenderTexture, RearViewMirrorQuality> _mirrorTextures = new Dictionary<RenderTexture, RearViewMirrorQuality>();
        private float _nextMirrorScan;
        private int _lastMirrorQuality = -1;
        private bool _graphicsProvidersActive;
        private static Plugin _mirrorPlugin;

        internal static int GetMirrorQuality() => Mathf.Clamp(ModPrefs.GetInt(PrefKeyMirrorQuality, 0), 0, 3);

        internal static string GetMirrorQualityLabel(int quality)
        {
            return quality == 0 ? "Original" : (1 << Mathf.Clamp(quality, 0, 3)) + "x";
        }

        internal static void SetMirrorQuality(int quality)
        {
            ModPrefs.SetInt(PrefKeyMirrorQuality, Mathf.Clamp(quality, 0, 3));
            _mirrorPlugin?.UpdateMirrors();
        }

        private void Update()
        {
            _mirrorPlugin = this;
            if (Time.unscaledTime < _nextMirrorScan) return;
            _nextMirrorScan = Time.unscaledTime + 1f;
            UpdateMirrors();
        }

        private void UpdateMirrors()
        {
            int quality = GetMirrorQuality();
            // Graphics providers already own the camera/material targets and apply our setting.
            bool tweaks = IsGraphicsProviderActive("shibe.easydeliveryco.sebtweaks");
            bool ultrawide = IsGraphicsProviderActive("shibe.easydeliveryco.sebultrawide");
            if (tweaks || ultrawide)
            {
                RestoreMirrors();
                if (quality != _lastMirrorQuality || !_graphicsProvidersActive) RefreshGraphicsProviders();
                _lastMirrorQuality = quality;
                _graphicsProvidersActive = true;
                return;
            }
            _lastMirrorQuality = quality;
            _graphicsProvidersActive = false;
            // Restore before searching: Original must also undo changes on inactive cameras.
            if (quality == 0)
            {
                RestoreMirrors();
                return;
            }

            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var camera in cameras)
            {
                RenderTexture texture = camera.targetTexture;
                if (texture == null || texture.name != "RearView") continue;
                if (!_mirrorTextures.TryGetValue(texture, out var mirror))
                {
                    mirror = new RearViewMirrorQuality(texture);
                    _mirrorTextures.Add(texture, mirror);
                }
                if (!mirror.Apply(quality))
                {
                    Log?.LogWarning("Could not resize the rear-view mirror. Restoring Original quality.");
                    ModPrefs.SetInt(PrefKeyMirrorQuality, 0);
                    RestoreMirrors();
                    return;
                }
            }
        }

        private static bool IsGraphicsProviderActive(string guid)
        {
            return BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(guid, out var info) &&
                info.Instance != null && info.Instance.isActiveAndEnabled;
        }

        private static void RefreshGraphicsProviders()
        {
            foreach (string guid in new[] { "shibe.easydeliveryco.sebtweaks", "shibe.easydeliveryco.sebultrawide" })
            {
                if (!IsGraphicsProviderActive(guid)) continue;
                try
                {
                    var type = BepInEx.Bootstrap.Chainloader.PluginInfos[guid].Instance.GetType();
                    type.GetMethod("RefreshPixelation", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
                }
                catch (System.Exception e)
                {
                    Log?.LogWarning("Could not refresh mirror graphics: " + e.Message);
                }
            }
        }

        private void RestoreMirrors()
        {
            foreach (var mirror in _mirrorTextures.Values) mirror.Apply(0);
            _mirrorTextures.Clear();
        }

        private void OnDisable()
        {
            RestoreMirrors();
            _lastMirrorQuality = -1;
            _graphicsProvidersActive = false;
            if (_mirrorPlugin == this) _mirrorPlugin = null;
            RefreshGraphicsProviders();
        }
    }
}
