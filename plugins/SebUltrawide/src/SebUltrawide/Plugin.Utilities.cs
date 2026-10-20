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
    }
}
