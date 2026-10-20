using System;
using System.Collections.Generic;
using UnityEngine;

namespace SebBinds
{
    public static class SebBindsApi
    {
        public sealed class Page
        {
            public string Id;
            public string Title;
            public BindAction[] Actions;
        }

        private static readonly object Sync = new object();
        private static readonly List<Page> ExtraPages = new List<Page>();
        private const string PrefKeyShiftMode = "SebBinds_ShiftMode";

        public static void RegisterActionsPage(string id, string title, params BindAction[] actions)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("id is required", nameof(id));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("title is required", nameof(title));
            if (actions == null) throw new ArgumentNullException(nameof(actions));

            var page = new Page
            {
                Id = id.Trim(),
                Title = title.Trim(),
                Actions = actions
            };

            lock (Sync)
            {
                // Replace by id.
                for (int i = 0; i < ExtraPages.Count; i++)
                {
                    if (string.Equals(ExtraPages[i].Id, page.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        ExtraPages[i] = page;
                        return;
                    }
                }
                ExtraPages.Add(page);
            }
        }

        public static ShiftMode GetShiftMode()
        {
            return (ShiftMode)Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyShiftMode, (int)ShiftMode.Toggle), 0, 1);
        }

        public static void SetShiftMode(ShiftMode mode)
        {
            PlayerPrefs.SetInt(PrefKeyShiftMode, (int)mode);
        }

        public static ShiftMode NextShiftMode(ShiftMode mode)
        {
            return mode == ShiftMode.Toggle ? ShiftMode.Hold : ShiftMode.Toggle;
        }

        public static string GetShiftModeLabel(ShiftMode mode)
        {
            return mode == ShiftMode.Hold ? "Hold" : "Toggle";
        }

        private static readonly BindingScheme[] SchemesControllerKeyboard = { BindingScheme.Controller, BindingScheme.Keyboard };
        private static readonly BindingScheme[] SchemesControllerKeyboardWheel = { BindingScheme.Controller, BindingScheme.Keyboard, BindingScheme.Wheel };
        private static readonly EdgeTracker ActionEdges = new EdgeTracker();

        private static BindingScheme[] ActiveSchemes =>
            WheelInterop.IsWheelPluginPresent() ? SchemesControllerKeyboardWheel : SchemesControllerKeyboard;

        // Modified while the scheme's modifier is held, otherwise Normal.
        public static BindingLayer GetActiveLayer(BindingScheme scheme)
        {
            var modifier = BindingStore.GetModifierBinding(scheme);
            return modifier.Kind != BindingKind.None && BindingEvaluator.IsDown(modifier)
                ? BindingLayer.Modified
                : BindingLayer.Normal;
        }

        // normalFallback: while the modifier is held, use the Normal bind when the action has no Modified bind.
        public static BindingInput GetActiveBinding(BindingScheme scheme, BindAction action, bool normalFallback = false)
        {
            var layer = GetActiveLayer(scheme);
            var binding = BindingStore.GetBinding(scheme, layer, action);
            if (normalFallback && binding.Kind == BindingKind.None && layer == BindingLayer.Modified)
            {
                binding = BindingStore.GetBinding(scheme, BindingLayer.Normal, action);
            }
            return binding;
        }

        // True when the action has a bind on either layer of any active scheme.
        public static bool IsActionBound(BindAction action)
        {
            var schemes = ActiveSchemes;
            for (int i = 0; i < schemes.Length; i++)
            {
                if (BindingStore.GetBinding(schemes[i], BindingLayer.Normal, action).Kind != BindingKind.None ||
                    BindingStore.GetBinding(schemes[i], BindingLayer.Modified, action).Kind != BindingKind.None)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsActionDown(BindAction action, bool normalFallback = false)
        {
            var schemes = ActiveSchemes;
            for (int i = 0; i < schemes.Length; i++)
            {
                var binding = GetActiveBinding(schemes[i], action, normalFallback);
                if (binding.Kind != BindingKind.None && BindingEvaluator.IsDown(binding))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool WasActionPressed(BindAction action, bool normalFallback = false)
        {
            var schemes = ActiveSchemes;
            for (int i = 0; i < schemes.Length; i++)
            {
                var binding = GetActiveBinding(schemes[i], action, normalFallback);
                if (binding.Kind != BindingKind.None && BindingEvaluator.WasPressedThisFrame(binding))
                {
                    return true;
                }
            }
            return false;
        }

        // Tracked per action, not per bind: if the modifier flips the layer mid-hold, the held bind
        // drops out of the active layer and this reports the release instead of leaving the action stuck.
        public static bool WasActionReleased(BindAction action, bool normalFallback = false)
        {
            int key = ((int)action << 1) | (normalFallback ? 1 : 0);
            return ActionEdges.Released(key, Time.frameCount, IsActionDown(action, normalFallback));
        }

        public static void RegisterWheelSource(IWheelSource source)
        {
            WheelInterop.Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        internal static List<Page> GetExtraPagesSnapshot()
        {
            lock (Sync)
            {
                return new List<Page>(ExtraPages);
            }
        }

        internal static bool HasExtraPage(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            lock (Sync)
            {
                for (int i = 0; i < ExtraPages.Count; i++)
                {
                    if (string.Equals(ExtraPages[i].Id, id.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
