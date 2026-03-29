using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LightCrosshair
{
    public static class DisplayManager
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint idEventThread,
            uint dwmsEventTime);

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        private static bool _isGammaApplied = false;
        private static CancellationTokenSource? _cts;
        private static readonly Debouncer _applyDebouncer = new(100);
        private static readonly object _hookSync = new();
        private static IntPtr _foregroundHookHandle;
        private static WinEventDelegate? _foregroundHookProc;
        private static DateTime _lastApplyLogUtc = DateTime.MinValue;
        private static DateTime _lastApplyAttemptUtc = DateTime.MinValue;
        private static DateTime _lastTargetDecisionLogUtc = DateTime.MinValue;
        private static string _lastTargetDecision = string.Empty;

        private static int _lastGammaValue = int.MinValue;
        private static int _lastContrastValue = int.MinValue;
        private static int _lastBrightnessValue = int.MinValue;
        private static int _lastVibranceValue = int.MinValue;

        public static ColorBackendInfo GetBackendInfo() => GammaController.GetBackendInfo();

        public static void StartMonitoring()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            EnsureForegroundHook();

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        CheckForegroundAndApply();
                    }
                    catch (Exception ex)
                    {
                        Program.LogError(ex, "DisplayManager.StartMonitoring loop");
                    }
                    await Task.Delay(1000, token); // Poll every 1 second
                }
            }, token);
        }

        public static void StopMonitoring()
        {
            _cts?.Cancel();
            ReleaseForegroundHook();
            GammaController.RestoreOriginal();
            _isGammaApplied = false;
        }
        
        public static void ForceApplyNow()
        {
            ApplyTargetGamma();
        }

        public static void RequestApplyDebounced()
        {
            _applyDebouncer.Trigger(() =>
            {
                try
                {
                    CheckForegroundAndApply(forceUpdate: true);
                }
                catch
                {
                    // Best-effort deferred apply.
                }
            });
        }

        public static string? TryGetForegroundProcessName()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
            {
                return null;
            }

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0)
            {
                return null;
            }

            try
            {
                using var proc = Process.GetProcessById((int)pid);
                if (string.IsNullOrWhiteSpace(proc.ProcessName))
                {
                    return null;
                }

                return proc.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? proc.ProcessName
                    : $"{proc.ProcessName}.exe";
            }
            catch
            {
                return null;
            }
        }

        public static void CheckForegroundAndApply(bool forceUpdate = false)
        {
            var cfg = CrosshairConfig.Instance;
            if (!cfg.EnableGammaOverride)
            {
                if (_isGammaApplied)
                {
                    GammaController.RestoreOriginal();
                    _isGammaApplied = false;
                }
                return;
            }

            // If no specific process is set, we apply it globally
            if (string.IsNullOrWhiteSpace(cfg.TargetProcessName))
            {
                if (!_isGammaApplied || forceUpdate)
                {
                    ApplyTargetGamma();
                }
                return;
            }

            // Try to match foreground window
            var active = TryGetForegroundProcessName();
            if (!string.IsNullOrWhiteSpace(active))
            {
                try
                {
                    var activeName = NormalizeProcessName(active);
                    activeName = activeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? activeName[..^4]
                        : activeName;

                    var targetName = NormalizeProcessName(cfg.TargetProcessName);
                    targetName = targetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? targetName[..^4]
                        : targetName;

                    bool isMatch = string.Equals(activeName, targetName, StringComparison.OrdinalIgnoreCase);
                    bool isAppItself = string.Equals(activeName, "LightCrosshair", StringComparison.OrdinalIgnoreCase);
                    LogTargetDecision(targetName, activeName, isMatch, forceUpdate);

                    if (isMatch || isAppItself)
                    {
                        if (!_isGammaApplied || forceUpdate)
                            ApplyTargetGamma();
                    }
                    else if (!isMatch && !isAppItself && _isGammaApplied)
                    {
                        GammaController.RestoreOriginal();
                        _isGammaApplied = false;
                    }
                }
                catch
                {
                    // Foreground process not available at this moment.
                }
            }
            else if (_isGammaApplied)
            {
                // Fail-safe: if foreground cannot be resolved while in target mode, restore.
                LogTargetDecision(NormalizeProcessName(cfg.TargetProcessName), "<unresolved>", isMatch: false, forceUpdate);
                GammaController.RestoreOriginal();
                _isGammaApplied = false;
            }
        }

        private static void LogTargetDecision(string targetName, string activeName, bool isMatch, bool forceUpdate)
        {
            string decision = $"target={targetName};active={activeName};match={isMatch}";
            var now = DateTime.UtcNow;
            bool shouldLog = forceUpdate
                || !string.Equals(_lastTargetDecision, decision, StringComparison.Ordinal)
                || now - _lastTargetDecisionLogUtc > TimeSpan.FromSeconds(8);

            if (!shouldLog)
            {
                return;
            }

            Program.LogDebug($"Target process check -> target={targetName} active={activeName} match={isMatch}", nameof(DisplayManager));
            _lastTargetDecision = decision;
            _lastTargetDecisionLogUtc = now;
        }

        private static string NormalizeProcessName(string value)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            if (trimmed.Contains('\\') || trimmed.Contains('/'))
            {
                trimmed = Path.GetFileName(trimmed);
            }

            if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return trimmed.Contains('.') ? trimmed : $"{trimmed}.exe";
        }

        private static void ApplyTargetGamma()
        {
            var cfg = CrosshairConfig.Instance;

            bool sameAsLast =
                cfg.GammaValue == _lastGammaValue &&
                cfg.ContrastValue == _lastContrastValue &&
                cfg.BrightnessValue == _lastBrightnessValue &&
                cfg.VibranceValue == _lastVibranceValue;

            // Avoid flooding expensive GPU APIs while sliders are moving quickly.
            if (sameAsLast && DateTime.UtcNow - _lastApplyAttemptUtc < TimeSpan.FromMilliseconds(120))
            {
                return;
            }

            _lastApplyAttemptUtc = DateTime.UtcNow;

            bool applied = GammaController.SetGamma(cfg.GammaValue, cfg.ContrastValue, cfg.BrightnessValue, cfg.VibranceValue);

            _lastGammaValue = cfg.GammaValue;
            _lastContrastValue = cfg.ContrastValue;
            _lastBrightnessValue = cfg.BrightnessValue;
            _lastVibranceValue = cfg.VibranceValue;

            if (DateTime.UtcNow - _lastApplyLogUtc > TimeSpan.FromSeconds(2) || applied != _isGammaApplied)
            {
                var info = GammaController.GetBackendInfo();
                Program.LogDebug($"Display apply -> gamma={cfg.GammaValue} contrast={cfg.ContrastValue} brightness={cfg.BrightnessValue} vibrance={cfg.VibranceValue} target={cfg.TargetProcessName} applied={applied} backend={info.BackendName}", nameof(DisplayManager));
                _lastApplyLogUtc = DateTime.UtcNow;
            }

            _isGammaApplied = applied;
        }
        
        public static void CancelCurrentGamma()
        {
            _isGammaApplied = false;
        }

        private static void EnsureForegroundHook()
        {
            lock (_hookSync)
            {
                if (_foregroundHookHandle != IntPtr.Zero)
                {
                    return;
                }

                _foregroundHookProc = OnForegroundWindowChanged;
                _foregroundHookHandle = SetWinEventHook(
                    EVENT_SYSTEM_FOREGROUND,
                    EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero,
                    _foregroundHookProc,
                    0,
                    0,
                    WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

                if (_foregroundHookHandle == IntPtr.Zero)
                {
                    Program.LogDebug("Foreground hook install failed; monitoring continues with polling.", nameof(DisplayManager));
                    _foregroundHookProc = null;
                    return;
                }

                Program.LogDebug("Foreground hook installed.", nameof(DisplayManager));
            }
        }

        private static void ReleaseForegroundHook()
        {
            lock (_hookSync)
            {
                if (_foregroundHookHandle == IntPtr.Zero)
                {
                    return;
                }

                try
                {
                    if (!UnhookWinEvent(_foregroundHookHandle))
                    {
                        Program.LogDebug("Foreground hook uninstall reported failure.", nameof(DisplayManager));
                    }
                }
                catch
                {
                    // Best-effort cleanup.
                }
                finally
                {
                    _foregroundHookHandle = IntPtr.Zero;
                    _foregroundHookProc = null;
                }
            }
        }

        private static void OnForegroundWindowChanged(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint idEventThread,
            uint dwmsEventTime)
        {
            if (eventType != EVENT_SYSTEM_FOREGROUND)
            {
                return;
            }

            RequestApplyDebounced();
        }
    }
}
