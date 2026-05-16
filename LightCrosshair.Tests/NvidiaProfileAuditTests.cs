using System;
using System.Linq;
using LightCrosshair.GpuDriver;
using Xunit;

namespace LightCrosshair.Tests;

public class NvidiaProfileAuditTests
{
    [Fact]
    public void Catalog_ContainsExpectedSettings()
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
            !setting.IsReadOnly);

        Assert.Contains(definitions, setting =>
            setting.SettingId == 0x0005F543u &&
            setting.DisplayName == "Low Latency CPL State" &&
            setting.IsReferenceOnly);

        Assert.Contains(definitions, setting =>
            setting.SettingId == 0x00A879CFu &&
            setting.DisplayName == "Vertical Sync" &&
            setting.UiHint == NvidiaProfileSettingUiHint.Dropdown &&
            !setting.IsReadOnly);

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
    public void Catalog_DoesNotExposeRawEditorHints()
    {
        Assert.DoesNotContain(NvidiaProfileSettingCatalog.All, setting => setting.UiHint == NvidiaProfileSettingUiHint.RawEditor);
    }

    [Fact]
    public void Catalog_DoesNotExposeDlssProfileSettings()
    {
        Assert.DoesNotContain(
            NvidiaProfileSettingCatalog.All,
            setting => setting.DisplayName.Contains("DLSS", StringComparison.OrdinalIgnoreCase) ||
                       setting.DisplayName.Contains("Frame Generation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WriteCatalog_ContainsOnlyLowLatencyAndVSync()
    {
        var writableIds = NvidiaProfileSettingWriteCatalog.All.Select(setting => setting.SettingId).ToArray();

        Assert.Equal(
            new[]
            {
                NvidiaProfileSettingCatalog.LowLatencyModeSettingId,
                NvidiaProfileSettingCatalog.VerticalSyncSettingId
            },
            writableIds);
    }

    [Fact]
    public void WriteCatalog_DoesNotExposeDlssOrRawWrites()
    {
        Assert.DoesNotContain(
            NvidiaProfileSettingWriteCatalog.All,
            setting => setting.DisplayName.Contains("DLSS", StringComparison.OrdinalIgnoreCase) ||
                       setting.DisplayName.Contains("Frame Generation", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            NvidiaProfileSettingCatalog.All,
            setting => !setting.IsReadOnly && setting.UiHint == NvidiaProfileSettingUiHint.RawEditor);
    }

    [Theory]
    [InlineData(NvidiaProfileSettingCatalog.GSyncApplicationModeSettingId)]
    [InlineData(NvidiaProfileSettingCatalog.GSyncApplicationStateSettingId)]
    [InlineData(NvidiaProfileSettingCatalog.GSyncApplicationRequestedStateSettingId)]
    public void GSyncCatalogEntries_AreReadOnlyReferenceOnly(uint settingId)
    {
        var definition = NvidiaProfileSettingCatalog.All.Single(setting => setting.SettingId == settingId);

        Assert.True(definition.IsReadOnly);
        Assert.True(definition.IsReferenceOnly);
    }

    [Theory]
    [InlineData(NvidiaProfileSettingCatalog.GSyncApplicationModeSettingId)]
    [InlineData(NvidiaProfileSettingCatalog.GSyncApplicationStateSettingId)]
    [InlineData(NvidiaProfileSettingCatalog.GSyncApplicationRequestedStateSettingId)]
    public void WriteCatalog_RejectsGSyncUntilMappingIsValidated(uint settingId)
    {
        Assert.False(NvidiaProfileSettingWriteCatalog.TryGet(settingId, out _));

        var result = NvidiaProfileSettingWriteCatalog.ValidateRequest(
            @"C:\Games\sample.exe",
            new NvidiaProfileSettingWriteRequest(settingId, 1u));

        Assert.False(result.IsValid);
        Assert.Equal(NvidiaProfileWriteStatus.NotAllowed, result.Status);
    }

    [Fact]
    public void WriteCatalog_ValidateRequest_RejectsBlankTarget()
    {
        var result = NvidiaProfileSettingWriteCatalog.ValidateRequest(
            "",
            new NvidiaProfileSettingWriteRequest(NvidiaProfileSettingCatalog.LowLatencyModeSettingId, 1u));

        Assert.False(result.IsValid);
        Assert.Equal(NvidiaProfileWriteStatus.InvalidTarget, result.Status);
        Assert.Null(result.Definition);
    }

    [Fact]
    public void WriteCatalog_ValidateRequest_RejectsUnknownSetting()
    {
        var result = NvidiaProfileSettingWriteCatalog.ValidateRequest(
            @"C:\Games\sample.exe",
            new NvidiaProfileSettingWriteRequest(0xDEADBEEFu, 1u));

        Assert.False(result.IsValid);
        Assert.Equal(NvidiaProfileWriteStatus.NotAllowed, result.Status);
        Assert.Null(result.Definition);
    }

    [Fact]
    public void WriteCatalog_ValidateRequest_RejectsUnknownValue()
    {
        var result = NvidiaProfileSettingWriteCatalog.ValidateRequest(
            @"C:\Games\sample.exe",
            new NvidiaProfileSettingWriteRequest(NvidiaProfileSettingCatalog.VerticalSyncSettingId, 0xDEADBEEFu));

        Assert.False(result.IsValid);
        Assert.Equal(NvidiaProfileWriteStatus.NotAllowed, result.Status);
        Assert.NotNull(result.Definition);
    }

    [Fact]
    public void WriteCatalog_ValidateRequest_AcceptsKnownTargetSettingAndValue()
    {
        var result = NvidiaProfileSettingWriteCatalog.ValidateRequest(
            @"C:\Games\sample.exe",
            new NvidiaProfileSettingWriteRequest(NvidiaProfileSettingCatalog.VerticalSyncSettingId, 0x60925292u));

        Assert.True(result.IsValid);
        Assert.Equal(NvidiaProfileWriteStatus.Success, result.Status);
        Assert.NotNull(result.Definition);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    public void WriteCatalog_AllowsOnlyLowLatencyOffOn(uint rawValue)
    {
        Assert.True(NvidiaProfileSettingWriteCatalog.CanWrite(
            NvidiaProfileSettingCatalog.LowLatencyModeSettingId,
            rawValue));
    }

    [Theory]
    [InlineData(2u)]
    [InlineData(0x0005F543u)]
    public void WriteCatalog_RejectsUnsupportedLowLatencyValues(uint rawValue)
    {
        Assert.False(NvidiaProfileSettingWriteCatalog.CanWrite(
            NvidiaProfileSettingCatalog.LowLatencyModeSettingId,
            rawValue));
    }

    [Fact]
    public void WriteCatalog_RejectsLowLatencyUltraUntilDrsMappingIsValidated()
    {
        var result = NvidiaProfileSettingWriteCatalog.ValidateRequest(
            @"C:\Games\sample.exe",
            new NvidiaProfileSettingWriteRequest(NvidiaProfileSettingCatalog.LowLatencyModeSettingId, 2u));

        Assert.False(result.IsValid);
        Assert.Equal(NvidiaProfileWriteStatus.NotAllowed, result.Status);
    }

    [Fact]
    public void LowLatencyCplState_ExposesUltraAsReadOnlyReference()
    {
        var setting = NvidiaProfileSettingCatalog.All.First(setting =>
            setting.SettingId == NvidiaProfileSettingCatalog.LowLatencyCplStateSettingId);

        Assert.True(setting.IsReadOnly);
        Assert.True(setting.IsReferenceOnly);
        Assert.Equal("Ultra", setting.KnownValues[2u]);
        Assert.Contains(
            "Ultra is visible from CPL State when present, but not writable until NVIDIA hardware validation confirms the required DRS write mapping.",
            setting.HelpText,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0x60925292u)]
    [InlineData(0x08416747u)]
    [InlineData(0x47814940u)]
    [InlineData(0x18888888u)]
    public void WriteCatalog_AllowsOnlySafeVSyncValues(uint rawValue)
    {
        Assert.True(NvidiaProfileSettingWriteCatalog.CanWrite(
            NvidiaProfileSettingCatalog.VerticalSyncSettingId,
            rawValue));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(0xDEADBEEFu)]
    public void WriteCatalog_RejectsUnsupportedVSyncValues(uint rawValue)
    {
        Assert.False(NvidiaProfileSettingWriteCatalog.CanWrite(
            NvidiaProfileSettingCatalog.VerticalSyncSettingId,
            rawValue));
    }

    [Theory]
    [InlineData(0x0005F543u)]
    [InlineData(0x1194F158u)]
    [InlineData(0x10A879CFu)]
    [InlineData(0x10A879ACu)]
    [InlineData(0xDEADBEEFu)]
    public void WriteCatalog_RejectsReferenceOnlyAndUnknownSettings(uint settingId)
    {
        Assert.False(NvidiaProfileSettingWriteCatalog.TryGet(settingId, out _));
        Assert.False(NvidiaProfileSettingWriteCatalog.CanWrite(settingId, 0u));
    }

    [Fact]
    public void AuditInvalidTarget_UsesInvalidTargetStatus()
    {
        var result = NvidiaProfileAuditResult.InvalidTarget("missing target");

        Assert.Equal(NvidiaProfileAuditStatus.InvalidTarget, result.Status);
        Assert.All(result.Settings, item => Assert.Equal(NvidiaProfileAuditStatus.InvalidTarget, item.Status));
    }

    [Fact]
    public void BackupModel_CapturesAbsentPreviousState()
    {
        var backup = new NvidiaProfileSettingBackup(
            @"C:\Games\sample.exe",
            "LightCrosshair_sample.exe",
            NvidiaProfileSettingCatalog.VerticalSyncSettingId,
            WasPresent: false,
            PreviousRawValue: null,
            WrittenRawValue: 0x08416747u,
            Timestamp: DateTimeOffset.UnixEpoch,
            LightCrosshairVersion: "1.4.0");

        Assert.False(backup.WasPresent);
        Assert.Null(backup.PreviousRawValue);
        Assert.Equal(0x08416747u, backup.WrittenRawValue);
    }

    [Fact]
    public void WriteResult_CanRepresentRestoreUnavailableWithoutNvapi()
    {
        var result = NvidiaProfileSettingWriteResult.FromStatus(
            NvidiaProfileWriteStatus.RestoreUnavailable,
            "No backup",
            @"C:\Games\sample.exe");

        Assert.False(result.Succeeded);
        Assert.Equal(NvidiaProfileWriteStatus.RestoreUnavailable, result.Status);
    }
}
