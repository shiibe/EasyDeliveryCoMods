using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace SebTweaks
{
    public sealed partial class Plugin
    {
        internal static float JobPayoutMult => Mathf.Clamp(GetFloat(PrefKeyJobPayoutMult, 1f), 0.1f, 10f);
        internal static float GasPriceMult => Mathf.Clamp(GetFloat(PrefKeyGasPriceMult, 1f), 0.1f, 3f);
        internal static float GasConsumptionMult => Mathf.Clamp(GetFloat(PrefKeyGasConsumptionMult, 1f), 0.05f, 3f);
        internal static float EnergyLossMult => Mathf.Clamp(GetFloat(PrefKeyEnergyLossMult, 1f), 0.05f, 3f);
        internal static float TempLossMult => Mathf.Clamp(GetFloat(PrefKeyTempLossMult, 1f), 0.05f, 3f);

        internal static bool GodInvincibleTruck => GetInt(PrefKeyGodInvincibleTruck, 0) == 1;

        internal static bool IceCrackEnabled => GetInt(PrefKeyIceCrackEnabled, 1) == 1;

        internal static float FogMult => Mathf.Clamp(GetFloat(PrefKeyFogMult, 1f), 0f, 3f);

        internal static float WorldLightMult => Mathf.Clamp(GetFloat(PrefKeyWorldLightMult, 1f), 0f, 3f);

        internal static float WorldLightColorR => Mathf.Clamp(GetFloat(PrefKeyWorldLightColorR, 1f), 0f, 2f);
        internal static float WorldLightColorG => Mathf.Clamp(GetFloat(PrefKeyWorldLightColorG, 1f), 0f, 2f);
        internal static float WorldLightColorB => Mathf.Clamp(GetFloat(PrefKeyWorldLightColorB, 1f), 0f, 2f);

        internal static bool TimeManual => GetInt(PrefKeyTimeMode, 0) == 1;
        internal static float ManualTime01 => Mathf.Repeat(GetFloat(PrefKeyTimeOfDay, 0.25f), 1f);

        internal static bool FreezeTime => GetInt(PrefKeyFreezeTime, 0) == 1;

        internal static bool WeatherManual => GetInt(PrefKeyWeatherMode, 0) == 1;
        internal static float ManualWeatherIntensity01 => Mathf.Clamp01(GetFloat(PrefKeyWeatherIntensity, 0.4f));

        internal static float ApplyJobPayoutMultiplier(float price)
        {
            if (!IsInGameNow())
            {
                return price;
            }
            return price * JobPayoutMult;
        }

        [HarmonyPatch(typeof(jobBoard), "DoPayment")]
        [HarmonyPriority(Priority.Last)]
        private static class JobBoard_DoPayment_Patch
        {
            private static void Prefix(ref float price)
            {
                price = ApplyJobPayoutMultiplier(price);
            }
        }

        [HarmonyPatch(typeof(jobBoard), "DrawJobList")]
        [HarmonyPriority(Priority.Last)]
        private static class JobBoard_DrawJobList_Patch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var list = new List<CodeInstruction>(instructions);
                var toMoney = AccessTools.Method(typeof(jobBoard), "ToMoney", new[] { typeof(float) });
                var apply = AccessTools.Method(typeof(Plugin), nameof(ApplyJobPayoutMultiplier));

                if (toMoney == null || apply == null)
                {
                    return list;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    var ins = list[i];
                    if (ins.Calls(toMoney))
                    {
                        // Stack before call: [jobBoard this] [float price]
                        // Insert: ApplyJobPayoutMultiplier(price)
                        list.Insert(i, new CodeInstruction(OpCodes.Call, apply));
                        i += 1;
                    }
                }

                return list;
            }
        }

        [HarmonyPatch(typeof(FuelPrice), "UpdatePrice")]
        [HarmonyPriority(Priority.Last)]
        private static class FuelPrice_UpdatePrice_Patch
        {
            private static void Postfix(FuelPrice __instance, sTransactionNPC npc)
            {
                if (!IsInGameNow())
                {
                    return;
                }
                if (__instance == null || npc == null)
                {
                    return;
                }

                float m = GasPriceMult;
                if (Mathf.Abs(m - 1f) < 0.0001f)
                {
                    return;
                }

                npc.price *= m;
            }
        }

        [HarmonyPatch(typeof(FuelPrice), "MakePayment")]
        [HarmonyPriority(Priority.Last)]
        private static class FuelPrice_MakePayment_Patch
        {
            private static void Prefix(FuelPrice __instance, ref float __state)
            {
                if (!IsInGameNow())
                {
                    __state = 0f;
                    return;
                }
                if (__instance == null)
                {
                    __state = 0f;
                    return;
                }

                __state = __instance.costPerLiter;

                float m = GasPriceMult;
                if (Mathf.Abs(m - 1f) < 0.0001f)
                {
                    return;
                }

                __instance.costPerLiter = __state * m;
            }

            private static void Postfix(FuelPrice __instance, float __state)
            {
                if (!IsInGameNow())
                {
                    return;
                }
                if (__instance == null)
                {
                    return;
                }
                if (__state <= 0f)
                {
                    return;
                }
                __instance.costPerLiter = __state;
            }
        }

        [HarmonyPatch(typeof(sHUD), "DoFuelMath")]
        [HarmonyPriority(Priority.Last)]
        private static class Hud_DoFuelMath_Patch
        {
            private static void Prefix(sHUD __instance, ref float __state)
            {
                if (!IsInGameNow())
                {
                    __state = 0f;
                    return;
                }

                if (__instance == null)
                {
                    __state = 0f;
                    return;
                }

                bool freezeFuel = GetInt(PrefKeyFreezeRefillFuel, 0) == 1;
                if (freezeFuel)
                {
                    // Sentinel: freeze mode.
                    __state = -999f;
                    return;
                }

                __state = __instance.fuel;
            }

            private static void Postfix(sHUD __instance, float __state)
            {
                if (__instance == null)
                {
                    return;
                }

                if (!IsInGameNow())
                {
                    return;
                }

                // Freeze: enforce saved fuel target.
                if (__state < -100f)
                {
                    try
                    {
                        float fuel01 = Mathf.Clamp01(GetFloat(PrefKeyRefillFuel01, 1f));
                        __instance.fuel = fuel01 * __instance.fuelCapacity;

                        // Keep the car's fuelScale consistent with the forced value.
                        if (__instance.LowFuel())
                        {
                            __instance.navigation.car.fuelScale = Mathf.Clamp01(__instance.fuel / __instance.fuelCapacity / 0.25f);
                        }
                        else
                        {
                            __instance.navigation.car.fuelScale = 1f;
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                    return;
                }

                float oldFuel = __state;
                float newFuel = __instance.fuel;
                float consumed = oldFuel - newFuel;
                if (consumed <= 0f)
                {
                    return;
                }

                float adjusted = Mathf.Max(0f, oldFuel - consumed * GasConsumptionMult);
                if (Mathf.Abs(adjusted - newFuel) < 0.0001f)
                {
                    return;
                }

                __instance.fuel = adjusted;

                // Keep the car's fuelScale consistent with the adjusted value.
                try
                {
                    if (__instance.LowFuel())
                    {
                        __instance.navigation.car.fuelScale = Mathf.Clamp01(__instance.fuel / __instance.fuelCapacity / 0.25f);
                        return;
                    }
                    __instance.navigation.car.fuelScale = 1f;
                }
                catch
                {
                    // ignore
                }
            }
        }

        [HarmonyPatch(typeof(sHUD), "LowerEnergy")]
        [HarmonyPriority(Priority.Last)]
        private static class Hud_LowerEnergy_Patch
        {
            private static void Prefix(sHUD __instance, ref float delta)
            {
                if (!IsInGameNow())
                {
                    return;
                }

                if (__instance != null && GetInt(PrefKeyFreezeRefillEnergy, 0) == 1)
                {
                    float energy01 = Mathf.Clamp01(GetFloat(PrefKeyRefillEnergy01, 1f));
                    __instance.energy = energy01 * __instance.energyCapacity;
                    delta = 0f;
                    return;
                }

                delta *= EnergyLossMult;
            }
        }

        [HarmonyPatch(typeof(sHUD), "DoTemperature")]
        [HarmonyPriority(Priority.Last)]
        private static class Hud_DoTemperature_Patch
        {
            private static void Prefix(sHUD __instance, ref float __state)
            {
                if (!IsInGameNow())
                {
                    __state = 0f;
                    return;
                }
                if (__instance == null)
                {
                    __state = 0f;
                    return;
                }

                bool freezeTemp = GetInt(PrefKeyFreezeRefillTemp, 0) == 1;
                if (freezeTemp)
                {
                    __state = __instance.temperatureRate;
                    __instance.temperatureRate = 0f;
                    return;
                }

                // Temporarily scale the temperature loss rate so the game's own logic
                // (low energy doubling, shelter buffs, warnings, frost intensity) stays coherent.
                __state = __instance.temperatureRate;
                __instance.temperatureRate = __state * TempLossMult;
            }

            private static void Postfix(sHUD __instance, float __state)
            {
                if (!IsInGameNow())
                {
                    return;
                }
                if (__instance == null)
                {
                    return;
                }

                __instance.temperatureRate = __state;

                // Freeze temperature after the game's own logic runs.
                if (GetInt(PrefKeyFreezeRefillTemp, 0) == 1)
                {
                    try
                    {
                        float limit = __instance.temperatureLimit;
                        if (limit > 0f)
                        {
                            float temp01 = Mathf.Clamp01(GetFloat(PrefKeyRefillTemp01, 1f));
                            __instance.temperature = Mathf.Clamp(temp01 * limit, 0f, limit);
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CarDamage), "OnCollisionEnter")]
        [HarmonyPriority(Priority.Last)]
        private static class CarDamage_OnCollisionEnter_Patch
        {
            private static bool Prefix()
            {
                if (!IsInGameNow())
                {
                    return true;
                }
                return !GodInvincibleTruck;
            }
        }

        [HarmonyPatch(typeof(IceCrack), "Update")]
        [HarmonyPriority(Priority.Last)]
        private static class IceCrack_Update_Patch
        {
            private static bool Prefix(IceCrack __instance)
            {
                if (!IsInGameNow())
                {
                    return true;
                }

                if (IceCrackEnabled)
                {
                    return true;
                }

                // Disabled: ensure it's "off" and don't run the mechanic.
                try
                {
                    __instance.iceCollider.enabled = true;
                }
                catch
                {
                    // ignore
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(sDayNightCycle), "Update")]
        [HarmonyPriority(Priority.Last)]
        private static class DayNightCycle_Update_Patch
        {
            private static void Prefix(sDayNightCycle __instance)
            {
                if (!IsInGameNow())
                {
                    return;
                }
                if (__instance == null)
                {
                    return;
                }

                // Only do lightweight toggles up-front.
                __instance.doTime = !FreezeTime;
                __instance.randomWeather = !WeatherManual;
            }

            private static void Postfix(sDayNightCycle __instance)
            {
                if (!IsInGameNow())
                {
                    return;
                }
                if (__instance == null)
                {
                    return;
                }

                // Enforce settings after the game's own Update runs.
                float t = ManualTime01;
                if (FreezeTime)
                {
                    __instance.doTime = false;
                    __instance.time = t;
                }
                else
                {
                    __instance.doTime = true;
                    if (Time.unscaledTime < TimeUserUntil)
                    {
                        __instance.time = t;
                    }
                }

                if (WeatherManual)
                {
                    __instance.randomWeather = false;

                    if (sWeatherSystem.instance != null)
                    {
                        sWeatherSystem.instance.intensity = ManualWeatherIntensity01;
                    }
                }

                // Atmosphere tuning (works even when oldLighting is disabled).
                float lm = WorldLightMult;
                float mr = WorldLightColorR;
                float mg = WorldLightColorG;
                float mb = WorldLightColorB;
                if (Mathf.Abs(lm - 1f) < 0.0001f && Mathf.Abs(mr - 1f) < 0.0001f && Mathf.Abs(mg - 1f) < 0.0001f && Mathf.Abs(mb - 1f) < 0.0001f)
                {
                    return;
                }

                try
                {
                    var c = RenderSettings.fogColor;
                    c.r = Mathf.Clamp01(c.r * mr * lm);
                    c.g = Mathf.Clamp01(c.g * mg * lm);
                    c.b = Mathf.Clamp01(c.b * mb * lm);
                    RenderSettings.fogColor = c;
                    RenderSettings.ambientLight = c;
                }
                catch
                {
                    // ignore
                }
            }
        }

        [HarmonyPatch(typeof(sDayNightCycle), "SetRandomWeather")]
        [HarmonyPriority(Priority.Last)]
        private static class DayNightCycle_SetRandomWeather_Patch
        {
            private static bool Prefix(sDayNightCycle __instance)
            {
                if (!IsInGameNow())
                {
                    return true;
                }
                // Preserve vanilla behavior in Auto mode; block randomization in Manual mode.
                return !WeatherManual;
            }
        }

        [HarmonyPatch(typeof(sWeatherSystem), "UpdateWeather")]
        [HarmonyPriority(Priority.Last)]
        private static class WeatherSystem_UpdateWeather_Patch
        {
            private static void Postfix(sWeatherSystem __instance)
            {
                if (!IsInGameNow())
                {
                    return;
                }
                if (__instance == null)
                {
                    return;
                }

                float m = FogMult;
                if (Mathf.Abs(m - 1f) < 0.0001f)
                {
                    return;
                }

                // Mirror the old ultrawide approach: multiply the final fog density.
                // (FogVolume will override in LateUpdate; we patch that too.)
                RenderSettings.fogDensity = Mathf.Max(0.000001f, RenderSettings.fogDensity * m);
            }
        }

        [HarmonyPatch(typeof(FogVolume), "LateUpdate")]
        [HarmonyPriority(Priority.Last)]
        private static class FogVolume_LateUpdate_Patch
        {
            private static FieldInfo _fogVolumePField;

            private static void Postfix(FogVolume __instance)
            {
                if (!IsInGameNow())
                {
                    return;
                }
                if (__instance == null)
                {
                    return;
                }

                float m = FogMult;
                if (Mathf.Abs(m - 1f) < 0.0001f)
                {
                    return;
                }

                // FogVolume only sets fog density when p != 1.0; avoid double-applying
                // on frames where FogVolume doesn't override the weather fog.
                try
                {
                    _fogVolumePField ??= AccessTools.Field(typeof(FogVolume), "p");
                    if (_fogVolumePField != null)
                    {
                        float p = (float)_fogVolumePField.GetValue(__instance);
                        if (Mathf.Abs(p - 1f) < 0.0001f)
                        {
                            return;
                        }
                    }
                }
                catch
                {
                    // ignore
                }

                RenderSettings.fogDensity = Mathf.Max(0.000001f, RenderSettings.fogDensity * m);
            }
        }
    }
}
