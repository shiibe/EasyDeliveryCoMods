using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SebTweaks
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("shibe.easydeliveryco.sebcore", BepInDependency.DependencyFlags.HardDependency)]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "shibe.easydeliveryco.sebtweaks";
        public const string PluginName = "SebTweaks";
        public const string PluginVersion = "1.0.2";

        private void Awake()
        {
            Log = base.Logger;

            // Migrate legacy time mode -> freeze time.
            try
            {
                if (PlayerPrefs.HasKey(PrefKeyTimeMode) && !PlayerPrefs.HasKey(PrefKeyFreezeTime))
                {
                    int legacy = GetInt(PrefKeyTimeMode, 0);
                    SetInt(PrefKeyFreezeTime, legacy == 1 ? 1 : 0);
                }
            }
            catch
            {
                // ignore
            }

            // Register the cartridge so it shows up in SebCore.
            SebCore.CartridgeApps.RegisterApp(new SebCore.CartridgeApps.App
            {
                DisplayName = "Tweaks",
                FileName = "tweaks",
                PluginGuid = PluginGuid,
                ListenerName = SebTweaksMenuWindow.ListenerName,
                ListenerData = SebTweaksMenuWindow.ListenerData,
                WindowTypeName = typeof(SebTweaksMenuWindow).FullName
            });

            try
            {
                new Harmony(PluginGuid).PatchAll();
            }
            catch (System.Exception e)
            {
                Log?.LogError("Harmony patching failed: " + e);
            }
        }

        internal static BepInEx.Logging.ManualLogSource Log;

        private static readonly string[] MenuScenes =
        {
            "TitleScreen",
            "Credits",
            "Ending",
            "TrainIntro"
        };

        private static int _inGameCacheFrame = -1;
        private static bool _inGameCached;

        internal static bool IsInGameNow()
        {
            if (_inGameCacheFrame == Time.frameCount)
            {
                return _inGameCached;
            }

            _inGameCacheFrame = Time.frameCount;
            bool inGame = true;

            try
            {
                string scene = SceneManager.GetActiveScene().name;
                for (int i = 0; i < MenuScenes.Length; i++)
                {
                    if (scene == MenuScenes[i])
                    {
                        inGame = false;
                        break;
                    }
                }
            }
            catch
            {
                // ignore
            }

            // Fallback: require HUD in scene.
            if (inGame && Object.FindFirstObjectByType<sHUD>() == null)
            {
                inGame = false;
            }

            _inGameCached = inGame;
            return inGame;
        }

        internal const string PrefKeyJobPayoutMult = "SebTweaks_JobPayoutMult";
        internal const string PrefKeyGasPriceMult = "SebTweaks_GasPriceMult";
        internal const string PrefKeyGasConsumptionMult = "SebTweaks_GasConsumptionMult";
        internal const string PrefKeyEnergyLossMult = "SebTweaks_EnergyLossMult";
        internal const string PrefKeyTempLossMult = "SebTweaks_TempLossMult";

        internal const string PrefKeyFogMult = "SebTweaks_FogMult";
        internal const string PrefKeyWorldLightMult = "SebTweaks_WorldLightMult";
        internal const string PrefKeyWorldLightColorR = "SebTweaks_WorldLightColorR";
        internal const string PrefKeyWorldLightColorG = "SebTweaks_WorldLightColorG";
        internal const string PrefKeyWorldLightColorB = "SebTweaks_WorldLightColorB";
        internal const string PrefKeyTimeMode = "SebTweaks_TimeMode"; // Legacy: 0=Auto,1=Manual
        internal const string PrefKeyTimeOfDay = "SebTweaks_TimeOfDay"; // 0..1
        internal const string PrefKeyFreezeTime = "SebTweaks_FreezeTime"; // 0/1
        internal const string PrefKeyWeatherMode = "SebTweaks_WeatherMode"; // 0=Auto,1=Manual
        internal const string PrefKeyWeatherIntensity = "SebTweaks_WeatherIntensity"; // 0..1

        internal const string PrefKeyIceCrackEnabled = "SebTweaks_IceCrackEnabled"; // 0/1

        internal const string PrefKeyGodNoEnergyLoss = "SebTweaks_God_NoEnergyLoss";
        internal const string PrefKeyGodNoGasLoss = "SebTweaks_God_NoGasLoss";
        internal const string PrefKeyGodNoTempLoss = "SebTweaks_God_NoTempLoss";
        internal const string PrefKeyGodInvincibleTruck = "SebTweaks_God_InvTruck";

        // Shared graphics prefs (match SebUltrawide keys so settings are unified).
        internal const string PrefKeyFovLegacy = "UltrawideFovOverride";
        internal const string PrefKeyFovThirdPerson = "UltrawideFovOverride_ThirdPerson";
        internal const string PrefKeyFovFirstPerson = "UltrawideFovOverride_FirstPerson";
        internal const string PrefKeyPixelationMode = "UltrawidePixelationMode";
        internal const string PrefKeyPixelationModeVersion = "UltrawidePixelationModeVersion";
        internal const string PrefKeyViewDistanceMode = "UltrawideViewDistanceMode";
        internal const string PrefKeyViewDistanceModeVersion = "UltrawideViewDistanceModeVersion";

        // SebTweaks-only graphics prefs.
        internal const string PrefKeyGfxVsyncMode = "SebTweaks_Gfx_VSyncMode"; // 0=Default,1=On,2=Off
        // PrefKeyGfxFpsCap: 0=Default, -1=Uncapped, >0=cap
        internal const string PrefKeyGfxFpsMode = "SebTweaks_Gfx_FpsMode"; // Legacy
        internal const string PrefKeyGfxFpsCap = "SebTweaks_Gfx_FpsCap";

        internal static float GetFloat(string key, float def)
        {
            try { return PlayerPrefs.GetFloat(key, def); }
            catch { return def; }
        }

        internal static void SetFloat(string key, float value)
        {
            try { PlayerPrefs.SetFloat(key, value); }
            catch { }
        }

        internal static int GetInt(string key, int def)
        {
            try { return PlayerPrefs.GetInt(key, def); }
            catch { return def; }
        }

        internal static void SetInt(string key, int value)
        {
            try { PlayerPrefs.SetInt(key, value); }
            catch { }
        }

        internal static float TimeUserUntil;

        internal static void MarkTimeUser(float seconds = 2f)
        {
            TimeUserUntil = Time.unscaledTime + Mathf.Max(0.1f, seconds);
        }
    }
}

