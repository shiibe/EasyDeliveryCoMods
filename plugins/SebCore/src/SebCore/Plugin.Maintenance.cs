using BepInEx.Configuration;

namespace SebCore
{
    public partial class Plugin
    {
        private static ConfigEntry<bool> _clearModPrefs;

        private void OnEnable()
        {
            Instance = this;
        }

        private void Start()
        {
            _clearModPrefs ??= Config.Bind(
                "Maintenance",
                "clear_mod_prefs",
                false,
                "If true, clears known mod PlayerPrefs at runtime, then flips back to false. Use to recover from bad bindings."
            );

            TryClearModPrefsIfRequested();
        }

        private void Update()
        {
            TryClearModPrefsIfRequested();
        }

        internal static bool RequestClearModPrefs()
        {
            if (Instance == null || Instance.Config == null)
            {
                return false;
            }

            _clearModPrefs ??= Instance.Config.Bind(
                "Maintenance",
                "clear_mod_prefs",
                false,
                "If true, clears known mod PlayerPrefs at runtime, then flips back to false. Use to recover from bad bindings."
            );

            _clearModPrefs.Value = true;
            Instance.Config.Save();
            return true;
        }

        private void TryClearModPrefsIfRequested()
        {
            if (_clearModPrefs == null || !_clearModPrefs.Value)
            {
                return;
            }

            var result = ModPrefsResetter.ClearAllKnownModPrefs();
            Logger.LogInfo("Cleared mod prefs (deleted " + result.DeletedCount + " keys)");

            _clearModPrefs.Value = false;
            Config.Save();
        }
    }
}
