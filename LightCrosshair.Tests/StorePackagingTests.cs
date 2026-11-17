using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public class StorePackagingTests
{
    [Theory]
    [InlineData(0, false)] // Disabled
    [InlineData(1, false)] // DisabledByUser (Task Manager)
    [InlineData(2, true)]  // Enabled
    [InlineData(3, false)] // DisabledByPolicy
    [InlineData(4, true)]  // EnabledByPolicy
    public void StartupTaskStateMapsToTheSwitch(int state, bool on) => Assert.Equal(on, StartupRegistration.IsOn(state));

    [Theory]
    [InlineData(true, null, false)]  // ZIP/installer build: the stored switch stays the source of truth
    [InlineData(false, null, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]  // turned off in Task Manager
    [InlineData(false, true, true)]  // enabled by policy
    public void StoredSwitchFollowsTheRealStartupTask(bool stored, bool? actual, bool sync) =>
        Assert.Equal(sync, StartupRegistration.NeedsSync(stored, actual));

    [Fact]
    public async Task UnpackagedProcessHasNoStartupTask()
    {
        Assert.False(AppPackage.IsPackaged);
        Assert.Null(await StartupRegistration.PackagedStateAsync());
    }

    [Fact]
    public async Task StartupTaskAbiFailsCleanlyWithoutAPackage()
    {
        // Activation and the GetAsync vtable call run for real; Windows refuses because the test host has no package.
        var error = await Assert.ThrowsAnyAsync<Exception>(() => StartupTaskAbi.RunAsync(StartupRegistration.TaskId, StartupTaskAbi.Action.Read));
        Assert.True(error.HResult < 0, error.ToString());
    }

    [Fact]
    public void ManifestMatchesTheCode()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "LightCrosshair.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var manifest = XDocument.Load(Path.Combine(dir!.FullName, "packaging", "msix", "Package.appxmanifest"));
        var all = manifest.Descendants().ToList();
        var app = Assert.Single(all, e => e.Name.LocalName == "Application");
        Assert.Equal("LightCrosshair.exe", (string?)app.Attribute("Executable"));
        Assert.Equal("Windows.FullTrustApplication", (string?)app.Attribute("EntryPoint"));
        var task = Assert.Single(all, e => e.Name.LocalName == "StartupTask");
        Assert.Equal(StartupRegistration.TaskId, (string?)task.Attribute("TaskId"));
        Assert.Equal("false", (string?)task.Attribute("Enabled"));
        Assert.Equal("LightCrosshair.exe", (string?)task.Parent!.Attribute("Executable"));
        Assert.Contains(all, e => e.Name.LocalName == "Capability" && (string?)e.Attribute("Name") == "runFullTrust");
    }
}
