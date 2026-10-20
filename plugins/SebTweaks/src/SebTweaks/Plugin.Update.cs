using static SebCore.ModPrefs;
using UnityEngine;

namespace SebTweaks
{
    public sealed partial class Plugin
    {
        private static float _nextPoll;

        private void Update()
        {
            // Cheats: optional per-stat freeze enforcement.
            // Run every frame so values stay locked even if other game logic changes them.
            try
            {
                ApplyRefillFreezesIfEnabled();
            }
            catch
            {
                // ignore
            }

            // Poll at a low cadence.
            if (Time.unscaledTime < _nextPoll)
            {
                return;
            }
            _nextPoll = Time.unscaledTime + 0.25f;

            ApplyGravityMode();
        }

        private static void ApplyGravityMode()
        {
            try
            {
                Physics.gravity = new Vector3(0f, -9.81f * GravityMult, 0f);
            }
            catch
            {
                // ignore
            }
        }

        private static void ApplyRefillFreezesIfEnabled()
        {
            if (!IsInGameNow())
            {
                return;
            }

            bool freezeEnergy = GetInt(PrefKeyFreezeRefillEnergy, 0) == 1;
            bool freezeFuel = GetInt(PrefKeyFreezeRefillFuel, 0) == 1;
            bool freezeTemp = GetInt(PrefKeyFreezeRefillTemp, 0) == 1;
            if (!freezeEnergy && !freezeFuel && !freezeTemp)
            {
                return;
            }

            var hud = UnityEngine.Object.FindFirstObjectByType<sHUD>();
            if (hud == null)
            {
                return;
            }

            if (freezeEnergy && hud.energyCapacity > 0f)
            {
                float e01 = Mathf.Clamp01(GetFloat(PrefKeyRefillEnergy01, hud.energy / hud.energyCapacity));
                hud.energy = e01 * hud.energyCapacity;
            }

            if (freezeFuel && hud.fuelCapacity > 0f)
            {
                float f01 = Mathf.Clamp01(GetFloat(PrefKeyRefillFuel01, hud.fuel / hud.fuelCapacity));
                hud.fuel = f01 * hud.fuelCapacity;

                try
                {
                    if (hud.LowFuel())
                    {
                        hud.navigation.car.fuelScale = Mathf.Clamp01(hud.fuel / hud.fuelCapacity / 0.25f);
                    }
                    else
                    {
                        hud.navigation.car.fuelScale = 1f;
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (freezeTemp)
            {
                float limit = hud.temperatureLimit;
                if (limit > 0f)
                {
                    float t01 = Mathf.Clamp01(GetFloat(PrefKeyRefillTemp01, hud.temperature / limit));
                    hud.temperature = Mathf.Clamp(t01 * limit, 0f, limit);
                }
            }
        }
    }
}
