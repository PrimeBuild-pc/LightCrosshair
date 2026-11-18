using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LightCrosshair.UI.Controls;
using LightCrosshair.UI.Core;
using LightCrosshair.UI.Localization;
using LightCrosshair.UI.Services;
using LightCrosshair.UI.ViewModels;
using Xunit;

namespace LightCrosshair.UI.Tests;

[Collection("UiSta")]
public class Review4Tests
{
    [Fact]
    public void FirstOpenCentersOnTheMonitorUnderTheMouse()
    {
        var areas = new[] { new Rect(0, 0, 1920, 1080), new Rect(1920, 0, 1280, 800) };
        var place = WindowPlacer.Place(null, new Point(2000, 400), areas, 1180, 740);
        Assert.False(place.Maximized);
        Assert.Equal(1920 + (1280 - 1180) / 2d, place.Bounds.Left);
        Assert.Equal((800 - 740) / 2d, place.Bounds.Top);
        Assert.Equal(1180, place.Bounds.Width);
        Assert.Equal(740, place.Bounds.Height);

        var shrunk = WindowPlacer.Place(null, new Point(20, 20), [new Rect(0, 0, 1000, 600)], 1180, 740);
        Assert.False(shrunk.Maximized);
        Assert.Equal(new Rect(0, 0, 1000, 600), shrunk.Bounds);
    }

    [Fact]
    public void SavedBoundsAreClampedAndMaximizedOnlyOnAVisibleMonitor()
    {
        var areas = new[] { new Rect(0, 0, 1920, 1080), new Rect(1920, 0, 1280, 1024) };
        var hanging = WindowPlacer.Place(
            new WindowPlacement { Left = 1700, Top = -80, Width = 900, Height = 700 },
            new Point(100, 100),
            areas,
            1180,
            740);
        Assert.False(hanging.Maximized);
        Assert.True(hanging.Bounds.Left >= 1920);
        Assert.True(hanging.Bounds.Right <= 1920 + 1280);
        Assert.True(hanging.Bounds.Top >= 0);
        Assert.True(hanging.Bounds.Bottom <= 1024);

        var maximized = WindowPlacer.Place(
            new WindowPlacement { Left = 40, Top = 40, Width = 800, Height = 600, Maximized = true },
            new Point(10, 10),
            areas,
            1180,
            740);
        Assert.True(maximized.Maximized);

        var missing = WindowPlacer.Place(
            new WindowPlacement { Left = 9000, Top = 9000, Width = 800, Height = 600, Maximized = true },
            new Point(10, 10),
            areas,
            1180,
            740);
        Assert.False(missing.Maximized);
        Assert.True(missing.Bounds.Left >= 0 && missing.Bounds.Right <= 1920);
        Assert.True(missing.Bounds.Top >= 0 && missing.Bounds.Bottom <= 1080);
    }

    [Fact]
    public void MonitorNumbersComeFromTheDeviceName()
    {
        Assert.Equal(1, MonitorLabels.Number(@"\\.\DISPLAY1", 4));
        Assert.Equal(12, MonitorLabels.Number(@"\\.\DISPLAY12", 0));
        Assert.Equal(3, MonitorLabels.Number(@"\\.\DISPLAY", 2));
    }

    [Fact]
    public void MonitorLabelsAndColorErrorsUsePlainWords()
    {
        UiSta.Run(() =>
        {
            Assert.Equal(Loc.Instance.Format("Settings_MonitorName", "DELL S2721DGF", 1) + " · " + Loc.Instance["Settings_Primary"], MonitorLabels.Format("DELL S2721DGF", 1, true));
            Assert.Equal(Loc.Instance.Format("Settings_MonitorName", "LG 27GN800-B", 2), MonitorLabels.Format("LG 27GN800-B", 2, false));
            Assert.Equal(Loc.Instance.Format("Settings_MonitorName", Loc.Instance["Settings_Display"], 3), MonitorLabels.Format("  ", 3, false));
            Assert.Equal(Loc.Instance["Games_ColorApplyFailed"], DisplayColorText.For("apply_failed"));
            Assert.Equal(Loc.Instance["Games_ColorRestoreFailed"], DisplayColorText.For("restore_failed"));
            Assert.Equal(Loc.Instance["Games_ColorUnsupported"], DisplayColorText.For("unsupported"));
            Assert.Equal("", DisplayColorText.For(null));
            Assert.Equal("", DisplayColorText.For("other"));
        });
    }

    [Fact]
    public void ParkLowersPriorityUntilTheWindowIsActiveWithNoGame()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), ParkPolicy.Duration);
        Assert.Equal(ProcessPriorityClass.BelowNormal, ParkPolicy.Priority(parked: true, gameForeground: false));
        Assert.Equal(ProcessPriorityClass.BelowNormal, ParkPolicy.Priority(parked: false, gameForeground: true));
        Assert.Equal(ProcessPriorityClass.Normal, ParkPolicy.Priority(parked: false, gameForeground: false));

        var running = new Snapshot
        {
            Runtime = new RuntimeState { ActiveGameProfileId = "game" },
            Store = new StoreDocument(),
        };
        Assert.True(ParkPolicy.GameIsForeground(running));
        var foreground = new Snapshot
        {
            Runtime = new RuntimeState { ForegroundProcess = "Game.exe" },
            Store = new StoreDocument { GameProfiles = [new GameProfile { ProcessName = "game.exe" }] },
        };
        Assert.True(ParkPolicy.GameIsForeground(foreground));
        Assert.False(ParkPolicy.GameIsForeground(new Snapshot
        {
            Runtime = new RuntimeState { ForegroundProcess = "notepad.exe" },
            Store = new StoreDocument(),
        }));
    }

    [Fact]
    public async Task UsingAPresetSavesItAndASecondUseKeepsTheCopy()
    {
        var core = new FakeCoreClient(new InlineMarshaler());
        await core.ConnectAsync();
        var library = new LibraryViewModel(core, new FakeUi(), new StudioViewModel(core, new FakeUi()));
        core.SnapshotChanged += (_, snapshot) => library.Apply(snapshot);
        library.Apply(core.Snapshot!);
        await library.LoadPresetsAsync();
        var preset = library.AllPresets[0];

        library.BeginUse(preset, null);
        Assert.True(library.UseOpen);
        Assert.True(library.ShowUseName);
        Assert.False(library.ShowUseExisting);
        Assert.Equal(preset.Design.Name, library.UseName);
        library.UseName = "Copied";
        await library.ConfirmUseCommand.ExecuteAsync(null);

        var saved = core.Snapshot!.Store.Crosshairs.Single(item => item.SourcePresetId == preset.Design.Id);
        Assert.Equal("Copied", saved.Name);
        Assert.NotEqual(preset.Design.Id, saved.Id);
        Assert.Equal(saved.Id, core.Snapshot.Store.Settings.DefaultCrosshairId);
        Assert.Equal(saved.Id, core.Snapshot.Runtime.ActiveCrosshairId);
        Assert.False(library.UseOpen);

        library.BeginUse(preset, null);
        Assert.True(library.ShowUseExisting);
        Assert.False(library.ShowUseName);
        var count = core.Snapshot.Store.Crosshairs.Count;
        await library.ConfirmUseCommand.ExecuteAsync(null);
        Assert.Equal(count, core.Snapshot.Store.Crosshairs.Count);
        Assert.Equal(saved.Id, core.Snapshot.Store.Settings.DefaultCrosshairId);

        library.BeginUse(preset, null);
        library.SaveAnotherCopyCommand.Execute(null);
        Assert.True(library.ShowUseName);
        library.UseName = "Second";
        await library.ConfirmUseCommand.ExecuteAsync(null);
        Assert.Equal(2, core.Snapshot.Store.Crosshairs.Count(item => item.SourcePresetId == preset.Design.Id));
    }

    [Fact]
    public async Task APresetChosenForAGameDoesNotChangeTheDefault()
    {
        var core = new FakeCoreClient(new InlineMarshaler());
        await core.ConnectAsync();
        var games = new GamesViewModel(core, new FakeUi());
        var library = new LibraryViewModel(core, new FakeUi(), new StudioViewModel(core, new FakeUi()));
        library.GameCrosshairChanged = games.NoteExternalCrosshair;
        core.SnapshotChanged += (_, snapshot) =>
        {
            library.Apply(snapshot);
            games.Apply(snapshot);
        };
        library.Apply(core.Snapshot!);
        games.Apply(core.Snapshot!);
        await library.LoadPresetsAsync();
        var preset = library.AllPresets[1];
        var profile = games.Profiles[0];
        var previousDefault = core.Snapshot!.Store.Settings.DefaultCrosshairId;

        library.BeginUse(preset, profile.Id);
        library.UseName = "For game";
        await library.ConfirmUseCommand.ExecuteAsync(null);

        var copy = core.Snapshot.Store.Crosshairs.Single(item => item.Name == "For game");
        Assert.Equal(preset.Design.Id, copy.SourcePresetId);
        Assert.Equal(copy.Id, core.Snapshot.Store.GameProfiles.Single(item => item.Id == profile.Id).CrosshairId);
        Assert.Equal(previousDefault, core.Snapshot.Store.Settings.DefaultCrosshairId);
        Assert.Equal(copy.Id, games.CrosshairId);
    }

    [Fact]
    public async Task ClickingASavedCrosshairMakesItActiveAndRenameKeepsTheDesign()
    {
        var core = new FakeCoreClient(new InlineMarshaler());
        await core.ConnectAsync();
        var library = new LibraryViewModel(core, new FakeUi(), new StudioViewModel(core, new FakeUi()));
        core.SnapshotChanged += (_, snapshot) => library.Apply(snapshot);
        library.Apply(core.Snapshot!);
        var card = library.Mine.First(item => item.Design.Id != core.Snapshot!.Store.Settings.DefaultCrosshairId);
        await library.ApplyDefaultCommand.ExecuteAsync(card);
        Assert.Equal(card.Design.Id, core.Snapshot!.Store.Settings.DefaultCrosshairId);
        Assert.Equal(card.Design.Id, core.Snapshot.Runtime.ActiveCrosshairId);
        Assert.Equal(card.Design.Id, library.Mine.Single(item => item.InUse).Design.Id);

        var current = library.Mine.Single(item => item.Design.Id == card.Design.Id);
        current.BeginRename();
        current.RenameText = "Renamed dot";
        await library.CommitRenameCommand.ExecuteAsync(current);
        Assert.Contains(core.Snapshot.Store.Crosshairs, item => item.Id == card.Design.Id && item.Name == "Renamed dot");
    }

    [Fact]
    public async Task ChoosingASavedCrosshairForAGameSavesThatProfile()
    {
        var core = new FakeCoreClient(new InlineMarshaler());
        await core.ConnectAsync();
        var games = new GamesViewModel(core, new FakeUi());
        games.Apply(core.Snapshot!);
        var other = core.Snapshot!.Store.Crosshairs.First(item => item.Id != games.CrosshairId).Id;
        games.CrosshairId = other;
        Assert.Equal(other, core.Snapshot.Store.GameProfiles[0].CrosshairId);
        Assert.False(games.IsDirty);
    }

    [Fact]
    public async Task TheMouseMenuPicksAPlainButtonAndForcesTheWheelToToggle()
    {
        var core = new FakeCoreClient(new InlineMarshaler());
        await core.ConnectAsync();
        var hotkeys = new HotkeysViewModel(core, new FakeUi());
        hotkeys.Apply(core.Snapshot!);
        var row = hotkeys.Rows.First(item => item.Action == "HoldVisibility");
        row.Binding = new LightCrosshair.Contracts.InputBinding { Key = "X", Modifiers = KeyModifiers.Alt | KeyModifiers.Shift, Mode = BindingMode.HoldToShow };

        // The ▾ menu of the single binding control commits through the same path as a capture.
        UiSta.Run(() =>
        {
            var box = new BindingCaptureBox { Binding = row.Binding, CapturedCommand = hotkeys.CommitCommand, CommandParameter = "HoldVisibility" };
            box.PickMouse("Mouse5");
            Assert.Equal("Mouse5", row.Binding.Key);
            Assert.Equal(KeyModifiers.None, row.Binding.Modifiers);
            Assert.Equal(BindingMode.HoldToShow, row.Binding.Mode);
            var toggle = hotkeys.Rows.First(item => item.Action == "ToggleVisibility");
            var toggleBox = new BindingCaptureBox { Binding = toggle.Binding, CapturedCommand = hotkeys.CommitCommand, CommandParameter = "ToggleVisibility" };
            Assert.Equal(["RightClick", "MiddleClick", "Mouse4", "Mouse5", "WheelUp", "WheelDown"], toggleBox.MenuKeys);
            toggleBox.PickMouse("WheelDown");
            Assert.Equal("WheelDown", toggle.Binding.Key);
            Assert.Equal(BindingMode.Toggle, toggle.Binding.Mode);
            Assert.DoesNotContain(new BindingCaptureBox { AllowWheel = false }.MenuKeys, HotkeyRules.IsWheel);
        });
        await Task.CompletedTask;
    }

    [Fact]
    public void CaptureIgnoresTheOpeningClickThenTakesMouseButtonsAndTheWheel()
    {
        UiSta.Run(() =>
        {
            var box = new BindingCaptureBox();
            var window = new Window { Content = box, Width = 240, Height = 80, ShowInTaskbar = false, WindowStyle = WindowStyle.None };
            window.Show();
            try
            {
                box.StartCapture(openingClickHeld: true);
                Assert.Null(box.MouseBinding(MouseButton.Middle));
                Assert.Null(box.MouseBinding(MouseButton.Left));
                Assert.Null(box.WheelBinding(120));
                box.ReleaseOpeningClick();
                Assert.Equal("MiddleClick", box.MouseBinding(MouseButton.Middle));
                Assert.Equal("RightClick", box.MouseBinding(MouseButton.Right));
                Assert.Equal("Mouse4", box.MouseBinding(MouseButton.XButton1));
                Assert.Equal("Mouse5", box.MouseBinding(MouseButton.XButton2));
                Assert.Null(box.MouseBinding(MouseButton.Left));
                Assert.Equal("WheelUp", box.WheelBinding(120));
                Assert.Equal("WheelDown", box.WheelBinding(-120));
                Assert.Null(box.WheelBinding(0));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ClosingTheMainWindowParksAndExitsAfterTheTimer()
    {
        var root = Path.Combine(Path.GetTempPath(), "lc-ui-park-" + Guid.NewGuid().ToString("N"));
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
                var window = new MainWindow { DataContext = main, ShowInTaskbar = false, ParkDuration = TimeSpan.FromMilliseconds(150) };
                var previousMain = Application.Current.MainWindow;
                Application.Current.MainWindow = window;
                window.Show();
                try
                {
                    main.StartAsync().GetAwaiter().GetResult();
                    window.Close();
                    Pump(DispatcherPriority.ApplicationIdle);
                    Assert.False(window.IsVisible);
                    Assert.False(main.AllowExit);
                    Assert.Equal(ProcessPriorityClass.BelowNormal, Process.GetCurrentProcess().PriorityClass);
                    Assert.Null(core.LastPreview);
                    Pump(TimeSpan.FromMilliseconds(500));
                    Assert.True(main.AllowExit);
                    Assert.False(window.IsLoaded);
                }
                finally
                {
                    if (Application.Current != null) Application.Current.MainWindow = previousMain;
                    try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal; }
                    catch (Exception) { }
                }
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT", previousRoot);
            try { Process.GetCurrentProcess().PriorityClass = previousPriority; }
            catch (Exception) { }
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void Pump(DispatcherPriority priority)
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(priority, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static void Pump(TimeSpan time)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = time };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
