using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal sealed record GameContext(string CrosshairId, GameProfile? Game, bool Visible, bool FpsEnabled, InputBinding Binding)
{
    public DisplayColorSettings? DisplayColors => Game is { DisplayColorsEnabled: true } ? Game.DisplayColors : null;
}

internal static class GameRules
{
    public static GameContext Evaluate(StoreDocument document, string? foreground, GameContext? previous = null)
    {
        if (foreground == "lightcrosshair.ui.exe" && previous is not null) return previous;
        var game = document.GameProfiles.FirstOrDefault(g => g.Enabled && g.ProcessName == foreground);
        var settings = document.Settings;
        return new(game?.CrosshairId ?? settings.DefaultCrosshairId, game,
            settings.CrosshairVisible && (game is not null || !settings.ShowOnlyInConfiguredGames),
            game?.FpsOverlay ?? settings.FpsOverlay.Enabled, game?.VisibilityBinding ?? settings.Hotkeys.ToggleVisibility);
    }
}
