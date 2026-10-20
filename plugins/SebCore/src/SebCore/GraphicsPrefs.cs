using UnityEngine;

namespace SebCore
{
    // Pref keys and migrations for the shared graphics settings. Kept free of
    // camera/render types so the regression tests can link it directly.
    public static partial class ModGraphics
    {
        // Key strings predate SebCore ownership; keep them so saved settings survive.
        internal const string PrefKeyFovLegacy = "UltrawideFovOverride";
        internal const string PrefKeyFovThirdPerson = "UltrawideFovOverride_ThirdPerson";
        internal const string PrefKeyFovFirstPerson = "UltrawideFovOverride_FirstPerson";
        internal const string PrefKeyPixelationMode = "UltrawidePixelationMode";
        internal const string PrefKeyPixelationModeVersion = "UltrawidePixelationModeVersion";
        internal const string PrefKeyViewDistanceMode = "UltrawideViewDistanceMode";
        internal const string PrefKeyViewDistanceModeVersion = "UltrawideViewDistanceModeVersion";
        internal const string PrefKeyGfxVsyncMode = "SebTweaks_Gfx_VSyncMode"; // v2: 0=Off,1=On (legacy v1: 0=Default,1=On,2=Off)
        internal const string PrefKeyGfxVsyncModeVersion = "SebTweaks_Gfx_VSyncModeVersion";
        internal const string PrefKeyMirrorQuality = "SebTruck_MirrorQuality";

        // 0 None, 1 Finer, 2 Fine, 3 Default, 4 Large
        public static int GetPixelationMode()
        {
            int raw = PlayerPrefs.GetInt(PrefKeyPixelationMode, 3);
            int version = PlayerPrefs.GetInt(PrefKeyPixelationModeVersion, 1);

            // v1 mapping: 0 Disable, 1 Fine, 2 Default, 3 Large
            if (version < 2 && PlayerPrefs.HasKey(PrefKeyPixelationMode))
            {
                switch (raw)
                {
                    case 1:
                        raw = 2;
                        break;
                    case 2:
                        raw = 3;
                        break;
                    case 3:
                        raw = 4;
                        break;
                }
                PlayerPrefs.SetInt(PrefKeyPixelationModeVersion, 2);
                PlayerPrefs.SetInt(PrefKeyPixelationMode, raw);
            }

            return Mathf.Clamp(raw, 0, 4);
        }

        // 0 Near, 1 Default, 2 Far, 3 Max
        public static int GetViewDistanceMode()
        {
            int raw = PlayerPrefs.GetInt(PrefKeyViewDistanceMode, 1);
            int version = PlayerPrefs.GetInt(PrefKeyViewDistanceModeVersion, 1);

            // v1 mapping: 0 Default, 1 Far, 2 Farther, 3 Distant, 4 Max
            if (version < 2 && PlayerPrefs.HasKey(PrefKeyViewDistanceMode))
            {
                switch (raw)
                {
                    case 1:
                        raw = 0; // old Far -> Near
                        break;
                    case 3:
                        raw = 2; // old Distant -> Far
                        break;
                    case 4:
                        raw = 3; // old Max -> Max
                        break;
                    default:
                        raw = 1; // old Default/Farther/other -> Default
                        break;
                }
                PlayerPrefs.SetInt(PrefKeyViewDistanceModeVersion, 2);
                PlayerPrefs.SetInt(PrefKeyViewDistanceMode, raw);
            }

            return Mathf.Clamp(raw, 0, 3);
        }

        // 0 Off, 1 On (default On)
        public static int GetVsyncMode()
        {
            if (!PlayerPrefs.HasKey(PrefKeyGfxVsyncMode))
            {
                PlayerPrefs.SetInt(PrefKeyGfxVsyncModeVersion, 2);
                PlayerPrefs.SetInt(PrefKeyGfxVsyncMode, 1);
                return 1;
            }

            int raw = PlayerPrefs.GetInt(PrefKeyGfxVsyncMode, -1);

            // v1 mapping: 0 Default, 1 On, 2 Off
            if (PlayerPrefs.GetInt(PrefKeyGfxVsyncModeVersion, 0) < 2)
            {
                int migrated = raw == 2 ? 0 : 1;
                PlayerPrefs.SetInt(PrefKeyGfxVsyncModeVersion, 2);
                PlayerPrefs.SetInt(PrefKeyGfxVsyncMode, migrated);
                return migrated;
            }

            return raw == 1 ? 1 : 0;
        }

        // 0 Original, 1..3 = 2x/4x/8x
        public static int GetMirrorQuality() => Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyMirrorQuality, 0), 0, 3);
    }
}
