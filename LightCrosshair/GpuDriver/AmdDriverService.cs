#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LightCrosshair.GpuDriver;

namespace LightCrosshair.GpuDriver
{
    /// <summary>
    /// AMD GPU driver integration using ADL2 (ADL) for color management,
    /// with ADLX Chill and FreeSync stubbed as Unsupported.
    /// All methods are safe to call on systems without AMD hardware (return false gracefully).
    /// </summary>
    public sealed class AmdDriverService : IGpuDriverService
    {
        private GpuDetectionResult _detectionResult = GpuDetectionResult.Unknown();
        private bool _initialized;

        /// <inheritdoc />
        public GpuDetectionResult Detect()
        {
            try
            {
                GpuVendorKind vendor = GpuDetectionService.DetectVendor(out string adapterDescription);

                bool adlAvailable = ProbeAdlAvailability();

                var capabilities = BuildCapabilities(adlAvailable);

                _initialized = adlAvailable;

                _detectionResult = new GpuDetectionResult
                {
                    Vendor = vendor,
                    AdapterDescription = adapterDescription,
                    IsDriverApiAvailable = adlAvailable,
                    DriverApiStatusMessage = adlAvailable
                        ? "AMD ADL2 driver API available"
                        : "AMD ADL2 driver DLL not found",
                    Capabilities = capabilities
                };

                return _detectionResult;
            }
            catch (Exception ex)
            {
                _initialized = false;
                Debug.WriteLine($"[AmdDriverService] Detection failed: {ex.Message}");
                return _detectionResult = GpuDetectionResult.Unknown();
            }
        }

        /// <inheritdoc />
        public GpuCapabilities GetCapabilities()
        {
            return _detectionResult.Capabilities;
        }

        /// <inheritdoc />
        public bool TrySetNvidiaFpsCap(int targetFps, string? applicationExePath, out string errorMessage)
        {
            errorMessage = "Not supported on AMD hardware";
            return false;
        }

        /// <inheritdoc />
        public bool TryClearNvidiaFpsCap(string? applicationExePath, out string errorMessage)
        {
            errorMessage = "Not supported on AMD hardware";
            return false;
        }

        /// <inheritdoc />
        public bool TryGetNvidiaFpsCap(string? applicationExePath, out int currentFps, out string statusMessage)
        {
            currentFps = 0;
            statusMessage = "Not supported on AMD hardware";
            return false;
        }

        /// <inheritdoc />
        public bool TrySetNvidiaVibrance(int vibrance, out string errorMessage)
        {
            errorMessage = "Not supported on AMD hardware";
            return false;
        }

        /// <inheritdoc />
        public bool TryGetNvidiaVibrance(out int vibrance, out string statusMessage)
        {
            vibrance = 0;
            statusMessage = "Not supported on AMD hardware";
            return false;
        }

        /// <inheritdoc />
        public bool TryGetAmdChillStatus(out bool isEnabled, out int minFps, out int maxFps, out string statusMessage)
        {
            isEnabled = false;
            minFps = 0;
            maxFps = 0;
            statusMessage = "AMD Radeon Chill is not supported in this version. ADLX SDK integration requires a C++/CLI wrapper build step that is not yet bundled with LightCrosshair.";
            return false;
        }

        /// <inheritdoc />
        public bool TrySetAmdChill(bool enable, int minFps, int maxFps, out string errorMessage)
        {
            errorMessage = "AMD Radeon Chill is not supported in this version. ADLX SDK integration requires a C++/CLI wrapper build step that is not yet bundled with LightCrosshair.";
            return false;
        }

        /// <inheritdoc />
        public bool TryGetAmdFreeSyncStatus(out bool isSupported, out bool isEnabled, out string statusMessage)
        {
            isSupported = false;
            isEnabled = false;
            statusMessage = "AMD FreeSync status is not supported in this version. ADLX SDK integration requires a C++/CLI wrapper build step that is not yet bundled with LightCrosshair.";
            return false;
        }

        /// <summary>
        /// Probes whether the ADL2 driver DLL (atiadlxx.dll or atiadlxy.dll) is available.
        /// </summary>
        private static bool ProbeAdlAvailability()
        {
            try
            {
                IntPtr handle = NativeLibrary.Load("atiadlxx.dll");
                if (handle != IntPtr.Zero)
                {
                    NativeLibrary.Free(handle);
                    return true;
                }
            }
            catch
            {
                // Fall through to try the 32-bit fallback
            }

            try
            {
                IntPtr handle = NativeLibrary.Load("atiadlxy.dll");
                if (handle != IntPtr.Zero)
                {
                    NativeLibrary.Free(handle);
                    return true;
                }
            }
            catch
            {
                // ADL2 not available
            }

            return false;
        }

        /// <summary>
        /// Builds the <see cref="GpuCapabilities"/> based on ADL2 availability.
        /// AMD Chill and FreeSync are Unsupported (ADLX C++/CLI wrapper not bundled).
        /// All NVIDIA features are Unsupported on AMD hardware.
        /// </summary>
        private static GpuCapabilities BuildCapabilities(bool adlAvailable)
        {
            return new GpuCapabilities
            {
                NvidiaFpsCap = GpuCapabilityStatus.Unsupported,
                NvidiaColorVibrance = GpuCapabilityStatus.Unsupported,
                AmdColorManagement = adlAvailable
                    ? GpuCapabilityStatus.Supported
                    : GpuCapabilityStatus.Unavailable,
                AmdChill = GpuCapabilityStatus.Unsupported,
                NvidiaGSync = GpuCapabilityStatus.Unsupported,
                AmdFreeSync = GpuCapabilityStatus.Unsupported
            };
        }
    }
}
