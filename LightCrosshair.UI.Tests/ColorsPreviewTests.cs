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
        var watch=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<100;i++) preview.Update(new(){Gamma=50+i});
        await preview.EndAsync();
        Assert.True(watch.ElapsedMilliseconds >= 90); // colors and end requests share the rate limit
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
        // Games: the released value stays previewed (not saved) until Save, which then ends the preview.
        main.Games.UseColors=true; main.Games.Gamma=125; await Task.Delay(200);
        Assert.Equal(125,core.ColorPreviews.Last()!.Gamma); Assert.Null(core.Snapshot.Store.GameProfiles[0].DisplayColors);
        await main.Games.SaveCommand.ExecuteAsync(null); Assert.Equal(125,core.Snapshot.Store.GameProfiles[0].DisplayColors!.Gamma);
        Assert.Null(core.ColorPreviews.Last());
        main.Games.Gamma=130; await Task.Delay(200); Assert.Equal(130,core.ColorPreviews.Last()!.Gamma);
        await main.NavigateCommand.ExecuteAsync("hotkeys"); Assert.Null(core.ColorPreviews.Last());
        main.Settings.BeginColorsCommand.Execute(null); main.Settings.Gamma=116; main.Park();
        await main.Settings.EndColorsCommand.ExecuteAsync(null); Assert.Null(core.ColorPreviews.Last());
    }
}
