using UnityEngine;

namespace SebCore
{
    public static class ModPrefs
    {
        public static float GetFloat(string key, float fallback)
        {
            try { return PlayerPrefs.GetFloat(key, fallback); }
            catch { return fallback; }
        }

        public static void SetFloat(string key, float value)
        {
            try { PlayerPrefs.SetFloat(key, value); }
            catch { }
        }

        public static int GetInt(string key, int fallback)
        {
            try { return PlayerPrefs.GetInt(key, fallback); }
            catch { return fallback; }
        }

        public static void SetInt(string key, int value)
        {
            try { PlayerPrefs.SetInt(key, value); }
            catch { }
        }

        public static bool GetBool(string key, bool fallback)
        {
            return GetInt(key, fallback ? 1 : 0) != 0;
        }

        public static void SetBool(string key, bool value)
        {
            SetInt(key, value ? 1 : 0);
        }

        public static void DeleteKey(string key)
        {
            try { PlayerPrefs.DeleteKey(key); }
            catch { }
        }

        public static bool HasKey(string key)
        {
            try { return PlayerPrefs.HasKey(key); }
            catch { return false; }
        }

        public static void Save()
        {
            try { PlayerPrefs.Save(); }
            catch { }
        }
    }
}
