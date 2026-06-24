namespace LightCrosshair.GpuDriver
{
    public enum GpuVendorKind
    {
        Unknown,
        Nvidia,
        Amd,
        Intel
    }

    public enum GpuCapabilityStatus
    {
        Unsupported,
        Unavailable,
        ReadOnly,
        Supported
    }

    public readonly record struct GpuCapabilities
    {
        public GpuCapabilityStatus NvidiaFpsCap { get; init; }
        public GpuCapabilityStatus NvidiaColorVibrance { get; init; }
        public GpuCapabilityStatus AmdColorManagement { get; init; }
        public GpuCapabilityStatus AmdChill { get; init; }
        public GpuCapabilityStatus NvidiaGSync { get; init; }
        public GpuCapabilityStatus AmdFreeSync { get; init; }

        public static GpuCapabilities None() => new()
        {
            NvidiaFpsCap = GpuCapabilityStatus.Unsupported,
            NvidiaColorVibrance = GpuCapabilityStatus.Unsupported,
            AmdColorManagement = GpuCapabilityStatus.Unsupported,
            AmdChill = GpuCapabilityStatus.Unsupported,
            NvidiaGSync = GpuCapabilityStatus.Unsupported,
            AmdFreeSync = GpuCapabilityStatus.Unsupported
        };
    }

    public readonly record struct GpuDetectionResult
    {
        public GpuVendorKind Vendor { get; init; }
        public string AdapterDescription { get; init; }
        public bool IsDriverApiAvailable { get; init; }
        public string DriverApiStatusMessage { get; init; }
        public GpuCapabilities Capabilities { get; init; }

        public static GpuDetectionResult Unknown() => new()
        {
            Vendor = GpuVendorKind.Unknown,
            AdapterDescription = string.Empty,
            IsDriverApiAvailable = false,
            DriverApiStatusMessage = "GPU detection not performed or failed",
            Capabilities = GpuCapabilities.None()
        };
    }

    public interface IGpuDriverService
    {
        GpuDetectionResult Detect();
        GpuCapabilities GetCapabilities();
        bool TrySetNvidiaFpsCap(int targetFps, string? applicationExePath, out string errorMessage);
        bool TryClearNvidiaFpsCap(string? applicationExePath, out string errorMessage);
        bool TryGetNvidiaFpsCap(string? applicationExePath, out int currentFps, out string statusMessage);
        NvidiaProfileAuditResult AuditNvidiaProfileSettings(string? applicationExePath);
        NvidiaProfileSettingWriteResult ApplyNvidiaProfileSetting(
            string? applicationExePath,
            NvidiaProfileSettingWriteRequest request,
            string lightCrosshairVersion);
        NvidiaProfileSettingWriteResult RestoreNvidiaProfileSetting(string? applicationExePath, uint settingId);
        bool TrySetNvidiaVibrance(int vibrance, out string errorMessage);
        bool TryGetNvidiaVibrance(out int vibrance, out string statusMessage);
        bool TryGetAmdChillStatus(out bool isEnabled, out int minFps, out int maxFps, out string statusMessage);
        bool TrySetAmdChill(bool enable, int minFps, int maxFps, out string errorMessage);
        bool TryGetAmdFreeSyncStatus(out bool isSupported, out bool isEnabled, out string statusMessage);
    }

    public sealed class NullGpuDriverService : IGpuDriverService
    {
        public GpuDetectionResult Detect() => GpuDetectionResult.Unknown();
        public GpuCapabilities GetCapabilities() => GpuCapabilities.None();

        public bool TrySetNvidiaFpsCap(int targetFps, string? applicationExePath, out string errorMessage) =>
            Unsupported("NVIDIA driver API not available (NullGpuDriverService).", out errorMessage);

        public bool TryClearNvidiaFpsCap(string? applicationExePath, out string errorMessage) =>
            Unsupported("NVIDIA driver API not available (NullGpuDriverService).", out errorMessage);

        public bool TryGetNvidiaFpsCap(string? applicationExePath, out int currentFps, out string statusMessage)
        {
            currentFps = 0;
            return Unsupported("NVIDIA driver API not available (NullGpuDriverService).", out statusMessage);
        }

        public NvidiaProfileAuditResult AuditNvidiaProfileSettings(string? applicationExePath) =>
            NvidiaProfileAuditResult.Unsupported("NVIDIA driver API not available (NullGpuDriverService).");

        public NvidiaProfileSettingWriteResult ApplyNvidiaProfileSetting(
            string? applicationExePath,
            NvidiaProfileSettingWriteRequest request,
            string lightCrosshairVersion) =>
            NvidiaProfileSettingWriteResult.FromStatus(
                NvidiaProfileWriteStatus.Unsupported,
                "NVIDIA driver API not available (NullGpuDriverService).",
                applicationExePath ?? string.Empty);

        public NvidiaProfileSettingWriteResult RestoreNvidiaProfileSetting(string? applicationExePath, uint settingId) =>
            NvidiaProfileSettingWriteResult.FromStatus(
                NvidiaProfileWriteStatus.Unsupported,
                "NVIDIA driver API not available (NullGpuDriverService).",
                applicationExePath ?? string.Empty);

        public bool TrySetNvidiaVibrance(int vibrance, out string errorMessage) =>
            Unsupported("NVIDIA driver API not available (NullGpuDriverService).", out errorMessage);

        public bool TryGetNvidiaVibrance(out int vibrance, out string statusMessage)
        {
            vibrance = 0;
            return Unsupported("NVIDIA driver API not available (NullGpuDriverService).", out statusMessage);
        }

        public bool TryGetAmdChillStatus(out bool isEnabled, out int minFps, out int maxFps, out string statusMessage)
        {
            isEnabled = false;
            minFps = 0;
            maxFps = 0;
            return Unsupported("AMD driver API not available (NullGpuDriverService).", out statusMessage);
        }

        public bool TrySetAmdChill(bool enable, int minFps, int maxFps, out string errorMessage) =>
            Unsupported("AMD driver API not available (NullGpuDriverService).", out errorMessage);

        public bool TryGetAmdFreeSyncStatus(out bool isSupported, out bool isEnabled, out string statusMessage)
        {
            isSupported = false;
            isEnabled = false;
            return Unsupported("AMD driver API not available (NullGpuDriverService).", out statusMessage);
        }

        private static bool Unsupported(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
