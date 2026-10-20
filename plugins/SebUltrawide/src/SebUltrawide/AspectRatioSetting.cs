using System.Globalization;

namespace SebUltrawide
{
    // Parses the aspect_ratio config value. Plain .NET so the regression tests can run it.
    internal static class AspectRatioSetting
    {
        internal enum Kind
        {
            Vanilla,
            Auto,
            Window,
            Display,
            Fixed
        }

        internal static Kind Parse(string value, out float aspect)
        {
            aspect = 0f;
            string normalized = string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "default":
                case "native":
                case "vanilla":
                    return Kind.Vanilla;
                case "auto":
                case "match":
                    return Kind.Auto;
                case "window":
                    return Kind.Window;
            }

            string[] parts = normalized.Split(':');
            if (parts.Length == 2 && TryParseFloat(parts[0], out float w) && TryParseFloat(parts[1], out float h) && h > 0.01f)
            {
                aspect = w / h;
            }
            else if (parts.Length == 1 && TryParseFloat(normalized, out float direct))
            {
                aspect = direct;
            }

            // Empty, "display", unparseable or tiny ratios fall back to the display's aspect.
            return aspect > 0.1f ? Kind.Fixed : Kind.Display;
        }

        // Invariant culture: "2.39" must not read as 239 on comma-decimal systems.
        private static bool TryParseFloat(string s, out float value)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
