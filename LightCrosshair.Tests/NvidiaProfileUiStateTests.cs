using System;
using System.IO;
using System.Linq;
using LightCrosshair.GpuDriver;
using Xunit;

namespace LightCrosshair.Tests;

public class NvidiaProfileUiStateTests
{
    [Fact]
    public void InitialState_RequiresExplicitRefresh()
    {
        var state = NvidiaProfileUiState.Initial();

        Assert.False(state.HasAuditResult);
        Assert.False(state.CanApplyProfileSettings);
        Assert.Contains("click Refresh", state.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShouldAuditAutomatically_NeverAuditsOnOpenOrTabSelection()
    {
        Assert.False(NvidiaProfileUiState.ShouldAuditAutomatically(NvidiaProfileAuditTrigger.UiOpen));
        Assert.False(NvidiaProfileUiState.ShouldAuditAutomatically(NvidiaProfileAuditTrigger.GpuTabSelected));
    }

    [Fact]
    public void RunExplicitAudit_UnsupportedResult_ReturnsNonCrashingState()
    {
        var state = NvidiaProfileUiState.RunExplicitAudit(
            @"C:\Games\sample.exe",
            () => NvidiaProfileAuditResult.Unsupported("NVIDIA driver API not available"));

        Assert.True(state.HasAuditResult);
        Assert.False(state.CanApplyProfileSettings);
        Assert.Contains("Unsupported", state.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void RunExplicitAudit_AccessViolation_ReturnsConciseErrorState()
    {
        var state = NvidiaProfileUiState.RunExplicitAudit(
            @"C:\Games\sample.exe",
            () => throw new AccessViolationException("driver fault"));

        Assert.True(state.HasAuditResult);
        Assert.False(state.CanApplyProfileSettings);
        Assert.Contains("NVIDIA driver call failed", state.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettingsWindowConstructor_UsesPassiveGpuDriverInitialization()
    {
        string source = ReadRepoFile("LightCrosshair", "SettingsWindow.xaml.cs");
        string constructorStartup = Slice(
            source,
            "public SettingsWindow(IProfileService profiles)",
            "NvidiaProfileRefreshButton.Click");

        Assert.Contains("InitializeGpuDriverUiPassive();", constructorStartup, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeGpuDriverService();", constructorStartup, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshNvidiaProfileAudit();", constructorStartup, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindowGpuTabSelection_DoesNotRefreshNvidiaProfileAudit()
    {
        string source = ReadRepoFile("LightCrosshair", "SettingsWindow.xaml.cs");
        string selectionHandler = Slice(source, "SettingsTabControl.SelectionChanged", "NvidiaProfileRefreshButton.Click");

        Assert.Contains("GPU Driver tab selected", selectionHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshNvidiaProfileAudit();", selectionHandler, StringComparison.Ordinal);
        Assert.Contains("NvidiaProfileRefreshButton.Click += async (_, __) => await RefreshNvidiaProfileAudit();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericGpuRefresh_UsesSafeVendorDetectionOnly()
    {
        string source = ReadRepoFile("LightCrosshair", "SettingsWindow.xaml.cs");
        string refreshHandler = Slice(source, "private void GpuRefreshButton_Click()", "private async Task NvidiaFpsCapApplyButton_Click()");

        Assert.Contains("RefreshGpuDriverDetectionSafely();", refreshHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("GpuDriverServiceFactory.Create", refreshHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverSettingsSession", refreshHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("NVIDIA.Initialize", refreshHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("NvAPIWrapper", refreshHandler, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsWindow_DirectNvidiaActionsUseHelperClient()
    {
        string source = ReadRepoFile("LightCrosshair", "SettingsWindow.xaml.cs");
        string nvidiaActions = Slice(source, "private async Task NvidiaFpsCapApplyButton_Click()", "private void SetNvidiaProfileAuditInitialPrompt()");

        Assert.Contains("RunNvidiaHelperAsync", nvidiaActions, StringComparison.Ordinal);
        Assert.Contains("NvidiaHelperRequest.AuditProfile", nvidiaActions, StringComparison.Ordinal);
        Assert.Contains("NvidiaHelperRequest.ApplyProfileSetting", nvidiaActions, StringComparison.Ordinal);
        Assert.Contains("NvidiaHelperRequest.RestoreProfileSetting", nvidiaActions, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverSettingsSession", nvidiaActions, StringComparison.Ordinal);
        Assert.DoesNotContain("NVIDIA.Initialize", nvidiaActions, StringComparison.Ordinal);
        Assert.DoesNotContain("NvAPIWrapper", nvidiaActions, StringComparison.Ordinal);
    }

    [Fact]
    public void NvidiaProfileLayout_PutsStatusOnSecondaryRows()
    {
        string xaml = ReadRepoFile("LightCrosshair", "SettingsWindow.xaml");
        string profileGrid = Slice(xaml, "<Grid Margin=\"0,0,0,8\">", "</Grid>");

        Assert.DoesNotContain("Text=\"Status\" FontWeight=\"SemiBold\"", profileGrid, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"NvidiaProfileLowLatencyStatusText\" Grid.Row=\"4\"", profileGrid, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"NvidiaProfileVSyncStatusText\" Grid.Row=\"8\"", profileGrid, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", profileGrid, StringComparison.Ordinal);
        Assert.Contains("<ColumnDefinition Width=\"*\"/>", profileGrid, StringComparison.Ordinal);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing start marker: {startMarker}");
        Assert.True(end > start, $"Missing end marker: {endMarker}");
        return source[start..end];
    }

    private static string ReadRepoFile(params string[] relativeParts)
    {
        string root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(new[] { root }.Concat(relativeParts).ToArray()));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LightCrosshair.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find LightCrosshair repository root.");
    }
}
