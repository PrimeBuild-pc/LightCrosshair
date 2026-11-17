using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LightCrosshair.UI.Core;
using LightCrosshair.UI.Localization;
using LightCrosshair.UI.Services;
using LightCrosshair.UI.ViewModels;
using Xunit;

namespace LightCrosshair.UI.Tests;

[Collection("UiSta")]
public class ReleaseBlockerUiTests
{
    [Fact]
    public async Task FpsOverlayHotkeyIsARowWithAltFAndJoinsConflictChecks()
    {
        var core = new FakeCoreClient(new InlineMarshaler()); await core.ConnectAsync();
        var hotkeys = new HotkeysViewModel(core, new FakeUi()); hotkeys.Apply(core.Snapshot!);
        var row = hotkeys.Rows.Single(item => item.Action == "ToggleFpsOverlay");
        Assert.False(row.AllowsHold); Assert.True(row.AllowWheel);
        Assert.Equal("F", row.Binding.Key); Assert.Equal(KeyModifiers.Alt, row.Binding.Modifiers);
        var next = hotkeys.Rows.Single(item => item.Action == "NextCrosshair");
        await hotkeys.CommitCommand.ExecuteAsync(new HotkeyCommit { Action = "NextCrosshair", Binding = new() { Key = "F", Modifiers = KeyModifiers.Alt } });
        Assert.Contains(Loc.Instance["Hotkeys_ToggleFps"], next.Warning);
        Assert.Equal("C", core.Snapshot!.Store.Settings.Hotkeys.NextCrosshair.Key);
        await hotkeys.CommitCommand.ExecuteAsync(new HotkeyCommit { Action = "ToggleFpsOverlay", Binding = new() { Key = "MiddleClick", Mode = BindingMode.HoldToHide } });
        Assert.Equal("MiddleClick", core.Snapshot.Store.Settings.Hotkeys.ToggleFpsOverlay.Key);
        Assert.Equal(BindingMode.Toggle, core.Snapshot.Store.Settings.Hotkeys.ToggleFpsOverlay.Mode);
    }

    [Fact]
    public async Task GameBindingOverridesAreThreeChoicesThatSaveImmediately()
    {
        var core = new FakeCoreClient(new InlineMarshaler()); await core.ConnectAsync();
        var games = new GamesViewModel(core, new FakeUi()); games.Apply(core.Snapshot!);
        string id = games.Selected!.Id;
        GameProfile Stored() => core.Snapshot!.Store.GameProfiles.Single(p => p.Id == id);
        Assert.Equal("follow", games.VisibilityPolicy); Assert.False(games.HasOwnVisibility);
        Assert.Contains("X", games.GlobalVisibilityText);
        games.VisibilityPolicy = "off";
        Assert.Equal("", Stored().VisibilityBinding!.Key);
        games.VisibilityPolicy = "custom";
        Assert.True(games.HasOwnVisibility); Assert.Equal("X", Stored().VisibilityBinding!.Key);
        games.CommitVisibilityCommand.Execute(new HotkeyCommit { Action = "VisibilityBinding", Binding = new() { Key = "Mouse4" } });
        Assert.Equal("Mouse4", Stored().VisibilityBinding!.Key);
        games.VisibilityPolicy = "follow";
        Assert.Null(Stored().VisibilityBinding);
        // An unrelated unsaved edit stays unsaved while a binding override is stored.
        games.Name = "Renamed but not saved";
        games.HoldPolicy = "custom";
        Assert.Equal("RightClick", Stored().HoldVisibility!.Key);
        Assert.NotEqual("Renamed but not saved", Stored().Name);
        Assert.True(games.IsDirty);
    }

    [Fact]
    public async Task GraphColorFollowsTheTextColorUntilItsOwnColorIsChosen()
    {
        var core = new FakeCoreClient(new InlineMarshaler());
        var main = new MainViewModel(core, new FakeUi(), LaunchOptions.Parse(["--fake-core"])); await main.StartAsync();
        var settings = main.Settings;
        Assert.False(settings.UseGraphColor); Assert.Equal(settings.TextColor, settings.GraphColor);
        settings.UseGraphColor = true; settings.GraphColor = "#3DDC97"; await settings.FlushAsync();
        Assert.Equal("#3DDC97", core.Snapshot!.Store.Settings.FpsOverlay.GraphColor, ignoreCase: true);
        settings.UseGraphColor = false; await settings.FlushAsync();
        Assert.Null(core.Snapshot.Store.Settings.FpsOverlay.GraphColor);
    }

    [Fact]
    public async Task CloseButtonParksByDefaultAndExitsEverythingWhenChosen()
    {
        var root = Path.Combine(Path.GetTempPath(), "lc-ui-close-" + Guid.NewGuid().ToString("N"));
        var previousRoot = Environment.GetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT");
        var previousPriority = Process.GetCurrentProcess().PriorityClass;
        Environment.SetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT", root);
        var core = new FakeCoreClient(new InlineMarshaler());
        await core.ConnectAsync();
        try
        {
            UiSta.Run(() =>
            {
                var main = new MainViewModel(core, new FakeUi(), new LaunchOptions(UiPages.Library, null, true, null));
                Assert.True(main.Settings.CloseKeepsRunning);
                bool shutdown = false;
                main.Settings.ShutdownApp = () => { shutdown = true; main.AllowExit = true; };
                var window = new MainWindow { DataContext = main, ShowInTaskbar = false };
                var previousMain = Application.Current.MainWindow;
                Application.Current.MainWindow = window;
                window.Show();
                try
                {
                    main.StartAsync().GetAwaiter().GetResult();
                    main.Settings.CloseButtonExits = true;
                    Pump(TimeSpan.FromMilliseconds(400)); // settings save is debounced on this dispatcher
                    Assert.True(core.Snapshot!.Store.Settings.CloseButtonExits);
                    window.Close();
                    Pump(TimeSpan.FromMilliseconds(300));
                    Assert.True(shutdown);
                    Assert.Equal(CoreLinkState.Idle, core.State); // quit was sent to Core
                }
                finally
                {
                    if (window.IsLoaded) { main.AllowExit = true; window.Close(); }
                    Application.Current.MainWindow = previousMain;
                    try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal; } catch (Exception) { }
                }
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT", previousRoot);
            try { Process.GetCurrentProcess().PriorityClass = previousPriority; } catch (Exception) { }
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task DialogFocusesTheSafeChoiceAndEscapeCancels()
    {
        var host = new DialogHost();
        var confirm = host.ShowAsync("Delete", "Sure?", [("Delete", false, true), ("Cancel", false, false)], cancelIndex: 1);
        Assert.Equal(1, host.FocusIndex);
        host.Cancel();
        Assert.Equal(1, await confirm); Assert.False(host.IsOpen);
        var unsaved = host.ShowAsync("Unsaved", "?", [("Save", true, false), ("Discard", false, false), ("Cancel", false, false)], cancelIndex: 2);
        Assert.Equal(0, host.FocusIndex);
        host.Cancel(); Assert.Equal(2, await unsaved);
        var ok = new AppUi(new InlineMarshaler()).NotifyAsync("LightCrosshair", "Something failed");
        Assert.False(ok.IsCompleted);
    }

    [Fact]
    public void DialogInTheWindowTakesKeyboardFocusAndEscape()
    {
        var root = Path.Combine(Path.GetTempPath(), "lc-ui-dialog-" + Guid.NewGuid().ToString("N"));
        var previousRoot = Environment.GetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT");
        Environment.SetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT", root);
        try
        {
            UiSta.Run(() =>
            {
                var ui = new AppUi(new InlineMarshaler());
                var main = new MainViewModel(new FakeCoreClient(new InlineMarshaler()), ui, new LaunchOptions(UiPages.Library, null, true, null)) { AllowExit = true };
                var window = new MainWindow { DataContext = main, ShowInTaskbar = false };
                window.Show();
                try
                {
                    var answer = ui.ConfirmAsync("Delete", "Sure?", "Delete");
                    Pump(TimeSpan.FromMilliseconds(200));
                    var focused = Assert.IsAssignableFrom<Button>(Keyboard.FocusedElement);
                    Assert.Equal(Loc.Instance["Cancel"], focused.Content);
                    focused.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(focused)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                    Pump(TimeSpan.FromMilliseconds(50));
                    Assert.True(answer.IsCompletedSuccessfully); Assert.False(answer.GetAwaiter().GetResult());
                }
                finally { window.Close(); }
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT", previousRoot);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void NoWindowsMessageBoxesAnywhere()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "LightCrosshair.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var offenders = new[] { "LightCrosshair", "LightCrosshair.UI" }
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(dir!.FullName, project), "*.*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".xaml", StringComparison.Ordinal))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Where(path => File.ReadAllText(path).Contains("MessageBox", StringComparison.Ordinal) || File.ReadAllText(path).Contains("TaskDialog", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(offenders);
    }

    private static void Pump(TimeSpan time)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = time };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
