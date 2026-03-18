using System;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace SebCore
{
    internal static class CartridgeApps
    {
        internal struct App
        {
            public string DisplayName;
            public string FileName;
            public string PluginGuid;
            public string ListenerName;
            public string ListenerData;
            public string WindowTypeName;
        }

        internal static readonly App Wheel = new App
        {
            DisplayName = "Wheel",
            FileName = "wheel",
            PluginGuid = "shibe.easydeliveryco.logiwheel",
            ListenerName = "G920Menu",
            ListenerData = "listener_G920Menu",
            WindowTypeName = "SebLogiWheel.WheelMenuWindow"
        };

        internal static readonly App Ultrawide = new App
        {
            DisplayName = "Graphics",
            FileName = "wide",
            PluginGuid = "shibe.easydeliveryco.ultrawide",
            ListenerName = "UltrawideMenu",
            ListenerData = "listener_UltrawideMenu",
            WindowTypeName = "SebUltrawide.UltrawideMenuWindow"
        };

        internal static readonly App Binds = new App
        {
            DisplayName = "Binds",
            FileName = "binds",
            PluginGuid = "shibe.easydeliveryco.sebbinds",
            ListenerName = "SebBindsMenu",
            ListenerData = "listener_SebBindsMenu",
            WindowTypeName = "SebBinds.BindsMenuWindow"
        };

        internal static readonly App Truck = new App
        {
            DisplayName = "Truck",
            FileName = "truck",
            PluginGuid = "shibe.easydeliveryco.sebtruck",
            ListenerName = "SebTruckMenu",
            ListenerData = "listener_SebTruckMenu",
            WindowTypeName = "SebTruck.TruckMenuWindow"
        };

        internal static bool IsInstalled(App app)
        {
            return Chainloader.PluginInfos.ContainsKey(app.PluginGuid);
        }

        internal static bool EnsureListener(DesktopDotExe desktop, App app)
        {
            if (desktop == null)
            {
                return false;
            }

            if (desktop.transform.Find(app.ListenerName) != null)
            {
                return true;
            }

            if (!Chainloader.PluginInfos.TryGetValue(app.PluginGuid, out var info) || info == null || info.Instance == null)
            {
                Plugin.LogDebug("Cartridge not loaded: '" + app.PluginGuid + "'");
                return false;
            }

            Assembly asm = info.Instance.GetType().Assembly;
            Type t = asm.GetType(app.WindowTypeName, throwOnError: false);
            if (t == null)
            {
                // Back-compat for stale plugin folders during rename.
                if (string.Equals(app.WindowTypeName, "SebUltrawide.UltrawideMenuWindow", StringComparison.Ordinal))
                {
                    t = asm.GetType("EasyDeliveryCoUltrawide.UltrawideMenuWindow", throwOnError: false);
                }
                else if (string.Equals(app.WindowTypeName, "SebLogiWheel.WheelMenuWindow", StringComparison.Ordinal))
                {
                    t = asm.GetType("EasyLogiWheelSupport.WheelMenuWindow", throwOnError: false);
                }
            }

            if (t == null)
            {
                Plugin.LogDebug(
                    "Failed to resolve window type '" + app.WindowTypeName + "' for '" + app.PluginGuid + "' (asm=" + asm.GetName().Name + ")"
                );
                return false;
            }

            var go = new GameObject(app.ListenerName);
            go.transform.SetParent(desktop.transform, false);
            go.AddComponent(t);
            return true;
        }
    }
}
