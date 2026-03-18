using BepInEx;

namespace SebUltrawide
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("shibe.easydeliveryco.sebcore", BepInDependency.DependencyFlags.HardDependency)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "shibe.easydeliveryco.sebultrawide";
        public const string PluginName = "SebUltrawide";
        public const string PluginVersion = "1.0.0";
    }
}
