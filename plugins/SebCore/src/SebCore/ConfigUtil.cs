using System;
using System.IO;
using BepInEx;

namespace SebCore
{
    public static class ConfigUtil
    {
        public static void CopyLegacyConfigIfNeeded(string oldGuid, string newGuid)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(oldGuid) || string.IsNullOrWhiteSpace(newGuid) || string.Equals(oldGuid, newGuid, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                string cfgDir = Paths.ConfigPath;
                string oldPath = Path.Combine(cfgDir, oldGuid + ".cfg");
                string newPath = Path.Combine(cfgDir, newGuid + ".cfg");

                if (File.Exists(oldPath) && !File.Exists(newPath))
                {
                    File.Copy(oldPath, newPath);
                }
            }
            catch
            {
                // Best effort migration only.
            }
        }
    }
}
