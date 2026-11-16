using LightCrosshair.UI.Core;
using LightCrosshair.UI.Services;
using LightCrosshair.UI.ViewModels;
using Xunit;
namespace LightCrosshair.UI.Tests;
[Collection("UiSta")]
public class ColorsPreviewTests
{
    [Fact]
    public async Task PreviewDropsIntermediateValuesAndAlwaysSendsFinalBeforeEnding()
    {
        var core=new FakeCoreClient(); var preview=new DisplayColorsPreview(core); preview.Begin();
        for(int i=0;i<100;i++) preview.Update(new(){Gamma=50+i});
        await preview.EndAsync();
        Assert.InRange(core.ColorPreviews.Count,2,3); Assert.Null(core.ColorPreviews.Last());
        Assert.Equal(149,core.ColorPreviews[^2]!.Gamma);
        int count=core.ColorPreviews.Count; preview.Update(new(){Gamma=120}); await preview.EndAsync(); Assert.Equal(count,core.ColorPreviews.Count);
    }
    [Fact]
    public async Task SettingsPersistColorsAndGamesPreviewWithoutSavingTheirWorkingCopy()
    {
        var core=new FakeCoreClient(); var main=new MainViewModel(core,new FakeUi(),LaunchOptions.Parse(["--fake-core"])); await main.StartAsync();
        main.Settings.UseColors=true; main.Settings.BeginColorsCommand.Execute(null); main.Settings.Gamma=115;
        await main.Settings.EndColorsCommand.ExecuteAsync(null);
        Assert.True(core.Snapshot!.Store.Settings.DisplayColorsEnabled); Assert.Equal(115,core.Snapshot.Store.Settings.DisplayColors!.Gamma);
        main.Games.UseColors=true; main.Games.BeginColorsCommand.Execute(null); main.Games.Gamma=125;
        await main.Games.EndColorsCommand.ExecuteAsync(null);
        Assert.Null(core.ColorPreviews.Last()); Assert.Null(core.Snapshot.Store.GameProfiles[0].DisplayColors);
        await main.Games.SaveCommand.ExecuteAsync(null); Assert.Equal(125,core.Snapshot.Store.GameProfiles[0].DisplayColors!.Gamma);
        main.Settings.BeginColorsCommand.Execute(null); main.Settings.Gamma=116; main.Park();
        await main.Settings.EndColorsCommand.ExecuteAsync(null); Assert.Null(core.ColorPreviews.Last());
    }
}
