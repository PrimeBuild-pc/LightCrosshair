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
    public async Task PencilOpensThatCrosshairInStudio()
    {
        var core = new FakeCoreClient(new InlineMarshaler());
        var ui = new FakeUi { UnsavedAnswer = UnsavedChoice.Cancel };
        var main = new MainViewModel(core, ui, LaunchOptions.Parse(["--fake-core", "--page", "library"])); await main.StartAsync();
        Assert.True(main.Library.Mine.Count >= 2);
        var target = main.Library.Mine.Last();
        await main.Library.EditCommand.ExecuteAsync(target);
        Assert.Equal(UiPages.Studio, main.Page);
        Assert.Equal(target.Design.Id, main.Studio.Current!.Id);
        // With unsaved Studio changes, Cancel keeps the current design and page.
        main.Studio.DesignName = "edited"; Assert.True(main.Studio.IsDirty);
        await main.NavigateCommand.ExecuteAsync(UiPages.Library);
        Assert.Equal(UiPages.Studio, main.Page);
        await main.Library.EditCommand.ExecuteAsync(main.Library.Mine.First());
        Assert.Equal(target.Design.Id, main.Studio.Current!.Id); Assert.Equal("edited", main.Studio.DesignName);
    }

    [Fact]
    public void ColorSwatchesAreHighContrastCrosshairColors()
    {
        Assert.Equal(12, LightCrosshair.UI.Controls.ColorField.Presets.Length);
        foreach (var hex in new[] { "#00FF00", "#FFFF00", "#00FFFF", "#FF00FF", "#FF0000", "#0096FF", "#FFFFFF", "#000000" })
            Assert.Contains(hex, LightCrosshair.UI.Controls.ColorField.Presets);
    }

    [Theory]
    [InlineData(1.0)] [InlineData(1.25)] [InlineData(1.5)]
    public void LibraryThumbnailsKeepTheRealPixelsAtScreenDpi(double dpi)
    {
        static string Png(Action<uint[]> draw)
        {
            var pixels = new uint[256 * 256]; draw(pixels);
            var source = System.Windows.Media.Imaging.BitmapSource.Create(256, 256, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null, pixels, 256 * 4);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
            using var stream = new MemoryStream(); encoder.Save(stream); return Convert.ToBase64String(stream.ToArray());
        }
        static uint[] Read(string png, out System.Windows.Media.Imaging.BitmapFrame frame)
        {
            frame = System.Windows.Media.Imaging.BitmapDecoder.Create(new MemoryStream(Convert.FromBase64String(png)), System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames[0];
            var pixels = new uint[frame.PixelWidth * frame.PixelHeight]; frame.CopyPixels(pixels, frame.PixelWidth * 4, 0); return pixels;
        }
        // A 2 px cross with a 4 px gap, as Core renders it: thumbnail = the same pixels, 1:1, centered.
        string cross = Png(p => { for (int i = 0; i < 10; i++) foreach (int k in new[] { 128, 129 }) { p[k * 256 + 124 - i] = p[k * 256 + 133 + i] = p[(124 - i) * 256 + k] = p[(133 + i) * 256 + k] = 0xFF00FF00; } });
        int box = (int)Math.Round(72 * dpi);
        var thumb = Read(PixelThumb.Fit(cross, box, dpi), out var frame);
        Assert.Equal(box, frame.PixelWidth); Assert.InRange(Math.Abs(frame.DpiX - 96 * dpi), 0, 0.05);
        Assert.Equal(80, thumb.Count(p => p == 0xFF00FF00));
        Assert.Equal(0, thumb.Count(p => p != 0 && p != 0xFF00FF00));
        for (int y = 0; y < box; y++) for (int x = 0; x < box; x++) Assert.Equal(thumb[y * box + x], thumb[y * box + (box - 1 - x)]);
        // Larger than the card: reduced by a whole factor so it still fits.
        var large = Read(PixelThumb.Fit(Png(p => { for (int y = 20; y < 236; y++) for (int x = 20; x < 236; x++) p[y * 256 + x] = 0xFFFFFFFF; }), box, dpi), out _);
        Assert.Equal(0u, large[0]); Assert.Equal(0xFFFFFFFF, large[(box / 2) * box + box / 2]);
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
    [Fact]
    public async Task GlobalShowHideCanReuseTheChordOfAGamesOwnShowHide()
    {
        // Reported: global show/hide was Alt+Right click, a game had its own Alt+X, and Alt+X could not be set back.
        var core = new FakeCoreClient(new InlineMarshaler()); await core.ConnectAsync();
        var game = core.Snapshot!.Store.GameProfiles[0];
        game.VisibilityBinding = new() { Key = "X", Modifiers = KeyModifiers.Alt };
        await core.SaveGameProfileAsync(game);
        var hotkeys = new HotkeysViewModel(core, new FakeUi()); hotkeys.Apply(core.Snapshot!);
        await hotkeys.CommitCommand.ExecuteAsync(new HotkeyCommit { Action = "ToggleVisibility", Binding = new() { Key = "RightClick", Modifiers = KeyModifiers.Alt, Mode = BindingMode.HoldToHide } });
        Assert.Equal(BindingMode.Toggle, core.Snapshot!.Store.Settings.Hotkeys.ToggleVisibility.Mode);
        await hotkeys.CommitCommand.ExecuteAsync(new HotkeyCommit { Action = "ToggleVisibility", Binding = new() { Key = "X", Modifiers = KeyModifiers.Alt } });
        var row = hotkeys.Rows.Single(item => item.Action == "ToggleVisibility");
        Assert.Equal("", row.Warning);
        Assert.Equal("X", core.Snapshot!.Store.Settings.Hotkeys.ToggleVisibility.Key);
        Assert.False(row.AllowsHold);
        // Other actions still may not take a chord a game uses for show/hide.
        await hotkeys.CommitCommand.ExecuteAsync(new HotkeyCommit { Action = "NextCrosshair", Binding = new() { Key = "X", Modifiers = KeyModifiers.Alt } });
        Assert.Equal("C", core.Snapshot!.Store.Settings.Hotkeys.NextCrosshair.Key);
    }
}
