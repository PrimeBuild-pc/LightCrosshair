using LightCrosshair.UI.Core;
using LightCrosshair.UI.Services;
using LightCrosshair.UI.ViewModels;
using Xunit;

namespace LightCrosshair.UI.Tests;

[Collection("UiSta")]
public class FinalRoundTests
{
    [Fact]
    public async Task WhileHeldHasItsOwnRowAndGamesKeepFollowOffAndOwnDistinct()
    {
        var core = new FakeCoreClient(new InlineMarshaler()); await core.ConnectAsync();
        var hotkeys = new HotkeysViewModel(core, new FakeUi()); hotkeys.Apply(core.Snapshot!);
        var row = hotkeys.Rows.Single(item => item.Action == "HoldVisibility");
        Assert.Equal("", row.Binding.Key); Assert.True(row.HoldOnly);
        Assert.DoesNotContain(row.MouseChoices, choice => HotkeyRules.IsWheel(choice.Key));
        await hotkeys.SetMouseCommand.ExecuteAsync(new MousePick { Action = "HoldVisibility", Key = "RightClick" });
        Assert.Equal("RightClick", core.Snapshot!.Store.Settings.Hotkeys.HoldVisibility.Key);
        Assert.Equal(BindingMode.HoldToHide, core.Snapshot.Store.Settings.Hotkeys.HoldVisibility.Mode);
        Assert.Equal("X", core.Snapshot.Store.Settings.Hotkeys.ToggleVisibility.Key);
        var games = new GamesViewModel(core, new FakeUi()); games.Apply(core.Snapshot);
        Assert.Equal("follow", games.HoldPolicy);
        games.HoldPolicy = "off"; await games.SaveCommand.ExecuteAsync(null);
        Assert.Equal("", core.Snapshot.Store.GameProfiles[0].HoldVisibility!.Key);
        games.HoldPolicy = "custom"; games.HoldMouseKey = "Mouse5"; games.HoldMode = BindingMode.HoldToShow;
        await games.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Mouse5", core.Snapshot.Store.GameProfiles[0].HoldVisibility!.Key);
        Assert.Equal(BindingMode.HoldToShow, core.Snapshot.Store.GameProfiles[0].HoldVisibility!.Mode);
        games.HoldPolicy = "follow"; await games.SaveCommand.ExecuteAsync(null);
        Assert.Null(core.Snapshot.Store.GameProfiles[0].HoldVisibility);
    }
    [Fact]
    public async Task PresetPopupUsesTheCurrentGameWhileExplicitDefaultLeavesThatGameAlone()
    {
        var core = new FakeCoreClient(new InlineMarshaler()); await core.ConnectAsync();
        string fallback = core.Snapshot!.Store.Settings.DefaultCrosshairId;
        var game = core.Snapshot.Store.GameProfiles[0]; core.SetActiveGameProfile(game.Id);
        var library = new LibraryViewModel(core, new FakeUi(), new StudioViewModel(core, new FakeUi()));
        core.SnapshotChanged += (_, snapshot) => library.Apply(snapshot); library.Apply(core.Snapshot!);
        await library.LoadPresetsAsync(); library.BeginUse(library.AllPresets[0], null);
        await library.ConfirmUseCommand.ExecuteAsync(null);
        string selected = core.Snapshot!.Runtime.ActiveCrosshairId;
        Assert.NotEqual(fallback, selected);
        Assert.Equal(selected, core.Snapshot.Store.GameProfiles[0].CrosshairId);
        Assert.Equal(fallback, core.Snapshot.Store.Settings.DefaultCrosshairId);
        Assert.Contains(game.Name, library.Mine.Single(item => item.Design.Id == selected).UsageText);
        var fallbackCard = library.Mine.Single(item => item.Design.Id == fallback);
        Assert.Equal(LightCrosshair.UI.Localization.Loc.Instance["Library_Default"], fallbackCard.UsageText);
        await library.SetDefaultForAllAppsCommand.ExecuteAsync(library.Mine.Single(item => item.Design.Id == selected));
        Assert.Equal(selected, core.Snapshot.Store.Settings.DefaultCrosshairId);
        Assert.Equal(selected, core.Snapshot.Store.GameProfiles[0].CrosshairId);
        core.SetActiveGameProfile(null); library.ApplyRuntime(core.Snapshot.Runtime);
        await library.ApplyDefaultCommand.ExecuteAsync(library.Mine.First(item => item.Design.Id != selected));
        Assert.NotEqual(selected, core.Snapshot.Store.Settings.DefaultCrosshairId);
        Assert.Equal(selected, core.Snapshot.Store.GameProfiles[0].CrosshairId);
    }

    [Fact]
    public async Task UseSavedCardChangesTheActiveGameAndPreservesTheDefault()
    {
        var core = new FakeCoreClient(new InlineMarshaler()); await core.ConnectAsync();
        var game = core.Snapshot!.Store.GameProfiles[0];
        string fallback = core.Snapshot.Store.Settings.DefaultCrosshairId;
        core.SetActiveGameProfile(game.Id);
        var library = new LibraryViewModel(core, new FakeUi(), new StudioViewModel(core, new FakeUi()));
        core.SnapshotChanged += (_, snapshot) => library.Apply(snapshot); library.Apply(core.Snapshot!);
        var card = library.Mine.First(item => item.Design.Id != fallback);
        await library.ApplyDefaultCommand.ExecuteAsync(card);
        Assert.Equal(card.Design.Id, core.Snapshot!.Store.GameProfiles[0].CrosshairId);
        Assert.Equal(card.Design.Id, core.Snapshot.Runtime.ActiveCrosshairId);
        Assert.Equal(fallback, core.Snapshot.Store.Settings.DefaultCrosshairId);
    }
}
