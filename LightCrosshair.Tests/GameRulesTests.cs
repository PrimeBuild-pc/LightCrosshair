using LightCrosshair.Contracts;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public class GameRulesTests
{
    [Theory]
    [InlineData(null, false, true, false)]
    [InlineData("unconfigured.exe", true, false, false)]
    [InlineData("game.exe", true, true, true)]
    [InlineData("disabled.exe", true, false, false)]
    public void ForegroundTableResolvesAllOverrides(string? foreground, bool gamesOnly, bool visible, bool game)
    {
        var store = LegacyMigration.Defaults(); store.Settings.ShowOnlyInConfiguredGames = gamesOnly;
        store.Crosshairs.Add(new() { Id = "game-cross", Name = "Game" });
        store.GameProfiles.Add(new() { Id = "game", ProcessName = "game.exe", CrosshairId = "game-cross", FpsOverlay = true, VisibilityBinding = new() { Key = "F6" } });
        store.GameProfiles.Add(new() { Id = "off", ProcessName = "disabled.exe", Enabled = false, CrosshairId = "game-cross" });
        var context = GameRules.Evaluate(store, foreground);
        Assert.Equal(visible, context.Visible); Assert.Equal(game, context.Game is not null); Assert.Equal(game, context.FpsEnabled);
        Assert.Equal(game ? "game-cross" : store.Settings.DefaultCrosshairId, context.CrosshairId);
        Assert.Equal(game ? "F6" : "X", context.Binding.Key);
        Assert.Same(context, GameRules.Evaluate(store, "lightcrosshair.ui.exe", context));
        var leave = GameRules.Evaluate(store, "explorer.exe", context);
        Assert.Null(leave.Game); Assert.Equal(!gamesOnly, leave.Visible);
    }
    [Fact]
    public void ExplicitOffWinsOverGlobalOnAndHiddenPreferenceSurvivesGameTransitions()
    {
        var s = LegacyMigration.Defaults(); s.Settings.FpsOverlay.Enabled = true; s.Settings.CrosshairVisible = false;
        s.GameProfiles.Add(new() { ProcessName = "game.exe", CrosshairId = s.Settings.DefaultCrosshairId, FpsOverlay = false });
        var game = GameRules.Evaluate(s, "game.exe"); Assert.False(game.Visible); Assert.False(game.FpsEnabled);
        var other = GameRules.Evaluate(s, "other.exe"); Assert.False(other.Visible); Assert.True(other.FpsEnabled);
    }
    [Fact]
    public void FpsBufferIgnoresInvalidSamplesAndCalculatesWorstOnePercent()
    {
        var buffer = new FpsBuffer(); buffer.Add(double.NaN); buffer.Add(-1); Assert.False(buffer.Snapshot().HasData);
        for (int i = 0; i < 99; i++) buffer.Add(10); buffer.Add(100);
        var result = buffer.Snapshot(); Assert.Equal(10, result.OnePercentLow); Assert.Equal(100, result.Frametime);
        buffer.Clear(); Assert.False(buffer.Snapshot().HasData);
    }
}
