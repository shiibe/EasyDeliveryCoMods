using System;
using System.Collections.Generic;

// In-memory platform stand-ins: the linked production stores and parser run unchanged.
namespace UnityEngine
{
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, int> Values = new Dictionary<string, int>();
        public static bool HasKey(string key) => Values.ContainsKey(key);
        public static int GetInt(string key, int fallback) => Values.TryGetValue(key, out int value) ? value : fallback;
        public static void SetInt(string key, int value) => Values[key] = value;
        public static void DeleteKey(string key) => Values.Remove(key);
        public static void DeleteAll() => Values.Clear();
        public static void Save() { }
    }
    public static class Mathf
    {
        public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float MoveTowards(float current, float target, float step) => current + Math.Clamp(target - current, -step, step);
    }
}
namespace UnityEngine.InputSystem
{
    public enum Key
    {
        None, A = 1, Z = 26, Digit0 = 30, Digit9 = 39, Numpad0 = 40, Numpad1,
        Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,
        Semicolon = 60, Quote, Comma, Period, Slash, Backslash, LeftBracket, RightBracket,
        Minus, Equals, Backquote, NumpadDivide, NumpadMultiply, NumpadPlus, NumpadMinus,
        NumpadPeriod, E, Space, S, LeftShift, Tab, Escape, W, D, C, X, R, H,
        DownArrow, LeftArrow, RightArrow, UpArrow, Q, F1, F2, F3, F4
    }
}
namespace BepInEx
{
    public class BaseUnityPlugin { }
    public sealed class BepInPlugin : Attribute
    {
        public BepInPlugin(string guid, string name, string version) { }
    }
    public sealed class BepInDependency : Attribute
    {
        public enum DependencyFlags { HardDependency }
        public BepInDependency(string guid, DependencyFlags flags) { }
    }
}
namespace BepInEx.Logging { public class ManualLogSource { } }
namespace BepInEx.Bootstrap
{
    public class PluginInfo { public BepInEx.BaseUnityPlugin Instance; }
    public static class Chainloader
    {
        public static readonly Dictionary<string, PluginInfo> PluginInfos = new Dictionary<string, PluginInfo>();
    }
}
public class sInputManager { }
namespace SebBinds
{
    public partial class Plugin
    {
        private static int _defaultsEnsuredForInputInstanceId;
        private static bool _didMigrateLegacyBinds;
        internal static bool DefaultsNeedReseeding => _defaultsEnsuredForInputInstanceId == 0 && !_didMigrateLegacyBinds;
    }
    internal static class DefaultPreset
    {
        // The reset test only exercises routing and production cache/default clearing.
        internal static int InstallCalls;
        internal static bool TryInstallFromInputManager(sInputManager input, bool? forceOnController, bool forceReinstall)
        {
            InstallCalls++;
            return false;
        }
    }
}
