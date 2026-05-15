using System.Diagnostics;

namespace LightCrosshair.GpuDriver
{
    /// <summary>
    /// Identifies the GPU vendor for driver-level integration.
    /// </summary>
    public enum GpuVendorKind
    {
        Unknown,
        Nvidia,
        Amd,
        Intel
    }

    /// <summary>
    /// Describes the support level of a GPU driver feature.
    /// </summary>
    public enum GpuCapabilityStatus
    {
        /// <summary>Feature not available on this hardware/driver.</summary>
        Unsupported,

        /// <summary>Feature exists but API not accessible (driver not installed, etc.).</summary>
        Unavailable,

        /// <summary>Feature can be queried but not changed.</summary>
        ReadOnly,

        /// <summary>Feature fully supported (read + write).</summary>
        Supported
    }

    /// <summary>
    /// Describes which GPU driver features are available on the current system.
    /// </summary>
    public readonly record struct GpuCapabilities
    {
        /// <summary>NVIDIA DRS frame rate limiter.</summary>
        public GpuCapabilityStatus NvidiaFpsCap { get; init; }

        /// <summary>NVIDIA digital vibrance via driver.</summary>
        public GpuCapabilityStatus NvidiaColorVibrance { get; init; }

        /// <summary>AMD brightness/contrast/saturation/vibrance.</summary>
        public GpuCapabilityStatus AmdColorManagement { get; init; }

        /// <summary>AMD Radeon Chill.</summary>
        public GpuCapabilityStatus AmdChill { get; init; }

        /// <summary>NVIDIA G-Sync control.</summary>
        public GpuCapabilityStatus NvidiaGSync { get; init; }

        /// <summary>AMD FreeSync control.</summary>
        public GpuCapabilityStatus AmdFreeSync { get; init; }

        /// <summary>
        /// Returns a <see cref="GpuCapabilities"/> with all features set to <see cref="GpuCapabilityStatus.Unsupported"/>.
        /// </summary>
        public static GpuCapabilities None() =>
            new()
            {
                NvidiaFpsCap = GpuCapabilityStatus.Unsupported,
                NvidiaColorVibrance = GpuCapabilityStatus.Unsupported,
                AmdColorManagement = GpuCapabilityStatus.Unsupported,
                AmdChill = GpuCapabilityStatus.Unsupported,
                NvidiaGSync = GpuCapabilityStatus.Unsupported,
                AmdFreeSync = GpuCapabilityStatus.Unsupported
            };
    }

    /// <summary>
    /// Result of the GPU detection process.
    /// </summary>
    public readonly record struct GpuDetectionResult
    {
        /// <summary>Detected GPU vendor.</summary>
        public GpuVendorKind Vendor { get; init; }

        /// <summary>Human-readable adapter description (empty string if unknown).</summary>
        public string AdapterDescription { get; init; }

        /// <summary>Whether the vendor-specific driver API is accessible.</summary>
        public bool IsDriverApiAvailable { get; init; }

        /// <summary>Human-readable status message for the driver API availability.</summary>
        public string DriverApiStatusMessage { get; init; }

        /// <summary>Capabilities detected for this GPU.</summary>
        public GpuCapabilities Capabilities { get; init; }

        /// <summary>
        /// Returns a <see cref="GpuDetectionResult"/> representing an unknown/unavailable GPU.
        /// </summary>
        public static GpuDetectionResult Unknown() =>
            new()
            {
                Vendor = GpuVendorKind.Unknown,
                AdapterDescription = string.Empty,
                IsDriverApiAvailable = false,
                DriverApiStatusMessage = "GPU detection not performed or failed",
                Capabilities = GpuCapabilities.None()
            };
    }

    /// <summary>
    /// Service for GPU driver-level integration: detection, capability query, and feature control.
    /// </summary>
    public interface IGpuDriverService
    {
        /// <summary>
        /// Synchronous detection of the GPU vendor and driver capabilities.
        /// Must not throw; returns <see cref="GpuDetectionResult.Unknown"/> on failure.
        /// </summary>
        GpuDetectionResult Detect();

        /// <summary>
        /// Returns cached capabilities after <see cref="Detect"/> has been called.
        /// </summary>
        GpuCapabilities GetCapabilities();

        /// <summary>
        /// Attempts to set an NVIDIA driver-level FPS cap for the given application.
        /// </summary>
        /// <param name="targetFps">Target FPS value.</param>
        /// <param name="applicationExePath">Optional full path to the application executable.</param>
        /// <param name="errorMessage">Error message if the operation fails.</param>
        /// <returns><see langword="true"/> if the cap was applied; <see langword="false"/> if unsupported or failed.</returns>
        bool TrySetNvidiaFpsCap(int targetFps, string? applicationExePath, out string errorMessage);

        /// <summary>
        /// Attempts to clear the NVIDIA driver-level FPS cap for the given application.
        /// </summary>
        /// <param name="applicationExePath">Optional full path to the application executable.</param>
        /// <param name="errorMessage">Error message if the operation fails.</param>
        /// <returns><see langword="true"/> if the cap was cleared; <see langword="false"/> if unsupported or failed.</returns>
        bool TryClearNvidiaFpsCap(string? applicationExePath, out string errorMessage);

        /// <summary>
        /// Attempts to read the current NVIDIA driver-level FPS cap for the given application.
        /// </summary>
        /// <param name="applicationExePath">Optional full path to the application executable.</param>
        /// <param name="currentFps">Current FPS cap value, or 0 if no cap is set.</param>
        /// <param name="statusMessage">Status message describing the result.</param>
        /// <returns><see langword="true"/> if the cap was queried; <see langword="false"/> if unsupported or failed.</returns>
        bool TryGetNvidiaFpsCap(string? applicationExePath, out int currentFps, out string statusMessage);

        /// <summary>
        /// Reads allowlisted NVIDIA profile settings for a target application without creating or modifying profiles.
        /// </summary>
        /// <param name="applicationExePath">Full path to the target application executable.</param>
        /// <returns>Read-only audit result for allowlisted NVIDIA profile settings.</returns>
        NvidiaProfileAuditResult AuditNvidiaProfileSettings(string? applicationExePath);

        /// <summary>
        /// Attempts to set NVIDIA digital vibrance (0-100).
        /// </summary>
        /// <param name="vibrance">Vibrance value (0-100).</param>
        /// <param name="errorMessage">Error message if the operation fails.</param>
        /// <returns><see langword="true"/> if vibrance was set; <see langword="false"/> if unsupported or failed.</returns>
        bool TrySetNvidiaVibrance(int vibrance, out string errorMessage);

        /// <summary>
        /// Attempts to read the current NVIDIA digital vibrance value.
        /// </summary>
        /// <param name="vibrance">Current vibrance value (0-100).</param>
        /// <param name="statusMessage">Status message describing the result.</param>
        /// <returns><see langword="true"/> if vibrance was queried; <see langword="false"/> if unsupported or failed.</returns>
        bool TryGetNvidiaVibrance(out int vibrance, out string statusMessage);

        /// <summary>
        /// Attempts to read the current AMD Radeon Chill status.
        /// </summary>
        /// <param name="isEnabled">Whether Chill is currently enabled.</param>
        /// <param name="minFps">Minimum FPS configured for Chill.</param>
        /// <param name="maxFps">Maximum FPS configured for Chill.</param>
        /// <param name="statusMessage">Status message describing the result.</param>
        /// <returns><see langword="true"/> if Chill status was queried; <see langword="false"/> if unsupported or failed.</returns>
        bool TryGetAmdChillStatus(out bool isEnabled, out int minFps, out int maxFps, out string statusMessage);

        /// <summary>
        /// Attempts to configure AMD Radeon Chill.
        /// </summary>
        /// <param name="enable">Whether to enable or disable Chill.</param>
        /// <param name="minFps">Minimum FPS for Chill.</param>
        /// <param name="maxFps">Maximum FPS for Chill.</param>
        /// <param name="errorMessage">Error message if the operation fails.</param>
        /// <returns><see langword="true"/> if Chill was configured; <see langword="false"/> if unsupported or failed.</returns>
        bool TrySetAmdChill(bool enable, int minFps, int maxFps, out string errorMessage);

        /// <summary>
        /// Attempts to read the current AMD FreeSync status.
        /// </summary>
        /// <param name="isSupported">Whether FreeSync is supported by the display.</param>
        /// <param name="isEnabled">Whether FreeSync is currently enabled.</param>
        /// <param name="statusMessage">Status message describing the result.</param>
        /// <returns><see langword="true"/> if FreeSync status was queried; <see langword="false"/> if unsupported or failed.</returns>
        bool TryGetAmdFreeSyncStatus(out bool isSupported, out bool isEnabled, out string statusMessage);
    }

    /// <summary>
    /// Null-object implementation of <see cref="IGpuDriverService"/>.
    /// All methods return false / Unknown / Unsupported. Never throws.
    /// </summary>
    public sealed class NullGpuDriverService : IGpuDriverService
    {
        /// <inheritdoc />
        public GpuDetectionResult Detect() => GpuDetectionResult.Unknown();

        /// <inheritdoc />
        public GpuCapabilities GetCapabilities() => GpuCapabilities.None();

        /// <inheritdoc />
        public bool TrySetNvidiaFpsCap(int targetFps, string? applicationExePath, out string errorMessage)
        {
            errorMessage = "NVIDIA driver API not available (NullGpuDriverService).";
            return false;
        }

        /// <inheritdoc />
        public bool TryClearNvidiaFpsCap(string? applicationExePath, out string errorMessage)
        {
            errorMessage = "NVIDIA driver API not available (NullGpuDriverService).";
            return false;
        }

        /// <inheritdoc />
        public bool TryGetNvidiaFpsCap(string? applicationExePath, out int currentFps, out string statusMessage)
        {
            currentFps = 0;
            statusMessage = "NVIDIA driver API not available (NullGpuDriverService).";
            return false;
        }

        /// <inheritdoc />
        public NvidiaProfileAuditResult AuditNvidiaProfileSettings(string? applicationExePath) =>
            NvidiaProfileAuditResult.Unsupported("NVIDIA driver API not available (NullGpuDriverService).");

        /// <inheritdoc />
        public bool TrySetNvidiaVibrance(int vibrance, out string errorMessage)
        {
            errorMessage = "NVIDIA driver API not available (NullGpuDriverService).";
            return false;
        }

        /// <inheritdoc />
        public bool TryGetNvidiaVibrance(out int vibrance, out string statusMessage)
        {
            vibrance = 0;
            statusMessage = "NVIDIA driver API not available (NullGpuDriverService).";
            return false;
        }

        /// <inheritdoc />
        public bool TryGetAmdChillStatus(out bool isEnabled, out int minFps, out int maxFps, out string statusMessage)
        {
            isEnabled = false;
            minFps = 0;
            maxFps = 0;
            statusMessage = "AMD driver API not available (NullGpuDriverService).";
            return false;
        }

        /// <inheritdoc />
        public bool TrySetAmdChill(bool enable, int minFps, int maxFps, out string errorMessage)
        {
            errorMessage = "AMD driver API not available (NullGpuDriverService).";
            return false;
        }

        /// <inheritdoc />
        public bool TryGetAmdFreeSyncStatus(out bool isSupported, out bool isEnabled, out string statusMessage)
        {
            isSupported = false;
            isEnabled = false;
            statusMessage = "AMD driver API not available (NullGpuDriverService).";
            return false;
        }
    }
}
