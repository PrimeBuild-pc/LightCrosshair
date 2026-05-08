using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Session;

namespace LightCrosshair
{
    public readonly record struct FpsMetricsSnapshot(
        bool HasData,
        int SampleCount,
        double InstantFps,
        double AverageFps,
        double OnePercentLowFps,
        double LatestFrameTimeMs,
        int GeneratedFrameCount,
        bool IsGeneratedFrameDataAvailable,
        double[] RecentFrameTimesMs
    )
    {
        public FramePacingStats PacingStats { get; init; } = FramePacingStats.Empty;
        public FrameGenerationDetectionResult FrameGenerationStatus { get; init; } = FrameGenerationDetectionResult.Unknown;
    }

    public readonly record struct FramePacingStats(
        bool HasData,
        int SampleCount,
        double AverageFrameTimeMs,
        double MinFrameTimeMs,
        double MaxFrameTimeMs,
        double StandardDeviationFrameTimeMs,
        double FrameTimeVarianceMsSquared,
        double JitterMs,
        double OnePercentLowFps,
        double PointOnePercentLowFps,
        int HitchCount,
        double HitchThresholdMs,
        double StabilityScore
    )
    {
        public static FramePacingStats Empty { get; } = new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, FrameTimingStatistics.DefaultHitchThresholdMs, 0);
    }

    internal static class FrameTimingStatistics
    {
        public const double DefaultHitchThresholdMs = 50.0;

        public static FramePacingStats Calculate(ReadOnlySpan<double> frameTimesMs, int sampleCount, double hitchThresholdMs = DefaultHitchThresholdMs)
        {
            int count = Math.Min(sampleCount, frameTimesMs.Length);
            if (count <= 0)
            {
                return FramePacingStats.Empty;
            }

            if (double.IsNaN(hitchThresholdMs) || double.IsInfinity(hitchThresholdMs) || hitchThresholdMs <= 0)
            {
                hitchThresholdMs = DefaultHitchThresholdMs;
            }

            double sum = 0;
            double sumSquares = 0;
            double min = double.MaxValue;
            double max = 0;
            double jitterSum = 0;
            int jitterPairs = 0;
            int hitchCount = 0;
            double previous = 0;

            for (int i = 0; i < count; i++)
            {
                double value = frameTimesMs[i];
                sum += value;
                sumSquares += value * value;
                min = Math.Min(min, value);
                max = Math.Max(max, value);

                if (value > hitchThresholdMs)
                {
                    hitchCount++;
                }

                if (i > 0)
                {
                    jitterSum += Math.Abs(value - previous);
                    jitterPairs++;
                }

                previous = value;
            }

            double average = sum / count;
            double variance = Math.Max(0, (sumSquares / count) - (average * average));
            double standardDeviation = Math.Sqrt(variance);
            double jitter = jitterPairs > 0 ? jitterSum / jitterPairs : 0;

            double[] sorted = frameTimesMs.Slice(0, count).ToArray();
            Array.Sort(sorted);

            double onePercentLow = CalculateLowFps(sorted, 0.01);
            double pointOnePercentLow = CalculateLowFps(sorted, 0.001);
            double stabilityScore = CalculateStabilityScore(average, standardDeviation, jitter, hitchCount, count);

            return new FramePacingStats(
                true,
                count,
                average,
                min,
                max,
                standardDeviation,
                variance,
                jitter,
                onePercentLow,
                pointOnePercentLow,
                hitchCount,
                hitchThresholdMs,
                stabilityScore);
        }

        private static double CalculateLowFps(double[] sortedAscendingFrameTimes, double fraction)
        {
            int count = sortedAscendingFrameTimes.Length;
            if (count == 0)
            {
                return 0;
            }

            int worstCount = Math.Max(1, (int)Math.Ceiling(count * fraction));
            double sum = 0;
            for (int i = count - worstCount; i < count; i++)
            {
                sum += sortedAscendingFrameTimes[i];
            }

            return ToFps(sum / worstCount);
        }

        private static double CalculateStabilityScore(double averageFrameTimeMs, double standardDeviationMs, double jitterMs, int hitchCount, int sampleCount)
        {
            if (sampleCount <= 0 || averageFrameTimeMs <= 0)
            {
                return 0;
            }

            // Original 0-100 score: lower variance, jitter, and hitch density means steadier pacing.
            double standardDeviationPenalty = (standardDeviationMs / averageFrameTimeMs) * 45.0;
            double jitterPenalty = (jitterMs / averageFrameTimeMs) * 35.0;
            double hitchPenalty = ((double)hitchCount / sampleCount) * 20.0;
            return Math.Clamp(100.0 - standardDeviationPenalty - jitterPenalty - hitchPenalty, 0, 100);
        }

        private static double ToFps(double frameTimeMs)
        {
            return frameTimeMs <= 0 ? 0 : 1000.0 / frameTimeMs;
        }
    }

    public sealed class FpsMetricsBuffer
    {
        private readonly object _sync = new();
        private readonly double[] _frameTimesMs;
        private readonly bool[] _generatedFlags;
        private int _head;
        private int _count;

        private readonly double[][] _snapshotBuffers;
        private int _snapshotBufIdx;
        private readonly double[] _worstFramesBuffer;
        private bool _generatedFrameDetectionAvailable;
        private FrameGenerationDetectionResult _frameGenerationStatus = FrameGenerationDetectionResult.Unknown;

        public FpsMetricsBuffer(int capacity = 1000)
        {
            if (capacity < 8) throw new ArgumentOutOfRangeException(nameof(capacity));
            _frameTimesMs = new double[capacity];
            _generatedFlags = new bool[capacity];

            _snapshotBuffers = new double[][] 
            { 
                new double[capacity], 
                new double[capacity], 
                new double[capacity] 
            };
            int maxOnePercentCount = Math.Max(1, (int)Math.Ceiling(capacity * 0.01));
            _worstFramesBuffer = new double[maxOnePercentCount];
        }

        public int Capacity => _frameTimesMs.Length;

        public void Clear()
        {
            lock (_sync)
            {
                _head = 0;
                _count = 0;
                _generatedFrameDetectionAvailable = false;
                _frameGenerationStatus = FrameGenerationDetectionResult.Unknown;
            }
        }

        public void SetGeneratedFrameDetectionAvailable(bool isAvailable)
        {
            lock (_sync)
            {
                _generatedFrameDetectionAvailable = isAvailable;
            }
        }

        public void SetFrameGenerationStatus(FrameGenerationDetectionResult status)
        {
            lock (_sync)
            {
                _frameGenerationStatus = status;
            }
        }

        public void AddFrame(double frameTimeMs, bool isGeneratedFrame = false)
        {
            if (double.IsNaN(frameTimeMs) || double.IsInfinity(frameTimeMs)) return;
            if (frameTimeMs <= 0.05 || frameTimeMs > 500) return;

            lock (_sync)
            {
                _frameTimesMs[_head] = frameTimeMs;
                _generatedFlags[_head] = isGeneratedFrame;
                _head = (_head + 1) % _frameTimesMs.Length;
                if (_count < _frameTimesMs.Length) _count++;
            }
        }

        public FpsMetricsSnapshot Snapshot()
        {
            int currentCount;
            bool genAvailable;
            FrameGenerationDetectionResult frameGenerationStatus;
            double[] values;
            bool[] flagsCopy;

            lock (_sync)
            {
                if (_count == 0)
                {
                    return new FpsMetricsSnapshot(false, 0, 0, 0, 0, 0, 0, false, Array.Empty<double>());
                }

                _snapshotBufIdx = (_snapshotBufIdx + 1) % 3;
                values = _snapshotBuffers[_snapshotBufIdx];

                int start = (_head - _count + _frameTimesMs.Length) % _frameTimesMs.Length;
                
                // Copy all to values for drawing the graph later
                if (start + _count <= _frameTimesMs.Length)
                {
                    Array.Copy(_frameTimesMs, start, values, 0, _count);
                }
                else
                {
                    int firstPart = _frameTimesMs.Length - start;
                    Array.Copy(_frameTimesMs, start, values, 0, firstPart);
                    Array.Copy(_frameTimesMs, 0, values, firstPart, _count - firstPart);
                }

                // We can release the lock early for mathematical computations
                // We just need a copy of generated frame flags
                flagsCopy = new bool[_count];
                if (start + _count <= _generatedFlags.Length)
                {
                    Array.Copy(_generatedFlags, start, flagsCopy, 0, _count);
                }
                else
                {
                    int firstPart = _generatedFlags.Length - start;
                    Array.Copy(_generatedFlags, start, flagsCopy, 0, firstPart);
                    Array.Copy(_generatedFlags, 0, flagsCopy, firstPart, _count - firstPart);
                }
                
                currentCount = _count;
                genAvailable = _generatedFrameDetectionAvailable;
                frameGenerationStatus = _frameGenerationStatus;
            } // END LOCK

            // 1) Fix Spikes (Sliding Window): We compute real-time FPS using the last ~500ms of frames
            // 3) Fix 1% Low Logic: We compute Average and 1% Low over a rolling ~1000ms window
            double halfSecTimeSum = 0;
            int halfSecCount = 0;
            double oneSecTimeSum = 0;
            int oneSecCount = 0;
            int generatedCount = 0;

            for (int i = 0; i < currentCount; i++)
            {
                int idx = currentCount - 1 - i;
                double v = values[idx];
                
                if (oneSecTimeSum < 1000.0) 
                {
                    oneSecTimeSum += v;
                    oneSecCount++;
                    if (flagsCopy[idx]) generatedCount++;
                    
                    if (halfSecTimeSum < 500.0)
                    {
                        halfSecTimeSum += v;
                        halfSecCount++;
                    }
                }
                else
                {
                    // Stop going further back once we exceed 1000ms
                    break;
                }
            }

            if (oneSecCount == 0) oneSecCount = 1;
            if (halfSecCount == 0) halfSecCount = 1;
            if (halfSecTimeSum <= 0) halfSecTimeSum = 1;

            int onePercentCount = Math.Max(1, (int)Math.Ceiling(oneSecCount * 0.01));
            Array.Clear(_worstFramesBuffer, 0, _worstFramesBuffer.Length);

            for (int i = 0; i < oneSecCount; i++)
            {
                int idx = currentCount - 1 - i;
                double v = values[idx];

                // Maintain highest (worst) frame times in descending order
                if (v > _worstFramesBuffer[onePercentCount - 1])
                {
                    for (int j = 0; j < onePercentCount; j++)
                    {
                        if (v > _worstFramesBuffer[j])
                        {
                            // Shift elements down
                            for (int k = onePercentCount - 1; k > j; k--)
                            {
                                _worstFramesBuffer[k] = _worstFramesBuffer[k - 1];
                            }
                            _worstFramesBuffer[j] = v;
                            break;
                        }
                    }
                }
            }

            double latest = values[currentCount - 1];
            // Sliding window FPS matching the ~500ms time segment
            double instantFps = halfSecCount * (1000.0 / halfSecTimeSum);
            double avgFrameTime = oneSecTimeSum / oneSecCount;
            
            double badFramesSum = 0;
            for (int i = 0; i < onePercentCount; i++) badFramesSum += _worstFramesBuffer[i];
            double onePercentFrameTime = badFramesSum / onePercentCount;

            int visibleGeneratedCount = genAvailable ? Math.Max(generatedCount, frameGenerationStatus.GeneratedFrameCount) : 0;
            var pacingStats = FrameTimingStatistics.Calculate(values.AsSpan(currentCount - oneSecCount, oneSecCount), oneSecCount);

            return new FpsMetricsSnapshot(
                true,
                currentCount,
                instantFps,
                ToFps(avgFrameTime),
                ToFps(onePercentFrameTime),
                latest,
                visibleGeneratedCount,
                genAvailable,
                values
            )
            {
                PacingStats = pacingStats,
                FrameGenerationStatus = frameGenerationStatus
            };
        }

        private static double ToFps(double frameTimeMs)
        {
            return frameTimeMs <= 0 ? 0 : 1000.0 / frameTimeMs;
        }
    }

    public static class SystemFpsMonitor
    {
        private const int RtssPollMs = 120;
        private const int EtwFallbackQuietWindowSeconds = 2;
        private const int UiTextRefreshThrottleMs = 333;
        private static readonly TimeSpan TrackingRefreshInterval = TimeSpan.FromMilliseconds(500);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
        
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        
        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private const uint EVENT_SYSTEM_FOREGROUND = 3;
        private const uint WINEVENT_OUTOFCONTEXT = 0;

        private static IntPtr _hook;
        private static WinEventDelegate? _dele;

        private static CancellationTokenSource? _cts;
        private static Task? _etwTask;
        private static Task? _rtssTask;
        private static readonly object _stateSync = new();
        private static readonly FpsMetricsBuffer _metrics = new(1000);
        private static readonly Dictionary<int, DateTime> _lastPresentByPid = new();
        private static DateTime _lastEtwSampleUtc = DateTime.MinValue;
        private static DateTime _lastTrackingRefreshUtc = DateTime.MinValue;
        private static DateTime _lastRtssProcessProbeUtc = DateTime.MinValue;
        private static DateTime _lastRtssUnexpectedLogUtc = DateTime.MinValue;
        private static string _lastRtssUnexpectedSignature = string.Empty;
        private static bool _isRtssProcessAvailable;
        private static readonly string[] RtssProcessNames =
        {
            "RTSS",
            "RTSSHooksLoader",
            "RTSSHooksLoader64"
        };
        private static DateTime _nextRtssMemoryOpenAttemptUtc = DateTime.MinValue;
        private static long _lastUiTextRefreshTick;
        private static int _trackedProcessId;
        private static string _trackedProcessName = string.Empty;
        private static readonly FrameGenerationDetector _frameGenerationDetector = new();

        private static string _currentFpsText = "FPS: --";
        private static string _statusText = "Idle";
        private static string _activeSource = "None";
        public static string CurrentFpsText => Volatile.Read(ref _currentFpsText);
        public static string StatusText => Volatile.Read(ref _statusText);
        public static string ActiveSource => Volatile.Read(ref _activeSource);
        public static int PreferredUiRefreshMs => 50;
        public static int PreferredUiTextRefreshMs => UiTextRefreshThrottleMs;

        public static void RequestTrackingRefresh()
        {
            RefreshTrackedProcessFromForeground(forceRefresh: true);
        }

        public static void Start()
        {
            StopBackgroundWorkers();

            if (_hook != IntPtr.Zero)
            {
                UnhookWinEvent(_hook);
                _hook = IntPtr.Zero;
                _dele = null;
            }
            _cts = new CancellationTokenSource();

            _metrics.Clear();
            _lastPresentByPid.Clear();
            SetCurrentFpsText("FPS: --");
            SetStatusText("Starting telemetry...");
            SetActiveSource("None");
            _trackedProcessId = 0;
            _trackedProcessName = string.Empty;
            _frameGenerationDetector.Reset();
            _lastTrackingRefreshUtc = DateTime.MinValue;

            _dele = new WinEventDelegate(WinEventProc);
            _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _dele, 0, 0, WINEVENT_OUTOFCONTEXT);

            // Initialize tracking from the current foreground window.
            UpdateTrackedProcess(GetForegroundWindow());

            _etwTask = Task.Run(() => RunEtwLoop(_cts.Token), _cts.Token);
            _rtssTask = Task.Run(() => RunRtssLoop(_cts.Token), _cts.Token);
        }

        public static void Stop()
        {
            StopBackgroundWorkers();

            if (_hook != IntPtr.Zero)
            {
                UnhookWinEvent(_hook);
                _hook = IntPtr.Zero;
                _dele = null;
            }

            SetStatusText("Stopped");
            SetActiveSource("None");
            _trackedProcessId = 0;
            _trackedProcessName = string.Empty;
            _lastPresentByPid.Clear();
            _frameGenerationDetector.Reset();
            _lastTrackingRefreshUtc = DateTime.MinValue;
        }

        private static void StopBackgroundWorkers()
        {
            var cts = _cts;
            var etwTask = _etwTask;
            var rtssTask = _rtssTask;

            _cts = null;
            _etwTask = null;
            _rtssTask = null;

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

            WaitForTask(etwTask);
            WaitForTask(rtssTask);

            cts?.Dispose();
        }

        private static void WaitForTask(Task? task)
        {
            if (task == null)
            {
                return;
            }

            try
            {
                // TODO: Replace this blocking shutdown with async cancellation that observes
                // worker exceptions and disposes the CTS only after both workers complete.
                task.Wait(1500);
            }
            catch
            {
                // Best-effort wait.
            }
        }

        public static FpsMetricsSnapshot GetSnapshot() => _metrics.Snapshot();

        private static readonly Guid DxgiProviderGuid = new Guid("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
        private static readonly Guid D3D9ProviderGuid = new Guid("783ACA0A-FA53-4457-BB8E-11327BDDE440");
        private const int DXGI_PRESENT_START_EVENT_ID = 42;
        private const int D3D9_PRESENT_START_EVENT_ID = 1;

        private static async Task RunEtwLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    string sessionName = $"LightCrosshair.Fps.{Environment.ProcessId}.{Guid.NewGuid():N}";
                    using var session = new TraceEventSession(sessionName);
                    session.StopOnDispose = true;

                    session.EnableProvider(DxgiProviderGuid);
                    session.EnableProvider(D3D9ProviderGuid);

                    session.Source.Dynamic.All += data =>
                    {
                        if (token.IsCancellationRequested) return;
                        
                        // Match DXGI Present semantics from PresentMon.
                        // Keep this parser allocation-free for hot ETW paths.
                        bool isPresentStart = false;
                        if (data.ProviderGuid == DxgiProviderGuid && (int)data.ID == DXGI_PRESENT_START_EVENT_ID)
                        {
                            isPresentStart = true;
                        }
                        else if (data.ProviderGuid == D3D9ProviderGuid && (int)data.ID == D3D9_PRESENT_START_EVENT_ID)
                        {
                            isPresentStart = true;
                        }

                        if (!isPresentStart) return;

                        if (!TryGetTrackedProcessId(out int trackedPid)) return;

                        int eventPid = data.ProcessID;
                        if (eventPid <= 0 || eventPid != trackedPid) return;

                        var now = data.TimeStamp;
                        DateTime? previous = null;
                        lock (_stateSync)
                        {
                            if (_lastPresentByPid.TryGetValue(eventPid, out var last))
                            {
                                previous = last;
                            }
                            _lastPresentByPid[eventPid] = now;
                        }

                        if (previous.HasValue)
                        {
                            double frameTimeMs = (now - previous.Value).TotalMilliseconds;
                            OnFrameTime(frameTimeMs, "ETW", isGeneratedFrame: false);
                        }
                    };

                    SetStatusText("ETW active");
                    using var _ = token.Register(() =>
                    {
                        try { session.Dispose(); }
                        catch (Exception ex)
                        {
                            Program.LogDebug($"[ETW] session dispose on cancellation failed: {ex.Message}", "SystemFpsMonitor");
                        }
                    });
                    session.Source.Process();
                }
                catch (UnauthorizedAccessException ex)
                {
                    if (token.IsCancellationRequested) break;
                    Program.LogDebug($"[ETW] Access denied. Admin privileges required for pure anti-cheat safe FPS monitor. Falling back to legacy RTSS. Error: {ex.Message}", "SystemFpsMonitor");
                    SetStatusText("ETW requires Admin. Legacy mode active.");
                    await Task.Delay(5000, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested) break;
                    Program.LogError(ex, "SystemFpsMonitor.RunEtwLoop");
                    SetStatusText("ETW unavailable, legacy mode active.");
                    await Task.Delay(5000, token).ConfigureAwait(false);
                }
            }
        }

        private static void RunRtssLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    bool hasTrackedPid = TryGetTrackedProcessId(out int trackedPid, forceRefresh: true);
                    bool accepted = TryReadRtssFrameTime(out double frameTimeMs, hasTrackedPid ? trackedPid : 0);
                    if (accepted)
                    {
                        bool etwRecent = DateTime.UtcNow - _lastEtwSampleUtc < TimeSpan.FromSeconds(EtwFallbackQuietWindowSeconds);
                        if (!etwRecent)
                        {
                            OnFrameTime(frameTimeMs, "RTSS", isGeneratedFrame: false);
                            if (ActiveSource != "ETW")
                            {
                                SetStatusText("Fallback source active (RTSS)");
                            }
                        }
                    }
                    else if (ActiveSource == "None")
                    {
                        SetStatusText("Waiting for ETW/RTSS frames");
                    }
                }
                catch (FileNotFoundException)
                {
                    if (ActiveSource == "None")
                    {
                        SetStatusText("Waiting for ETW/RTSS frames");
                    }
                }
                catch (IOException)
                {
                    if (ActiveSource == "None")
                    {
                        SetStatusText("Waiting for ETW/RTSS frames");
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    if (ActiveSource == "None")
                    {
                        SetStatusText("Waiting for ETW/RTSS frames");
                    }
                }
                catch (Exception ex)
                {
                    if (ShouldLogRtssUnexpected(ex))
                    {
                        Program.LogError(ex, "SystemFpsMonitor.RunRtssLoop");
                    }

                    if (ActiveSource == "None")
                    {
                        SetStatusText("Telemetry unavailable");
                    }
                }

                Thread.Sleep(RtssPollMs);
            }
        }

        private static bool TryReadRtssFrameTime(out double frameTimeMs, int requiredPid)
        {
            frameTimeMs = 0;

            if (!IsRtssProcessAvailable())
            {
                return false;
            }

            if (DateTime.UtcNow < _nextRtssMemoryOpenAttemptUtc)
            {
                return false;
            }

            try
            {
                using var mmf = MemoryMappedFile.OpenExisting("RTSSSharedMemoryV2");
                using var accessor = mmf.CreateViewAccessor();

                uint signature = accessor.ReadUInt32(0);
                if (signature != 0x53535452) return false; // 'RTSS'

                uint appEntrySize = accessor.ReadUInt32(8);
                uint appArrOffset = accessor.ReadUInt32(12);
                uint appArrSize = accessor.ReadUInt32(16);

                for (uint i = 0; i < appArrSize; i++)
                {
                    long offset = appArrOffset + (i * appEntrySize);
                    uint pid = accessor.ReadUInt32(offset);
                    if (pid == 0) continue;
                    if (requiredPid > 0 && pid != (uint)requiredPid) continue;

                    // RTSS frame time is in microseconds at offset 280 of app entry.
                    uint frameTimeUs = accessor.ReadUInt32(offset + 280);
                    if (frameTimeUs == 0 || frameTimeUs > 1_000_000) continue;

                    frameTimeMs = frameTimeUs / 1000.0;
                    return true;
                }

                return false;
            }
            catch (FileNotFoundException)
            {
                // RTSS process may exist while shared memory is not initialized yet.
                // Apply a back-off to prevent debugger loop spam and CPU churn
                _nextRtssMemoryOpenAttemptUtc = DateTime.UtcNow.AddSeconds(2);
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static bool ShouldLogRtssUnexpected(Exception ex)
        {
            string signature = $"{ex.GetType().FullName}:{ex.Message}";
            DateTime now = DateTime.UtcNow;

            if (string.Equals(_lastRtssUnexpectedSignature, signature, StringComparison.Ordinal)
                && now - _lastRtssUnexpectedLogUtc < TimeSpan.FromSeconds(5))
            {
                return false;
            }

            _lastRtssUnexpectedSignature = signature;
            _lastRtssUnexpectedLogUtc = now;
            return true;
        }

        private static bool IsRtssProcessAvailable()
        {
            var now = DateTime.UtcNow;
            if (now - _lastRtssProcessProbeUtc < TimeSpan.FromSeconds(2))
            {
                return _isRtssProcessAvailable;
            }

            _lastRtssProcessProbeUtc = now;
            try
            {
                _isRtssProcessAvailable = IsAnyProcessRunning(RtssProcessNames);
            }
            catch
            {
                _isRtssProcessAvailable = false;
            }

            return _isRtssProcessAvailable;
        }

        private static bool IsAnyProcessRunning(string[] processNames)
        {
            for (int i = 0; i < processNames.Length; i++)
            {
                Process[] processes = Process.GetProcessesByName(processNames[i]);
                bool found = false;

                for (int p = 0; p < processes.Length; p++)
                {
                    if (!found)
                    {
                        found = true;
                    }

                    processes[p].Dispose();
                }

                if (found)
                {
                    return true;
                }
            }

            return false;
        }

        private static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            UpdateTrackedProcess(hwnd);
        }

        private static void RefreshTrackedProcessFromForeground(bool forceRefresh)
        {
            DateTime now = DateTime.UtcNow;
            lock (_stateSync)
            {
                if (!forceRefresh && now - _lastTrackingRefreshUtc < TrackingRefreshInterval)
                {
                    return;
                }

                _lastTrackingRefreshUtc = now;
            }

            IntPtr hwnd = GetForegroundWindow();
            if (hwnd != IntPtr.Zero)
            {
                UpdateTrackedProcess(hwnd);
            }
        }

        private static void UpdateTrackedProcess(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            GetWindowThreadProcessId(hwnd, out uint pidValue);
            if (pidValue == 0) return;

            if (pidValue == (uint)Environment.ProcessId)
            {
                // Ignore self-foreground transitions caused by overlay z-order operations.
                return;
            }

            int nextPid = ResolveCandidateProcessId(out string processName, (int)pidValue);
            lock (_stateSync)
            {
                if (nextPid != _trackedProcessId)
                {
                    _trackedProcessId = nextPid;
                    _trackedProcessName = processName;
                    _lastPresentByPid.Clear();
                    _metrics.Clear();
                    _frameGenerationDetector.Reset();
                    SetActiveSource("None");
                    SetStatusText(nextPid > 0
                        ? $"Tracking {processName} ({nextPid})"
                        : "Waiting for game window focus");

                    Program.LogDebug(
                        nextPid > 0
                            ? $"Tracked process updated to {processName} ({nextPid})"
                            : "Tracked process cleared (no valid game foreground process)",
                        nameof(SystemFpsMonitor));
                }
            }
        }

        private static bool TryGetTrackedProcessId(out int trackedPid, bool forceRefresh = false)
        {
            if (forceRefresh)
            {
                RefreshTrackedProcessFromForeground(forceRefresh: false);
            }

            lock (_stateSync)
            {
                trackedPid = _trackedProcessId;
                return trackedPid > 0;
            }
        }

        private static int ResolveCandidateProcessId(out string processName, int pid)
        {
            processName = string.Empty;
            if (pid == Environment.ProcessId) return 0;

            try
            {
                using var process = Process.GetProcessById(pid);
                string activeName = process.ProcessName ?? string.Empty;        
                if (string.IsNullOrWhiteSpace(activeName)) return 0;

                if (IsIgnoredForegroundProcess(activeName)) return 0;

                string configuredTarget = NormalizeProcessName(CrosshairConfig.Instance.TargetProcessName);
                if (!string.IsNullOrWhiteSpace(configuredTarget))
                {
                    string activeExe = NormalizeProcessName(activeName);        
                    if (!string.Equals(activeExe, configuredTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        return 0;
                    }
                }

                processName = NormalizeProcessName(activeName);
                return pid;
            }
            catch
            {
                return 0;
            }
        }

        private static int ResolveCandidateProcessId(out string processName)    
        {
            return ResolveCandidateProcessId(out processName, 0); // Not used directly anymore but kept to avoid unused errors if any. Replace full later.
        }

        private static bool IsIgnoredForegroundProcess(string processName)
        {
            return processName.Equals("dwm", StringComparison.OrdinalIgnoreCase)
                   || processName.Equals("explorer", StringComparison.OrdinalIgnoreCase)
                   || processName.Equals("applicationframehost", StringComparison.OrdinalIgnoreCase)
                   || processName.Equals("shellexperiencehost", StringComparison.OrdinalIgnoreCase)
                   || processName.Equals("startmenuexperiencehost", StringComparison.OrdinalIgnoreCase)
                   || processName.Equals("searchhost", StringComparison.OrdinalIgnoreCase)
                   || processName.Equals("textinputhost", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeProcessName(string? value)
        {
            string trimmed = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return string.Empty;
            return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : $"{trimmed}.exe";
        }

        private static void OnFrameTime(double frameTimeMs, string source, bool isGeneratedFrame)
        {
            if (source == "ETW") _lastEtwSampleUtc = DateTime.UtcNow;

            lock (_stateSync)
            {
                if (source == "ETW" && ActiveSource != "ETW")
                {
                    // Reset to avoid mixing stale fallback data with ETW when we promote source quality.
                    _metrics.Clear();
                    _frameGenerationDetector.Reset();
                }

                bool runGenInference = CrosshairConfig.Instance.ShowGenFrames;
                FrameGenerationDetectionResult frameGenerationStatus = FrameGenerationDetectionResult.Unknown;
                if (runGenInference)
                {
                    frameGenerationStatus = _frameGenerationDetector.AddFrame(frameTimeMs, source);
                    isGeneratedFrame = false;
                }
                else
                {
                    isGeneratedFrame = false;
                    _frameGenerationDetector.Reset();
                }

                bool genDetectionAvailable = runGenInference && frameGenerationStatus.State != FrameGenerationState.Unsupported;
                _metrics.SetGeneratedFrameDetectionAvailable(genDetectionAvailable);
                _metrics.SetFrameGenerationStatus(frameGenerationStatus);
                SetActiveSource(source);
                _metrics.AddFrame(frameTimeMs, isGeneratedFrame);
            }

            RefreshUiTextThrottled();
        }

        private static void RefreshUiTextThrottled()
        {
            long now = Environment.TickCount64;
            long last = Interlocked.Read(ref _lastUiTextRefreshTick);
            if (now - last < UiTextRefreshThrottleMs) return;
            Interlocked.Exchange(ref _lastUiTextRefreshTick, now);

            var snapshot = _metrics.Snapshot();
            if (!snapshot.HasData)
            {
                SetCurrentFpsText("FPS: --");
                return;
            }

            var parts = new List<string>
            {
                $"FPS: {snapshot.InstantFps:0}",
                $"AVG: {snapshot.AverageFps:0}",
                $"1% Low: {snapshot.OnePercentLowFps:0}"
            };

            if (CrosshairConfig.Instance.ShowGenFrames)
            {
                parts.Add(FormatCompactFrameGeneration(snapshot));
            }
            if (!string.IsNullOrEmpty(ActiveSource) && ActiveSource != "None")
            {
                parts.Add($"[{ActiveSource}]");
            }
            SetCurrentFpsText(string.Join(" | ", parts));
        }

        private static void SetCurrentFpsText(string value)
        {
            Volatile.Write(ref _currentFpsText, value);
        }

        private static string FormatCompactFrameGeneration(FpsMetricsSnapshot snapshot)
        {
            if (!snapshot.IsGeneratedFrameDataAvailable)
            {
                return "FG: N/A";
            }

            var status = snapshot.FrameGenerationStatus;
            return status.State switch
            {
                FrameGenerationState.Detected when status.IsVerifiedSignal => "FG: VERIFIED",
                FrameGenerationState.Suspected => $"FG: SUSPECT {status.Confidence * 100:0}%",
                FrameGenerationState.Unknown => "FG: UNKNOWN",
                _ => "FG: OFF"
            };
        }

        private static void SetStatusText(string value)
        {
            Volatile.Write(ref _statusText, value);
        }

        private static void SetActiveSource(string value)
        {
            Volatile.Write(ref _activeSource, value);
        }
    }
}

