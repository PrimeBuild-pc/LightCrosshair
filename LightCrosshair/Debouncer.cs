using System;
using System.Threading;

namespace LightCrosshair
{
    public sealed class Debouncer : IDisposable
    {
        private readonly int _milliseconds;
        private readonly object _gate = new();
        private System.Threading.Timer? _timer;
        private Action? _action;
        private bool _disposed;

        public Debouncer(int milliseconds) => _milliseconds = milliseconds;

        public void Trigger(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            lock (_gate)
            {
                if (_disposed) return;
                _action = action;
                _timer ??= new System.Threading.Timer(_ => Run(), null, Timeout.Infinite, Timeout.Infinite);
                _timer.Change(_milliseconds, Timeout.Infinite);
            }
        }

        private void Run()
        {
            Action? action;
            lock (_gate)
            {
                if (_disposed) return;
                action = _action;
                _action = null;
                _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            }

            action?.Invoke();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _timer?.Dispose();
                _timer = null;
                _action = null;
            }
        }
    }
}
