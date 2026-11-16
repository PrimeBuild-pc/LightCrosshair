using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightCrosshair.UI.Core;
using LightCrosshair.UI.Localization;
using LightCrosshair.UI.Services;

namespace LightCrosshair.UI.ViewModels;

public sealed class MonitorChoice
{
    public string DeviceName { get; init; } = "";
    public string Label { get; init; } = "";
}

public sealed class LanguageOption
{
    public required string Id { get; init; }
    public required string LabelKey { get; init; }
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ICoreClient _core;
    private readonly IUiServices _ui;
    private readonly IReleaseFeed _feed;
    private readonly TimeSpan _debounce;
    private AppSettings _settings = new();
    private bool _suspend;
    private int _saveGeneration;
    private int _savedGeneration;
    private Task _pending = Task.CompletedTask;

    public SettingsViewModel(ICoreClient core, IUiServices ui, IReleaseFeed? feed = null, TimeSpan? debounce = null)
    {
        _core = core;
        _ui = ui;
        _feed = feed ?? new GitHubReleaseFeed();
        _debounce = debounce ?? TimeSpan.FromMilliseconds(200);
        VersionLabel = AppVersion.Display;
        Loc.Instance.Changed += (_, _) =>
        {
            RebuildMonitorChoices();
            OnPropertyChanged(nameof(VersionText));
        };
    }

    public ObservableCollection<MonitorChoice> MonitorChoices { get; } = new();
    public ObservableCollection<MonitorChoice> FpsMonitorChoices { get; } = new();

    public string VersionText => Loc.Instance.Format("Settings_Version", VersionLabel);

    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new() { Id = "auto", LabelKey = "Settings_LangAuto" },
        new() { Id = "en", LabelKey = "Settings_LangEn" },
        new() { Id = "it", LabelKey = "Settings_LangIt" },
        new() { Id = "es", LabelKey = "Settings_LangEs" },
        new() { Id = "zh-Hans", LabelKey = "Settings_LangZh" },
        new() { Id = "ru", LabelKey = "Settings_LangRu" },
        new() { Id = "ja", LabelKey = "Settings_LangJa" },
    ];

    public string VersionLabel { get; }

    [ObservableProperty] private string _error = "";
    [ObservableProperty] private string _updateMessage = "";
    [ObservableProperty] private bool _checking;
    [ObservableProperty] private IReadOnlyList<MonitorInfo> _monitors = [];

    public string Language { get => _settings.Language; set { if (_settings.Language == value) return; _settings.Language = value; Loc.Instance.Apply(value); OnPropertyChanged(); Touch(); } }
    public string? MonitorDeviceName
    {
        get => _settings.MonitorDeviceName ?? Monitors.FirstOrDefault(item => item.IsPrimary)?.DeviceName ?? "";
        set
        {
            var primary = Monitors.FirstOrDefault(item => item.IsPrimary)?.DeviceName;
            var next = string.IsNullOrEmpty(value) || value == primary ? null : value;
            if (_settings.MonitorDeviceName == next) return;
            _settings.MonitorDeviceName = next;
            OnPropertyChanged();
            Touch();
        }
    }
    public int OffsetX { get => _settings.OffsetX; set { if (_settings.OffsetX == value) return; _settings.OffsetX = value; OnPropertyChanged(); Touch(); } }
    public int OffsetY { get => _settings.OffsetY; set { if (_settings.OffsetY == value) return; _settings.OffsetY = value; OnPropertyChanged(); Touch(); } }
    public bool ShowOnlyInConfiguredGames { get => _settings.ShowOnlyInConfiguredGames; set { if (_settings.ShowOnlyInConfiguredGames == value) return; _settings.ShowOnlyInConfiguredGames = value; OnPropertyChanged(); Touch(); } }
    public bool HideDuringScreenRecording { get => _settings.HideDuringScreenRecording; set { if (_settings.HideDuringScreenRecording == value) return; _settings.HideDuringScreenRecording = value; OnPropertyChanged(); Touch(); } }
    public bool StartWithWindows { get => _settings.StartWithWindows; set { if (_settings.StartWithWindows == value) return; _settings.StartWithWindows = value; OnPropertyChanged(); Touch(); } }
    public bool SuggestKnownGames { get => _settings.SuggestKnownGames; set { if (_settings.SuggestKnownGames == value) return; _settings.SuggestKnownGames = value; OnPropertyChanged(); Touch(); } }
    public bool FpsEnabled { get => _settings.FpsOverlay.Enabled; set { if (_settings.FpsOverlay.Enabled == value) return; _settings.FpsOverlay.Enabled = value; OnPropertyChanged(); Touch(); } }
    public string FpsMonitor { get => _settings.FpsOverlay.MonitorDeviceName ?? ""; set { var next = string.IsNullOrEmpty(value) ? null : value; if (_settings.FpsOverlay.MonitorDeviceName == next) return; _settings.FpsOverlay.MonitorDeviceName = next; OnPropertyChanged(); Touch(); } }
    public int FpsOffsetX { get => _settings.FpsOverlay.OffsetX; set { if (_settings.FpsOverlay.OffsetX == value) return; _settings.FpsOverlay.OffsetX = value; OnPropertyChanged(); Touch(); } }
    public int FpsOffsetY { get => _settings.FpsOverlay.OffsetY; set { if (_settings.FpsOverlay.OffsetY == value) return; _settings.FpsOverlay.OffsetY = value; OnPropertyChanged(); Touch(); } }
    public double FpsScale { get => _settings.FpsOverlay.Scale; set { if (_settings.FpsOverlay.Scale == value) return; _settings.FpsOverlay.Scale = value; OnPropertyChanged(); Touch(); } }
    public bool ShowFrametime { get => _settings.FpsOverlay.ShowFrametime; set { if (_settings.FpsOverlay.ShowFrametime == value) return; _settings.FpsOverlay.ShowFrametime = value; OnPropertyChanged(); Touch(); } }
    public bool Show1PercentLow { get => _settings.FpsOverlay.Show1PercentLow; set { if (_settings.FpsOverlay.Show1PercentLow == value) return; _settings.FpsOverlay.Show1PercentLow = value; OnPropertyChanged(); Touch(); } }
    public string TextColor { get => _settings.FpsOverlay.TextColor; set => SetColor(value, color => _settings.FpsOverlay.TextColor = color, nameof(TextColor), () => _settings.FpsOverlay.TextColor); }
    public string BackgroundColor { get => _settings.FpsOverlay.BackgroundColor; set => SetColor(value, color => _settings.FpsOverlay.BackgroundColor = color, nameof(BackgroundColor), () => _settings.FpsOverlay.BackgroundColor); }
    public double BackgroundOpacityPercent { get => _settings.FpsOverlay.BackgroundOpacity * 100; set { var next = Math.Clamp(value, 0, 100) / 100.0; if (_settings.FpsOverlay.BackgroundOpacity == next) return; _settings.FpsOverlay.BackgroundOpacity = next; OnPropertyChanged(); Touch(); } }
    public bool CheckForUpdates { get => _settings.CheckForUpdates; set { if (_settings.CheckForUpdates == value) return; _settings.CheckForUpdates = value; OnPropertyChanged(); Touch(); } }

    public int MonitorWidth => Monitors.FirstOrDefault(item => item.DeviceName == _settings.MonitorDeviceName)?.Width
        ?? Monitors.FirstOrDefault(item => item.IsPrimary)?.Width ?? 1920;
    public int MonitorHeight => Monitors.FirstOrDefault(item => item.DeviceName == _settings.MonitorDeviceName)?.Height
        ?? Monitors.FirstOrDefault(item => item.IsPrimary)?.Height ?? 1080;

    public void Apply(Snapshot snapshot)
    {
        if (_saveGeneration != _savedGeneration) return;
        _suspend = true;
        _settings = JsonClone.Clone(snapshot.Store.Settings);
        Monitors = snapshot.Runtime.Monitors;
        RebuildMonitorChoices();
        Loc.Instance.Apply(_settings.Language);
        NotifyAll();
        _suspend = false;
    }

    public Task FlushAsync() => _pending;

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        if (Checking) return;
        Checking = true;
        UpdateMessage = Loc.Instance["Settings_Checking"];
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var latest = await _feed.GetLatestTagAsync(timeout.Token).ConfigureAwait(true);
            var current = AppVersion.Display;
            if (latest == null)
            {
                UpdateMessage = Loc.Instance["Settings_CheckFailed"];
                return;
            }
            var comparison = UpdateVersionComparer.CompareVersions(latest, current);
            if (comparison == null)
                UpdateMessage = Loc.Instance.Format("Settings_CompareFailed", latest, current);
            else if (comparison > 0)
            {
                UpdateMessage = Loc.Instance.Format("Settings_UpdateAvailable", latest);
                _ui.OpenUrl(GitHubReleaseFeed.ReleasesUrl);
            }
            else if (comparison == 0)
                UpdateMessage = Loc.Instance.Format("Settings_UpToDate", current);
            else
                UpdateMessage = Loc.Instance.Format("Settings_LocalNewer", current, latest);
        }
        catch (OperationCanceledException) { UpdateMessage = Loc.Instance["Settings_CheckFailed"]; }
        catch (Exception) { UpdateMessage = Loc.Instance["Settings_CheckFailed"]; }
        finally { Checking = false; }
    }

    [RelayCommand]
    private async Task ExitAsync()
    {
        var ok = await _ui.ConfirmAsync(Loc.Instance["Exit_Title"], Loc.Instance["Exit_Message"], Loc.Instance["Settings_Exit"]).ConfigureAwait(true);
        if (!ok) return;
        try { await _core.QuitAsync().ConfigureAwait(true); }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); return; }
        catch (Exception) { Error = ErrorText.For(null); return; }
        System.Windows.Application.Current?.Shutdown();
    }

    [RelayCommand]
    private void OpenWebsite() => _ui.OpenUrl("https://primebuild.website/");

    [RelayCommand]
    private void OpenSource() => _ui.OpenUrl("https://github.com/PrimeBuild-pc/LightCrosshair");

    private void SetColor(string value, Action<string> apply, string propertyName, Func<string> current)
    {
        var hex = StudioViewModel.NormalizeHex(value);
        if (hex == null || hex == current()) return;
        apply(hex);
        OnPropertyChanged(propertyName);
        Touch();
    }

    private void Touch()
    {
        if (_suspend) return;
        var generation = ++_saveGeneration;
        _pending = SaveAsync(generation);
    }

    private async Task SaveAsync(int generation)
    {
        try
        {
            if (_debounce > TimeSpan.Zero)
                await Task.Delay(_debounce).ConfigureAwait(true);
            if (generation != _saveGeneration) return;
            var saved = await _core.SaveSettingsAsync(JsonClone.Clone(_settings)).ConfigureAwait(true);
            _savedGeneration = generation;
            if (generation == _saveGeneration)
            {
                _suspend = true;
                _settings = saved;
                NotifyAll();
                _suspend = false;
            }
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    private void RebuildMonitorChoices()
    {
        MonitorChoices.Clear();
        foreach (var monitor in Monitors)
            MonitorChoices.Add(new MonitorChoice { DeviceName = monitor.DeviceName, Label = monitor.FriendlyName });
        FpsMonitorChoices.Clear();
        FpsMonitorChoices.Add(new MonitorChoice { DeviceName = "", Label = Loc.Instance["Settings_SameMonitor"] });
        foreach (var monitor in Monitors)
            FpsMonitorChoices.Add(new MonitorChoice { DeviceName = monitor.DeviceName, Label = monitor.FriendlyName });
    }

    private void NotifyAll()
    {
        foreach (var name in new[]
        {
            nameof(Language), nameof(MonitorDeviceName), nameof(OffsetX), nameof(OffsetY), nameof(ShowOnlyInConfiguredGames),
            nameof(HideDuringScreenRecording), nameof(StartWithWindows), nameof(SuggestKnownGames), nameof(FpsEnabled),
            nameof(FpsMonitor), nameof(FpsOffsetX), nameof(FpsOffsetY), nameof(FpsScale), nameof(ShowFrametime),
            nameof(Show1PercentLow), nameof(TextColor), nameof(BackgroundColor), nameof(BackgroundOpacityPercent),
            nameof(CheckForUpdates), nameof(MonitorWidth), nameof(MonitorHeight),
        })
            OnPropertyChanged(name);
    }
}

public static class AppVersion
{
    public static string Display
    {
        get
        {
            var version = typeof(AppVersion).Assembly.GetName().Version;
            if (version == null) return "2.0.0";
            return new Version(version.Major, version.Minor, version.Build < 0 ? 0 : version.Build).ToString();
        }
    }
}
