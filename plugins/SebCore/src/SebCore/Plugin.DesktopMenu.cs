using System;
using System.Globalization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SebCore
{
    public partial class Plugin
    {
        private static ConfigEntry<string> _desktopMenuIconX;
        private static ConfigEntry<string> _desktopMenuIconY;

        private void Awake()
        {
            _log = Logger;
            _debugLogging = Config.Bind("Logging", "debug_logging", false, "Log extra debug information.");

            _desktopMenuIconX = Config.Bind("Menu", "sebcore_icon_x", "5.5", "Main Menu icon X position. Example: 5.5");
            _desktopMenuIconY = Config.Bind("Menu", "sebcore_icon_y", "3.25", "Main Menu icon Y position. Example: 3.25");

            _clearModPrefs ??= Config.Bind(
                "Maintenance",
                "clear_mod_prefs",
                false,
                "If true, clears known mod PlayerPrefs at runtime, then flips back to false. Use to recover from bad bindings."
            );

            var harmony = new Harmony(PluginGuid);
            harmony.Patch(
                original: AccessTools.Method(typeof(DesktopDotExe), "Setup"),
                postfix: new HarmonyMethod(typeof(Plugin), nameof(DesktopDotExe_Setup_Postfix))
            );

            // Run early so users can recover even if menus are broken.
            TryClearModPrefsIfRequested();
        }

        private static float ParseDesktopIconFloat(string value, float fallback, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string trimmed = value.Trim();
            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                return parsed;
            }
            if (float.TryParse(trimmed, out parsed))
            {
                return parsed;
            }

            LogDebug("Failed to parse " + label + "='" + value + "', using " + fallback.ToString("0.###", CultureInfo.InvariantCulture) + ".");
            return fallback;
        }

        private static void DesktopDotExe_Setup_Postfix(DesktopDotExe __instance)
        {
            if (__instance == null)
            {
                return;
            }

            // Main Menu icon for launching SebCore.
            // We keep this fixed (no config) since cartridges are launched inside the SebCore window.
            bool visible = true;
            float x = ParseDesktopIconFloat(_desktopMenuIconX != null ? _desktopMenuIconX.Value : null, 3.0f, "sebcore_icon_x");
            float y = ParseDesktopIconFloat(_desktopMenuIconY != null ? _desktopMenuIconY.Value : null, 3.25f, "sebcore_icon_y");
            var position = new Vector2(x, y);

            DesktopDotExe.File existingFile = null;
            foreach (var file in __instance.files)
            {
                if (file != null && string.Equals(file.name, SebCoreMenuWindow.FileName, StringComparison.OrdinalIgnoreCase))
                {
                    existingFile = file;
                    break;
                }
            }

            if (existingFile == null)
            {
                var file = new DesktopDotExe.File(__instance.R, __instance)
                {
                    name = SebCoreMenuWindow.FileName,
                    type = DesktopDotExe.FileType.exe,
                    data = SebCoreMenuWindow.ListenerData,
                    icon = 7,
                    iconHover = 7,
                    position = position,
                    visible = visible,
                    cantFolder = false
                };
                __instance.files.Add(file);
            }
            else
            {
                existingFile.icon = 7;
                existingFile.iconHover = 7;
                existingFile.position = position;
                existingFile.visible = visible;
            }

            var root = __instance.transform;
            if (root.Find(SebCoreMenuWindow.ListenerName) == null)
            {
                var listener = new GameObject(SebCoreMenuWindow.ListenerName);
                listener.transform.SetParent(root, false);
                listener.AddComponent<SebCoreMenuWindow>();
            }
        }
    }
}
