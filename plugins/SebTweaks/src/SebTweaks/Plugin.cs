using static SebCore.ModPrefs;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SebTweaks
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("shibe.easydeliveryco.sebcore", "1.1.1")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "shibe.easydeliveryco.sebtweaks";
        public const string PluginName = "SebTweaks";
        public const string PluginVersion = "1.1.3";

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

                // Migrate the old SebTweaks-only rally key to the key shared with SebTruck.
                if (HasKey(PrefKeyRallyManualTransmissionLegacy))
                {
                    if (!HasKey(PrefKeyRallyManualTransmission))
                    {
                        SetInt(PrefKeyRallyManualTransmission, GetInt(PrefKeyRallyManualTransmissionLegacy, 0));
                    }
                    DeleteKey(PrefKeyRallyManualTransmissionLegacy);
                }
            }
            catch
            {
                // ignore
            }

            // FOV, pixelation, view distance and vsync live in SebCore.
            SebCore.ModGraphics.Enable();

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
                var harmony = new Harmony(PluginGuid);
                harmony.PatchAll();
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
        internal const string PrefKeyGravityMult = "SebTweaks_GravityMult";
        internal const string PrefKeyTimeMode = "SebTweaks_TimeMode"; // Legacy: 0=Auto,1=Manual
        internal const string PrefKeyTimeOfDay = "SebTweaks_TimeOfDay"; // 0..1
        internal const string PrefKeyFreezeTime = "SebTweaks_FreezeTime"; // 0/1
        internal const string PrefKeyWeatherMode = "SebTweaks_WeatherMode"; // 0=Auto,1=Manual
        internal const string PrefKeyWeatherIntensity = "SebTweaks_WeatherIntensity"; // 0..1

        internal const string PrefKeyIceCrackEnabled = "SebTweaks_IceCrackEnabled"; // 0/1

        internal const string PrefKeyGodInvincibleTruck = "SebTweaks_God_InvTruck";
        // Shared with SebTruck so both menus control one setting.
        internal const string PrefKeyRallyManualTransmission = "SebTruck_RallyManualTransmission";
        internal const string PrefKeyRallyManualTransmissionLegacy = "SebTweaks_Rally_ManualTransmission";
        internal const string PrefKeyRallyDebugInfo = "SebTweaks_Rally_DebugInfo";

        // Cheats/refill targets (0..1). Used when the corresponding freeze toggle is enabled.
        internal const string PrefKeyRefillEnergy01 = "SebTweaks_Refill_Energy01";
        internal const string PrefKeyRefillFuel01 = "SebTweaks_Refill_Fuel01";
        internal const string PrefKeyRefillTemp01 = "SebTweaks_Refill_Temp01";

        internal const string PrefKeyFreezeRefillEnergy = "SebTweaks_Refill_FreezeEnergy"; // 0/1
        internal const string PrefKeyFreezeRefillFuel = "SebTweaks_Refill_FreezeFuel"; // 0/1
        internal const string PrefKeyFreezeRefillTemp = "SebTweaks_Refill_FreezeTemp"; // 0/1

        // PrefKeyGfxFpsCap: 0=Default, -1=Uncapped, >0=cap
        internal const string PrefKeyGfxFpsMode = "SebTweaks_Gfx_FpsMode"; // Legacy
        internal const string PrefKeyGfxFpsCap = "SebTweaks_Gfx_FpsCap";

        internal static float TimeUserUntil;

        internal static void MarkTimeUser(float seconds = 2f)
        {
            TimeUserUntil = Time.unscaledTime + Mathf.Max(0.1f, seconds);
        }
    }
}
