using System;
using System.Threading;

namespace LightCrosshair
{
    public sealed class Debouncer : IDisposable
    {
        private readonly int _ms;
    private System.Threading.Timer? _timer;
        private readonly object _gate = new();
        private Action? _action;

        public Debouncer(int milliseconds) => _ms = milliseconds;

        public void Trigger(Action action)
        {
            lock (_gate)
            {
                _action = action;
                _timer?.Dispose();
                _timer = new System.Threading.Timer(_ =>
                {
                    Action? a;
                    lock (_gate) a = _action;
                    a?.Invoke();
                }, null, _ms, Timeout.Infinite);
            }
        }

        public void Dispose() => _timer?.Dispose();
    }
}
