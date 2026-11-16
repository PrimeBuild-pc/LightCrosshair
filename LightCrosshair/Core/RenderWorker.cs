using System.Collections.Concurrent;
using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal sealed class RenderWorker : IDisposable
{
    private sealed record Job(Func<object?> Render, CancellationToken Token, Func<bool> Allowed, TaskCompletionSource<object?> Completion);
    private readonly BlockingCollection<Job> _queue = new(64);
    private readonly object _gate = new();
    private Thread? _thread;
    private bool _disposed;
    internal Task<object?> Enqueue(Func<object?> render, CancellationToken token, Func<bool>? allowed = null)
    {
        var job = new Job(render, token, allowed ?? (() => true), new(TaskCreationOptions.RunContinuationsAsynchronously));
        lock (_gate)
        {
            if (_disposed || token.IsCancellationRequested || !_queue.TryAdd(job)) { Reject(job); return job.Completion.Task; }
            if (_thread is null)
            {
                _thread = new(Run) { Name = "Crosshair render", IsBackground = true, Priority = ThreadPriority.BelowNormal };
                _thread.Start();
            }
        }
        return job.Completion.Task;
    }
    private void Run()
    {
        foreach (var job in _queue.GetConsumingEnumerable())
        {
            try
            {
                if (job.Token.IsCancellationRequested || !job.Allowed()) { Reject(job); continue; }
                job.Completion.TrySetResult(job.Render());
            }
            catch (Exception ex) { job.Completion.TrySetException(ex); }
        }
    }
    private static void Reject(Job job) => job.Completion.TrySetException(new CoreException(IpcErrorCodes.BadRequest, "Render canceled: UI disconnected, parked or render queue full."));
    internal void DropPending() { while (_queue.TryTake(out var job)) Reject(job); }
    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; DropPending(); _queue.CompleteAdding(); }
        // A currently running bounded raster finishes normally. Never abort GDI+.
    }
}
