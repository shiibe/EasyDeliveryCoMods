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
            WindowTypeName = "EasyLogiWheelSupport.WheelMenuWindow"
        };

        internal static readonly App Ultrawide = new App
        {
            DisplayName = "Ultrawide",
            FileName = "wide",
            PluginGuid = "shibe.easydeliveryco.ultrawide",
            ListenerName = "UltrawideMenu",
            ListenerData = "listener_UltrawideMenu",
            WindowTypeName = "EasyDeliveryCoUltrawide.UltrawideMenuWindow"
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
                return false;
            }

            Assembly asm = info.Instance.GetType().Assembly;
            Type t = asm.GetType(app.WindowTypeName, throwOnError: false);
            if (t == null)
            {
                return false;
            }

            var go = new GameObject(app.ListenerName);
            go.transform.SetParent(desktop.transform, false);
            go.AddComponent(t);
            return true;
        }
    }
}
