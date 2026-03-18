using BepInEx;

namespace SebCore
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "shibe.easydeliveryco.sebcore";
        public const string PluginName = "SebCore";
        public const string PluginVersion = "1.0.2";

        internal static Plugin Instance;
    }
}
