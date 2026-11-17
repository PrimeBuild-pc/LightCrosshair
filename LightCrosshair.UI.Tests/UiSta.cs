using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace LightCrosshair.UI.Tests;

internal static class UiSta
{
    private static Dispatcher? _dispatcher;
    private static readonly object Gate = new();

    public static void Run(Action action)
    {
        Ensure();
        Exception? error = null;
        _dispatcher!.Invoke(() =>
        {
            if (Application.Current != null)
                Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    private static void Ensure()
    {
        if (_dispatcher != null) return;
        lock (Gate)
        {
            if (_dispatcher != null) return;
            using var ready = new ManualResetEventSlim();
            Exception? startError = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    try { app.InitializeComponent(); }
                    catch (InvalidOperationException) { }
                    if (!app.Resources.Contains("Vis"))
                        throw new InvalidOperationException("App resources are not loaded.");
                    _dispatcher = Dispatcher.CurrentDispatcher;
                }
                catch (Exception ex)
                {
                    startError = ex;
                }
                ready.Set();
                if (startError == null) Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            ready.Wait();
            if (startError != null) throw startError;
        }
    }
}
