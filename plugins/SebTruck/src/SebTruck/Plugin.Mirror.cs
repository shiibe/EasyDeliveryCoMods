using SebCore;
using UnityEngine;

namespace SebTruck
{
    public partial class Plugin
    {
        // The rear mirror render target is owned by SebCore's shared graphics pipeline.
        internal static int GetMirrorQuality() => ModGraphics.GetMirrorQuality();

        internal static string GetMirrorQualityLabel(int quality)
        {
            return quality == 0 ? "Original" : (1 << Mathf.Clamp(quality, 0, 3)) + "x";
        }

        internal static void SetMirrorQuality(int quality) => ModGraphics.SetMirrorQuality(quality);

        private void OnEnable() => ModGraphics.EnableMirrorQuality(true);

        private void OnDisable() => ModGraphics.EnableMirrorQuality(false);
    }
}
