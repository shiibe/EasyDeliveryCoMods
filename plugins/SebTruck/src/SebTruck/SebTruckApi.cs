using UnityEngine;

namespace SebTruck
{
    // Minimal public API for cross-mod interop.
    // Keep this stable so other mods can reflection-call without hard dependencies.
    public static class SebTruckApi
    {
        // -1=R, 0=N, 1..GetManualGearCount()
        public static void SetManualGear(int gear)
        {
            if (!Plugin.GetManualTransmissionEnabled())
            {
                return;
            }

            int count = Plugin.GetManualGearCount();
            if (gear > 0)
            {
                gear = Mathf.Clamp(gear, 1, count);
            }
            else if (gear < 0)
            {
                gear = -1;
            }
            else
            {
                gear = 0;
            }

            Plugin.SetManualGearDirect(gear);
        }

        public static bool IsManualEnabled()
        {
            return Plugin.GetManualTransmissionEnabled();
        }

        public static int GetManualGearCount()
        {
            return Plugin.GetManualGearCount();
        }
    }
}
