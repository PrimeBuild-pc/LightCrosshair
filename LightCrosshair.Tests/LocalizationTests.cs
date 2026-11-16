using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Resources;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public class LocalizationTests
{
    [Theory]
    [InlineData("en")] [InlineData("it")] [InlineData("es")] [InlineData("zh-Hans")] [InlineData("ru")] [InlineData("ja")]
    public void EveryCoreLocaleContainsEveryKey(string language)
    {
        var resources = new ResourceManager("LightCrosshair.Resources.Strings", typeof(CoreStrings).Assembly);
        var baseline = resources.GetResourceSet(CultureInfo.InvariantCulture, true, true)!.Cast<DictionaryEntry>().Select(e => (string)e.Key).Order().ToArray();
        var translated = resources.GetResourceSet(CultureInfo.GetCultureInfo(language), true, false)!.Cast<DictionaryEntry>().Select(e => (string)e.Key).Order().ToArray();
        Assert.Equal(baseline, translated);
        foreach (string key in baseline) Assert.False(string.IsNullOrWhiteSpace(CoreStrings.Get(language, key, "Example")));
        Assert.Contains("Example", CoreStrings.Get(language, "SuggestGame", "Example"));
    }
    [Fact]
    public void CoreDoesNotReferenceWpf()
    {
        var references = typeof(Program).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.DoesNotContain("PresentationFramework", references); Assert.DoesNotContain("PresentationCore", references);
    }
}
