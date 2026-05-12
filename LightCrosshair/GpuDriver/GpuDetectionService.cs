using LightCrosshair;

namespace LightCrosshair.GpuDriver
{
    /// <summary>
    /// Static service for GPU vendor detection using Win32 EnumDisplayDevices.
    /// Delegates to the existing <see cref="GpuVendorDetector"/> internally.
    /// </summary>
    public static class GpuDetectionService
    {
        /// <summary>
        /// Detects the primary GPU vendor using Win32 display device enumeration.
        /// Maps the result from the existing <see cref="GpuVendor"/> enum to <see cref="GpuVendorKind"/>.
        /// </summary>
        /// <param name="adapterDescription">Human-readable adapter description string.</param>
        /// <returns>The detected <see cref="GpuVendorKind"/>; returns <see cref="GpuVendorKind.Unknown"/> on any failure.</returns>
        public static GpuVendorKind DetectVendor(out string adapterDescription)
        {
            try
            {
                var result = GpuVendorDetector.DetectPrimary();
                adapterDescription = result.AdapterDescription;

                return result.Vendor switch
                {
                    GpuVendor.Nvidia => GpuVendorKind.Nvidia,
                    GpuVendor.Amd => GpuVendorKind.Amd,
                    GpuVendor.Intel => GpuVendorKind.Intel,
                    _ => GpuVendorKind.Unknown
                };
            }
            catch
            {
                adapterDescription = string.Empty;
                return GpuVendorKind.Unknown;
            }
        }
    }
}
