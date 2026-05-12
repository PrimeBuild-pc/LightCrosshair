using System.Diagnostics;

namespace LightCrosshair.GpuDriver
{
    /// <summary>
    /// Factory for creating the appropriate <see cref="IGpuDriverService"/> based on the detected GPU vendor.
    /// </summary>
    public static class GpuDriverServiceFactory
    {
        /// <summary>
        /// Detects the GPU vendor and returns the corresponding <see cref="IGpuDriverService"/>.
        /// Must not throw; returns <see cref="NullGpuDriverService"/> on any failure.
        /// </summary>
        public static IGpuDriverService Create()
        {
            try
            {
                GpuVendorKind vendor = GpuDetectionService.DetectVendor(out string adapterDescription);

                Debug.WriteLine(
                    $"[GpuDriverServiceFactory] Detected vendor: {vendor}, adapter: {adapterDescription}");

                return vendor switch
                {
                    GpuVendorKind.Nvidia => new NvidiaDriverService(),
                    GpuVendorKind.Amd => new NullGpuDriverService(),
                    _ => new NullGpuDriverService()
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GpuDriverServiceFactory] Detection failed: {ex.Message}");
                return new NullGpuDriverService();
            }
        }
    }
}
