using System.Xml.Linq;
using Xunit;

namespace LightCrosshair.UI.Tests;

public class LocalizationTests
{
    private static readonly string[] Languages = ["", "it", "es", "zh-Hans", "ru", "ja"];
    private static readonly string[] VisibleAttributes = ["Text", "Content", "Header", "Title", "ToolTip", "PlaceholderText"];

    [Fact]
    public void EveryLanguageHasEveryKeyAndNoEmptyValues()
    {
        var root = RepoRoot();
        var english = Keys(Path.Combine(root, "LightCrosshair.UI", "Resources", "Strings.resx"));
        Assert.NotEmpty(english);
        foreach (var language in Languages)
        {
            var file = language.Length == 0
                ? "Strings.resx"
                : "Strings." + language + ".resx";
            var keys = Keys(Path.Combine(root, "LightCrosshair.UI", "Resources", file));
            var missing = english.Keys.Except(keys.Keys).OrderBy(key => key).ToArray();
            var extra = keys.Keys.Except(english.Keys).OrderBy(key => key).ToArray();
            var empty = keys.Where(pair => string.IsNullOrWhiteSpace(pair.Value)).Select(pair => pair.Key).ToArray();
            Assert.True(missing.Length == 0, file + " missing " + string.Join(", ", missing));
            Assert.True(extra.Length == 0, file + " extra " + string.Join(", ", extra));
            Assert.True(empty.Length == 0, file + " empty " + string.Join(", ", empty));
        }
    }

    [Fact]
    public void XamlHasNoHardcodedUserVisibleStrings()
    {
        var root = RepoRoot();
        var files = Directory.GetFiles(Path.Combine(root, "LightCrosshair.UI"), "*.xaml", SearchOption.AllDirectories);
        var offenders = new List<string>();
        foreach (var file in files)
        {
            var doc = XDocument.Load(file);
            foreach (var element in doc.Descendants())
            {
                foreach (var attribute in element.Attributes())
                {
                    var name = attribute.Name.LocalName;
                    var visible = VisibleAttributes.Contains(name) || name.EndsWith("AutomationProperties.Name", StringComparison.Ordinal);
                    if (!visible || IsMarkup(attribute.Value)) continue;
                    offenders.Add(Path.GetFileName(file) + " " + name + "=\"" + attribute.Value + "\"");
                }
                foreach (var text in element.Nodes().OfType<XText>())
                {
                    var value = text.Value.Trim();
                    var parent = text.Parent?.Name.LocalName;
                    if (value.Length == 0 || IsMarkup(value) || value.StartsWith('#') || parent is "Color" or "FontFamily" or "Double")
                        continue;
                    offenders.Add(Path.GetFileName(file) + " text \"" + value + "\"");
                }
            }
        }
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    private static bool IsMarkup(string value)
    {
        var text = value.Trim();
        if (text.Length == 0 || text.StartsWith('{')) return true;
        return double.TryParse(text, out _);
    }

    private static Dictionary<string, string> Keys(string path)
    {
        var doc = XDocument.Load(path);
        return doc.Descendants("data")
            .Where(element => element.Attribute("type") == null)
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? "");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LightCrosshair.UI", "LightCrosshair.UI.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not find the UI project from " + AppContext.BaseDirectory);
    }
}
