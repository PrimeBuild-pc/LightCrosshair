using System;

namespace LightCrosshair
{
    public static class UpdateVersionComparer
    {
        public static int? CompareVersions(string latest, string current)
        {
            if (!TryParseVersionLoose(latest, out var latestVer)) return null;
            if (!TryParseVersionLoose(current, out var currentVer)) return null;
            return latestVer.CompareTo(currentVer);
        }

        public static bool TryParseVersionLoose(string value, out Version version)
        {
            version = new Version(0, 0);
            if (string.IsNullOrWhiteSpace(value)) return false;

            string normalized = NormalizeVersionTag(value);
            if (Version.TryParse(normalized, out var parsedVersion) && parsedVersion != null)
            {
                version = parsedVersion;
                return true;
            }

            var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
            int major;
            if (parts.Length == 1 && int.TryParse(parts[0], out major))
            {
                version = new Version(major, 0);
                return true;
            }

            if (parts.Length == 2
                && int.TryParse(parts[0], out major)
                && int.TryParse(parts[1], out int minor))
            {
                version = new Version(major, minor);
                return true;
            }

            return false;
        }

        public static string NormalizeVersionTag(string value)
        {
            string normalized = (value ?? string.Empty).Trim().TrimStart('v', 'V');
            int suffixIndex = normalized.IndexOfAny(new[] { '-', '+', ' ' });
            if (suffixIndex >= 0)
            {
                normalized = normalized.Substring(0, suffixIndex);
            }

            return normalized;
        }
    }
}
