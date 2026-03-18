using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace SebTruck
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("shibe.easydeliveryco.sebcore", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("shibe.easydeliveryco.sebbinds", BepInDependency.DependencyFlags.HardDependency)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "shibe.easydeliveryco.sebtruck";
        public const string PluginName = "SebTruck";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(Plugin));

            // Expose truck-specific binds via SebBinds.
            try
            {
                SebBinds.SebBindsApi.RegisterActionsPage(
                    id: "sebtruck",
                    title: "Truck",
                    SebBinds.BindAction.IgnitionToggle,
                    SebBinds.BindAction.ToggleGearbox,
                    SebBinds.BindAction.ShiftUp,
                    SebBinds.BindAction.ShiftDown
                );
            }
            catch
            {
                // SebBinds may not be installed.
            }

            Log.LogInfo("Loaded");
        }
    }
}
