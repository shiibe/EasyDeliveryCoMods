using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SebTruck
{
    public partial class Plugin
    {
        internal const string PrefKeyMountSnowTires = "SebTruck_MountSnowTires";
        internal const string PrefKeyMountIceChains = "SebTruck_MountIceChains";
        internal const string PrefKeyMountRamBar = "SebTruck_MountRamBar";
        internal const string PrefKeyHorn = "SebTruck_Horn";

        // meowHorn is the truck's default horn.
        internal static readonly string[] HornLabels = { "Meow", "Woof" };
        private static readonly string[] HornClipNames = { "meowHorn", "hornAlt" };

        private static readonly FieldInfo HeadlightsHornField = AccessTools.Field(typeof(Headlights), "horn");
        private static readonly UnityEngine.Object[] HornClipCache = new UnityEngine.Object[HornClipNames.Length];
        private static UnityEngine.Object _gameHorn;
        private static UnityEngine.Object _appliedHorn;
        private static float _nextHornClipScan;
        private static TruckUpgrades _truckUpgrades;

        internal static bool GetMountSnowTires() => PlayerPrefs.GetInt(PrefKeyMountSnowTires, 1) != 0;
        internal static void SetMountSnowTires(bool mounted) => PlayerPrefs.SetInt(PrefKeyMountSnowTires, mounted ? 1 : 0);
        internal static bool GetMountIceChains() => PlayerPrefs.GetInt(PrefKeyMountIceChains, 1) != 0;
        internal static void SetMountIceChains(bool mounted) => PlayerPrefs.SetInt(PrefKeyMountIceChains, mounted ? 1 : 0);

        internal static bool GetMountRamBar() => PlayerPrefs.GetInt(PrefKeyMountRamBar, 1) != 0;
        internal static void SetMountRamBar(bool mounted) => PlayerPrefs.SetInt(PrefKeyMountRamBar, mounted ? 1 : 0);

        internal static bool HasSnowTiresUpgrade() => _truckUpgrades != null && _truckUpgrades.hasTires;
        internal static bool HasIceChainsUpgrade() => _truckUpgrades != null && _truckUpgrades.hasChains;
        internal static bool HasRamBarUpgrade() => _truckUpgrades != null && _truckUpgrades.hasBumper;

        internal static int GetHornIndex() => PlayerPrefs.GetInt(PrefKeyHorn, 0) == 1 ? 1 : 0;
        internal static void SetHornIndex(int index) => PlayerPrefs.SetInt(PrefKeyHorn, Mathf.Clamp(index, 0, HornLabels.Length - 1));

        internal static void ResetEquipmentDefaults()
        {
            SetMountSnowTires(true);
            SetMountIceChains(true);
            SetMountRamBar(true);
            SetHornIndex(0);
            PlayerPrefs.Save();
        }

        // Leave hasTires/hasChains alone: the game saves them as ownership.
        [HarmonyPatch(typeof(TruckUpgrades), "LateUpdate")]
        [HarmonyPostfix]
        private static void TruckUpgrades_LateUpdate_Postfix(TruckUpgrades __instance, SnowTires ___snowTires, BumperBar ___bumper)
        {
            _truckUpgrades = __instance;
            if (___bumper != null)
            {
                ___bumper.active = __instance.hasBumper && GetMountRamBar();
            }
            if (___snowTires == null)
            {
                return;
            }

            ___snowTires.hasSnowTires = __instance.hasTires && GetMountSnowTires();
            ___snowTires.hasIceChains = __instance.hasChains && GetMountIceChains();
        }

        // The game checks hasBumper directly; hide it only during the call so it is never saved as lost.
        private static bool HideUnmountedRamBar()
        {
            bool hide = _truckUpgrades != null && _truckUpgrades.hasBumper && !GetMountRamBar();
            if (hide)
            {
                _truckUpgrades.hasBumper = false;
            }
            return hide;
        }

        private static Exception RestoreRamBar(Exception exception, bool hidden)
        {
            if (hidden && _truckUpgrades != null)
            {
                _truckUpgrades.hasBumper = true;
            }
            return exception;
        }

        [HarmonyPatch(typeof(CarDamage), "CheckDeadVelocity")]
        [HarmonyPrefix]
        private static void CarDamage_CheckDeadVelocity_RamBarPrefix(out bool __state) => __state = HideUnmountedRamBar();

        [HarmonyPatch(typeof(CarDamage), "CheckDeadVelocity")]
        [HarmonyFinalizer]
        private static Exception CarDamage_CheckDeadVelocity_RamBarFinalizer(Exception __exception, bool __state) => RestoreRamBar(__exception, __state);

        [HarmonyPatch(typeof(Crashable), "OnCollisionEnter")]
        [HarmonyPrefix]
        private static void Crashable_OnCollisionEnter_RamBarPrefix(out bool __state) => __state = HideUnmountedRamBar();

        [HarmonyPatch(typeof(Crashable), "OnCollisionEnter")]
        [HarmonyFinalizer]
        private static Exception Crashable_OnCollisionEnter_RamBarFinalizer(Exception __exception, bool __state) => RestoreRamBar(__exception, __state);

        [HarmonyPatch(typeof(Crashable), "MakeRigidbody")]
        [HarmonyPrefix]
        private static void Crashable_MakeRigidbody_RamBarPrefix(out bool __state) => __state = HideUnmountedRamBar();

        [HarmonyPatch(typeof(Crashable), "MakeRigidbody")]
        [HarmonyFinalizer]
        private static Exception Crashable_MakeRigidbody_RamBarFinalizer(Exception __exception, bool __state) => RestoreRamBar(__exception, __state);

        [HarmonyPatch(typeof(Headlights), "Update")]
        [HarmonyPrefix]
        private static void Headlights_Update_Prefix(Headlights __instance)
        {
            if (__instance == null || HeadlightsHornField == null)
            {
                return;
            }

            // A horn we didn't set is the game's (base or alt character).
            var current = HeadlightsHornField.GetValue(__instance) as UnityEngine.Object;
            if (current != _appliedHorn)
            {
                _gameHorn = current;
            }

            var wanted = GetSelectedHornClip();
            if (wanted == null)
            {
                wanted = _gameHorn;
            }
            if (wanted != current)
            {
                HeadlightsHornField.SetValue(__instance, wanted);
            }
            _appliedHorn = wanted;
        }

        // Null when the clip isn't loaded in this scene.
        private static UnityEngine.Object GetSelectedHornClip()
        {
            int index = GetHornIndex();
            string clipName = HornClipNames[index];
            if (HornClipCache[index] != null)
            {
                return HornClipCache[index];
            }

            // FindObjectsOfTypeAll is slow.
            if (Time.unscaledTime < _nextHornClipScan)
            {
                return null;
            }
            _nextHornClipScan = Time.unscaledTime + 5f;

            var audioClipType = Type.GetType("UnityEngine.AudioClip, UnityEngine.AudioModule");
            if (audioClipType == null)
            {
                return null;
            }

            foreach (var clip in Resources.FindObjectsOfTypeAll(audioClipType))
            {
                if (clip != null && clip.name == clipName)
                {
                    HornClipCache[index] = clip;
                    return clip;
                }
            }
            return null;
        }

        // Reflection: SebTruck has no UnityEngine.AudioModule reference.
        private static UnityEngine.Object CreateAudioClip(string name, float[] data, int sampleRate)
        {
            var audioClipType = Type.GetType("UnityEngine.AudioClip, UnityEngine.AudioModule");
            var create = audioClipType?.GetMethod("Create", new[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(bool) });
            if (create == null)
            {
                return null;
            }

            var clip = create.Invoke(null, new object[] { name, data.Length, 1, sampleRate, false }) as UnityEngine.Object;
            if (clip != null)
            {
                audioClipType.GetMethod("SetData", new[] { typeof(float[]), typeof(int) })?.Invoke(clip, new object[] { data, 0 });
            }
            return clip;
        }
    }
}
