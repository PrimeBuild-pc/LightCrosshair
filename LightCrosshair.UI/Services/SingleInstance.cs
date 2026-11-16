namespace LightCrosshair.UI.Services;

public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    public bool IsFirst { get; }

    public SingleInstance()
    {
        _mutex = new Mutex(false, @"Local\LightCrosshair.UI");
        try
        {
            IsFirst = _mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            IsFirst = true;
        }
    }

    public void Dispose()
    {
        if (IsFirst)
        {
            try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _mutex.Dispose();
    }
}
