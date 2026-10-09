using BepInEx;
using BepInEx.Logging;

namespace SebBinds
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("shibe.easydeliveryco.sebcore", BepInDependency.DependencyFlags.HardDependency)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "shibe.easydeliveryco.sebbinds";
        public const string PluginName = "SebBinds";
        public const string PluginVersion = "1.1.5";

        internal static ManualLogSource Log;

        // Latest sInputManager seen by our patch. Used by UI code to re-seed defaults on demand.
        internal static sInputManager LastInputManager;

        public static void ResetBindings()
        {
            BindingStore.ClearAll();
            AxisBindingStore.ClearAll();
            _defaultsEnsuredForInputInstanceId = 0;
            _didMigrateLegacyBinds = false;
            if (LastInputManager != null)
            {
                DefaultPreset.TryInstallFromInputManager(LastInputManager, forceOnController: true, forceReinstall: true);
            }
            AxisDefaults.EnsureControllerDefaults();
            KeyboardDefaults.EnsureDefaults();
            UnityEngine.PlayerPrefs.Save();
        }
    }
}
