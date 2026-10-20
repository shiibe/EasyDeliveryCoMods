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
        internal const string PrefKeyHorn = "SebTruck_Horn";

        // The game's horn clips have the animal sound mixed in; meowHorn is the truck's default.
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

        internal static bool HasSnowTiresUpgrade() => _truckUpgrades != null && _truckUpgrades.hasTires;
        internal static bool HasIceChainsUpgrade() => _truckUpgrades != null && _truckUpgrades.hasChains;

        internal static int GetHornIndex() => PlayerPrefs.GetInt(PrefKeyHorn, 0) == 1 ? 1 : 0;
        internal static void SetHornIndex(int index) => PlayerPrefs.SetInt(PrefKeyHorn, Mathf.Clamp(index, 0, HornLabels.Length - 1));

        internal static void ResetEquipmentDefaults()
        {
            SetMountSnowTires(true);
            SetMountIceChains(true);
            SetHornIndex(0);
            PlayerPrefs.Save();
        }

        // Ownership (hasTires/hasChains) is saved by the game, so only what SnowTires has mounted is changed.
        [HarmonyPatch(typeof(TruckUpgrades), "LateUpdate")]
        [HarmonyPostfix]
        private static void TruckUpgrades_LateUpdate_Postfix(TruckUpgrades __instance, SnowTires ___snowTires)
        {
            _truckUpgrades = __instance;
            if (___snowTires == null)
            {
                return;
            }

            ___snowTires.hasSnowTires = __instance.hasTires && GetMountSnowTires();
            ___snowTires.hasIceChains = __instance.hasChains && GetMountIceChains();
        }

        [HarmonyPatch(typeof(Headlights), "Update")]
        [HarmonyPrefix]
        private static void Headlights_Update_Prefix(Headlights __instance)
        {
            if (__instance == null || HeadlightsHornField == null)
            {
                return;
            }

            // Anything we didn't put there (base horn, alt-appearance horn) is the game's choice.
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

        // Null means "use the game's horn" (the clip isn't loaded in this scene).
        private static UnityEngine.Object GetSelectedHornClip()
        {
            int index = GetHornIndex();
            string clipName = HornClipNames[index];
            if (HornClipCache[index] != null)
            {
                return HornClipCache[index];
            }

            // FindObjectsOfTypeAll is slow; retry a missing clip at most every few seconds.
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

        // AudioClip via reflection: SebTruck has no UnityEngine.AudioModule reference.
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
