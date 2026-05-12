using LightCrosshair.GpuDriver;
using Xunit;

namespace LightCrosshair.Tests;

public class GpuDriverTests
{
    [Fact]
    public void NullGpuDriverService_Detect_ReturnsUnknown()
    {
        var service = new NullGpuDriverService();
        var result = service.Detect();
        Assert.Equal(GpuVendorKind.Unknown, result.Vendor);
        Assert.False(result.IsDriverApiAvailable);
    }

    [Fact]
    public void NullGpuDriverService_GetCapabilities_ReturnsAllUnsupported()
    {
        var service = new NullGpuDriverService();
        service.Detect(); // initialize
        var caps = service.GetCapabilities();
        Assert.Equal(GpuCapabilityStatus.Unsupported, caps.NvidiaFpsCap);
        Assert.Equal(GpuCapabilityStatus.Unsupported, caps.NvidiaColorVibrance);
        Assert.Equal(GpuCapabilityStatus.Unsupported, caps.AmdColorManagement);
        Assert.Equal(GpuCapabilityStatus.Unsupported, caps.AmdChill);
        Assert.Equal(GpuCapabilityStatus.Unsupported, caps.NvidiaGSync);
        Assert.Equal(GpuCapabilityStatus.Unsupported, caps.AmdFreeSync);
    }

    [Fact]
    public void NullGpuDriverService_TrySetNvidiaFpsCap_ReturnsFalse()
    {
        var service = new NullGpuDriverService();
        var result = service.TrySetNvidiaFpsCap(60, null, out string error);
        Assert.False(result);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void NullGpuDriverService_TryClearNvidiaFpsCap_ReturnsFalse()
    {
        var service = new NullGpuDriverService();
        var result = service.TryClearNvidiaFpsCap(null, out string error);
        Assert.False(result);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void NullGpuDriverService_TryGetNvidiaFpsCap_ReturnsFalse()
    {
        var service = new NullGpuDriverService();
        var result = service.TryGetNvidiaFpsCap(null, out int fps, out string status);
        Assert.False(result);
        Assert.Equal(0, fps);
        Assert.NotEmpty(status);
    }

    [Fact]
    public void NullGpuDriverService_TrySetNvidiaVibrance_ReturnsFalse()
    {
        var service = new NullGpuDriverService();
        var result = service.TrySetNvidiaVibrance(50, out string error);
        Assert.False(result);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void NullGpuDriverService_TryGetNvidiaVibrance_ReturnsFalse()
    {
        var service = new NullGpuDriverService();
        var result = service.TryGetNvidiaVibrance(out int vibrance, out string status);
        Assert.False(result);
        Assert.Equal(0, vibrance);
        Assert.NotEmpty(status);
    }

    [Fact]
    public void NullGpuDriverService_TryGetAmdChillStatus_ReturnsFalse()
    {
        var service = new NullGpuDriverService();
        var result = service.TryGetAmdChillStatus(out bool enabled, out int min, out int max, out string status);
        Assert.False(result);
        Assert.False(enabled);
        Assert.NotEmpty(status);
    }

    [Fact]
    public void NullGpuDriverService_TrySetAmdChill_ReturnsFalse()
    {
        var service = new NullGpuDriverService();
        var result = service.TrySetAmdChill(true, 30, 60, out string error);
        Assert.False(result);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void NullGpuDriverService_TryGetAmdFreeSyncStatus_ReturnsFalse()
    {
        var service = new NullGpuDriverService();
        var result = service.TryGetAmdFreeSyncStatus(out bool supported, out bool enabled, out string status);
        Assert.False(result);
        Assert.False(supported);
        Assert.False(enabled);
        Assert.NotEmpty(status);
    }

    [Fact]
    public void GpuCapabilities_None_ReturnsAllUnsupported()
    {
        var caps = GpuCapabilities.None();
        Assert.Equal(GpuCapabilityStatus.Unsupported, caps.NvidiaFpsCap);
        Assert.Equal(GpuCapabilityStatus.Unsupported, caps.NvidiaColorVibrance);
        Assert.Equal(GpuCapabilityStatus.Unsupported, caps.AmdColorManagement);
        Assert.Equal(GpuCapabilityStatus.Unsupported, caps.AmdChill);
        Assert.Equal(GpuCapabilityStatus.Unsupported, caps.NvidiaGSync);
        Assert.Equal(GpuCapabilityStatus.Unsupported, caps.AmdFreeSync);
    }

    [Fact]
    public void GpuDetectionResult_Unknown_ReturnsUnknownVendor()
    {
        // Use fully-qualified name to avoid ambiguity with internal
        // LightCrosshair.GpuDetectionResult in GpuColorManagement.cs
        var result = LightCrosshair.GpuDriver.GpuDetectionResult.Unknown();
        Assert.Equal(GpuVendorKind.Unknown, result.Vendor);
        Assert.False(result.IsDriverApiAvailable);
        Assert.Equal(GpuCapabilityStatus.Unsupported, result.Capabilities.NvidiaFpsCap);
    }

    [Fact]
    public void GpuDriverServiceFactory_Create_DoesNotThrow()
    {
        // Factory must not throw even on systems without NVIDIA/AMD
        var ex = Record.Exception(() => GpuDriverServiceFactory.Create());
        Assert.Null(ex);
    }

    [Fact]
    public void GpuDriverServiceFactory_Create_ReturnsNonNull()
    {
        var service = GpuDriverServiceFactory.Create();
        Assert.NotNull(service);
    }

    [Fact]
    public void AmdDriverService_Detect_DoesNotThrow()
    {
        var service = new AmdDriverService();
        var ex = Record.Exception(() => service.Detect());
        Assert.Null(ex);
    }

    [Fact]
    public void AmdDriverService_NvidiaFpsCap_ReturnsFalse()
    {
        var service = new AmdDriverService();
        service.Detect();
        var result = service.TrySetNvidiaFpsCap(60, null, out string error);
        Assert.False(result);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void AmdDriverService_AmdChill_ReturnsUnsupported()
    {
        var service = new AmdDriverService();
        service.Detect();
        var result = service.TryGetAmdChillStatus(out _, out _, out _, out string status);
        Assert.False(result);
        Assert.NotEmpty(status);
    }
}
