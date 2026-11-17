using System;
using System.Linq;
using LightCrosshair.Contracts;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public class CrosshairCyclingTests
{
    private static StoreDocument Store(int favorites)
    {
        var store = LegacyMigration.Defaults();
        store.Crosshairs = Enumerable.Range(0, 3).Select(i => new CrosshairDesign { Id = $"c{i}", Name = $"Crosshair {i}", Favorite = i < favorites, UpdatedAt = DateTimeOffset.UnixEpoch.AddDays(i) }).ToList();
        store.Settings.DefaultCrosshairId = "c0";
        store.GameProfiles.Add(new() { Id = "game", ProcessName = "game.exe", CrosshairId = "c0" });
        return store;
    }

    [Theory]
    [InlineData(0, "c2")]
    [InlineData(1, "c2")]
    [InlineData(2, "c1")]
    [InlineData(3, "c2")]
    public void NextUsesAtLeastTwoFavoritesOtherwiseAllInLibraryOrder(int favorites, string expected)
    {
        var store = Store(favorites);
        Assert.Equal(expected, CrosshairCycling.Apply(store, "c0", null, true));
        Assert.Equal(expected, store.Settings.DefaultCrosshairId);
        Assert.Equal("c0", store.GameProfiles[0].CrosshairId);
    }

    [Theory]
    [InlineData(true, "c2")]
    [InlineData(false, "c0")]
    public void ActiveOutsideFavoritesStartsAtTheDirectionalEnd(bool forward, string expected)
    {
        var store = Store(0); store.Crosshairs[0].Favorite = store.Crosshairs[2].Favorite = true;
        Assert.Equal(expected, CrosshairCycling.Apply(store, "c1", "game", forward));
        Assert.Equal(expected, store.GameProfiles[0].CrosshairId);
        Assert.Equal("c0", store.Settings.DefaultCrosshairId);
    }

    [Theory]
    [InlineData("game.exe", "game")]
    [InlineData("explorer.exe", null)]
    [InlineData("disabled.exe", null)]
    public void SelectionPersistsInTheResolvedContext(string process, string? expectedGame)
    {
        var store = Store(0); store.GameProfiles.Add(new() { Id = "disabled", ProcessName = "disabled.exe", CrosshairId = "c0", Enabled = false });
        var context = GameRules.Evaluate(store, process);
        Assert.Equal(expectedGame, context.Game?.Id);
        CrosshairCycling.Apply(store, context.CrosshairId, context.Game?.Id, true);
        var persisted = Validation.Copy(store);
        Assert.Equal("c2", GameRules.Evaluate(persisted, process).CrosshairId);
        Assert.Equal(expectedGame is null ? "c2" : "c0", persisted.Settings.DefaultCrosshairId);
        Assert.Equal(expectedGame is null ? "c0" : "c2", persisted.GameProfiles[0].CrosshairId);
        Assert.Equal("c0", persisted.GameProfiles[1].CrosshairId);
    }

    [Fact]
    public void PreviousWrapsAndSingleCrosshairRemainsSelected()
    {
        var store = Store(0);
        Assert.Equal("c0", CrosshairCycling.Apply(store, "c2", null, false));
        store.Crosshairs.RemoveRange(1, 2);
        Assert.Equal("c0", CrosshairCycling.Apply(store, "c0", null, true));
        Assert.Equal("c0", CrosshairCycling.Apply(store, "c0", null, false));
        store.Crosshairs.Clear();
        Assert.Null(CrosshairCycling.Apply(store, "c0", null, true));
    }
}
