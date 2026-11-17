using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LightCrosshair.Core;
using LightCrosshair.Contracts;
using LightCrosshair.GpuDriver;
using Xunit;

namespace LightCrosshair.Tests;

public sealed class ColorRecoveryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "lc2-colors-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    [Fact]
    public async Task RefusedDriverApplyReportsUnsupportedOnceAndRollbackKeepsTheErrorUntilSuccessOrLeave()
    {
        var fake = new FakeColors { ApplySucceeds = false, ApplyError = "ADL_Display_Color_Set returned -3 (not supported)" };
        var session = new ColorSession(_directory, () => fake);
        var statuses = new System.Collections.Generic.List<string?>();
        session.StatusChanged += (_, code) => statuses.Add(code);
        await session.SetAsync(new() { Gamma = 115 });
        Assert.Equal(new[] { "unsupported" }, statuses);
        Assert.Equal(1, fake.Restores);
        Assert.False(File.Exists(Path.Combine(_directory, "colors-applied.json")));
        await session.SetAsync(new() { Gamma = 115 });
        string log = File.ReadAllText(Path.Combine(_directory, "logs", "core.log"));
        Assert.Contains(fake.ApplyError, log);
        Assert.Single(log.Split('\n'), line => line.Contains("Display color transaction"));
        Assert.Equal("unsupported", statuses.Last());
        fake.ApplySucceeds = true;
        await session.SetAsync(new()); Assert.Null(statuses.Last());
        fake.ApplySucceeds = false;
        await session.SetAsync(new()); Assert.Equal("unsupported", statuses.Last());
        await session.SetAsync(null); Assert.Null(statuses.Last());
    }
    [Fact]
    public async Task ObsoleteHardwareCompletionCannotPublishAnErrorIntoTheNextContext()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new FakeColors { ApplySucceeds = false, BeforeApply = () => { entered.SetResult(); release.Task.GetAwaiter().GetResult(); } };
        var session = new ColorSession(_directory, () => fake);
        var statuses = new System.Collections.Generic.List<string?>();
        session.StatusChanged += (_, code) => statuses.Add(code);
        Task apply = session.SetAsync(new()); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task leave = session.SetAsync(null); release.SetResult(); await Task.WhenAll(apply, leave);
        Assert.Single(statuses); Assert.Null(statuses[0]);
    }
    [Fact]
    public async Task RestoreFailurePreservesItsPhaseAndVendorMessage()
    {
        var fake = new FakeColors(); var session = new ColorSession(_directory, () => fake);
        string? status = null; session.StatusChanged += (_, code) => status = code;
        await session.SetAsync(new()); fake.RestoreSucceeds = false;
        await session.SetAsync(null);
        Assert.Equal("restore_failed", status);
        Assert.Contains("ADL restore returned -3", File.ReadAllText(Path.Combine(_directory, "logs", "core.log")));
        Assert.True(File.Exists(Path.Combine(_directory, "colors-applied.json")));
        fake.RestoreSucceeds = true; await session.SetAsync(null); Assert.Null(status);
    }
    [Fact]
    public void LifecycleLogRetainsTheOperationMessage()
    {
        Program.LogLifecycle("ADL baseline captured for 12 channels.", "AmdAdlColorManager");
        string log = File.ReadAllText(Path.Combine(Program.DataRoot, "logs", "core.log"));
        Assert.Contains("AmdAdlColorManager: ADL baseline captured for 12 channels.", log);
    }
    [Fact]
    public async Task BaselineIsDurableBeforeApplyAndNextProcessRestoresIt()
    {
        string marker = Path.Combine(_directory, "colors-applied.json");
        var first = new FakeColors { BeforeApply = () => Assert.True(File.Exists(marker)) };
        await new ColorSession(_directory, () => first).SetAsync(new() { Gamma = 120 });
        Assert.True(File.Exists(marker)); Assert.Equal(1, first.Applies);
        var afterCrash = new FakeColors();
        await new ColorSession(_directory, () => afterCrash).RecoverAsync();
        Assert.Equal(1, afterCrash.Restores); Assert.NotNull(afterCrash.Loaded?.Global);
        Assert.False(File.Exists(marker));
    }
    [Fact]
    public async Task FailedRestorationKeepsMarkerAndBlocksAnotherApply()
    {
        var fake = new FakeColors(); var session = new ColorSession(_directory, () => fake);
        await session.SetAsync(new()); fake.RestoreSucceeds = false;
        await session.SetAsync(null);
        await session.SetAsync(new() { Gamma = 140 });
        Assert.Equal(1, fake.Applies); Assert.True(File.Exists(Path.Combine(_directory, "colors-applied.json")));
        fake.RestoreSucceeds = true; await session.SetAsync(null);
        Assert.False(File.Exists(Path.Combine(_directory, "colors-applied.json")));
    }
    [Fact]
    public async Task VibranceRecoveryKeepsExactDriverUnitsInsteadOfRoundingToPercent()
    {
        var baseline = new NvidiaVibranceState("display", 543);
        NvidiaVibranceState? restored = null;
        NvidiaHelperResponse Helper(NvidiaHelperRequest request)
        {
            if (request.Operation == NvidiaHelperOperations.GetVibrance) return new() { Success = true, VibranceState = baseline };
            if (request.Operation == NvidiaHelperOperations.RestoreVibrance) restored = request.VibranceState;
            return new() { Success = true };
        }
        await new ColorSession(_directory, () => new FakeColors(), Helper).SetAsync(new() { NvidiaDigitalVibrance = 75 });
        await new ColorSession(_directory, () => new FakeColors(), Helper).RecoverAsync();
        Assert.Equal(baseline, restored);
        Assert.False(File.Exists(Path.Combine(_directory, "colors-applied.json")));
    }
    [Fact]
    public async Task LeavingAGameDiscardsQueuedStaleAppliesBehindASlowDriverCall()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new FakeColors { BeforeApply = () => { started.TrySetResult(); release.Task.GetAwaiter().GetResult(); } };
        var session = new ColorSession(_directory, () => fake);
        Task first = session.SetAsync(new() { Gamma = 110 });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task obsolete = session.SetAsync(new() { Gamma = 120 });
        Task leave = session.SetAsync(null);
        release.SetResult();
        await Task.WhenAll(first, obsolete, leave);
        Assert.Equal(1, fake.Applies); Assert.Equal(1, fake.Restores);
        Assert.False(File.Exists(Path.Combine(_directory, "colors-applied.json")));
    }
    [Fact]
    public async Task LiveChangesWriteOverTheKeptBaselineWithoutRestoringBetweenValues()
    {
        string marker = Path.Combine(_directory, "colors-applied.json");
        var fake = new FakeColors(); int created = 0;
        var session = new ColorSession(_directory, () => { created++; return fake; });
        foreach (int gamma in new[] { 110, 120, 130, 120 }) await session.SetAsync(new() { Gamma = gamma });
        Assert.Equal(4, fake.Applies); Assert.Equal(0, fake.Restores); Assert.Equal(1, created);
        Assert.Equal(130 - 10, fake.Last?.Gamma);
        Assert.True(File.Exists(marker));
        await session.SetAsync(null);
        Assert.Equal(1, fake.Restores); Assert.False(File.Exists(marker));
    }
    [Fact]
    public async Task DroppingNvidiaVibranceRestoresTheBaselineBeforeTheNextValues()
    {
        int vibranceRestores = 0;
        NvidiaHelperResponse Helper(NvidiaHelperRequest request)
        {
            if (request.Operation == NvidiaHelperOperations.GetVibrance) return new() { Success = true, VibranceState = new("display", 512) };
            if (request.Operation == NvidiaHelperOperations.RestoreVibrance) vibranceRestores++;
            return new() { Success = true };
        }
        var fake = new FakeColors(); var session = new ColorSession(_directory, () => fake, Helper);
        await session.SetAsync(new() { NvidiaDigitalVibrance = 70 });
        await session.SetAsync(new() { NvidiaDigitalVibrance = 80 });
        Assert.Equal(0, vibranceRestores);
        await session.SetAsync(new() { Gamma = 110 });
        Assert.Equal(1, vibranceRestores); Assert.Equal(1, fake.Restores);
    }
    private sealed class FakeColors : IGpuColorManager
    {
        public string BackendName => "Fake"; public GpuVendor Vendor => GpuVendor.Unknown;
        public string AdapterDescription => "Fake"; public bool IsVendorDriverPath => false; public string StatusMessage => "";
        public Action? BeforeApply; public bool RestoreSucceeds = true, ApplySucceeds = true; public string? ApplyError; public int Applies, Restores;
        public ColorRecoverySnapshot? Loaded;
        public bool TryCaptureOriginalState(out string? error) { error = null; return true; }
        public ColorAdjustment? Last;
        public bool TryApply(ColorAdjustment adjustment, out string? error) { ColorWriteGuard.BeforeWrite?.Invoke(); BeforeApply?.Invoke(); Applies++; Last = adjustment; error = ApplyError; return ApplySucceeds; }
        public bool TryRestore(out string? error) { Restores++; error = RestoreSucceeds ? null : "ADL restore returned -3"; return RestoreSucceeds; }
        public ColorRecoverySnapshot CaptureRecovery() => new() { Global = new() { Red = new ushort[256], Green = new ushort[256], Blue = new ushort[256] } };
        public void LoadRecovery(ColorRecoverySnapshot snapshot) => Loaded = snapshot;
    }
}
