namespace SebUltrawide
{
    public partial class Plugin
    {
        private static void LogDebug(string message)
        {
            if (_debugMode == null || !_debugMode.Value)
            {
                return;
            }

            _log.LogInfo(message);
        }
    }
}
