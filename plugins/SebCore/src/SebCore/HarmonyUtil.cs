using System;
using System.Reflection;
using HarmonyLib;

namespace SebCore
{
    public static class HarmonyUtil
    {
        public static void PatchByName(Harmony harmony, Type patchType, string typeName, string methodName, string prefix = null, string postfix = null, Action<string> logDebug = null)
        {
            if (harmony == null || patchType == null)
            {
                return;
            }

            var type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                logDebug?.Invoke("Type '" + typeName + "' not found for patch " + methodName + ".");
                return;
            }

            MethodInfo method;
            try
            {
                method = AccessTools.Method(type, methodName);
            }
            catch (AmbiguousMatchException)
            {
                method = ResolveAmbiguousMethod(type, methodName);
            }

            if (method == null)
            {
                logDebug?.Invoke("Method '" + typeName + "." + methodName + "' not found for patch.");
                return;
            }

            HarmonyMethod prefixMethod = string.IsNullOrWhiteSpace(prefix) ? null : new HarmonyMethod(patchType, prefix);
            HarmonyMethod postfixMethod = string.IsNullOrWhiteSpace(postfix) ? null : new HarmonyMethod(patchType, postfix);
            harmony.Patch(method, prefixMethod, postfixMethod);
        }

        private static MethodInfo ResolveAmbiguousMethod(Type type, string methodName)
        {
            MethodInfo best = null;
            int bestParamCount = int.MaxValue;
            for (var current = type; current != null; current = current.BaseType)
            {
                var methods = current.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (var candidate in methods)
                {
                    if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int paramCount = candidate.GetParameters().Length;
                    if (paramCount == 0)
                    {
                        return candidate;
                    }

                    if (paramCount < bestParamCount)
                    {
                        bestParamCount = paramCount;
                        best = candidate;
                    }
                }
            }

            return best;
        }
    }
}
