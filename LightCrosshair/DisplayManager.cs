using System;
using System.Collections.Generic;
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
        private static Task? _monitorTask;
        private static readonly Debouncer _applyDebouncer = new(100);
        private static readonly object _hookSync = new();
        private static readonly object _trackedProcessSync = new();
        private static readonly Dictionary<int, Process> _trackedTargetProcesses = new();
        private static IntPtr _foregroundHookHandle;
        private static WinEventDelegate? _foregroundHookProc;
        private static DateTime _lastApplyLogUtc = DateTime.MinValue;
        private static DateTime _lastApplyAttemptUtc = DateTime.MinValue;
        private static DateTime _lastTargetDecisionLogUtc = DateTime.MinValue;
        private static string _lastTargetDecision = string.Empty;
        private static string _trackedTargetProcessBaseName = string.Empty;
        private static bool _targetProcessWasObservedRunning;

        private static int _lastGammaValue = int.MinValue;
        private static int _lastContrastValue = int.MinValue;
        private static int _lastBrightnessValue = int.MinValue;
        private static int _lastVibranceValue = int.MinValue;

        public static ColorBackendInfo GetBackendInfo() => GammaController.GetBackendInfo();

        public static void StartMonitoring()
        {
            StopMonitorLoop();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            EnsureForegroundHook();

            _monitorTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        CheckForegroundAndApply();
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
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
            StopMonitorLoop();
            ReleaseForegroundHook();
            ClearTrackedTargetProcesses();
            GammaController.RestoreOriginal();
            _isGammaApplied = false;
        }

        private static void StopMonitorLoop()
        {
            var cts = _cts;
            var monitorTask = _monitorTask;

            _cts = null;
            _monitorTask = null;

            if (cts != null)
            {
                try
                {
                    cts.Cancel();
                }
                catch
                {
                    // Best-effort cancellation.
                }
            }

            if (monitorTask != null)
            {
                try
                {
                    monitorTask.Wait(1000);
                }
                catch
                {
                    // Best-effort wait.
                }
            }

            cts?.Dispose();
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
                ClearTrackedTargetProcesses();
                if (_isGammaApplied || forceUpdate)
                {
                    GammaController.RestoreOriginal();
                    _isGammaApplied = false;
                }
                return;
            }

            // If no specific process is set, we apply it globally
            if (string.IsNullOrWhiteSpace(cfg.TargetProcessName))
            {
                ClearTrackedTargetProcesses();
                if (!_isGammaApplied || forceUpdate)
                {
                    ApplyTargetGamma();
                }
                return;
            }

            var targetName = NormalizeProcessName(cfg.TargetProcessName);
            var targetProcessBaseName = targetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? targetName[..^4]
                : targetName;

            bool isTargetRunning = UpdateTrackedTargetProcesses(targetProcessBaseName);

            // In target-process mode, force restore as soon as the tracked process is no longer running.
            if (!isTargetRunning)
            {
                LogTargetDecision(targetProcessBaseName, "<not-running>", isMatch: false, forceUpdate);
                ClearTrackedTargetProcesses();

                if (_targetProcessWasObservedRunning)
                {
                    ResetDisplayConfigAfterTargetExit();
                    _targetProcessWasObservedRunning = false;
                }

                if (_isGammaApplied || forceUpdate)
                {
                    GammaController.RestoreOriginal();
                    _isGammaApplied = false;
                }

                return;
            }

            // Target mode is process-lifecycle based: apply while target is running.
            LogTargetDecision(targetProcessBaseName, "<running>", isMatch: true, forceUpdate);
            if (!_isGammaApplied || forceUpdate)
            {
                ApplyTargetGamma();
            }
        }

        private static void ResetDisplayConfigAfterTargetExit()
        {
            WpfSettingsHost.RefreshDisplayUiFromConfig();
            Program.LogDebug("Target process exited: display restored, profile color settings kept for next auto-detection.", nameof(DisplayManager));
        }

        private static bool UpdateTrackedTargetProcesses(string processNameNoExt)
        {
            if (string.IsNullOrWhiteSpace(processNameNoExt))
            {
                ClearTrackedTargetProcesses();
                return false;
            }

            Process[] processes = Array.Empty<Process>();
            var adoptedProcessIds = new HashSet<int>();
            var observedProcessIds = new HashSet<int>();
            try
            {
                processes = Process.GetProcessesByName(processNameNoExt);

                lock (_trackedProcessSync)
                {
                    if (!string.Equals(_trackedTargetProcessBaseName, processNameNoExt, StringComparison.OrdinalIgnoreCase))
                    {
                        ClearTrackedTargetProcesses_NoLock();
                        _trackedTargetProcessBaseName = processNameNoExt;
                    }

                    foreach (var process in processes)
                    {
                        int pid;
                        try
                        {
                            pid = process.Id;
                        }
                        catch
                        {
                            continue;
                        }

                        observedProcessIds.Add(pid);

                        if (_trackedTargetProcesses.ContainsKey(pid))
                        {
                            continue;
                        }

                        if (!TryAttachTrackedProcess_NoLock(process, pid))
                        {
                            continue;
                        }

                        adoptedProcessIds.Add(pid);
                    }

                    if (_trackedTargetProcesses.Count > 0)
                    {
                        var staleTrackedIds = new List<int>();
                        foreach (var tracked in _trackedTargetProcesses.Keys)
                        {
                            if (!observedProcessIds.Contains(tracked))
                            {
                                staleTrackedIds.Add(tracked);
                            }
                        }

                        foreach (int staleId in staleTrackedIds)
                        {
                            RemoveTrackedProcess_NoLock(staleId);
                        }
                    }

                    bool hasTracked = _trackedTargetProcesses.Count > 0;
                    if (hasTracked)
                    {
                        _targetProcessWasObservedRunning = true;
                    }

                    return hasTracked;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                foreach (var process in processes)
                {
                    int pid;
                    try
                    {
                        pid = process.Id;
                    }
                    catch
                    {
                        continue;
                    }

                    if (adoptedProcessIds.Contains(pid))
                    {
                        // Ownership is transferred to _trackedTargetProcesses.
                        continue;
                    }

                    try
                    {
                        process.Dispose();
                    }
                    catch
                    {
                        // Best-effort cleanup.
                    }
                }
            }
        }

        private static bool TryAttachTrackedProcess_NoLock(Process process, int pid)
        {
            try
            {
                process.EnableRaisingEvents = true;
                process.Exited += OnTrackedTargetProcessExited;

                if (process.HasExited)
                {
                    process.Exited -= OnTrackedTargetProcessExited;
                    return false;
                }

                _trackedTargetProcesses[pid] = process;
                return true;
            }
            catch
            {
                try
                {
                    process.Exited -= OnTrackedTargetProcessExited;
                }
                catch
                {
                    // Best-effort cleanup.
                }

                return false;
            }
        }

        private static void RemoveTrackedProcess_NoLock(int pid)
        {
            if (!_trackedTargetProcesses.TryGetValue(pid, out var trackedProcess))
            {
                return;
            }

            _trackedTargetProcesses.Remove(pid);
            ReleaseTrackedProcess(trackedProcess);
        }

        private static void ClearTrackedTargetProcesses()
        {
            lock (_trackedProcessSync)
            {
                ClearTrackedTargetProcesses_NoLock();
            }
        }

        private static void ClearTrackedTargetProcesses_NoLock()
        {
            foreach (var tracked in _trackedTargetProcesses.Values)
            {
                ReleaseTrackedProcess(tracked);
            }

            _trackedTargetProcesses.Clear();
            _trackedTargetProcessBaseName = string.Empty;
            _targetProcessWasObservedRunning = false;
        }

        private static void ReleaseTrackedProcess(Process process)
        {
            try
            {
                process.Exited -= OnTrackedTargetProcessExited;
            }
            catch
            {
                // Best-effort cleanup.
            }

            try
            {
                process.Dispose();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        private static void OnTrackedTargetProcessExited(object? sender, EventArgs e)
        {
            int exitedPid = -1;
            if (sender is Process process)
            {
                try
                {
                    exitedPid = process.Id;
                }
                catch
                {
                    exitedPid = -1;
                }
            }

            lock (_trackedProcessSync)
            {
                if (exitedPid >= 0)
                {
                    RemoveTrackedProcess_NoLock(exitedPid);
                }
            }

            Program.LogDebug($"Tracked target process exited (pid={exitedPid}). Re-evaluating display color state.", nameof(DisplayManager));

            try
            {
                CheckForegroundAndApply(forceUpdate: true);
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "DisplayManager.OnTrackedTargetProcessExited");
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
            bool wasApplied = _isGammaApplied;

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

            // Preserve applied state across transient apply failures so restore is never skipped.
            if (applied)
            {
                _isGammaApplied = true;
            }

            if (DateTime.UtcNow - _lastApplyLogUtc > TimeSpan.FromSeconds(2) || applied != wasApplied || wasApplied != _isGammaApplied)
            {
                var info = GammaController.GetBackendInfo();
                Program.LogDebug($"Display apply -> gamma={cfg.GammaValue} contrast={cfg.ContrastValue} brightness={cfg.BrightnessValue} vibrance={cfg.VibranceValue} target={cfg.TargetProcessName} applied={applied} trackedApplied={_isGammaApplied} backend={info.BackendName}", nameof(DisplayManager));
                _lastApplyLogUtc = DateTime.UtcNow;
            }
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
