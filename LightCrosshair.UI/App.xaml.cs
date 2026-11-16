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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        var options = LaunchOptions.Parse(e.Args);
        _instance = new SingleInstance();
        if (!_instance.IsFirst)
        {
            Shutdown(0);
            return;
        }

        var ui = new AppUi(new DispatcherMarshaler());
        ICoreClient core = options.FakeCore
            ? new FakeCoreClient(new DispatcherMarshaler())
            : new PipeCoreClient(new PipeCoreClientOptions { Marshaler = new DispatcherMarshaler() });
        var viewModel = new MainViewModel(core, ui, options);
        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        if (options.ScreenshotsDirectory == null)
            WindowPlacementStore.Restore(window, WindowPlacementStore.Load());
        window.SourceInitialized += (_, _) => ui.WindowHandle = new WindowInteropHelper(window).Handle;
        window.Show();
        ui.WindowHandle = new WindowInteropHelper(window).Handle;
        await viewModel.StartAsync().ConfigureAwait(true);
        if (options.ScreenshotsDirectory != null)
        {
            if (core is FakeCoreClient fake)
            {
                fake.SetSuggestion(new GameSuggestion { ProcessName = "samplegame2.exe", DisplayName = "Sample Game" });
                if (fake.Snapshot != null) viewModel.Games.Apply(fake.Snapshot);
            }
            await ScreenshotRunner.CaptureAsync(window, viewModel, options.ScreenshotsDirectory).ConfigureAwait(true);
            Shutdown(0);
        }
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
        var text = Loc.Instance["Error_Internal"];
        if (MainWindow != null)
            System.Windows.MessageBox.Show(MainWindow, text, Loc.Instance["AppName"]);
    }
}
