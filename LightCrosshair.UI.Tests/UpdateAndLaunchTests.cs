using LightCrosshair.UI.Services;
using Xunit;

namespace LightCrosshair.UI.Tests;

public class UpdateAndLaunchTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("V1.2.3-beta", "1.2.3")]
    [InlineData("1.2.3+build", "1.2.3")]
    [InlineData("1.2.3 extra", "1.2.3")]
    public void NormalizeStripsPrefixAndSuffix(string raw, string expected)
        => Assert.Equal(expected, UpdateVersionComparer.NormalizeVersionTag(raw));

    [Fact]
    public void CompareVersionsOrdersAndRejectsGarbage()
    {
        Assert.True(UpdateVersionComparer.CompareVersions("1.2.3-beta", "1.2.2") > 0);
        Assert.True(UpdateVersionComparer.CompareVersions("1.1.1", "1.1.2") < 0);
        Assert.Equal(0, UpdateVersionComparer.CompareVersions("v2.0.0", "2.0.0"));
        Assert.Null(UpdateVersionComparer.CompareVersions("latest", "1.0.0"));
        Assert.Null(UpdateVersionComparer.CompareVersions("1.0.0", ""));
    }

    [Fact]
    public void LaunchOptionsParsePageSuggestAndFlags()
    {
        var options = LaunchOptions.Parse(["--page", "games", "--suggest", "cs2.exe", "--fake-core", "--screenshots", "C:\\shots"]);
        Assert.Equal("games", options.Page);
        Assert.Equal("cs2.exe", options.Suggest);
        Assert.True(options.FakeCore);
        Assert.Equal("C:\\shots", options.ScreenshotsDirectory);

        var fallback = LaunchOptions.Parse(["--page", "nope"]);
        Assert.Equal("studio", fallback.Page);
        Assert.False(fallback.FakeCore);
    }
}
