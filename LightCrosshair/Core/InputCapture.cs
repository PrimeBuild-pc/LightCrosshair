namespace LightCrosshair.Core;

// Monotonic, renewable lease; the UI must keep renewing while capturing.
internal sealed class InputCapture(Func<long>? clock = null)
{
    internal const int TimeoutMilliseconds = 60_000;
    private readonly Func<long> _clock = clock ?? (() => Environment.TickCount64);
    private long? _deadline;
    public bool Active => _deadline is { } deadline && _clock() < deadline;
    public void Set(bool active) => _deadline = active ? _clock() + TimeoutMilliseconds : null;
}
