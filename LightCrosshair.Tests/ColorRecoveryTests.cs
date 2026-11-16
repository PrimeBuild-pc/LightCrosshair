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
    private sealed class FakeColors : IGpuColorManager
    {
        public string BackendName => "Fake"; public GpuVendor Vendor => GpuVendor.Unknown;
        public string AdapterDescription => "Fake"; public bool IsVendorDriverPath => false; public string StatusMessage => "";
        public Action? BeforeApply; public bool RestoreSucceeds = true; public int Applies, Restores;
        public ColorRecoverySnapshot? Loaded;
        public bool TryCaptureOriginalState(out string? error) { error = null; return true; }
        public bool TryApply(ColorAdjustment adjustment, out string? error) { ColorWriteGuard.BeforeWrite?.Invoke(); BeforeApply?.Invoke(); Applies++; error = null; return true; }
        public bool TryRestore(out string? error) { Restores++; error = null; return RestoreSucceeds; }
        public ColorRecoverySnapshot CaptureRecovery() => new() { Global = new() { Red = new ushort[256], Green = new ushort[256], Blue = new ushort[256] } };
        public void LoadRecovery(ColorRecoverySnapshot snapshot) => Loaded = snapshot;
    }
}
