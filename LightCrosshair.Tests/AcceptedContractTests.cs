using System.IO;
using System.Threading.Tasks;
using LightCrosshair.Contracts;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public sealed class AcceptedContractTests
{
    [Theory]
    [InlineData(LayerKind.Box)] [InlineData(LayerKind.Ring)]
    public void FillSurvivesValidationAndSharingAndCompositesOnceBeneathStroke(LayerKind kind)
    {
        var renderer = new LayerRenderer(Program.DataRoot);
        var design = new CrosshairDesign { Name = "Fill", AntiAlias = false, Layers = [new() { Kind = kind, Length = 30, Thickness = 3, Gap = 0, Color = "#00FF00", FillColor = "#ff0000", Opacity = .5 }] };
        var decoded = ShareCode.Decode(ShareCode.Encode(design, Program.DataRoot), Program.DataRoot);
        Assert.Equal("#FF0000", decoded.Layers[0].FillColor);
        using var image = renderer.Render(decoded, 64);
        var center = image.GetPixel(32, 32);
        Assert.InRange(center.A, 126, 128); Assert.Equal(255, center.R); Assert.Equal(0, center.G);
        var edge = image.GetPixel(32, 20);
        Assert.InRange(edge.A, 126, 128); Assert.Equal(255, edge.G); Assert.Equal(0, edge.R);
        design.Layers[0].FillColor = "red";
        Assert.Throws<CoreException>(() => Validation.Design(design, Program.DataRoot));
        Assert.Throws<CoreException>(() => ShareCode.Encode(design, Program.DataRoot));
    }
    [Fact]
    public async Task DisabledLegacyDisplayColorsRemainEditableAndOnlyApplyWhenEnabled()
    {
        string legacy = Path.GetDirectoryName(Program.DataRoot)!;
        Directory.CreateDirectory(legacy);
        await File.WriteAllTextAsync(Path.Combine(legacy, "profiles.json"), """
            [{"Id":"colors","Name":"Colors","DisplayTargetProcessName":"game.exe","HasDisplayColorProfile":true,
              "DisplayEnableGammaOverride":false,"DisplayGammaValue":125,"DisplayContrastValue":110,"DisplayBrightnessValue":90,"DisplayVibranceValue":70}]
            """);
        var document = await new CoreStore(Program.DataRoot).LoadAsync(legacy);
        var game = Assert.Single(document.GameProfiles);
        Assert.False(game.DisplayColorsEnabled); Assert.Equal(125, game.DisplayColors!.Gamma);
        Assert.Equal(110, game.DisplayColors.Contrast); Assert.Equal(90, game.DisplayColors.Brightness); Assert.Equal(70, game.DisplayColors.Vibrance);
        Assert.Null(GameRules.Evaluate(document, "game.exe").DisplayColors);
        game.DisplayColorsEnabled = true;
        Assert.Same(game.DisplayColors, GameRules.Evaluate(document, "game.exe").DisplayColors);
        Assert.Null(GameRules.Evaluate(document, "other.exe").DisplayColors);
    }
    [Fact]
    public void CaptureLeaseRenewsExpiresAndClearsOnExplicitEndOrDisconnect()
    {
        long now = 0; var capture = new InputCapture(() => now);
        Assert.False(capture.Active); capture.Set(true);
        now = 59_999; Assert.True(capture.Active); capture.Set(true);
        now = 60_000; Assert.True(capture.Active);
        now = 119_999; Assert.False(capture.Active);
        capture.Set(true); capture.Set(false); Assert.False(capture.Active);
    }
}
