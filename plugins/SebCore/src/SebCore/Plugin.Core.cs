using BepInEx.Configuration;
using BepInEx.Logging;

namespace SebCore
{
    public partial class Plugin
    {
        private static ManualLogSource _log;
        private static ConfigEntry<bool> _debugLogging;

        internal static void LogDebug(string message)
        {
            if (_debugLogging == null || !_debugLogging.Value)
            {
                return;
            }

            _log?.LogInfo("[debug] " + message);
        }
    }
}
