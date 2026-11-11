using System.Windows.Forms;

namespace LightCrosshair;

internal static class VisibilityBindingConflicts
{
    public static string? Find(VisibilityBinding binding, CrosshairConfig config, IEnumerable<CrosshairProfile> profiles)
    {
        if (binding.Input != BindingInput.Keyboard) return null;
        int Flags(bool alt, bool ctrl, bool shift, bool win) => (alt ? 1 : 0) | (ctrl ? 2 : 0) | (shift ? 4 : 0) | (win ? 8 : 0);
        if (binding.Matches(BindingInput.Keyboard, (int)config.CycleProfileHotkeyKey, Flags(config.CycleProfileHotkeyUseAlt, config.CycleProfileHotkeyUseControl, config.CycleProfileHotkeyUseShift, config.CycleProfileHotkeyUseWin)))
            return "Conflicts with Cycle Profiles Forward.";
        if (binding.Matches(BindingInput.Keyboard, (int)config.CycleProfilePrevHotkeyKey, Flags(config.CycleProfilePrevHotkeyUseAlt, config.CycleProfilePrevHotkeyUseControl, config.CycleProfilePrevHotkeyUseShift, config.CycleProfilePrevHotkeyUseWin)))
            return "Conflicts with Cycle Profiles Back.";
        if (binding.Matches(BindingInput.Keyboard, (int)config.SettingsWindowHotkeyKey, Flags(config.SettingsWindowHotkeyUseAlt, config.SettingsWindowHotkeyUseControl, config.SettingsWindowHotkeyUseShift, config.SettingsWindowHotkeyUseWin)))
            return "Conflicts with Toggle Settings Window.";
        foreach (var profile in profiles)
            if (profile.HotKey != Keys.None && binding.Matches(BindingInput.Keyboard, (int)profile.HotKey, 0))
                return $"Conflicts with profile '{profile.Name}'.";
        return null;
    }
}
