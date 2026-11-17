using Microsoft.Diagnostics.Tracing.Session;
using LightCrosshair.Core;
using System.Buffers.Binary;

namespace LightCrosshair;

internal readonly record struct FpsSample(bool HasData, double Fps, double Frametime, double OnePercentLow, string? Reason = null);

internal sealed class FpsBuffer
{
    private readonly double[] _times = new double[600];
    private int _next, _count;
    internal void Clear() { _next = _count = 0; }
    internal void Add(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds <= 0 || milliseconds > 10000) return;
        _times[_next] = milliseconds; _next = (_next + 1) % _times.Length; _count = Math.Min(_count + 1, _times.Length);
    }
    internal FpsSample Snapshot()
    {
        if (_count == 0) return default;
        double[] sorted = _times.AsSpan(0, _count).ToArray(); Array.Sort(sorted);
        int worst = Math.Max(1, (int)Math.Ceiling(_count * .01));
        return new(true, 1000 / sorted.Average(), _times[(_next - 1 + _times.Length) % _times.Length], 1000 / sorted.TakeLast(worst).Average());
    }
}

// Keep the v1 DXGI/D3D9 sources and retry a failed session while enabled. No kernel
// provider, injection or RTSS dependency is needed for DX11/DX12 presentation rates.
// Disabled means no session, worker, retry loop or timer.
internal sealed class SystemFpsMonitor
{
    private readonly object _gate = new();
    private readonly FpsPresentTracker _presents = new();
    private readonly Action<Action<int, ulong, DateTime>, CancellationToken> _capture;
    private readonly Func<DateTime> _now;
    private CancellationTokenSource? _stop;
    private Task _worker = Task.CompletedTask;
    private int _pid;
    private long _generation;
    private string? _failure;
    private static readonly Guid Dxgi = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9"), D3D9 = new("783ACA0A-FA53-4457-BB8E-11327BDDE440");
    internal SystemFpsMonitor(Action<Action<int, ulong, DateTime>, CancellationToken>? capture = null, Func<DateTime>? now = null)
    { _capture = capture ?? Capture; _now = now ?? (() => DateTime.Now); }
    public void Start(int pid)
    {
        lock (_gate)
        {
            if (_pid != pid) { _pid = pid; _presents.Clear(); }
            if (_stop is not null) return;
            _stop = new(); var stop = _stop; var previous = _worker; long generation = ++_generation;
            _failure = null;
            _worker = Task.Run(async () =>
            {
                // Serialize stop/start: an old session must finish before its replacement.
                await previous.ConfigureAwait(false);
                try { await RunAsync(generation, stop.Token).ConfigureAwait(false); }
                finally { stop.Dispose(); }
            });
        }
    }
    private async Task RunAsync(long generation, CancellationToken token)
    {
        string? logged = null;
        while (!token.IsCancellationRequested)
        {
            try
            {
                _capture((pid, swapchain, time) =>
                {
                    lock (_gate)
                    {
                        if (generation != _generation || token.IsCancellationRequested || pid <= 0 || pid != _pid) return;
                        _presents.Present(swapchain, time); _failure = null;
                    }
                }, token);
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested) return;
                string signature = ex.GetType().Name + ": " + ex.Message;
                if (signature != logged) { Program.LogError(ex, "FPS ETW capture failed; retrying in 5 seconds"); logged = signature; }
                lock (_gate)
                {
                    if (generation == _generation) _failure = ex is UnauthorizedAccessException ? "FpsAccessDenied" : "FpsUnavailable";
                }
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }
    private static void Capture(Action<int, ulong, DateTime> present, CancellationToken token)
    {
        if (token.IsCancellationRequested) return;
        using var session = new TraceEventSession($"LightCrosshair.Fps.{Environment.ProcessId}.{Guid.NewGuid():N}") { StopOnDispose = true };
        session.EnableProvider(Dxgi); session.EnableProvider(D3D9);
        // StopProcessing alone may leave ProcessTrace blocked on a quiet real-time
        // stream. Stopping the session releases it even after the game has exited.
        using var registration = token.Register(() => { try { session.Stop(); session.Source.StopProcessing(); } catch (ObjectDisposedException) { } });
        if (token.IsCancellationRequested) return;
        // Dynamic.All silently omits events without a registered payload decoder.
        // Present telemetry only needs the stable header and the SDK's pointer /
        // flags prefix, so also consume providers/versions without local manifests.
        session.Source.AllEvents += data =>
        {
            if (token.IsCancellationRequested || !IsPresentStart(data.ProviderGuid, (int)data.ID)) return;
            if (!TryReadPresent(data.ProviderGuid == Dxgi, data.PointerSize, data.EventData(), out ulong swapchain)) return;
            present(data.ProcessID, swapchain, data.TimeStamp);
        };
        // A real-time session otherwise delivers its buffers about once a second, so the graph
        // grew in one-second chunks. Flushing every 50 ms keeps it continuous (capture runs only while enabled).
        using var flusher = new System.Threading.Timer(_ => { try { session.Flush(); } catch (Exception) { } }, null, 50, 50);
        session.Source.Process();
    }
    internal static bool IsPresentStart(Guid provider, int id) => (provider == Dxgi && id is 42 or 55) || (provider == D3D9 && id == 1);
    internal static bool TryReadPresent(bool dxgi, int pointerSize, ReadOnlySpan<byte> payload, out ulong swapchain)
    {
        swapchain = 0;
        if (pointerSize is not (4 or 8) || payload.Length < pointerSize + 4) return false;
        swapchain = pointerSize == 8 ? BinaryPrimitives.ReadUInt64LittleEndian(payload) : BinaryPrimitives.ReadUInt32LittleEndian(payload);
        // D3D9 flag 1 means DONOTWAIT, not DXGI_PRESENT_TEST.
        return !dxgi || (BinaryPrimitives.ReadUInt32LittleEndian(payload[pointerSize..]) & 1) == 0;
    }
    public FpsSample Snapshot()
    {
        lock (_gate)
        {
            var sample = _presents.Snapshot(_now());
            return sample.HasData ? sample : new(false, 0, 0, 0, _pid <= 0 ? "FpsNoForeground" : _failure ?? "FpsNoFrames");
        }
    }
    internal FpsGraphPoint[] Graph() { lock (_gate) return _presents.Graph(_now()); }
    public async Task StopAsync()
    {
        CancellationTokenSource? stop; Task worker;
        lock (_gate)
        {
            stop = _stop; _stop = null; ++_generation; _presents.Clear(); _failure = null;
            worker = _worker;
        }
        // Cancellation may invoke ETW callbacks; never call it while holding _gate.
        if (stop is not null) await stop.CancelAsync().ConfigureAwait(false);
        await worker.ConfigureAwait(false);
    }
}
