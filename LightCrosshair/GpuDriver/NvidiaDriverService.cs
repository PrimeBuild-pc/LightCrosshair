#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using LightCrosshair.GpuDriver;
using NvAPIWrapper;
using NvAPIWrapper.Display;
using NvAPIWrapper.DRS;
using NvAPIWrapper.Native;

namespace LightCrosshair.GpuDriver
{
    /// <summary>
    /// NVIDIA GPU driver integration using NvAPIWrapper.Net.
    /// Provides frame rate limiting via DRS and digital vibrance control.
    /// All methods are safe to call on systems without NVIDIA hardware (return false gracefully).
    /// </summary>
    public sealed class NvidiaDriverService : IGpuDriverService
    {
        private GpuDetectionResult _detectionResult = GpuDetectionResult.Unknown();
        private bool _initialized;

        /// <inheritdoc />
        public GpuDetectionResult Detect()
        {
            try
            {
                NVIDIA.Initialize();
                _initialized = true;

                GpuVendorKind vendor = GpuDetectionService.DetectVendor(out string adapterDescription);

                var capabilities = ProbeCapabilities();

                _detectionResult = new GpuDetectionResult
                {
                    Vendor = vendor,
                    AdapterDescription = adapterDescription,
                    IsDriverApiAvailable = true,
                    DriverApiStatusMessage = "NVIDIA driver API initialized successfully",
                    Capabilities = capabilities
                };

                return _detectionResult;
            }
            catch (Exception ex)
            {
                _initialized = false;
                Debug.WriteLine($"[NvidiaDriverService] NVIDIA initialization failed: {ex.Message}");
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
            if (!_initialized)
            {
                errorMessage = "NVIDIA driver API not available";
                return false;
            }

            if (targetFps < 0 || targetFps > 1000 || (targetFps > 0 && targetFps < 15))
            {
                errorMessage = "targetFps must be 0 (disable) or between 15 and 1000";
                return false;
            }

            if (string.IsNullOrWhiteSpace(applicationExePath))
            {
                errorMessage =
                    "Select a target application before applying an NVIDIA driver FPS cap. " +
                    "Global profile fallback is not available. Go to Display Settings and set a Target Process.";
                return false;
            }

            try
            {
                using var session = DriverSettingsSession.CreateAndLoad();

                var profile = ResolveOrCreateProfile(session, applicationExePath, out string? profileNote);
                if (profile == null)
                {
                    errorMessage = profileNote ?? "Could not resolve or create an application profile.";
                    return false;
                }

                errorMessage = string.Empty;

                profile.SetSetting(KnownSettingId.PerformanceStateFrameRateLimiter, (uint)targetFps);
                session.Save();

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryClearNvidiaFpsCap(string? applicationExePath, out string errorMessage)
        {
            if (!_initialized)
            {
                errorMessage = "NVIDIA driver API not available";
                return false;
            }

            if (string.IsNullOrWhiteSpace(applicationExePath))
            {
                errorMessage =
                    "Select a target application before clearing an NVIDIA driver FPS cap. " +
                    "Global profile fallback is not available. Go to Display Settings and set a Target Process.";
                return false;
            }

            try
            {
                using var session = DriverSettingsSession.CreateAndLoad();

                var profile = ResolveExistingProfile(session, applicationExePath, out string? profileNote);
                if (profile == null)
                {
                    errorMessage = profileNote ?? "No application profile found to clear.";
                    return false;
                }

                profile.SetSetting(KnownSettingId.PerformanceStateFrameRateLimiter, 0u);
                session.Save();

                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryGetNvidiaFpsCap(string? applicationExePath, out int currentFps, out string statusMessage)
        {
            currentFps = 0;

            if (!_initialized)
            {
                statusMessage = "NVIDIA driver API not available";
                return false;
            }

            if (string.IsNullOrWhiteSpace(applicationExePath))
            {
                statusMessage = "No target application configured. Set a Target Process in Display Settings to read an FPS cap.";
                return false;
            }

            try
            {
                using var session = DriverSettingsSession.CreateAndLoad();

                // Read-only: find existing profile, do NOT create one.
                var profile = ResolveExistingProfile(session, applicationExePath, out _);

                if (profile == null)
                {
                    currentFps = 0;
                    string exeName = Path.GetFileName(applicationExePath);
                    statusMessage = $"No FPS cap (no profile found for '{exeName}')";
                    return true;
                }

                var setting = profile.GetSetting(KnownSettingId.PerformanceStateFrameRateLimiter);
                currentFps = (int)(uint)setting.CurrentValue;
                statusMessage = currentFps == 0 ? "No cap" : $"{currentFps} FPS cap active";

                return true;
            }
            catch (Exception ex)
            {
                currentFps = 0;
                statusMessage = ex.Message;
                return false;
            }
        }

        /// <inheritdoc />
        public NvidiaProfileAuditResult AuditNvidiaProfileSettings(string? applicationExePath)
        {
            if (!_initialized)
            {
                return NvidiaProfileAuditResult.Unsupported("NVIDIA driver API not available");
            }

            if (string.IsNullOrWhiteSpace(applicationExePath))
            {
                return NvidiaProfileAuditResult.InvalidTarget("Select a target application before auditing NVIDIA profile settings.");
            }

            try
            {
                using var session = DriverSettingsSession.CreateAndLoad();
                var profile = ResolveExistingProfile(session, applicationExePath, out string? profileNote);

                if (profile == null)
                {
                    return NvidiaProfileAuditResult.FromStatus(
                        NvidiaProfileAuditStatus.NoProfile,
                        applicationExePath,
                        null,
                        profileNote ?? "No NVIDIA application profile found for the target application.");
                }

                var items = NvidiaProfileSettingCatalog.All
                    .Select(definition => ReadProfileSetting(profile, definition))
                    .ToArray();

                return new NvidiaProfileAuditResult(
                    NvidiaProfileAuditStatus.Present,
                    applicationExePath,
                    profile.Name,
                    items,
                    $"NVIDIA profile '{profile.Name}' audited.");
            }
            catch (Exception ex)
            {
                return NvidiaProfileAuditResult.FromStatus(
                    NvidiaProfileAuditStatus.Error,
                    applicationExePath,
                    null,
                    ex.Message);
            }
        }

        /// <inheritdoc />
        public bool TrySetNvidiaVibrance(int vibrance, out string errorMessage)
        {
            if (!_initialized)
            {
                errorMessage = "NVIDIA driver API not available";
                return false;
            }

            if (vibrance < 0 || vibrance > 100)
            {
                errorMessage = "Vibrance must be between 0 and 100";
                return false;
            }

            try
            {
                var displays = Display.GetDisplays();

                if (displays.Length == 0)
                {
                    errorMessage = "No NVIDIA displays found";
                    return false;
                }

                var dvcInfo = DisplayApi.GetDVCInfo(displays[0].Handle);
                int level = (int)(vibrance / 100.0 * (dvcInfo.MaximumLevel - dvcInfo.MinimumLevel) + dvcInfo.MinimumLevel);

                DisplayApi.SetDVCLevel(displays[0].Handle, level);

                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryGetNvidiaVibrance(out int vibrance, out string statusMessage)
        {
            vibrance = 0;

            if (!_initialized)
            {
                statusMessage = "NVIDIA driver API not available";
                return false;
            }

            try
            {
                var displays = Display.GetDisplays();

                if (displays.Length == 0)
                {
                    statusMessage = "No NVIDIA displays found";
                    return false;
                }

                var dvcInfo = DisplayApi.GetDVCInfo(displays[0].Handle);
                vibrance = (int)((dvcInfo.CurrentLevel - dvcInfo.MinimumLevel) / (double)(dvcInfo.MaximumLevel - dvcInfo.MinimumLevel) * 100);
                statusMessage = $"Vibrance: {vibrance}% (driver level: {dvcInfo.CurrentLevel})";

                return true;
            }
            catch (Exception ex)
            {
                vibrance = 0;
                statusMessage = ex.Message;
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryGetAmdChillStatus(out bool isEnabled, out int minFps, out int maxFps, out string statusMessage)
        {
            isEnabled = false;
            minFps = 0;
            maxFps = 0;
            statusMessage = "Not supported on NVIDIA hardware";
            return false;
        }

        /// <inheritdoc />
        public bool TrySetAmdChill(bool enable, int minFps, int maxFps, out string errorMessage)
        {
            errorMessage = "Not supported on NVIDIA hardware";
            return false;
        }

        /// <inheritdoc />
        public bool TryGetAmdFreeSyncStatus(out bool isSupported, out bool isEnabled, out string statusMessage)
        {
            isSupported = false;
            isEnabled = false;
            statusMessage = "Not supported on NVIDIA hardware";
            return false;
        }

        /// <summary>
        /// Probes available NVIDIA driver capabilities.
        /// </summary>
        private static GpuCapabilities ProbeCapabilities()
        {
            var fpsCapStatus = ProbeFpsCap();
            var vibranceStatus = ProbeVibrance();
            // G-Sync is not exposed through NvAPIWrapper.Net; mark as unsupported.
            // AMD features are unsupported on NVIDIA hardware.

            return new GpuCapabilities
            {
                NvidiaFpsCap = fpsCapStatus,
                NvidiaColorVibrance = vibranceStatus,
                AmdColorManagement = GpuCapabilityStatus.Unsupported,
                AmdChill = GpuCapabilityStatus.Unsupported,
                NvidiaGSync = GpuCapabilityStatus.Unsupported,
                AmdFreeSync = GpuCapabilityStatus.Unsupported
            };
        }

        /// <summary>
        /// Probes whether the DRS (Driver Settings) FPS cap API is accessible.
        /// </summary>
        private static GpuCapabilityStatus ProbeFpsCap()
        {
            try
            {
                using var session = DriverSettingsSession.CreateAndLoad();
                session.Dispose();
                return GpuCapabilityStatus.Supported;
            }
            catch
            {
                return GpuCapabilityStatus.Unavailable;
            }
        }

        /// <summary>
        /// Probes whether the digital vibrance API is accessible.
        /// </summary>
        private static GpuCapabilityStatus ProbeVibrance()
        {
            try
            {
                var displays = Display.GetDisplays();
                if (displays.Length == 0)
                    return GpuCapabilityStatus.Unavailable;

                var dvcInfo = DisplayApi.GetDVCInfo(displays[0].Handle);
                return GpuCapabilityStatus.Supported;
            }
            catch
            {
                return GpuCapabilityStatus.Unavailable;
            }
        }

        /// <summary>
        /// Resolves or creates a DRS application-specific profile for the given executable path.
        /// Does NOT fall back to global profile. Returns null if no profile can be found or created.
        /// </summary>
        private static DriverSettingsProfile? ResolveOrCreateProfile(
            DriverSettingsSession session,
            string applicationExePath,
            out string? errorMessage)
        {
            var profile = ResolveExistingProfile(session, applicationExePath, out errorMessage);
            if (profile != null)
                return profile;

            if (errorMessage != null)
                return null;

            string exeName = Path.GetFileName(applicationExePath);

            // Create a new profile for this application
            try
            {
                profile = DriverSettingsProfile.CreateProfile(session, "LightCrosshair_" + exeName, null);
                ProfileApplication.CreateApplication(profile, exeName, exeName, null, null, false, null);
                return profile;
            }
            catch (Exception ex)
            {
                errorMessage = $"Could not create and bind application profile for '{exeName}': {ex.Message}";
                return null;
            }
        }

        /// <summary>
        /// Resolves an existing DRS profile without creating or modifying driver state.
        /// </summary>
        private static DriverSettingsProfile? ResolveExistingProfile(
            DriverSettingsSession session,
            string applicationExePath,
            out string? errorMessage)
        {
            errorMessage = null;

            string exeName = Path.GetFileName(applicationExePath);
            if (string.IsNullOrWhiteSpace(exeName))
            {
                errorMessage = "Target application path does not contain an executable name.";
                return null;
            }

            var profile = session.FindApplicationProfile(applicationExePath);
            if (profile != null)
                return profile;

            return session.FindApplicationProfile(exeName);
        }

        private static NvidiaProfileSettingAuditItem ReadProfileSetting(
            DriverSettingsProfile profile,
            NvidiaProfileSettingDefinition definition)
        {
            try
            {
                var setting = profile.GetSetting(definition.SettingId);
                uint rawValue = (uint)setting.CurrentValue;

                return new NvidiaProfileSettingAuditItem(
                    definition,
                    NvidiaProfileAuditStatus.Present,
                    rawValue,
                    definition.FormatFriendlyValue(rawValue),
                    "Present in resolved application profile.");
            }
            catch
            {
                return new NvidiaProfileSettingAuditItem(
                    definition,
                    NvidiaProfileAuditStatus.NotPresent,
                    null,
                    string.Empty,
                    "Setting is not present in the resolved application profile.");
            }
        }
    }
}
