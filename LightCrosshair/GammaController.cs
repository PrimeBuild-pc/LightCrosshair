using System;
using System.Windows.Forms;

namespace LightCrosshair
{
    public static class GammaController
    {
        private static readonly object _sync = new();
        private static IGpuColorManager _manager = GpuColorManagerFactory.CreateDefault();
        private static string _lastError = string.Empty;
        private static string _lastReportedPath = "";
        private static bool _hasCapturedOriginalState;
        private static DateTime _lastFailureLogUtc = DateTime.MinValue;

        public static ColorBackendInfo GetBackendInfo()
        {
            lock (_sync)
            {
                var status = string.IsNullOrWhiteSpace(_lastError)
                    ? _manager.StatusMessage
                    : $"{_manager.StatusMessage} Last error: {_lastError}";

                return new ColorBackendInfo(
                    _manager.BackendName,
                    _manager.Vendor,
                    _manager.AdapterDescription,
                    _manager.IsVendorDriverPath,
                    status);
            }
        }

        public static void RefreshProvider()
        {
            lock (_sync)
            {
                _manager = GpuColorManagerFactory.CreateDefault();
                _lastError = string.Empty;
                _hasCapturedOriginalState = false;
            }
        }

        public static void SaveOriginal()
        {
            lock (_sync)
            {
                if (_hasCapturedOriginalState)
                {
                    return;
                }

                if (!_manager.TryCaptureOriginalState(out var error))
                {
                    _lastError = error ?? "Failed to capture original display state.";
                    Program.LogDebug($"SaveOriginal failed: {_lastError}", nameof(GammaController));
                    return;
                }

                _hasCapturedOriginalState = true;
                Program.LogDebug("Captured original display state baseline.", nameof(GammaController));
            }
        }

        public static void RestoreOriginal()
        {
            var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (mainForm != null && mainForm.InvokeRequired)
            {
                mainForm.BeginInvoke(new Action(RestoreOriginalUI));
                return;
            }
            RestoreOriginalUI();
        }

        private static void RestoreOriginalUI()
        {
            lock (_sync)
            {
                if (!_manager.TryRestore(out var error) && !string.IsNullOrWhiteSpace(error))
                {
                    _lastError = error;
                    Program.LogDebug($"RestoreOriginal failed: {error}", nameof(GammaController));
                }
            }
        }

        public static bool SetGamma(int gamma, int contrast, int brightness = 100, int vibrance = 50)
        {
            var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (mainForm != null && mainForm.InvokeRequired)
            {
                bool result = false;
                mainForm.Invoke(new Action(() => result = SetGammaUI(gamma, contrast, brightness, vibrance)));
                return result;
            }

            return SetGammaUI(gamma, contrast, brightness, vibrance);
        }

        private static bool SetGammaUI(int gamma, int contrast, int brightness, int vibrance)
        {
            lock (_sync)
            {
                if (!_hasCapturedOriginalState)
                {
                    if (!_manager.TryCaptureOriginalState(out var captureError))
                    {
                        _lastError = captureError ?? "Cannot capture original display state before apply.";
                        Program.LogDebug($"SetGamma blocked: {_lastError}", nameof(GammaController));
                        ReportPathChange("baseline-capture-failed");
                        return false;
                    }

                    _hasCapturedOriginalState = true;
                }

                var adjustment = new ColorAdjustment(gamma, contrast, brightness, vibrance);
                if (!_manager.TryApply(adjustment, out var error))
                {
                    _lastError = error ?? "Failed to apply color adjustment.";
                    if (DateTime.UtcNow - _lastFailureLogUtc > TimeSpan.FromSeconds(1))
                    {
                        Program.LogDebug($"SetGamma failed: {_lastError}", nameof(GammaController));
                        _lastFailureLogUtc = DateTime.UtcNow;
                    }

                    ReportPathChange("hardware-failed");
                    return false;
                }

                _lastError = string.Empty;
                ReportPathChange("hardware-applied");
                return true;
            }
        }

        private static void ReportPathChange(string path)
        {
            if (string.Equals(_lastReportedPath, path, StringComparison.Ordinal))
            {
                return;
            }

            _lastReportedPath = path;
            var info = GetBackendInfo();
            Program.LogDebug($"Color path -> {path} | backend={info.BackendName} | status={info.StatusMessage}", nameof(GammaController));
        }
    }
}
