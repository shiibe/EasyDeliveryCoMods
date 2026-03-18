using BepInEx;
using BepInEx.Logging;

namespace SebBinds
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "shibe.easydeliveryco.sebbinds";
        public const string PluginName = "SebBinds";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
    }
}
