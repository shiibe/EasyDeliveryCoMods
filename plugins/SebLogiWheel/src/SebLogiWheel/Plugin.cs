using BepInEx;

namespace SebLogiWheel
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("shibe.easydeliveryco.sebcore", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("shibe.easydeliveryco.sebbinds", BepInDependency.DependencyFlags.HardDependency)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "shibe.easydeliveryco.seblogiwheel";
        public const string PluginName = "SebLogiWheel";
        public const string PluginVersion = "1.0.2";
    }
}
