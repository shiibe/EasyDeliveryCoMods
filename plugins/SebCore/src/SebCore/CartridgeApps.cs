using System;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace SebCore
{
    public static class CartridgeApps
    {
        private static readonly object _lock = new object();
        private static readonly System.Collections.Generic.List<App> _registeredApps = new System.Collections.Generic.List<App>();
        private static readonly System.Collections.Generic.HashSet<string> _registeredAppIds = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public struct App
        {
            public string DisplayName;
            public string FileName;
            public string PluginGuid;
            public string ListenerName;
            public string ListenerData;
            public string WindowTypeName;
        }

        public static readonly App Wheel = new App
        {
            DisplayName = "Wheel",
            FileName = "wheel",
            PluginGuid = "shibe.easydeliveryco.seblogiwheel",
            ListenerName = "G920Menu",
            ListenerData = "listener_G920Menu",
            WindowTypeName = "SebLogiWheel.WheelMenuWindow"
        };

        public static readonly App Ultrawide = new App
        {
            DisplayName = "Graphics",
            FileName = "wide",
            PluginGuid = "shibe.easydeliveryco.sebultrawide",
            ListenerName = "UltrawideMenu",
            ListenerData = "listener_UltrawideMenu",
            WindowTypeName = "SebUltrawide.UltrawideMenuWindow"
        };

        public static readonly App Binds = new App
        {
            DisplayName = "Binds",
            FileName = "binds",
            PluginGuid = "shibe.easydeliveryco.sebbinds",
            ListenerName = "SebBindsMenu",
            ListenerData = "listener_SebBindsMenu",
            WindowTypeName = "SebBinds.BindsMenuWindow"
        };

        public static readonly App Truck = new App
        {
            DisplayName = "Truck",
            FileName = "truck",
            PluginGuid = "shibe.easydeliveryco.sebtruck",
            ListenerName = "SebTruckMenu",
            ListenerData = "listener_SebTruckMenu",
            WindowTypeName = "SebTruck.TruckMenuWindow"
        };

        static CartridgeApps()
        {
            // Built-in cartridges (register first so they appear first).
            RegisterAppInternal(Binds);
            RegisterAppInternal(Truck);
            RegisterAppInternal(Ultrawide);
            RegisterAppInternal(Wheel);
        }

        public static App[] GetRegisteredAppsSnapshot()
        {
            lock (_lock)
            {
                return _registeredApps.ToArray();
            }
        }

        public static bool RegisterApp(App app, bool replaceIfExists = false)
        {
            if (!IsValidForRegistration(app, out string id))
            {
                return false;
            }

            lock (_lock)
            {
                if (_registeredAppIds.Contains(id))
                {
                    if (!replaceIfExists)
                    {
                        return false;
                    }

                    for (int i = 0; i < _registeredApps.Count; i++)
                    {
                        if (string.Equals(_registeredApps[i].FileName, id, StringComparison.OrdinalIgnoreCase))
                        {
                            _registeredApps[i] = app;
                            return true;
                        }
                    }

                    // Fallback if list/hash ever get out of sync.
                    return false;
                }

                _registeredApps.Add(app);
                _registeredAppIds.Add(id);
                return true;
            }
        }

        private static void RegisterAppInternal(App app)
        {
            if (!IsValidForRegistration(app, out string id))
            {
                return;
            }

            lock (_lock)
            {
                if (_registeredAppIds.Contains(id))
                {
                    return;
                }
                _registeredApps.Add(app);
                _registeredAppIds.Add(id);
            }
        }

        private static bool IsValidForRegistration(App app, out string id)
        {
            id = null;

            if (string.IsNullOrWhiteSpace(app.FileName))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(app.DisplayName))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(app.PluginGuid))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(app.ListenerName))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(app.ListenerData))
            {
                return false;
            }

            id = app.FileName.Trim();
            return id.Length != 0;
        }

        public static bool IsInstalled(App app)
        {
            if (string.IsNullOrWhiteSpace(app.PluginGuid))
            {
                return false;
            }
            return Chainloader.PluginInfos.ContainsKey(app.PluginGuid);
        }

        public static bool EnsureListener(DesktopDotExe desktop, App app)
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
            if (string.IsNullOrWhiteSpace(app.WindowTypeName))
            {
                Plugin.LogDebug("Missing window type name for '" + app.PluginGuid + "'");
                return false;
            }

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
