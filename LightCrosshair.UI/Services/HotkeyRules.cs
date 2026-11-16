using LightCrosshair.UI.Localization;

namespace LightCrosshair.UI.Services;

public static class HotkeyRules
{
    public static bool IsWheel(string? key) => key is "WheelUp" or "WheelDown";

    public static bool IsMouse(string? key) => key is "MiddleClick" or "RightClick" or "Mouse4" or "Mouse5" or "WheelUp" or "WheelDown";

    public static InputBinding Copy(InputBinding source) => new()
    {
        Key = source.Key ?? "",
        Modifiers = source.Modifiers,
        Mode = IsWheel(source.Key) ? BindingMode.Toggle : source.Mode,
    };

    public static bool Same(InputBinding a, InputBinding b) =>
        !string.IsNullOrEmpty(a.Key) &&
        string.Equals(a.Key, b.Key, StringComparison.OrdinalIgnoreCase) &&
        a.Modifiers == b.Modifiers;

    public static string Format(InputBinding? binding)
    {
        if (binding == null || string.IsNullOrEmpty(binding.Key))
            return Loc.Instance["Hotkeys_Unbound"];
        var parts = new List<string>(5);
        if (binding.Modifiers.HasFlag(KeyModifiers.Control)) parts.Add(Loc.Instance["Key_Ctrl"]);
        if (binding.Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add(Loc.Instance["Key_Alt"]);
        if (binding.Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add(Loc.Instance["Key_Shift"]);
        if (binding.Modifiers.HasFlag(KeyModifiers.Win)) parts.Add(Loc.Instance["Key_Win"]);
        parts.Add(binding.Key switch
        {
            "MiddleClick" => Loc.Instance["Key_MiddleClick"],
            "RightClick" => Loc.Instance["Key_RightClick"],
            "Mouse4" => Loc.Instance["Key_Mouse4"],
            "Mouse5" => Loc.Instance["Key_Mouse5"],
            "WheelUp" => Loc.Instance["Key_WheelUp"],
            "WheelDown" => Loc.Instance["Key_WheelDown"],
            var other => other,
        });
        return string.Join("+", parts);
    }

    /// <summary>Returns the other action id that uses the same chord, if any.</summary>
    public static string? ConflictWith(string action, InputBinding candidate, HotkeySettings hotkeys, IEnumerable<(string Action, InputBinding Binding)> gameOverrides)
    {
        if (string.IsNullOrEmpty(candidate.Key)) return null;
        foreach (var (other, binding) in All(hotkeys, gameOverrides))
        {
            if (other == action) continue;
            if (Same(candidate, binding)) return other;
        }
        return null;
    }

    public static IEnumerable<(string Action, InputBinding Binding)> All(HotkeySettings hotkeys, IEnumerable<(string Action, InputBinding Binding)> gameOverrides)
    {
        yield return ("ToggleVisibility", hotkeys.ToggleVisibility);
        yield return ("NextCrosshair", hotkeys.NextCrosshair);
        yield return ("PreviousCrosshair", hotkeys.PreviousCrosshair);
        yield return ("OpenSettings", hotkeys.OpenSettings);
        yield return ("HoldVisibility", hotkeys.HoldVisibility);
        foreach (var item in gameOverrides)
            yield return item;
    }
}
