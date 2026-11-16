using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightCrosshair.UI.Core;
using LightCrosshair.UI.Localization;
using LightCrosshair.UI.Services;

namespace LightCrosshair.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ICoreClient _core;
    private readonly IUiServices _ui;
    private bool _suspend;
    private bool _visible = true;

    public MainViewModel(ICoreClient core, IUiServices ui, LaunchOptions options)
    {
        _core = core;
        _ui = ui;
        Options = options;
        Studio = new StudioViewModel(core, ui);
        Library = new LibraryViewModel(core, ui, Studio);
        Games = new GamesViewModel(core, ui);
        Hotkeys = new HotkeysViewModel(core, ui);
        Settings = new SettingsViewModel(core, ui);
        Library.GameCrosshairChanged = Games.NoteExternalCrosshair;
        Games.NoteLaunchSuggest(options.Suggest);
        Page = options.Page;
        _core.StateChanged += (_, state) =>
        {
            LinkState = state;
            OnPropertyChanged(nameof(StatusKey));
            OnPropertyChanged(nameof(IsPageEnabled));
            OnPropertyChanged(nameof(ShowBlock));
            OnPropertyChanged(nameof(CanRetry));
            OnPropertyChanged(nameof(BlockKey));
        };
        _core.SnapshotChanged += (_, snapshot) => ApplySnapshot(snapshot);
        _core.RuntimeChanged += (_, runtime) =>
        {
            Hotkeys.ApplyRuntime(runtime);
            Games.ApplyRuntime(runtime);
            Library.NoteActive(string.IsNullOrEmpty(runtime.ActiveCrosshairId) ? _core.Snapshot?.Store.Settings.DefaultCrosshairId : runtime.ActiveCrosshairId);
            if (_core.Snapshot != null && (runtime.PendingSuggestion != null || Games.Suggestion != null))
                Games.Apply(_core.Snapshot);
        };
        _core.FocusRequested += (_, navigation) =>
        {
            if (navigation != null)
            {
                Games.NoteLaunchSuggest(navigation.SuggestProcess);
                _ = NavigateAsync(string.IsNullOrEmpty(navigation.Page) ? UiPages.Studio : navigation.Page);
            }
            ActivateRequested?.Invoke(this, EventArgs.Empty);
        };
        _core.CloseRequested += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        Loc.Instance.Changed += (_, _) => OnPropertyChanged(nameof(BlockKey));
    }

    public LaunchOptions Options { get; }
    public IUiServices Ui => _ui;
    public StudioViewModel Studio { get; }
    public LibraryViewModel Library { get; }
    public GamesViewModel Games { get; }
    public HotkeysViewModel Hotkeys { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty] private string _page = UiPages.Studio;
    [ObservableProperty] private CoreLinkState _linkState = CoreLinkState.Connecting;

    public object CurrentPage => Page switch
    {
        UiPages.Library => Library,
        UiPages.Games => Games,
        UiPages.Hotkeys => Hotkeys,
        UiPages.Settings => Settings,
        _ => Studio,
    };

    public string StatusKey => LinkState switch
    {
        CoreLinkState.Connected => "Status_Connected",
        CoreLinkState.Reconnecting or CoreLinkState.Connecting => "Status_Reconnecting",
        _ => "Status_Offline",
    };

    public bool IsPageEnabled => LinkState == CoreLinkState.Connected;
    public string BlockKey => LinkState == CoreLinkState.ProtocolMismatch ? "Error_ProtocolMismatch" : "Error_CoreMissing";
    public bool ShowBlock => LinkState is CoreLinkState.Failed or CoreLinkState.ProtocolMismatch;
    public bool CanRetry => LinkState == CoreLinkState.Failed;

    public bool CrosshairVisible
    {
        get => _visible;
        set
        {
            if (_visible == value) return;
            _visible = value;
            OnPropertyChanged();
            if (!_suspend) _ = PersistVisibilityAsync();
        }
    }

    public event EventHandler? ActivateRequested;
    public event EventHandler? CloseRequested;
    public bool AllowExit { get; set; }

    public void Park()
    {
        _ = Studio.LeaveAsync();
        Library.ReleaseCaches();
        SetMinimized(true);
        ProcessDuty.Apply(ParkPolicy.Priority(parked: true, gameForeground: false));
        ProcessDuty.TrimWorkingSet();
        OpenTiming.NoteParkedMemory();
    }

    public void Wake()
    {
        SetMinimized(false);
        ProcessDuty.Apply(ParkPolicy.Priority(parked: false, ParkPolicy.GameIsForeground(_core.Snapshot)));
    }

    public async Task StartAsync()
    {
        await _core.ConnectAsync().ConfigureAwait(true);
        LinkState = _core.State;
        if (_core.Snapshot != null) ApplySnapshot(_core.Snapshot);
        if (LinkState == CoreLinkState.Connected)
        {
            await Library.LoadPresetsAsync().ConfigureAwait(true);
            Games.SetPresetPicks(Library.AllPresets);
        }
        if (Page == UiPages.Studio) Studio.Enter();
        OnPropertyChanged(nameof(ShowBlock));
        OnPropertyChanged(nameof(CanRetry));
        OnPropertyChanged(nameof(IsPageEnabled));
        OnPropertyChanged(nameof(StatusKey));
    }

    [RelayCommand]
    private async Task NavigateAsync(string? page)
    {
        if (string.IsNullOrEmpty(page) || page == Page) return;
        if (Page == UiPages.Studio && Studio.IsDirty)
        {
            var choice = await _ui.AskUnsavedAsync().ConfigureAwait(true);
            if (choice == UnsavedChoice.Cancel) return;
            if (choice == UnsavedChoice.Save && !await Studio.SaveCoreAsync(newId: false).ConfigureAwait(true)) return;
            if (choice == UnsavedChoice.Discard) Studio.Revert();
        }
        if (Page == UiPages.Studio) await Studio.LeaveAsync().ConfigureAwait(true);
        Page = page;
        OnPropertyChanged(nameof(CurrentPage));
        if (page == UiPages.Studio) Studio.Enter();
    }

    [RelayCommand]
    private async Task RetryAsync()
    {
        LinkState = CoreLinkState.Connecting;
        OnPropertyChanged(nameof(ShowBlock));
        await StartAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private Task SaveCurrentAsync() => Page == UiPages.Studio ? Studio.SaveCoreAsync(false) : Task.CompletedTask;

    [RelayCommand]
    private void UndoCurrent()
    {
        if (Page == UiPages.Studio) Studio.UndoCommand.Execute(null);
    }

    [RelayCommand]
    private void RedoCurrent()
    {
        if (Page == UiPages.Studio) Studio.RedoCommand.Execute(null);
    }

    public void SetMinimized(bool minimized)
    {
        Studio.SetMinimized(minimized);
        Library.SetMinimized(minimized);
    }

    private void ApplySnapshot(Snapshot snapshot)
    {
        _suspend = true;
        _visible = snapshot.Store.Settings.CrosshairVisible;
        OnPropertyChanged(nameof(CrosshairVisible));
        _suspend = false;
        Loc.Instance.Apply(snapshot.Store.Settings.Language);
        var active = snapshot.Store.Crosshairs.FirstOrDefault(item => item.Id == snapshot.Store.Settings.DefaultCrosshairId)
            ?? snapshot.Store.Crosshairs.FirstOrDefault();
        if (active != null && !Studio.IsLoaded)
            Studio.Load(active);
        else
            Studio.OnSnapshot(snapshot);
        Library.Apply(snapshot);
        Games.Apply(snapshot);
        Hotkeys.Apply(snapshot);
        Settings.Apply(snapshot);
    }

    private async Task PersistVisibilityAsync()
    {
        var snapshot = _core.Snapshot;
        if (snapshot == null) return;
        var settings = JsonClone.Clone(snapshot.Store.Settings);
        settings.CrosshairVisible = _visible;
        try { await _core.SaveSettingsAsync(settings).ConfigureAwait(true); }
        catch (CoreException) { /* the next snapshot restores the switch */ }
    }

    partial void OnPageChanged(string value) => OnPropertyChanged(nameof(CurrentPage));
}
