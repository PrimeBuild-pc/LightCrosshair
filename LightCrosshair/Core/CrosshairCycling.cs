using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal static class CrosshairCycling
{
    internal static string? Apply(StoreDocument store, string activeId, string? gameId, bool forward)
    {
        var favorites = store.Crosshairs.Where(d => d.Favorite).ToList();
        var choices = (favorites.Count >= 2 ? favorites : store.Crosshairs).OrderByDescending(d => d.UpdatedAt).ToList();
        if (choices.Count == 0) return null;
        int current = choices.FindIndex(d => d.Id == activeId);
        int index = current < 0 ? (forward ? 0 : choices.Count - 1) : (current + (forward ? 1 : -1) + choices.Count) % choices.Count;
        string selected = choices[index].Id;
        var game = store.GameProfiles.FirstOrDefault(g => g.Id == gameId && g.Enabled);
        if (game is not null) game.CrosshairId = selected;
        else store.Settings.DefaultCrosshairId = selected;
        return selected;
    }
}
