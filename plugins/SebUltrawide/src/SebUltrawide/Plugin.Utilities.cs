using HarmonyLib;
using UnityEngine;

namespace SebUltrawide
{
    public partial class Plugin
    {
        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return "(null)";
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.001f && Mathf.Abs(a.y - b.y) < 0.001f;
        }

        private static Camera GetRacePlayerCamera(object racePlayer)
        {
            if (racePlayer == null)
            {
                return null;
            }

            var camField = AccessTools.Field(racePlayer.GetType(), "cam");
            if (camField == null)
            {
                return null;
            }

            return camField.GetValue(racePlayer) as Camera;
        }

        private static void PatchByName(Harmony harmony, string typeName, string methodName, string prefix = null, string postfix = null)
        {
            SebCore.HarmonyUtil.PatchByName(harmony, typeof(Plugin), typeName, methodName, prefix, postfix, LogDebug);
        }
    }
}
