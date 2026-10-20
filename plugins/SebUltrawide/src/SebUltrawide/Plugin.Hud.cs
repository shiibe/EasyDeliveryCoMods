using System.Collections.Generic;
using UnityEngine;

namespace SebUltrawide
{
    public partial class Plugin
    {
        private static readonly Dictionary<int, int> HudMiniRendererOriginalWidths = new Dictionary<int, int>();

        private struct PixelPerfectState
        {
            public int Width;
            public int Height;
            public float Aspect;
            public float Fov;
            public float Z;

            public bool Equals(PixelPerfectState other)
            {
                return Width == other.Width
                    && Height == other.Height
                    && Mathf.Abs(Aspect - other.Aspect) < 0.0001f
                    && Mathf.Abs(Fov - other.Fov) < 0.001f
                    && Mathf.Abs(Z - other.Z) < 0.0001f;
            }
        }

        private static bool PixelPerfectView_AdjustViewPlane_Prefix(pixelPerfectView __instance, Camera ___gameCamera)
        {
            // Narrower-than-16:9 windows keep the game's plane; widening it would squash the 16:9 image.
            if (__instance == null || ___gameCamera == null || !IsUltrawide())
            {
                return true;
            }

            float menuWidth = __instance.menuWidth;
            float menuHeight = __instance.menuHeight;
            if (menuWidth <= 0.01f || menuHeight <= 0.01f)
            {
                return true;
            }

            float sourceAspect = menuWidth / menuHeight;
            bool isWideTarget = sourceAspect > 1.65f && sourceAspect < 1.9f;
            if (!isWideTarget)
            {
                LogPixelPerfectSkip(__instance, sourceAspect);
                return true;
            }

            var transform = __instance.transform;
            float num = 2f * transform.localPosition.z * Mathf.Tan(___gameCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = GetWindowAspect();
            int id = __instance.GetInstanceID();
            var state = new PixelPerfectState
            {
                Width = Screen.width,
                Height = Screen.height,
                Aspect = aspect,
                Fov = ___gameCamera.fieldOfView,
                Z = transform.localPosition.z
            };

            if (!PixelPerfectStates.TryGetValue(id, out var previous) || !previous.Equals(state))
            {
                PixelPerfectStates[id] = state;
                transform.localScale = new Vector3(num * aspect, num, 1f);
            }
            LogPixelPerfectScale(__instance, aspect, sourceAspect);
            return false;
        }

        private static void MiniRenderer_Start_Postfix(MiniRenderer __instance)
        {
            if (!IsUltrawide())
            {
                return;
            }

            EnsureMiniRendererRenderTexture(__instance);
            ApplyHudDisplayScale(__instance);
        }

        private static void EnsureMiniRendererRenderTexture(MiniRenderer renderer)
        {
            if (renderer == null || renderer.width <= 0 || renderer.height <= 0)
            {
                return;
            }

            var rt = renderer.rt;
            if (rt != null && rt.width == renderer.width && rt.height == renderer.height)
            {
                return;
            }

            var newRt = new RenderTexture(renderer.width, renderer.height, rt != null ? rt.depth : 32);
            if (rt != null)
            {
                newRt.filterMode = rt.filterMode;
            }

            renderer.rt = newRt;
            UpdateMiniRendererTextures(renderer, newRt);

            if (MiniRenderer.renderers != null && !string.IsNullOrEmpty(renderer.rendererName))
            {
                MiniRenderer.renderers[renderer.rendererName] = newRt;
            }
        }

        private static void UpdateMiniRendererTextures(MiniRenderer renderer, RenderTexture rt)
        {
            var material = renderer.mat;
            if (material == null)
            {
                var unityRenderer = renderer.GetComponent<Renderer>();
                material = unityRenderer != null ? unityRenderer.material : null;
            }

            if (material != null)
            {
                material.SetTexture(MainTexId, rt);
                material.SetTexture(AlphaTexId, rt);
            }
        }

        private static void RefreshHudRendererAspect()
        {
            var hud = Object.FindFirstObjectByType<sHUD>();
            UpdateHudRendererAspect(hud != null ? hud.GetComponentInParent<MiniRenderer>(true) : null);
        }

        // Widen the HUD canvas on ultrawide; restore the original width when the window is no longer ultrawide.
        private static void UpdateHudRendererAspect(MiniRenderer miniRenderer)
        {
            if (miniRenderer == null || miniRenderer.width <= 0 || miniRenderer.height <= 0)
            {
                return;
            }

            bool ultrawide = IsUltrawide();
            int rendererId = miniRenderer.GetInstanceID();
            if (!HudMiniRendererOriginalWidths.TryGetValue(rendererId, out int originalWidth))
            {
                if (!ultrawide)
                {
                    return;
                }

                originalWidth = miniRenderer.width;
                HudMiniRendererOriginalWidths[rendererId] = originalWidth;
            }

            int targetWidth = ultrawide
                ? Mathf.Clamp(Mathf.RoundToInt(miniRenderer.height * GetWindowAspect()), 1, 8192)
                : originalWidth;
            if (miniRenderer.width == targetWidth)
            {
                return;
            }

            miniRenderer.width = targetWidth;
            EnsureMiniRendererRenderTexture(miniRenderer);
            LogDebug($"Set HUD MiniRenderer width to {targetWidth} (window aspect {GetWindowAspect():0.###}).");
        }

        private static void ApplyHudDisplayScale(MiniRenderer renderer)
        {
            if (renderer == null || renderer.width <= 0 || renderer.height <= 0)
            {
                return;
            }

            float windowAspect = GetWindowAspect();
            if (windowAspect <= 0.01f)
            {
                return;
            }

            var rt = renderer.rt;
            foreach (var meshRenderer in renderer.GetComponentsInParent<MeshRenderer>(true))
            {
                var material = meshRenderer != null ? meshRenderer.sharedMaterial : null;
                if (material == null)
                {
                    continue;
                }

                if (rt != null && material.mainTexture != rt && material.GetTexture(MainTexId) != rt)
                {
                    continue;
                }

                var transform = meshRenderer.transform;
                int id = transform.GetInstanceID();
                if (!HudDisplayOriginalScales.TryGetValue(id, out var baseScale))
                {
                    baseScale = transform.localScale;
                    HudDisplayOriginalScales[id] = baseScale;
                }

                float scaleX = windowAspect / (renderer.width / (float)renderer.height);
                transform.localScale = new Vector3(baseScale.x * scaleX, baseScale.y, baseScale.z);
                LogDebug($"Scaled HUD display to {scaleX:0.###}x (aspect {windowAspect:0.###}).");
            }
        }
    }
}
