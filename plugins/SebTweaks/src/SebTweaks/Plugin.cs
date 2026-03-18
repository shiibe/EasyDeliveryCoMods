using BepInEx;

namespace SebTweaks
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("shibe.easydeliveryco.sebcore", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "shibe.easydeliveryco.sebtweaks";
        public const string PluginName = "SebTweaks";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            // Register the cartridge so it shows up in SebCore.
            SebCore.CartridgeApps.RegisterApp(new SebCore.CartridgeApps.App
            {
                DisplayName = "Tweaks",
                FileName = "tweaks",
                PluginGuid = PluginGuid,
                ListenerName = SebTweaksMenuWindow.ListenerName,
                ListenerData = SebTweaksMenuWindow.ListenerData,
                WindowTypeName = typeof(SebTweaksMenuWindow).FullName
            });
        }
    }
}

