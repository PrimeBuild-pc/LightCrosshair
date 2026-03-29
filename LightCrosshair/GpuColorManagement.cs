using System;
using System.Runtime.InteropServices;

namespace LightCrosshair
{
    public enum GpuVendor
    {
        Unknown,
        Nvidia,
        Amd,
        Intel
    }

    public readonly record struct ColorAdjustment(int Gamma, int Contrast, int Brightness, int Vibrance);

    public sealed record ColorBackendInfo(
        string BackendName,
        GpuVendor Vendor,
        string AdapterDescription,
        bool IsVendorDriverPath,
        string StatusMessage);

    public interface IGpuColorManager
    {
        string BackendName { get; }
        GpuVendor Vendor { get; }
        string AdapterDescription { get; }
        bool IsVendorDriverPath { get; }
        string StatusMessage { get; }
        bool TryCaptureOriginalState(out string? error);
        bool TryApply(ColorAdjustment adjustment, out string? error);
        bool TryRestore(out string? error);
    }

    internal readonly record struct GpuDetectionResult(GpuVendor Vendor, string AdapterDescription);

    internal static class GpuVendorDetector
    {
        private const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAY_DEVICE
        {
            public int cb;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;

            public int StateFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        public static GpuDetectionResult DetectPrimary()
        {
            DISPLAY_DEVICE best = default;
            bool found = false;

            for (uint i = 0; i < 16; i++)
            {
                DISPLAY_DEVICE dd = new DISPLAY_DEVICE
                {
                    cb = Marshal.SizeOf<DISPLAY_DEVICE>()
                };

                if (!EnumDisplayDevices(null, i, ref dd, 0))
                {
                    break;
                }

                if ((dd.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0)
                {
                    continue;
                }

                best = dd;
                found = true;
                break;
            }

            if (!found)
            {
                return new GpuDetectionResult(GpuVendor.Unknown, "No attached adapter detected");
            }

            string combined = string.Join(" ", best.DeviceString ?? string.Empty, best.DeviceID ?? string.Empty);
            return new GpuDetectionResult(DetectVendor(combined), string.IsNullOrWhiteSpace(best.DeviceString) ? "Unknown adapter" : best.DeviceString);
        }

        private static GpuVendor DetectVendor(string source)
        {
            if (source.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GpuVendor.Nvidia;
            }

            if (source.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                source.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                source.IndexOf("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GpuVendor.Amd;
            }

            if (source.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GpuVendor.Intel;
            }

            return GpuVendor.Unknown;
        }
    }

    internal static class GpuColorManagerFactory
    {
        public static IGpuColorManager CreateDefault()
        {
            var detection = GpuVendorDetector.DetectPrimary();
            var win32 = new Win32GammaColorManager(detection);

            return detection.Vendor switch
            {
                GpuVendor.Nvidia => new NvidiaColorManager(detection, win32),
                GpuVendor.Amd => new AmdColorManager(detection, win32),
                _ => win32,
            };
        }
    }

    internal sealed class Win32GammaColorManager : IGpuColorManager
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct GammaRamp
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Red;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Green;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] Blue;
        }

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref GammaRamp lpRamp);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool GetDeviceGammaRamp(IntPtr hDC, out GammaRamp lpRamp);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateDC(string lpszDriver, string? lpszDevice, string? lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int RASTERCAPS = 38;
        private const int RC_GAMMA_RAMP = 0x00000100;
        private const int SM_REMOTESESSION = 0x1000;

        private readonly GpuDetectionResult _detection;
        private readonly object _sync = new();
        private GammaRamp _originalRamp;
        private bool _hasOriginalRamp;

        public Win32GammaColorManager(GpuDetectionResult detection)
        {
            _detection = detection;
        }

        public string BackendName => "Win32 Gamma Ramp";
        public GpuVendor Vendor => _detection.Vendor;
        public string AdapterDescription => _detection.AdapterDescription;
        public bool IsVendorDriverPath => false;
        public string StatusMessage => "Global display color path active (SetDeviceGammaRamp).";

        public bool TryCaptureOriginalState(out string? error)
        {
            lock (_sync)
            {
                if (_hasOriginalRamp)
                {
                    error = null;
                    return true;
                }

                IntPtr desktopDc = GetDC(IntPtr.Zero);
                if (TryCaptureOnDc(desktopDc, releaseWithUser32: true, releaseAfter: true, out error))
                {
                    return true;
                }

                IntPtr displayDc = CreateDC("DISPLAY", null, null, IntPtr.Zero);
                if (TryCaptureOnDc(displayDc, releaseWithUser32: false, releaseAfter: true, out var displayError))
                {
                    return true;
                }

                error = string.IsNullOrWhiteSpace(displayError)
                    ? error ?? "Failed to capture original gamma ramp."
                    : displayError;
                return false;
            }
        }

        public bool TryApply(ColorAdjustment adjustment, out string? error)
        {
            lock (_sync)
            {
                // Prefer desktop DC first, then fallback to a generic DISPLAY DC.
                IntPtr desktopDc = GetDC(IntPtr.Zero);
                if (TryApplyOnDc(desktopDc, releaseWithUser32: true, adjustment, out error))
                {
                    return true;
                }

                IntPtr displayDc = CreateDC("DISPLAY", null, null, IntPtr.Zero);
                if (TryApplyOnDc(displayDc, releaseWithUser32: false, adjustment, out var displayError))
                {
                    return true;
                }

                error = string.IsNullOrWhiteSpace(displayError)
                    ? error ?? "SetDeviceGammaRamp returned false on available display contexts."
                    : displayError;
                return false;
            }
        }

        private bool TryApplyOnDc(IntPtr dc, bool releaseWithUser32, ColorAdjustment adjustment, out string? error)
        {
            if (dc == IntPtr.Zero)
            {
                error = "Cannot access display device context.";
                return false;
            }

            try
            {
                int rasterCaps = GetDeviceCaps(dc, RASTERCAPS);
                bool hasGammaCap = (rasterCaps & RC_GAMMA_RAMP) != 0;
                bool isRemoteSession = GetSystemMetrics(SM_REMOTESESSION) != 0;
                if (!hasGammaCap)
                {
                    error = $"Display context does not expose gamma-ramp capability (RASTERCAPS=0x{rasterCaps:X}, remoteSession={isRemoteSession}).";
                    return false;
                }

                if (!_hasOriginalRamp)
                {
                    _ = TryCaptureOnDc(dc, releaseWithUser32: false, releaseAfter: false, out _);
                }

                var ramp = BuildRamp(adjustment);
                if (!SetDeviceGammaRamp(dc, ref ramp))
                {
                    int win32 = Marshal.GetLastWin32Error();
                    error = win32 == 0
                        ? $"SetDeviceGammaRamp returned false (RASTERCAPS=0x{rasterCaps:X}, remoteSession={isRemoteSession})."
                        : $"SetDeviceGammaRamp failed (Win32: {win32}, RASTERCAPS=0x{rasterCaps:X}, remoteSession={isRemoteSession}).";
                    return false;
                }

                error = null;
                return true;
            }
            finally
            {
                ReleaseDc(dc, releaseWithUser32);
            }
        }

        public bool TryRestore(out string? error)
        {
            lock (_sync)
            {
                if (!_hasOriginalRamp)
                {
                    error = null;
                    return true;
                }

                IntPtr desktopDc = GetDC(IntPtr.Zero);
                if (TryRestoreOnDc(desktopDc, releaseWithUser32: true, out error))
                {
                    return true;
                }

                IntPtr displayDc = CreateDC("DISPLAY", null, null, IntPtr.Zero);
                if (TryRestoreOnDc(displayDc, releaseWithUser32: false, out var displayError))
                {
                    return true;
                }

                error = string.IsNullOrWhiteSpace(displayError)
                    ? error ?? "Failed to restore original gamma ramp."
                    : displayError;
                return false;
            }
        }

        private bool TryCaptureOnDc(IntPtr dc, bool releaseWithUser32, bool releaseAfter, out string? error)
        {
            if (dc == IntPtr.Zero)
            {
                error = "Cannot access display device context while capturing baseline.";
                return false;
            }

            try
            {
                if (!GetDeviceGammaRamp(dc, out var original))
                {
                    int win32 = Marshal.GetLastWin32Error();
                    error = win32 == 0
                        ? "GetDeviceGammaRamp returned false while capturing baseline."
                        : $"GetDeviceGammaRamp failed while capturing baseline (Win32: {win32}).";
                    return false;
                }

                _originalRamp = original;
                _hasOriginalRamp = true;
                error = null;
                return true;
            }
            finally
            {
                if (releaseAfter)
                {
                    ReleaseDc(dc, releaseWithUser32);
                }
            }
        }

        private bool TryRestoreOnDc(IntPtr dc, bool releaseWithUser32, out string? error)
        {
            if (dc == IntPtr.Zero)
            {
                error = "Cannot access display device context while restoring.";
                return false;
            }

            try
            {
                var ramp = _originalRamp;
                if (!SetDeviceGammaRamp(dc, ref ramp))
                {
                    int win32 = Marshal.GetLastWin32Error();
                    error = win32 == 0
                        ? "Failed to restore original gamma ramp."
                        : $"Failed to restore original gamma ramp (Win32: {win32}).";
                    return false;
                }

                error = null;
                return true;
            }
            finally
            {
                ReleaseDc(dc, releaseWithUser32);
            }
        }

        private static void ReleaseDc(IntPtr dc, bool releaseWithUser32)
        {
            try
            {
                if (dc == IntPtr.Zero)
                {
                    return;
                }

                if (releaseWithUser32)
                {
                    _ = ReleaseDC(IntPtr.Zero, dc);
                }
                else
                {
                    _ = DeleteDC(dc);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        public static GammaRamp BuildRamp(ColorAdjustment adjustment)
        {
            double gammaPower = Math.Clamp(adjustment.Gamma / 100.0, 0.1, 3.0);
            double contrastScale = Math.Clamp(adjustment.Contrast / 100.0, 0.5, 1.5);
            double brightnessShift = Math.Clamp((adjustment.Brightness - 100) / 100.0, -0.5, 0.5);
            double vibranceNormalized = Math.Clamp((adjustment.Vibrance - 50) / 50.0, -1.0, 1.0);

            var ramp = new GammaRamp
            {
                Red = new ushort[256],
                Green = new ushort[256],
                Blue = new ushort[256],
            };

            for (int i = 0; i < 256; i++)
            {
                double n = i / 255.0;
                double gammaCorrected = Math.Pow(n, gammaPower);
                double luma = Math.Clamp((((gammaCorrected - 0.5) * contrastScale) + 0.5) + brightnessShift, 0.0, 1.0);

                // Neutral vibrance proxy: increase/decrease local color intensity without hue shift.
                double saturationScale = 1.0 + (vibranceNormalized * 0.35);
                double neutral = Math.Clamp(0.5 + ((luma - 0.5) * saturationScale), 0.0, 1.0);

                ramp.Red[i] = ToUShort(neutral);
                ramp.Green[i] = ToUShort(neutral);
                ramp.Blue[i] = ToUShort(neutral);
            }

            // Some drivers reject non-monotonic ramps.
            EnsureMonotonic(ramp.Red);
            EnsureMonotonic(ramp.Green);
            EnsureMonotonic(ramp.Blue);

            return ramp;
        }

        private static void EnsureMonotonic(ushort[] values)
        {
            if (values.Length == 0)
            {
                return;
            }

            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] < values[i - 1])
                {
                    values[i] = values[i - 1];
                }
            }

            values[^1] = ushort.MaxValue;
        }

        private static ushort ToUShort(double value)
        {
            int scaled = (int)Math.Round(value * ushort.MaxValue);
            return (ushort)Math.Clamp(scaled, 0, ushort.MaxValue);
        }
    }

    internal abstract class DelegatingGpuColorManager : IGpuColorManager
    {
        private readonly IGpuColorManager _fallback;

        protected DelegatingGpuColorManager(IGpuColorManager fallback)
        {
            _fallback = fallback;
        }

        public abstract string BackendName { get; }
        public abstract GpuVendor Vendor { get; }
        public abstract string AdapterDescription { get; }
        public abstract bool IsVendorDriverPath { get; }
        public abstract string StatusMessage { get; }

        protected IGpuColorManager Fallback => _fallback;

        public virtual bool TryCaptureOriginalState(out string? error) => _fallback.TryCaptureOriginalState(out error);
        public virtual bool TryApply(ColorAdjustment adjustment, out string? error) => _fallback.TryApply(adjustment, out error);
        public virtual bool TryRestore(out string? error) => _fallback.TryRestore(out error);
    }

    internal sealed class NvidiaColorManager : DelegatingGpuColorManager
    {
        [DllImport("nvapi64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NvAPI_Disp_SetGammaRamp(int displayId, ref Win32GammaColorManager.GammaRamp ramp);

        [DllImport("nvapi64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NvAPI_Initialize();

        private readonly GpuDetectionResult _detection;
        private readonly bool _nvapiRuntimeAvailable;
        private string _lastHdrState = "HDR state not sampled yet.";

        public NvidiaColorManager(GpuDetectionResult detection, IGpuColorManager fallback)
            : base(fallback)
        {
            _detection = detection;
            _nvapiRuntimeAvailable = NativeLibrary.TryLoad("nvapi64.dll", out nint handle);
            if (_nvapiRuntimeAvailable)
            {
                NativeLibrary.Free(handle);
                try { NvAPI_Initialize(); }
                catch (Exception ex)
                {
                    Program.LogDebug($"NvAPI_Initialize failed: {ex.Message}", nameof(NvidiaColorManager));
                }
            }
        }

        public override string BackendName => _nvapiRuntimeAvailable
            ? "NVIDIA (NVAPI Direct)"
            : "NVIDIA (Win32 fallback)";

        public override GpuVendor Vendor => _detection.Vendor;
        public override string AdapterDescription => _detection.AdapterDescription;
        public override bool IsVendorDriverPath => _nvapiRuntimeAvailable;
        public override string StatusMessage => _nvapiRuntimeAvailable
            ? $"NVAPI initialized. Direct Color Path active. {_lastHdrState}"
            : "NVAPI runtime not found; using global Win32 gamma ramp.";

        public override bool TryApply(ColorAdjustment adjustment, out string? error)
        {
            bool hdrActive = DisplayColorEnvironment.IsHdrActive(out _lastHdrState);
            if (hdrActive)
            {
                error = $"HDR is active. NVAPI color application is aborted to prevent HDR gamma corruption. {_lastHdrState}";
                return false;
            }

            if (_nvapiRuntimeAvailable)
            {
                try
                {
                    // Fallback to Desktop for ID 0 as typical usage
                    var ramp = Win32GammaColorManager.BuildRamp(adjustment);
                    int result = NvAPI_Disp_SetGammaRamp(0, ref ramp);
                    if (result == 0) // NVAPI_OK
                    {
                        error = null;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Program.LogDebug($"NVAPI SetGammaRamp failed: {ex.Message}", nameof(NvidiaColorManager));
                }
            }

            return base.TryApply(adjustment, out error);
        }
    }

    internal sealed class AmdColorManager : DelegatingGpuColorManager
    {
        private readonly GpuDetectionResult _detection;
        private readonly bool _adlxRuntimeAvailable;
        private readonly AmdAdlColorManager _adlColorManager;
        private string _lastRuntimeState = "Runtime not sampled yet.";
        private bool _lastAppliedViaDriverPath;
        private bool _win32FallbackPathTouched;

        public AmdColorManager(GpuDetectionResult detection, IGpuColorManager fallback)
            : base(fallback)
        {
            _detection = detection;
            _adlxRuntimeAvailable = IsAdlxRuntimeAvailable();
            _adlColorManager = new AmdAdlColorManager();
        }

        public override string BackendName => _adlxRuntimeAvailable
            ? (_lastAppliedViaDriverPath
                ? "AMD (driver color API active)"
                : "AMD (driver color API + Win32 fallback policy)")
            : "AMD (Win32 fallback)";

        public override GpuVendor Vendor => _detection.Vendor;
        public override string AdapterDescription => _detection.AdapterDescription;
        public override bool IsVendorDriverPath => _adlColorManager.IsAvailable;
        public override string StatusMessage => _adlxRuntimeAvailable
            ? $"AMD driver runtime detected. {_adlColorManager.AvailabilityMessage} {_lastRuntimeState}"
            : "AMD driver runtime not found; using global Win32 gamma ramp.";

        public override bool TryCaptureOriginalState(out string? error)
        {
            bool adlOk = _adlColorManager.TryCaptureOriginalState(out var adlError);
            bool win32Ok = Fallback.TryCaptureOriginalState(out var win32Error);

            if (adlOk || win32Ok)
            {
                error = null;
                return true;
            }

            error = string.IsNullOrWhiteSpace(adlError)
                ? win32Error
                : $"AMD baseline capture failed: {adlError} | Win32 baseline capture failed: {win32Error}";
            return false;
        }

        public override bool TryApply(ColorAdjustment adjustment, out string? error)
        {
            bool hdrActive = DisplayColorEnvironment.IsHdrActive(out string hdrDetails);
            _lastRuntimeState = hdrDetails;

            if (hdrActive)
            {
                error = $"HDR is active. AMD color application is aborted to prevent HDR gamma corruption. {hdrDetails}";
                return false;
            }

            string? adlError = null;
            bool adlCaptureOk = _adlColorManager.TryCaptureOriginalState(out var adlCaptureError);
            if (adlCaptureOk && _adlColorManager.TryApply(adjustment, out adlError))
            {
                _lastAppliedViaDriverPath = true;

                // ADL natively lacks Gamma handling via this API; it only sets brightness/contrast/saturation.
                // We MUST delegate the Gamma curve to the Win32 fall-back to actually apply the Gamma value.
                // We pass standard neutral values (100, 100, 50) so we don't multiply the already applied ADL values.
                var gammaOnlyAdjustment = new ColorAdjustment(adjustment.Gamma, 100, 100, 50);
                if (Fallback.TryApply(gammaOnlyAdjustment, out string? win32GammaError))
                {
                    _win32FallbackPathTouched = true;
                }
                else
                {
                    Program.LogDebug($"AMD ADL applied brightness/contrast/saturation, but the Win32 fallback for Gamma failed: {win32GammaError}", nameof(AmdColorManager));
                    error = $"AMD driver path OK, but Win32 Gamma fallback failed: {win32GammaError}";
                    return false;
                }

                error = null;
                return true;
            }

            if (!adlCaptureOk)
            {
                adlError = $"Driver baseline capture failed: {adlCaptureError}";
            }

            _lastAppliedViaDriverPath = false;

            if (Fallback.TryApply(adjustment, out var win32Error))
            {
                _win32FallbackPathTouched = true;
                error = null;
                return true;
            }

            error = string.IsNullOrWhiteSpace(adlError)
                ? win32Error
                : $"AMD driver path failed: {adlError} | Win32 fallback failed: {win32Error}";
            return false;
        }

        public override bool TryRestore(out string? error)
        {
            bool adlRestoreOk = true;
            bool win32RestoreOk = true;
            string? adlError = null;
            string? win32Error = null;
            bool hasAdlState = _adlColorManager.HasCapturedOriginalState;
            bool usedFallbackPath = _win32FallbackPathTouched;

            if (hasAdlState)
            {
                adlRestoreOk = _adlColorManager.TryRestore(out adlError);
            }

            if (usedFallbackPath)
            {
                win32RestoreOk = Fallback.TryRestore(out win32Error);
            }

            // One restore attempt should consume the fallback flag to avoid repeated stale failures.
            _win32FallbackPathTouched = false;

            if (adlRestoreOk)
            {
                error = null;
                return true;
            }

            if (!hasAdlState && usedFallbackPath && win32RestoreOk)
            {
                error = null;
                return true;
            }

            if (hasAdlState && !adlRestoreOk && !usedFallbackPath)
            {
                error = $"AMD restore failed: {adlError}";
                return false;
            }

            if (hasAdlState && !adlRestoreOk && usedFallbackPath && !win32RestoreOk)
            {
                error = $"AMD restore failed: {adlError} | Win32 restore failed: {win32Error}";
                return false;
            }

            if (hasAdlState && !adlRestoreOk && usedFallbackPath && win32RestoreOk)
            {
                // Keep app functional even if AMD restore is partially unsupported on this driver/output.
                error = null;
                return true;
            }

            error = string.IsNullOrWhiteSpace(win32Error)
                ? adlError
                : win32Error;
            return false;
        }

        private static bool IsAdlxRuntimeAvailable()
        {
            string[] candidates = { "amdadlx.dll", "atiadlxx.dll", "atiadlxy.dll" };
            foreach (var dll in candidates)
            {
                if (NativeLibrary.TryLoad(dll, out nint handle))
                {
                    NativeLibrary.Free(handle);
                    return true;
                }
            }

            return false;
        }
    }
}
