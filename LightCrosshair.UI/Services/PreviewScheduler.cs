namespace LightCrosshair.UI.Services;

public interface IDelay
{
    Task Delay(TimeSpan time, CancellationToken cancellationToken);
}

public sealed class TaskDelay : IDelay
{
    public Task Delay(TimeSpan time, CancellationToken cancellationToken) => Task.Delay(time, cancellationToken);
}

/// <summary>
/// Coalesces a stream of values to at most one send per interval and always keeps the latest.
/// </summary>
public sealed class PreviewScheduler<T>
{
    private readonly TimeSpan _interval;
    private DateTime _lastSent;
    private int _version;
    private T? _pending;
    private bool _waiting;

    public PreviewScheduler(TimeSpan interval) => _interval = interval;

    public int Version => _version;

    /// <summary>Returns a value that should be sent now, or null when it was queued.</summary>
    public T? Submit(T value, DateTime now, out TimeSpan wait)
    {
        _pending = value;
        _version++;
        if (_lastSent == default || now - _lastSent >= _interval)
        {
            _lastSent = now;
            wait = TimeSpan.Zero;
            _waiting = false;
            var send = _pending;
            _pending = default;
            return send;
        }

        wait = _interval - (now - _lastSent);
        _waiting = true;
        return default;
    }

    /// <summary>Drops a queued value so an in-flight wait cannot send it after a newer one.</summary>
    public void SuppressPending(DateTime now)
    {
        _version++;
        _waiting = false;
        _pending = default;
        _lastSent = now;
    }

    public T? TakePending(DateTime now, int version)
    {
        if (!_waiting || version != _version) return default;
        if (now < _lastSent + _interval) return default;
        _waiting = false;
        _lastSent = now;
        var send = _pending;
        _pending = default;
        return send;
    }
}
