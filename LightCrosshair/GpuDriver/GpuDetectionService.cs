using LightCrosshair;

namespace LightCrosshair.GpuDriver
{
    public static class GpuDetectionService
    {
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
