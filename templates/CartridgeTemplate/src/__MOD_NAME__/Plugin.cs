using BepInEx;

namespace __MOD_NAME__
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("shibe.easydeliveryco.sebcore", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "__MOD_GUID__";
        public const string PluginName = "__MOD_NAME__";
        public const string PluginVersion = "__MOD_VERSION__";

        private void Awake()
        {
            // Register the cartridge so it shows up in SebCore.
            SebCore.CartridgeApps.RegisterApp(new SebCore.CartridgeApps.App
            {
                DisplayName = "__DISPLAY_NAME__",
                FileName = "__FILE_NAME__",
                PluginGuid = PluginGuid,
                ListenerName = __MOD_NAME__MenuWindow.ListenerName,
                ListenerData = __MOD_NAME__MenuWindow.ListenerData,
                WindowTypeName = typeof(__MOD_NAME__MenuWindow).FullName
            });
        }
    }
}
