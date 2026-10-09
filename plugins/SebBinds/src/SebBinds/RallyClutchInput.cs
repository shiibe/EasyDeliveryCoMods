using UnityEngine;

namespace SebBinds
{
    internal static class RallyClutchInput
    {
        internal static float Advance(float previousClutch, float target, float step)
        {
            return Mathf.MoveTowards(previousClutch, target, step);
        }
    }
}
