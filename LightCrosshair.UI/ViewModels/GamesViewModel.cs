using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightCrosshair.UI.Core;
using LightCrosshair.UI.Localization;
using LightCrosshair.UI.Services;

namespace LightCrosshair.UI.ViewModels;

public sealed partial class GamesViewModel : ObservableObject
{
    private readonly ICoreClient _core;
    private readonly IUiServices _ui;
    private readonly HashSet<string> _notNow = new(StringComparer.OrdinalIgnoreCase);
    private GameProfile _working = new();
    private string _savedJson = "";
    private string? _launchSuggest;
    private bool _suspend;

    public ObservableCollection<GameProfile> Profiles { get; } = new();
    public ObservableCollection<CrosshairDesign> Crosshairs { get; } = new();
    public ObservableCollection<LibraryCard> PresetPicks { get; } = new();
    public ObservableCollection<ProcessEntry> Processes { get; } = new();

    [ObservableProperty] private GameProfile? _selected;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private GameSuggestion? _suggestion;
    [ObservableProperty] private bool _picking;
    [ObservableProperty] private GpuCapabilities _gpu = new();
    [ObservableProperty] private bool _useColors;
    [ObservableProperty] private bool _useCap;
    [ObservableProperty] private bool _customVisibility;
    [ObservableProperty] private string _fpsMode = "follow";
    [ObservableProperty] private string _displayColorsMessage = "";
    [ObservableProperty] private string _holdPolicy = "follow";
    public bool HasOwnHold => HoldPolicy == "custom";
    public InputBinding HoldBinding => _working.HoldVisibility ?? new() { Key = "", Mode = BindingMode.HoldToHide };
    public string HoldMouseKey
    {
        get => HotkeyRules.IsMouse(HoldBinding.Key) ? HoldBinding.Key : "";
        set { if (!_suspend && HasOwnHold && !string.IsNullOrEmpty(value) && value != HoldBinding.Key) CommitHold(new() { Action = "HoldVisibility", Binding = new() { Key = value, Modifiers = HoldBinding.Modifiers, Mode = HoldBinding.Mode } }); }
    }
    public BindingMode HoldMode
    {
        get => HoldBinding.Mode;
        set { if (!_suspend && HasOwnHold && value != BindingMode.Toggle) { _working.HoldVisibility!.Mode = value; Touch(); } }
    }
    partial void OnHoldPolicyChanged(string value)
    {
        OnPropertyChanged(nameof(HasOwnHold));
        if (_suspend) return;
        _working.HoldVisibility = value switch
        {
            "off" => new() { Key = "", Mode = BindingMode.HoldToHide },
            "custom" => _working.HoldVisibility is { Key.Length: > 0 } own ? own : new() { Key = "RightClick", Modifiers = KeyModifiers.None, Mode = BindingMode.HoldToHide },
            _ => null
        };
        Touch();
    }
    [RelayCommand]
    private void CommitHold(HotkeyCommit? commit)
    {
        if (commit is null || Selected is null) return;
        if (HotkeyRules.IsWheel(commit.Binding.Key)) { Error = Loc.Instance["Hotkeys_HoldButtonsOnly"]; return; }
        var copy = HotkeyRules.Copy(commit.Binding);
        if (copy.Mode == BindingMode.Toggle) copy.Mode = BindingMode.HoldToHide;
        _working.HoldVisibility = copy;
        HoldPolicy = "custom";
        Touch();
    }

    public bool GammaSupported => Gpu.SoftwareGamma;
    public bool NvidiaColorSupported => Gpu.NvidiaDigitalVibrance;
    public bool CapSupported => Gpu.NvidiaFpsCap;

    public string Name { get => _working.Name; set { _working.Name = value; Touch(); } }
    public string ProcessName => _working.ProcessName;
    public bool Enabled { get => _working.Enabled; set { _working.Enabled = value; Touch(); } }
    public string CrosshairId
    {
        get => _working.CrosshairId;
        set
        {
            if (_suspend || _working.CrosshairId == value) return;
            _working.CrosshairId = value ?? "";
            Touch();
            _ = PersistCrosshairAsync(Selected?.Id, _working.CrosshairId);
        }
    }
    public int Gamma { get => _working.DisplayColors?.Gamma ?? 100; set { Colors().Gamma = value; Touch(); } }
    public int Contrast { get => _working.DisplayColors?.Contrast ?? 100; set { Colors().Contrast = value; Touch(); } }
    public int Brightness { get => _working.DisplayColors?.Brightness ?? 100; set { Colors().Brightness = value; Touch(); } }
    public int Vibrance { get => _working.DisplayColors?.Vibrance ?? 50; set { Colors().Vibrance = value; Touch(); } }
    public int NvidiaVibrance { get => _working.DisplayColors?.NvidiaDigitalVibrance ?? 50; set { Colors().NvidiaDigitalVibrance = value; Touch(); } }
    public int Cap { get => _working.NvidiaFpsCap ?? 144; set { _working.NvidiaFpsCap = value; Touch(); } }
    public InputBinding Visibility => _working.VisibilityBinding ?? new InputBinding();

    public string SuggestionText => Suggestion == null ? "" : Loc.Instance.Format("Games_Suggest", Suggestion.DisplayName);

    public BindingMode VisibilityMode
    {
        get => _working.VisibilityBinding?.Mode ?? BindingMode.Toggle;
        set
        {
            if (_suspend || _working.VisibilityBinding == null) return;
            var copy = HotkeyRules.Copy(_working.VisibilityBinding);
            if (HotkeyRules.IsWheel(copy.Key) && value != BindingMode.Toggle)
            {
                Error = Loc.Instance["Hotkeys_WheelOnly"];
                OnPropertyChanged(nameof(VisibilityMode));
                return;
            }
            copy.Mode = value;
            ApplyVisibility(copy);
        }
    }

    public GamesViewModel(ICoreClient core, IUiServices ui)
    {
        _core = core;
        _ui = ui;
        Loc.Instance.Changed += (_, _) => OnPropertyChanged(nameof(SuggestionText));
    }

    public void ApplyRuntime(RuntimeState runtime) => DisplayColorsMessage = DisplayColorText.For(runtime.DisplayColorsError);

    public void SetPresetPicks(IEnumerable<LibraryCard> presets)
    {
        PresetPicks.Clear();
        foreach (var preset in presets)
            PresetPicks.Add(preset);
    }

    public void NoteExternalCrosshair(string profileId, string crosshairId)
    {
        if (Selected?.Id != profileId) return;
        _suspend = true;
        _working.CrosshairId = crosshairId;
        OnPropertyChanged(nameof(CrosshairId));
        _suspend = false;
        var previous = SavedProfile();
        if (previous != null)
        {
            previous.CrosshairId = crosshairId;
            _savedJson = JsonClone.Text(previous);
        }
        Touch();
    }

    public void NoteLaunchSuggest(string? process)
    {
        _launchSuggest = string.IsNullOrWhiteSpace(process) ? null : process.Trim().ToLowerInvariant();
    }

    public void Apply(Snapshot snapshot)
    {
        Gpu = snapshot.Runtime.Gpu;
        OnPropertyChanged(nameof(GammaSupported));
        OnPropertyChanged(nameof(NvidiaColorSupported));
        OnPropertyChanged(nameof(CapSupported));
        Profiles.Clear();
        foreach (var profile in snapshot.Store.GameProfiles.OrderBy(item => item.Name))
            Profiles.Add(profile);
        Crosshairs.Clear();
        foreach (var design in snapshot.Store.Crosshairs)
            Crosshairs.Add(design);
        var pending = snapshot.Runtime.PendingSuggestion;
        if (pending != null && (_notNow.Contains(pending.ProcessName) || snapshot.Store.Settings.DismissedSuggestions.Contains(pending.ProcessName, StringComparer.OrdinalIgnoreCase)))
            pending = null;
        if (pending == null && _launchSuggest != null && !_notNow.Contains(_launchSuggest))
            pending = new GameSuggestion { ProcessName = _launchSuggest, DisplayName = _launchSuggest };
        Suggestion = pending;
        DisplayColorsMessage = DisplayColorText.For(snapshot.Runtime.DisplayColorsError);

        if (Selected != null)
        {
            var updated = Profiles.FirstOrDefault(item => item.Id == Selected.Id);
            if (updated == null) Select(Profiles.FirstOrDefault());
            else if (!IsDirty) Select(updated);
            else Selected = updated;
        }
        else Select(Profiles.FirstOrDefault());
    }

    [RelayCommand]
    private async Task SetCaptureAsync(bool active)
    {
        try { await _core.SetInputCaptureAsync(active).ConfigureAwait(true); }
        catch (CoreException) { }
        catch (Exception) { }
    }

    [RelayCommand]
    private void Select(GameProfile? profile)
    {
        if (profile == null)
        {
            Selected = null;
            _working = new GameProfile();
            _savedJson = "";
            IsDirty = false;
            NotifyWorking();
            return;
        }
        _suspend = true;
        Selected = profile;
        _working = JsonClone.Clone(profile);
        _savedJson = JsonClone.Text(_working);
        IsDirty = false;
        UseColors = _working.DisplayColorsEnabled;
        UseCap = _working.NvidiaFpsCap != null;
        CustomVisibility = _working.VisibilityBinding != null;
        HoldPolicy = _working.HoldVisibility is null ? "follow" : string.IsNullOrEmpty(_working.HoldVisibility.Key) ? "off" : "custom";
        FpsMode = _working.FpsOverlay switch { true => "on", false => "off", _ => "follow" };
        _suspend = false;
        NotifyWorking();
    }

    partial void OnUseColorsChanged(bool value)
    {
        if (_suspend || Selected == null) return;
        _working.DisplayColorsEnabled = value;
        if (value) _working.DisplayColors ??= new DisplayColorSettings();
        Touch();
    }

    partial void OnUseCapChanged(bool value)
    {
        if (_suspend || Selected == null) return;
        _working.NvidiaFpsCap = value ? _working.NvidiaFpsCap ?? 144 : null;
        Touch();
    }

    partial void OnCustomVisibilityChanged(bool value)
    {
        if (_suspend || Selected == null) return;
        _working.VisibilityBinding = value ? _working.VisibilityBinding ?? new InputBinding { Key = "X", Modifiers = KeyModifiers.Alt } : null;
        OnPropertyChanged(nameof(Visibility));
        OnPropertyChanged(nameof(HoldBinding));
        OnPropertyChanged(nameof(HoldMode));
        OnPropertyChanged(nameof(HoldMouseKey));
        OnPropertyChanged(nameof(HasOwnHold));
        Touch();
    }

    partial void OnFpsModeChanged(string value)
    {
        if (_suspend || Selected == null) return;
        _working.FpsOverlay = value switch { "on" => true, "off" => false, _ => null };
        Touch();
    }

    [RelayCommand]
    private async Task ToggleEnabledAsync(GameProfile? profile)
    {
        if (profile == null) return;
        var copy = JsonClone.Clone(profile);
        copy.Enabled = !copy.Enabled;
        if (_working.Id == copy.Id) _working.Enabled = copy.Enabled;
        try { await _core.SaveGameProfileAsync(copy).ConfigureAwait(true); }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Selected == null) return;
        if (string.IsNullOrWhiteSpace(_working.Name)) _working.Name = _working.ProcessName;
        try
        {
            var saved = await _core.SaveGameProfileAsync(JsonClone.Clone(_working)).ConfigureAwait(true);
            _savedJson = JsonClone.Text(saved);
            IsDirty = false;
            _ui.Toast(Loc.Instance["Toast_Saved"]);
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Selected == null) return;
        var ok = await _ui.ConfirmAsync(Loc.Instance["DeleteGame_Title"], Loc.Instance.Format("DeleteGame_Message", Selected.Name), Loc.Instance["Delete"]).ConfigureAwait(true);
        if (!ok) return;
        try { await _core.DeleteGameProfileAsync(Selected.Id).ConfigureAwait(true); }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    [RelayCommand]
    private async Task StartAddAsync()
    {
        Picking = true;
        try
        {
            Processes.Clear();
            foreach (var process in await _core.ListProcessesAsync().ConfigureAwait(true))
                Processes.Add(process);
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); Picking = false; }
        catch (Exception) { Error = ErrorText.For(null); Picking = false; }
    }

    [RelayCommand]
    private async Task AddProcessAsync(ProcessEntry? process)
    {
        if (process == null) return;
        Picking = false;
        await CreateAsync(process.ProcessName, string.IsNullOrWhiteSpace(process.WindowTitle) ? process.ProcessName : process.WindowTitle).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task BrowseExeAsync()
    {
        var path = _ui.PickOpenFile(Loc.Instance["File_Exe"]);
        if (path == null) return;
        Picking = false;
        var file = Path.GetFileName(path);
        await CreateAsync(file, Path.GetFileNameWithoutExtension(file)).ConfigureAwait(true);
    }

    [RelayCommand]
    private void CancelPick() => Picking = false;

    [RelayCommand]
    private async Task AcceptSuggestionAsync()
    {
        if (Suggestion == null) return;
        try
        {
            var created = await _core.AcceptSuggestionAsync(Suggestion.ProcessName).ConfigureAwait(true);
            _launchSuggest = null;
            Suggestion = null;
            Select(created);
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    [RelayCommand]
    private void NotNow()
    {
        if (Suggestion == null) return;
        _notNow.Add(Suggestion.ProcessName);
        _launchSuggest = null;
        Suggestion = null;
    }

    [RelayCommand]
    private async Task NeverAsync()
    {
        if (Suggestion == null) return;
        var name = Suggestion.ProcessName;
        try
        {
            await _core.DismissSuggestionAsync(name).ConfigureAwait(true);
            _notNow.Add(name);
            _launchSuggest = null;
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    [RelayCommand]
    private void CommitVisibility(HotkeyCommit? commit)
    {
        if (commit == null || Selected == null) return;
        var binding = HotkeyRules.Copy(commit.Binding);
        binding.Mode = HotkeyRules.IsWheel(binding.Key) ? BindingMode.Toggle : VisibilityMode;
        ApplyVisibility(binding);
    }

    public void ApplyVisibility(InputBinding binding)
    {
        _working.VisibilityBinding = HotkeyRules.Copy(binding);
        CustomVisibility = true;
        OnPropertyChanged(nameof(Visibility));
        Touch();
    }

    private async Task CreateAsync(string processName, string displayName)
    {
        var snapshot = _core.Snapshot;
        if (snapshot == null) return;
        var profile = new GameProfile
        {
            Name = displayName,
            ProcessName = processName,
            CrosshairId = snapshot.Store.Settings.DefaultCrosshairId,
            Enabled = true,
        };
        try
        {
            var saved = await _core.SaveGameProfileAsync(profile).ConfigureAwait(true);
            Select(saved);
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    private async Task PersistCrosshairAsync(string? profileId, string crosshairId)
    {
        if (string.IsNullOrEmpty(profileId)) return;
        var profile = SavedProfile();
        if (profile == null || profile.Id != profileId) return;
        profile.CrosshairId = crosshairId;
        try
        {
            var saved = await _core.SaveGameProfileAsync(profile).ConfigureAwait(true);
            if (Selected?.Id != profileId) return;
            var baseline = SavedProfile();
            if (baseline == null) return;
            baseline.CrosshairId = saved.CrosshairId;
            _savedJson = JsonClone.Text(baseline);
            Touch();
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    private GameProfile? SavedProfile()
    {
        if (string.IsNullOrEmpty(_savedJson)) return null;
        var profile = System.Text.Json.JsonSerializer.Deserialize<GameProfile>(_savedJson, ContractJson.Options);
        return profile == null ? null : JsonClone.Clone(profile);
    }

    private DisplayColorSettings Colors() => _working.DisplayColors ??= new DisplayColorSettings();

    private void Touch()
    {
        if (_suspend || Selected == null) return;
        IsDirty = JsonClone.Text(_working) != _savedJson;
        Error = "";
        NotifyWorking();
    }

    private void NotifyWorking()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ProcessName));
        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(CrosshairId));
        OnPropertyChanged(nameof(Gamma));
        OnPropertyChanged(nameof(Contrast));
        OnPropertyChanged(nameof(Brightness));
        OnPropertyChanged(nameof(Vibrance));
        OnPropertyChanged(nameof(NvidiaVibrance));
        OnPropertyChanged(nameof(Cap));
        OnPropertyChanged(nameof(Visibility));
        OnPropertyChanged(nameof(HoldBinding));
        OnPropertyChanged(nameof(HoldMode));
        OnPropertyChanged(nameof(HoldMouseKey));
        OnPropertyChanged(nameof(HasOwnHold));
        OnPropertyChanged(nameof(VisibilityMode));
        OnPropertyChanged(nameof(SuggestionText));
    }
}
