using LightCrosshair.UI.Core;
using LightCrosshair.UI.Localization;
using LightCrosshair.UI.Services;
using LightCrosshair.UI.ViewModels;
using Xunit;

namespace LightCrosshair.UI.Tests;

public class ViewModelTests
{
    [Fact]
    public async Task StudioEditsUndoAsOneDragAndSaves()
    {
        var core = await ConnectedAsync();
        var studio = new StudioViewModel(core, new FakeUi());
        studio.Load(core.Snapshot!.Store.Crosshairs[0]);
        Assert.Equal(3, studio.Rows.Count);
        var original = studio.DesignName;

        studio.DesignName = "Renamed";
        Assert.True(studio.IsDirty);
        studio.UndoCommand.Execute(null);
        Assert.Equal(original, studio.DesignName);
        studio.RedoCommand.Execute(null);
        Assert.Equal("Renamed", studio.DesignName);

        studio.BeginEditCommand.Execute(null);
        var before = studio.Length;
        studio.Length = before + 4;
        studio.Length = before + 9;
        studio.EndEditCommand.Execute(null);
        studio.UndoCommand.Execute(null);
        Assert.Equal(before, studio.Length);

        studio.MoveDisplay(0, 2);
        Assert.Equal(studio.Rows[2].Layer.Id, studio.Selected!.Layer.Id);

        while (studio.CanAddLayer) studio.AddLayerCommand.Execute("Dot");
        Assert.Equal(ContractInfo.MaxLayers, studio.Rows.Count);
        studio.AddLayerCommand.Execute("Box");
        Assert.Equal(Loc.Instance["Studio_MaxLayers"], studio.Error);

        studio.DesignName = "Saved cross";
        Assert.True(await studio.SaveCoreAsync(false));
        Assert.False(studio.IsDirty);
        Assert.Contains(core.Snapshot.Store.Crosshairs, item => item.Name == "Saved cross");
    }

    [Fact]
    public async Task LivePreviewSendsTheLatestValueAfterTheInterval()
    {
        var core = await ConnectedAsync();
        var clock = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var delay = new GateDelay();
        var studio = new StudioViewModel(core, new FakeUi(), delay, () => clock, TimeSpan.FromMilliseconds(100));
        studio.Load(core.Snapshot!.Store.Crosshairs[0]);
        studio.LiveInGame = true;
        await WaitAsync(() => core.PreviewCount >= 1);
        var sent = core.PreviewCount;

        clock = clock.AddMilliseconds(10);
        studio.BeginEditCommand.Execute(null);
        studio.Length = 40;
        studio.Length = 55;
        await Task.Delay(30);
        Assert.Equal(sent, core.PreviewCount);

        studio.EndEditCommand.Execute(null);
        await WaitAsync(() => core.PreviewCount > sent);
        Assert.Equal(55, core.LastPreview!.Layers.First(layer => layer.Id == studio.Selected!.Layer.Id).Length);
    }

    [Fact]
    public async Task FakeRenderReturnsAPng()
    {
        var core = await ConnectedAsync();
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var png = core.RenderPngAsync(new RenderRequest
                {
                    Design = core.Snapshot!.Store.Crosshairs[0],
                    CanvasSize = 64,
                    Background = "#101010",
                }).GetAwaiter().GetResult();
                var bytes = Convert.FromBase64String(png);
                Assert.Equal(new byte[] { 137, 80, 78, 71 }, bytes.Take(4).ToArray());
                var image = new System.Windows.Media.Imaging.BitmapImage();
                using var stream = new MemoryStream(bytes);
                image.BeginInit();
                image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                Assert.Equal(64, image.PixelWidth);
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.True(error == null, error?.ToString());
    }

    [Fact]
    public async Task GamesToggleSuggestionAndUnsupportedGpu()
    {
        var core = await ConnectedAsync();
        var games = new GamesViewModel(core, new FakeUi());
        games.Apply(core.Snapshot!);
        var profile = games.Profiles[0];
        var enabled = profile.Enabled;
        await games.ToggleEnabledCommand.ExecuteAsync(profile);
        var stored = core.Snapshot!.Store.GameProfiles.Single(item => item.Id == profile.Id);
        Assert.NotEqual(enabled, stored.Enabled);

        games.UseColors = true;
        Assert.True(games.IsDirty);
        await games.SaveCommand.ExecuteAsync(null);
        Assert.NotNull(core.Snapshot.Store.GameProfiles.Single(item => item.Id == profile.Id).DisplayColors);

        var suggestion = new GameSuggestion { ProcessName = "samplegame2.exe", DisplayName = "Sample Game" };
        core.SetSuggestion(suggestion);
        games.Apply(core.Snapshot);
        Assert.Equal("samplegame2.exe", games.Suggestion!.ProcessName);
        games.NotNowCommand.Execute(null);
        Assert.Null(games.Suggestion);
        games.Apply(core.Snapshot);
        Assert.Null(games.Suggestion);

        var later = new GamesViewModel(core, new FakeUi());
        var banned = new GameSuggestion { ProcessName = "other.exe", DisplayName = "Other" };
        core.SetSuggestion(banned);
        later.Apply(core.Snapshot);
        Assert.NotNull(later.Suggestion);
        await later.NeverCommand.ExecuteAsync(null);
        Assert.Contains("other.exe", core.Snapshot.Store.Settings.DismissedSuggestions);
        core.SetSuggestion(banned);
        later.Apply(core.Snapshot);
        Assert.Null(later.Suggestion);

        core.SetGpu(new GpuCapabilities());
        games.Apply(core.Snapshot);
        Assert.False(games.GammaSupported);
        Assert.False(games.NvidiaColorSupported);
        Assert.False(games.CapSupported);
    }

    [Fact]
    public async Task HotkeysReportConflictsAndForceWheelToToggle()
    {
        var core = await ConnectedAsync();
        var hotkeys = new HotkeysViewModel(core, new FakeUi { WindowHandle = 0 });
        hotkeys.Apply(core.Snapshot!);
        var toggle = hotkeys.Rows.First(row => row.Action == "ToggleVisibility").Binding;
        await hotkeys.CommitCommand.ExecuteAsync(new HotkeyCommit
        {
            Action = "NextCrosshair",
            Binding = new InputBinding { Key = toggle.Key, Modifiers = toggle.Modifiers },
        });
        var next = hotkeys.Rows.First(row => row.Action == "NextCrosshair");
        Assert.Equal(Loc.Instance.Format("Hotkeys_Conflict", Loc.Instance["Hotkeys_ToggleVisibility"]), next.Warning);
        Assert.NotEqual(toggle.Key, next.Binding.Key);

        await hotkeys.CommitCommand.ExecuteAsync(new HotkeyCommit
        {
            Action = "ToggleVisibility",
            Binding = new InputBinding { Key = "WheelUp", Modifiers = KeyModifiers.Alt, Mode = BindingMode.HoldToShow },
        });
        var row = hotkeys.Rows.First(item => item.Action == "ToggleVisibility");
        Assert.Equal(BindingMode.Toggle, row.Binding.Mode);
        Assert.Equal("WheelUp", row.Binding.Key);

        await hotkeys.SetModeCommand.ExecuteAsync(new ModeChange { Action = "ToggleVisibility", Mode = BindingMode.HoldToHide });
        Assert.Equal(BindingMode.Toggle, row.Binding.Mode);
        Assert.Equal(Loc.Instance["Hotkeys_WheelOnly"], row.Warning);
    }

    [Fact]
    public async Task SettingsApplyLanguageAndUpdateMessages()
    {
        var core = await ConnectedAsync();
        var ui = new FakeUi();
        var feed = new StubFeed();
        var settings = new SettingsViewModel(core, ui, feed, TimeSpan.Zero);
        try
        {
            settings.Apply(core.Snapshot!);
            settings.Language = "it";
            await settings.FlushAsync();
            Assert.Equal("it", Loc.Instance.Language);
            Assert.Equal("it", core.Snapshot!.Store.Settings.Language);

            feed.Tag = "9.0.0";
            await settings.CheckUpdatesCommand.ExecuteAsync(null);
            Assert.Contains("9.0.0", settings.UpdateMessage);
            Assert.Contains(GitHubReleaseFeed.ReleasesUrl, ui.OpenedUrls);

            feed.Tag = AppVersion.Display;
            await settings.CheckUpdatesCommand.ExecuteAsync(null);
            Assert.Contains(AppVersion.Display, settings.UpdateMessage);
            Assert.Single(ui.OpenedUrls);

            feed.Tag = "not-a-version";
            await settings.CheckUpdatesCommand.ExecuteAsync(null);
            Assert.Contains("not-a-version", settings.UpdateMessage);
        }
        finally
        {
            Loc.Instance.Apply("en");
        }
    }

    private static async Task<FakeCoreClient> ConnectedAsync()
    {
        var core = new FakeCoreClient(new InlineMarshaler());
        await core.ConnectAsync();
        return core;
    }

    private static async Task WaitAsync(Func<bool> ready)
    {
        var start = DateTime.UtcNow;
        while (!ready())
        {
            if (DateTime.UtcNow - start > TimeSpan.FromSeconds(2))
                throw new TimeoutException("condition was not met");
            await Task.Delay(15);
        }
    }

    private sealed class GateDelay : IDelay
    {
        private TaskCompletionSource _next = New();

        public Task Delay(TimeSpan time, CancellationToken cancellationToken) => _next.Task;

        public void Release()
        {
            var current = _next;
            _next = New();
            current.TrySetResult();
        }

        private static TaskCompletionSource New() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class StubFeed : IReleaseFeed
    {
        public string? Tag { get; set; }
        public Task<string?> GetLatestTagAsync(CancellationToken cancellationToken) => Task.FromResult(Tag);
    }
}
