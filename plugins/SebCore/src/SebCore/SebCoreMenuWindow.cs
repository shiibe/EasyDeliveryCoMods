using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using UnityEngine;

namespace SebCore
{
    public class SebCoreMenuWindow : MonoBehaviour
    {
        public const string DefaultFileName = "mods";
        public static string FileName => Plugin.GetMenuIconName();
        public const string ListenerName = "SebCoreMenu";
        public const string ListenerData = "listener_SebCoreMenu";

        private float _mouseYLock;
        private UIUtil _util;
        private DesktopDotExe.WindowView _view;

        private bool _resetPrefsConfirm;

        private int _cartridgePageIndex;

        private MenuPage _page;

        private enum MenuPage
        {
            Main = 0,
            Settings = 1
        }

        public void FrameUpdate(DesktopDotExe.WindowView view)
        {
            if (view == null)
            {
                return;
            }

            _view = view;

            _util ??= new UIUtil();
            _util.M = view.M;
            _util.R = view.R;
            _util.Nav = view.M.nav;

            Rect p = new Rect(view.position * 8f, view.size * 8f);
            p.position += new Vector2(8f, 8f);

            if (_util.M.mouseButtonUp)
            {
                _mouseYLock = 0f;
            }

            if (_mouseYLock > 0f)
            {
                _util.M.mouse.y = _mouseYLock;
            }

            DrawMenu(p);
        }

        public void BackButtonPressed()
        {
            if (_page == MenuPage.Settings)
            {
                _resetPrefsConfirm = false;
                _page = MenuPage.Main;
                return;
            }

            CloseWindow();
        }

        private void CloseWindow()
        {
            _resetPrefsConfirm = false;
            _page = MenuPage.Main;
            _cartridgePageIndex = 0;
            _view?.Kill();
        }

        private void DrawMenu(Rect p)
        {
            float cx = p.x + p.width / 2f;
            float y = p.y + 10f;
            float line = 12f;
            float sectionGap = 4f;

            _util.Label("SebCore", cx, y);
            y += line + sectionGap;

            if (_page == MenuPage.Settings)
            {
                DrawSettings(p, cx, ref y, line, sectionGap);
                return;
            }

            DrawMain(p, cx, ref y, line, sectionGap);
        }

        private void DrawMain(Rect p, float cx, ref float y, float line, float sectionGap)
        {
            // Cartridge launcher.
            var desktop = _util.M;
            var apps = CartridgeApps.GetRegisteredAppsSnapshot().Where(CartridgeApps.IsInstalled).ToList();

            float btnGap = 22f;
            float colGap = 78f;
            float leftX = cx - colGap;
            float rightX = cx + colGap;

            const int pageSize = 10;
            bool needsPaging = apps.Count > pageSize;
            int pageCount = needsPaging ? (apps.Count + pageSize - 1) / pageSize : 1;
            if (_cartridgePageIndex < 0) _cartridgePageIndex = 0;
            if (_cartridgePageIndex > pageCount - 1) _cartridgePageIndex = pageCount - 1;

            int startIndex = needsPaging ? _cartridgePageIndex * pageSize : 0;
            int endIndex = needsPaging ? Mathf.Min(startIndex + pageSize, apps.Count) : apps.Count;
            int pageItems = endIndex - startIndex;

            if (apps.Count == 0)
            {
                _util.Label("(no cartridges installed)", cx, y + 8f);
            }
            else
            {
                // 2-column launcher grid without gaps.
                float yy = y;
                for (int i = startIndex; i < endIndex; i++)
                {
                    int localIndex = i - startIndex;
                    float x = (localIndex % 2 == 0) ? leftX : rightX;
                    if (_util.FancyButton(apps[i].DisplayName, x, yy))
                    {
                        if (!CartridgeApps.EnsureListener(desktop, apps[i]))
                        {
                            Plugin.LogDebug("Launch failed: could not ensure listener for '" + apps[i].FileName + "'");
                        }
                        else if (!DesktopAppLauncher.TryOpenProgramListener(desktop, _util.R, apps[i].FileName, apps[i].ListenerData))
                        {
                            Plugin.LogDebug("Launch failed: program listener open failed for '" + apps[i].FileName + "'");
                        }
                    }

                    if (localIndex % 2 == 1)
                    {
                        yy += btnGap;
                    }
                }

                if (pageItems % 2 == 1)
                {
                    yy += btnGap;
                }
            }

            // Bottom buttons.
            float navY = p.y + p.height - 18f;
            float settingsY = navY - 12f;

            if (_util.SimpleButtonRaw("Settings", cx, settingsY))
            {
                _resetPrefsConfirm = false;
                _page = MenuPage.Settings;
                return;
            }

            if (needsPaging)
            {
                float prevX = p.x + 40f;
                float nextX = p.x + p.width - 40f;
                if (_util.SimpleButtonRaw("Prev", prevX, navY))
                {
                    if (_cartridgePageIndex > 0)
                    {
                        _cartridgePageIndex--;
                    }
                }
                if (_util.SimpleButtonRaw("Back", cx, navY))
                {
                    CloseWindow();
                    return;
                }
                if (_util.SimpleButtonRaw("Next", nextX, navY))
                {
                    if (_cartridgePageIndex < pageCount - 1)
                    {
                        _cartridgePageIndex++;
                    }
                }

                _util.Label((_cartridgePageIndex + 1) + "/" + pageCount, p.x + p.width - 18f, p.y + 10f);
            }
            else
            {
                if (_util.SimpleButtonRaw("Back", cx, navY))
                {
                    CloseWindow();
                    return;
                }
            }
        }

        private void DrawSettings(Rect p, float cx, ref float y, float line, float sectionGap)
        {
            _util.Label("Settings", cx, y);
            y += line + sectionGap;

            float navY = p.y + p.height - 18f;
            float clearY = navY - 12f;
            float actionY = navY - 24f;

            if (_resetPrefsConfirm)
            {
                _util.Label("Clear all mod prefs?", cx, actionY - 12f);
                if (_util.SimpleButtonRaw("Confirm", cx, actionY))
                {
                    Plugin.RequestClearModPrefs();
                    _resetPrefsConfirm = false;
                }
                if (_util.SimpleButtonRaw("Cancel", cx, clearY))
                {
                    _resetPrefsConfirm = false;
                }
            }
            else
            {
                if (_util.SimpleButtonRaw("Clear Mod Prefs", cx, actionY))
                {
                    _resetPrefsConfirm = true;
                }
            }
            if (_util.SimpleButtonRaw("Back", cx, navY))
            {
                _resetPrefsConfirm = false;
                _page = MenuPage.Main;
                return;
            }

            y += sectionGap;
            _util.Label("Installed Mods", cx, y);
            y += line;

            List<string> items = GetCartridgeLabels();
            if (items.Count == 0)
            {
                _util.Label("(none detected)", cx, y);
                return;
            }

            int maxLines = 8;
            float yy = y;
            for (int i = 0; i < items.Count && i < maxLines; i++)
            {
                _util.Label(items[i], cx, yy);
                yy += line - 2f;
            }
        }

        private static List<string> GetCartridgeLabels()
        {
            try
            {
                return Chainloader.PluginInfos
                    .Where(kvp => kvp.Value != null && kvp.Value.Metadata != null)
                    .Select(kvp => kvp.Value.Metadata)
                    .Where(m => m.GUID != null && m.GUID.StartsWith("shibe.easydeliveryco.", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(m => m.Name + " v" + m.Version)
                    .ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }
    }
}
