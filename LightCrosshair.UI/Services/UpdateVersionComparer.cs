namespace LightCrosshair.UI.Services;

/// <summary>Same comparison rules as v1.9: strip a leading v and any - / + / space suffix, then compare Version.</summary>
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

        var normalized = NormalizeVersionTag(value);
        if (Version.TryParse(normalized, out var parsedVersion) && parsedVersion != null)
        {
            version = parsedVersion;
            return true;
        }

        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && int.TryParse(parts[0], out var major))
        {
            version = new Version(major, 0);
            return true;
        }

        if (parts.Length == 2
            && int.TryParse(parts[0], out major)
            && int.TryParse(parts[1], out var minor))
        {
            version = new Version(major, minor);
            return true;
        }

        return false;
    }

    public static string NormalizeVersionTag(string value)
    {
        var normalized = (value ?? string.Empty).Trim().TrimStart('v', 'V');
        var suffixIndex = normalized.IndexOfAny(['-', '+', ' ']);
        if (suffixIndex >= 0)
            normalized = normalized[..suffixIndex];
        return normalized;
    }
}
