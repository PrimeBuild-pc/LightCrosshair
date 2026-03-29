using System;
using System.Threading;

namespace LightCrosshair
{
    public sealed class Debouncer : IDisposable
    {
        private readonly int _ms;
        private System.Threading.Timer? _timer;
        private readonly object _gate = new();
        private long _version;
        private bool _disposed;

        private sealed class DebounceState
        {
            public DebounceState(long version, Action action)
            {
                Version = version;
                Action = action;
            }

            public long Version { get; }
            public Action Action { get; }
        }

        public Debouncer(int milliseconds) => _ms = milliseconds;

        public void Trigger(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            DebounceState state;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                long version = ++_version;
                state = new DebounceState(version, action);
                _timer?.Dispose();
                _timer = new System.Threading.Timer(_ =>
                {
                    var payload = (DebounceState)_!;
                    lock (_gate)
                    {
                        if (_disposed || payload.Version != _version)
                        {
                            return;
                        }

                        _timer?.Dispose();
                        _timer = null;
                    }

                    payload.Action();
                }, state, _ms, Timeout.Infinite);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _timer?.Dispose();
                _timer = null;
            }
        }
    }
}
