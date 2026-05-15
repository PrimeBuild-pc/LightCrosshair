using System.Linq;
using LightCrosshair.GpuDriver;
using Xunit;

namespace LightCrosshair.Tests;

public class NvidiaProfileAuditTests
{
    [Fact]
    public void Catalog_ContainsExpectedReadOnlySettings()
    {
        var definitions = NvidiaProfileSettingCatalog.All.ToArray();

        Assert.Contains(definitions, setting =>
            setting.SettingId == NvidiaProfileSettingCatalog.DriverFpsCapSettingId &&
            setting.DisplayName == "Driver FPS Cap" &&
            setting.UiHint == NvidiaProfileSettingUiHint.Number &&
            setting.IsReadOnly);

        Assert.Contains(definitions, setting =>
            setting.SettingId == 0x10835000u &&
            setting.DisplayName == "Low Latency Mode Enabled" &&
            setting.UiHint == NvidiaProfileSettingUiHint.Toggle &&
            setting.IsReadOnly);

        Assert.Contains(definitions, setting =>
            setting.SettingId == 0x0005F543u &&
            setting.DisplayName == "Low Latency CPL State" &&
            setting.IsReferenceOnly);

        Assert.Contains(definitions, setting =>
            setting.SettingId == 0x00A879CFu &&
            setting.DisplayName == "Vertical Sync" &&
            setting.UiHint == NvidiaProfileSettingUiHint.Dropdown &&
            setting.IsReadOnly);

        Assert.Contains(definitions, setting =>
            setting.SettingId == 0x1194F158u &&
            setting.DisplayName == "G-SYNC Application Mode" &&
            setting.UiHint == NvidiaProfileSettingUiHint.Dropdown &&
            setting.IsReadOnly);

        Assert.Contains(definitions, setting =>
            setting.SettingId == 0x10A879CFu &&
            setting.DisplayName == "G-SYNC Application State" &&
            setting.IsReferenceOnly &&
            setting.IsReadOnly);
    }

    [Theory]
    [InlineData(0x10835000u, 0u, "Off")]
    [InlineData(0x10835000u, 1u, "On")]
    [InlineData(0x0005F543u, 2u, "Ultra")]
    [InlineData(0x00A879CFu, 0x60925292u, "Application controlled")]
    [InlineData(0x00A879CFu, 0x08416747u, "Force off")]
    [InlineData(0x00A879CFu, 0x47814940u, "Force on")]
    [InlineData(0x00A879CFu, 0x18888888u, "Fast Sync")]
    [InlineData(0x1194F158u, 0u, "Off")]
    [InlineData(0x1194F158u, 1u, "Fullscreen only")]
    [InlineData(0x1194F158u, 2u, "Fullscreen and Windowed")]
    [InlineData(0x10A879CFu, 0u, "Allow")]
    [InlineData(0x10A879CFu, 1u, "Force Off")]
    [InlineData(0x10A879CFu, 2u, "Disallow")]
    [InlineData(0x10A879ACu, 0u, "Allow")]
    [InlineData(0x10A879ACu, 1u, "Force Off")]
    [InlineData(0x10A879ACu, 2u, "Disallow")]
    public void FormatFriendlyValue_KnownValues_ReturnsLabel(uint settingId, uint rawValue, string expected)
    {
        var definition = NvidiaProfileSettingCatalog.All.Single(setting => setting.SettingId == settingId);

        Assert.Equal(expected, definition.FormatFriendlyValue(rawValue));
    }

    [Fact]
    public void FormatFriendlyValue_UnknownValue_ReturnsHex()
    {
        var definition = NvidiaProfileSettingCatalog.All.Single(setting => setting.SettingId == 0x00A879CFu);

        Assert.Equal("0xDEADBEEF", definition.FormatFriendlyValue(0xDEADBEEFu));
    }

    [Fact]
    public void Catalog_DoesNotExposeWriteOrRawEditorHints()
    {
        Assert.DoesNotContain(NvidiaProfileSettingCatalog.All, setting => !setting.IsReadOnly);
        Assert.DoesNotContain(NvidiaProfileSettingCatalog.All, setting => setting.UiHint == NvidiaProfileSettingUiHint.RawEditor);
    }
}
