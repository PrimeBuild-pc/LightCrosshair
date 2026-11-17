using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using LightCrosshair.UI.Core;
using LightCrosshair.UI.Localization;
using LightCrosshair.UI.Services;
using LightCrosshair.UI.ViewModels;

namespace LightCrosshair.UI;

public partial class App : Application
{
    private SingleInstance? _instance;
    private AppUi? _ui;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        OpenTiming.MarkStart();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        var options = LaunchOptions.Parse(e.Args);
        _instance = new SingleInstance();
        if (!_instance.IsFirst)
        {
            Shutdown(0);
            return;
        }

        var ui = _ui = new AppUi(new DispatcherMarshaler());
        FakeCoreClient? fake = options.FakeCore ? new FakeCoreClient(new DispatcherMarshaler()) : null;
        if (fake != null && options.ScreenshotsDirectory != null) fake.UseReadmeSeed();
        ICoreClient core = fake != null ? fake : new PipeCoreClient(new PipeCoreClientOptions { Marshaler = new DispatcherMarshaler() });
        var viewModel = new MainViewModel(core, ui, options);
        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        var restoreMaximized = false;
        if (options.ScreenshotsDirectory == null)
            restoreMaximized = WindowPlacementStore.Restore(window, WindowPlacementStore.Load());
        else
            viewModel.AllowExit = true;
        window.SourceInitialized += (_, _) =>
        {
            ui.WindowHandle = new WindowInteropHelper(window).Handle;
            if (restoreMaximized) window.WindowState = WindowState.Maximized;
        };
        window.Show();
        ui.WindowHandle = new WindowInteropHelper(window).Handle;
        if (options.ScreenshotsDirectory != null)
        {
            await viewModel.StartAsync().ConfigureAwait(true);
            await ScreenshotRunner.CaptureAsync(window, viewModel, options.ScreenshotsDirectory).ConfigureAwait(true);
            Shutdown(0);
            return;
        }
        _ = window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { _ = viewModel.StartAsync(); }));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (MainWindow != null && Current?.Properties["shots"] == null)
            WindowPlacementStore.Save(MainWindow);
        _instance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        // In-app dialog only; a second failure while one is open just keeps that one.
        if (_ui != null && !_ui.Dialogs.IsOpen)
            _ = _ui.NotifyAsync(Loc.Instance["AppName"], Loc.Instance["Error_Internal"]);
    }
}
