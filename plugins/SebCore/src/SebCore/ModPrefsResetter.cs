using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace SebCore
{
    internal static class ModPrefsResetter
    {
        internal sealed class Result
        {
            public int DeletedCount;
            public List<string> DeletedKeys = new List<string>();
        }

        internal static Result ClearAllKnownModPrefs()
        {
            var result = new Result();

            // 1) Delete known const PrefKey* fields from loaded plugins.
            foreach (var kvp in Chainloader.PluginInfos)
            {
                var info = kvp.Value;
                if (info == null || info.Instance == null)
                {
                    continue;
                }

                Assembly asm = info.Instance.GetType().Assembly;
                TryDeletePrefKeyConstantsFromAssembly(asm, result);
            }

            // 2) Best-effort: clear generated bind keys.
            // Stable numeric keys:
            for (int layer = 0; layer <= 1; layer++)
            {
                for (int action = 0; action <= 512; action++)
                {
                    TryDeleteKey("ELWS_Bind_" + layer + "_" + action, result);
                }
            }

            // Legacy name keys, if SebBinds is present.
            TryDeleteLegacyBindNameKeys(result);

            PlayerPrefs.Save();
            return result;
        }

        private static void TryDeletePrefKeyConstantsFromAssembly(Assembly asm, Result result)
        {
            if (asm == null)
            {
                return;
            }

            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch
            {
                return;
            }

            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];
                if (t == null)
                {
                    continue;
                }

                FieldInfo[] fields;
                try
                {
                    fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                }
                catch
                {
                    continue;
                }

                foreach (var f in fields)
                {
                    if (f == null)
                    {
                        continue;
                    }
                    if (f.FieldType != typeof(string))
                    {
                        continue;
                    }
                    if (!f.IsLiteral || f.IsInitOnly)
                    {
                        continue;
                    }

                    // Convention: PrefKeyFoo = "SomeKey".
                    if (f.Name == null || !f.Name.StartsWith("PrefKey", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string key;
                    try
                    {
                        key = (string)f.GetRawConstantValue();
                    }
                    catch
                    {
                        continue;
                    }

                    TryDeleteKey(key, result);
                }
            }
        }

        private static void TryDeleteLegacyBindNameKeys(Result result)
        {
            // If SebBinds is present, iterate its BindAction enum names to clear legacy keys.
            if (!Chainloader.PluginInfos.TryGetValue("shibe.easydeliveryco.sebbinds", out var info) || info == null || info.Instance == null)
            {
                return;
            }

            Assembly asm = info.Instance.GetType().Assembly;
            Type actionEnum = asm.GetType("SebBinds.BindAction", throwOnError: false);
            if (actionEnum == null || !actionEnum.IsEnum)
            {
                return;
            }

            string[] names;
            try
            {
                names = Enum.GetNames(actionEnum);
            }
            catch
            {
                return;
            }

            for (int layer = 0; layer <= 1; layer++)
            {
                string layerName = layer == 0 ? "Normal" : "Modified";
                for (int i = 0; i < names.Length; i++)
                {
                    TryDeleteKey("ELWS_Bind_" + layerName + "_" + names[i], result);
                }
            }
        }

        private static void TryDeleteKey(string key, Result result)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            try
            {
                if (!PlayerPrefs.HasKey(key))
                {
                    return;
                }

                PlayerPrefs.DeleteKey(key);
                result.DeletedCount++;

                // Keep the list bounded; it is just for debugging.
                if (result.DeletedKeys.Count < 64)
                {
                    result.DeletedKeys.Add(key);
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
