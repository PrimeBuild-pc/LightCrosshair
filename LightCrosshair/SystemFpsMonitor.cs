using Microsoft.Diagnostics.Tracing.Session;

namespace LightCrosshair;

internal readonly record struct FpsSample(bool HasData, double Fps, double Frametime, double OnePercentLow);

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

// The v1 ETW Present-start parser, reduced to the three metrics retained in v2.
// No ETW session, worker, poll, retry loop or timer exists while disabled.
internal sealed class SystemFpsMonitor
{
    private readonly object _gate = new();
    private readonly FpsBuffer _buffer = new();
    private CancellationTokenSource? _stop;
    private Task _worker = Task.CompletedTask;
    private int _pid;
    private DateTime _last;
    private static readonly Guid Dxgi = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9"), D3D9 = new("783ACA0A-FA53-4457-BB8E-11327BDDE440");
    public void Start(int pid)
    {
        lock (_gate)
        {
            if (_pid != pid) { _pid = pid; _last = default; _buffer.Clear(); }
            if (_stop is not null) return;
            _stop = new(); var token = _stop.Token;
            _worker = Task.Run(() => Capture(token), token);
        }
    }
    private void Capture(CancellationToken token)
    {
        try
        {
            using var session = new TraceEventSession($"LightCrosshair.Fps.{Environment.ProcessId}.{Guid.NewGuid():N}") { StopOnDispose = true };
            using var registration = token.Register(() => { try { session.Source.StopProcessing(); } catch (ObjectDisposedException) { } });
            if (token.IsCancellationRequested) return;
            session.EnableProvider(Dxgi); session.EnableProvider(D3D9);
            session.Source.Dynamic.All += data =>
            {
                if (token.IsCancellationRequested || !((data.ProviderGuid == Dxgi && (int)data.ID == 42) || (data.ProviderGuid == D3D9 && (int)data.ID == 1))) return;
                lock (_gate)
                {
                    if (data.ProcessID != _pid) return;
                    if (_last != default) _buffer.Add((data.TimeStamp - _last).TotalMilliseconds);
                    _last = data.TimeStamp;
                }
            };
            session.Source.Process();
        }
        catch (Exception ex) { if (!token.IsCancellationRequested) Program.LogError(ex, "FPS capture unavailable"); }
    }
    public FpsSample Snapshot() { lock (_gate) return DateTime.Now - _last > TimeSpan.FromSeconds(2) ? default : _buffer.Snapshot(); }
    public Task StopAsync()
    {
        lock (_gate)
        {
            _stop?.Cancel(); _stop?.Dispose(); _stop = null; _last = default; _buffer.Clear();
            return _worker;
        }
    }
}
