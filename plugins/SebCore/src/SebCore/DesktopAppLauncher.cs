using System;
using HarmonyLib;
using UnityEngine;

namespace SebCore
{
    public static class DesktopAppLauncher
    {
        public static bool TryOpen(DesktopDotExe desktop, string fileName)
        {
            if (desktop == null || desktop.files == null || string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            DesktopDotExe.File target = null;
            foreach (var file in desktop.files)
            {
                if (file != null && string.Equals(file.name, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    target = file;
                    break;
                }
            }

            if (target == null)
            {
                return false;
            }

            DesktopDotExe.WindowView windowView;
            target.Execute(out windowView);
            if (windowView == null)
            {
                return false;
            }

            // DesktopDotExe sets this internally on click; use reflection to mirror that.
            var windowViewerField = AccessTools.Field(typeof(DesktopDotExe), "windowViewer");
            if (windowViewerField == null)
            {
                return false;
            }

            windowViewerField.SetValue(desktop, windowView);
            return true;
        }

        public static bool TryOpenProgramListener(DesktopDotExe desktop, string fileName, string listenerData)
        {
            return TryOpenProgramListener(desktop, desktop != null ? desktop.R : null, fileName, listenerData);
        }

        public static bool TryOpenProgramListener(DesktopDotExe desktop, MiniRenderer renderer, string fileName, string listenerData)
        {
            if (desktop == null || renderer == null || string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(listenerData))
            {
                return false;
            }

            // Create an ephemeral "file" to spawn the program window.
            var file = new DesktopDotExe.File(renderer, desktop)
            {
                name = fileName,
                type = DesktopDotExe.FileType.exe,
                data = listenerData,
                icon = 7,
                iconHover = 7,
                position = Vector2.zero,
                visible = false,
                cantFolder = true
            };

            DesktopDotExe.WindowView windowView;
            file.Execute(out windowView);
            if (windowView == null)
            {
                Plugin.LogDebug("Failed to open program listener: file='" + fileName + "' data='" + listenerData + "'");
                return false;
            }

            var windowViewerField = AccessTools.Field(typeof(DesktopDotExe), "windowViewer");
            if (windowViewerField == null)
            {
                return false;
            }

            windowViewerField.SetValue(desktop, windowView);
            return true;
        }
    }
}
