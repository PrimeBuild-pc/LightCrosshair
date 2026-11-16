using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using LightCrosshair.Core;
using Xunit;
using Xunit.Sdk;

[assembly: LightCrosshair.Tests.IsolatedData]

namespace LightCrosshair.Tests;

public sealed class IsolatedDataAttribute : BeforeAfterTestAttribute
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Also isolate discovery, constructors and any work outside a test's execution context.
        Environment.SetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT", NewRoot("run"));
    }
    private static string NewRoot(string name) => Path.Combine(Path.GetTempPath(), "lc2-tests", name + "-" + Guid.NewGuid().ToString("N"), "v2");
    public override void Before(MethodInfo methodUnderTest) => Program.ScopedDataRoot.Value = NewRoot(methodUnderTest.Name);
    public override void After(MethodInfo methodUnderTest)
    {
        string? root = Program.ScopedDataRoot.Value;
        Program.ScopedDataRoot.Value = null;
        if (root is not null && Directory.Exists(root)) Directory.Delete(root, true);
    }
}

public sealed class TestDataIsolationTests
{
    [Fact]
    public async Task DefaultLoggingAndStoreNeverUseTheProductionDataRoot()
    {
        string root = Program.DataRoot;
        Assert.StartsWith(Path.Combine(Path.GetTempPath(), "lc2-tests", nameof(DefaultLoggingAndStoreNeverUseTheProductionDataRoot)), root);
        Program.LogError(new InvalidOperationException("Isolation guard"), "Test guard");
        await new CoreStore(root).LoadAsync(Path.GetDirectoryName(root));
        Assert.Equal(root, Program.DataRoot);
        Assert.Contains("Isolation guard", await File.ReadAllTextAsync(Path.Combine(root, "logs", "core.log")));
        Assert.True(File.Exists(Path.Combine(root, "store.json")));
        Assert.DoesNotContain(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LightCrosshair"), root);
    }
}
