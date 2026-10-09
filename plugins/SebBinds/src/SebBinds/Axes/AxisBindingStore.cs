using UnityEngine;

namespace SebBinds
{
    public static class AxisBindingStore
    {
        // Cache decoded axis bindings to keep PlayerPrefs out of per-frame input code.
        private static readonly System.Collections.Generic.Dictionary<int, BindingInput> AxisCache =
            new System.Collections.Generic.Dictionary<int, BindingInput>(64);

        private static int CacheKey(BindingScheme scheme, AxisAction action)
        {
            return ((int)scheme << 16) | (int)action;
        }

        internal static void ClearRuntimeCache()
        {
            AxisCache.Clear();
        }

        private static string Key(BindingScheme scheme, AxisAction action)
        {
            // Stable numeric key.
            if (scheme == BindingScheme.Wheel)
            {
                return "SebBinds_Wheel_Axis_" + (int)action;
            }
            if (scheme == BindingScheme.Keyboard)
            {
                return "SebBinds_KB_Axis_" + (int)action;
            }
            return "SebBinds_Axis_" + (int)action;
        }

        public static BindingInput GetAxisBinding(BindingScheme scheme, AxisAction action)
        {
            int ck = CacheKey(scheme, action);
            if (AxisCache.TryGetValue(ck, out var cached))
            {
                return cached;
            }

            string key = Key(scheme, action);
            int raw = PlayerPrefs.GetInt(key, -1);
            var decoded = BindingStore.Decode(raw);

            // Migration from earlier builds where wheel axes were stored in the controller keyspace.
            if (!PlayerPrefs.HasKey(key) && scheme == BindingScheme.Wheel)
            {
                string legacy = Key(BindingScheme.Controller, action);
                int legacyRaw = PlayerPrefs.GetInt(legacy, -1);
                var legacyDecoded = BindingStore.Decode(legacyRaw);
                if (legacyDecoded.Kind == BindingKind.WheelAxis || legacyDecoded.Kind == BindingKind.WheelDpadAxis)
                {
                    PlayerPrefs.SetInt(key, legacyRaw);
                    PlayerPrefs.DeleteKey(legacy);
                    decoded = legacyDecoded;
                }
            }

            // Keep schemes isolated.
            if (scheme == BindingScheme.Controller && (decoded.Kind == BindingKind.WheelAxis || decoded.Kind == BindingKind.WheelDpadAxis))
            {
                decoded = new BindingInput { Kind = BindingKind.None, Code = 0 };
            }
            if (scheme == BindingScheme.Wheel && (decoded.Kind == BindingKind.GamepadAxis || decoded.Kind == BindingKind.GamepadDpadAxis || decoded.Kind == BindingKind.GamepadDpad))
            {
                decoded = new BindingInput { Kind = BindingKind.None, Code = 0 };
            }

            AxisCache[ck] = decoded;
            return decoded;
        }

        public static void SetAxisBinding(BindingScheme scheme, AxisAction action, BindingInput input)
        {
            // Keep schemes isolated.
            if (scheme == BindingScheme.Controller && (input.Kind == BindingKind.WheelAxis || input.Kind == BindingKind.WheelDpadAxis))
            {
                input = new BindingInput { Kind = BindingKind.None, Code = 0 };
            }
            if (scheme == BindingScheme.Wheel && (input.Kind == BindingKind.GamepadAxis || input.Kind == BindingKind.GamepadDpadAxis || input.Kind == BindingKind.GamepadDpad))
            {
                input = new BindingInput { Kind = BindingKind.None, Code = 0 };
            }

            PlayerPrefs.SetInt(Key(scheme, action), BindingStore.Encode(input));

            AxisCache[CacheKey(scheme, action)] = input;
        }

        public static void ClearAxisBinding(BindingScheme scheme, AxisAction action)
        {
            SetAxisBinding(scheme, action, new BindingInput { Kind = BindingKind.None, Code = 0 });
        }

        internal static bool HasAxisBinding(BindingScheme scheme, AxisAction action)
        {
            return PlayerPrefs.HasKey(Key(scheme, action));
        }

        // Back-compat: controller scheme.
        public static BindingInput GetAxisBinding(AxisAction action) => GetAxisBinding(BindingScheme.Controller, action);
        public static void SetAxisBinding(AxisAction action, BindingInput input) => SetAxisBinding(BindingScheme.Controller, action, input);
        public static void ClearAxisBinding(AxisAction action) => ClearAxisBinding(BindingScheme.Controller, action);

        public static void ClearAll()
        {
            foreach (AxisAction a in System.Enum.GetValues(typeof(AxisAction)))
            {
                PlayerPrefs.DeleteKey(Key(BindingScheme.Controller, a));
                PlayerPrefs.DeleteKey(Key(BindingScheme.Keyboard, a));
                PlayerPrefs.DeleteKey(Key(BindingScheme.Wheel, a));
            }

            ClearRuntimeCache();
        }

        public static void ClearScheme(BindingScheme scheme)
        {
            foreach (AxisAction a in System.Enum.GetValues(typeof(AxisAction)))
            {
                PlayerPrefs.DeleteKey(Key(scheme, a));
            }

            ClearRuntimeCache();
        }
    }
}
