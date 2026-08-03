using HarmonyLib;

namespace SebLogiWheel
{
    public partial class Plugin
    {
        private static void PatchByName(Harmony harmony, string typeName, string methodName, string prefix = null, string postfix = null)
        {
            SebCore.HarmonyUtil.PatchByName(harmony, typeof(Plugin), typeName, methodName, prefix, postfix, LogDebug);
        }
    }
}
