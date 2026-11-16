using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightCrosshair.UI.Core;
using LightCrosshair.UI.Localization;
using LightCrosshair.UI.Services;

namespace LightCrosshair.UI.ViewModels;

public sealed partial class HotkeyRow : ObservableObject
{
    public required string Action { get; init; }
    public required string LabelKey { get; init; }
    public bool AllowsHold { get; init; }
    public bool HoldOnly { get; init; }
    public IReadOnlyList<MouseButtonChoice> MouseChoices { get; set; } = [];
    public void OnMouseChoicesChanged() => OnPropertyChanged(nameof(MouseChoices));
    [ObservableProperty] private InputBinding _binding = new();
    [ObservableProperty] private string _display = "";
    [ObservableProperty] private string _warning = "";
    [ObservableProperty] private string _failure = "";
    [ObservableProperty] private bool _capturing;

    public void RefreshText() => Display = HotkeyRules.Format(Binding);
    public string MouseKey => HotkeyRules.IsMouse(Binding.Key) ? Binding.Key : "";
    partial void OnBindingChanged(InputBinding value) => OnPropertyChanged(nameof(MouseKey));

    public override string ToString() => Loc.Instance[LabelKey];
}

public sealed partial class HotkeysViewModel : ObservableObject
{
    private readonly ICoreClient _core;
    private readonly IUiServices _ui;
    private HotkeySettings _hotkeys = new();

    [RelayCommand]
    private async Task SetCaptureAsync(bool active)
    {
        try { await _core.SetInputCaptureAsync(active).ConfigureAwait(true); }
        catch (CoreException) { }
        catch (Exception) { }
    }

    public HotkeysViewModel(ICoreClient core, IUiServices ui)
    {
        _core = core;
        _ui = ui;
        Rows =
        [
            new HotkeyRow { Action = "ToggleVisibility", LabelKey = "Hotkeys_ToggleVisibility", AllowsHold = true },
            new HotkeyRow { Action = "HoldVisibility", LabelKey = "Hotkeys_WhileHeld", AllowsHold = true, HoldOnly = true },
            new HotkeyRow { Action = "NextCrosshair", LabelKey = "Hotkeys_Next" },
            new HotkeyRow { Action = "PreviousCrosshair", LabelKey = "Hotkeys_Previous" },
            new HotkeyRow { Action = "OpenSettings", LabelKey = "Hotkeys_OpenSettings" },
        ];
        RebuildMouseChoices();
        Loc.Instance.Changed += (_, _) =>
        {
            RebuildMouseChoices();
            foreach (var row in Rows) row.RefreshText();
        };
    }

    public IReadOnlyList<HotkeyRow> Rows { get; }
    public IReadOnlyList<MouseButtonChoice> MouseChoices { get; private set; } = [];

    [ObservableProperty] private string _error = "";

    public void Apply(Snapshot snapshot)
    {
        _hotkeys = JsonClone.Clone(snapshot.Store.Settings.Hotkeys);
        Set("ToggleVisibility", _hotkeys.ToggleVisibility);
        Set("NextCrosshair", _hotkeys.NextCrosshair);
        Set("PreviousCrosshair", _hotkeys.PreviousCrosshair);
        Set("OpenSettings", _hotkeys.OpenSettings);
        Set("HoldVisibility", _hotkeys.HoldVisibility);
        ApplyErrors(snapshot);
    }

    public void ApplyRuntime(RuntimeState runtime) => ApplyErrors(new Snapshot { Runtime = runtime, Store = _core.Snapshot?.Store ?? new StoreDocument() });

    [RelayCommand]
    private async Task CommitAsync(HotkeyCommit? commit)
    {
        if (commit == null) return;
        var row = Rows.First(item => item.Action == commit.Action);
        if (row.HoldOnly && HotkeyRules.IsWheel(commit.Binding.Key)) { row.Warning = Loc.Instance["Hotkeys_HoldButtonsOnly"]; return; }
        var binding = HotkeyRules.Copy(commit.Binding);
        if (row.HoldOnly && binding.Mode == BindingMode.Toggle) binding.Mode = row.Binding.Mode == BindingMode.HoldToShow ? BindingMode.HoldToShow : BindingMode.HoldToHide;
        if (!row.AllowsHold) binding.Mode = BindingMode.Toggle;
        if (HotkeyRules.IsWheel(binding.Key) && binding.Mode != BindingMode.Toggle)
        {
            row.Warning = Loc.Instance["Hotkeys_WheelOnly"];
            return;
        }
        var others = GameBindings();
        var conflict = HotkeyRules.ConflictWith(row.Action, binding, With(row.Action, binding), others);
        if (conflict != null)
        {
            row.Warning = Loc.Instance.Format("Hotkeys_Conflict", Label(conflict));
            row.RefreshText();
            return;
        }
        if (binding.Mode == BindingMode.Toggle && !HotkeyRules.IsMouse(binding.Key) && !string.IsNullOrEmpty(binding.Key) && KeyCodes.TryVirtualKey(binding.Key, out var virtualKey))
        {
            if (!HotkeyProbe.IsFree(_ui.WindowHandle, (int)binding.Modifiers, virtualKey))
            {
                row.Warning = Loc.Instance["Hotkeys_Reserved"];
                return;
            }
        }
        row.Warning = "";
        Assign(row.Action, binding);
        row.Binding = HotkeyRules.Copy(binding);
        row.RefreshText();
        await SaveAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ClearAsync(string? action)
    {
        if (action == null) return;
        var row = Rows.First(item => item.Action == action);
        row.Binding = new InputBinding { Mode = row.HoldOnly ? BindingMode.HoldToHide : BindingMode.Toggle };
        Assign(action, row.Binding);
        row.Warning = "";
        row.RefreshText();
        await SaveAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private Task SetMouseAsync(MousePick? pick)
    {
        if (pick == null || string.IsNullOrEmpty(pick.Key)) return Task.CompletedTask;
        var row = Rows.First(item => item.Action == pick.Action);
        var binding = HotkeyRules.Copy(row.Binding);
        binding.Key = pick.Key;
        return CommitAsync(new HotkeyCommit { Action = pick.Action, Binding = binding });
    }

    private void RebuildMouseChoices()
    {
        MouseChoices =
        [
            new MouseButtonChoice("", Loc.Instance["Hotkeys_MouseButton"]),
            new MouseButtonChoice("MiddleClick", Loc.Instance["Key_MiddleClick"]),
            new MouseButtonChoice("RightClick", Loc.Instance["Key_RightClick"]),
            new MouseButtonChoice("Mouse4", Loc.Instance["Key_Mouse4"]),
            new MouseButtonChoice("Mouse5", Loc.Instance["Key_Mouse5"]),
            new MouseButtonChoice("WheelUp", Loc.Instance["Key_WheelUp"]),
            new MouseButtonChoice("WheelDown", Loc.Instance["Key_WheelDown"]),
        ];
        foreach (var row in Rows)
        { row.MouseChoices = MouseChoices.Where(choice => !row.HoldOnly || !HotkeyRules.IsWheel(choice.Key)).ToList(); row.OnMouseChoicesChanged(); }
        OnPropertyChanged(nameof(MouseChoices));
    }

    [RelayCommand]
    private async Task SetModeAsync(ModeChange? change)
    {
        if (change == null) return;
        var row = Rows.First(item => item.Action == change.Action);
        if (row.HoldOnly && change.Mode == BindingMode.Toggle) return;
        if (!row.AllowsHold && change.Mode != BindingMode.Toggle) return;
        if (HotkeyRules.IsWheel(row.Binding.Key) && change.Mode != BindingMode.Toggle)
        {
            row.Warning = Loc.Instance["Hotkeys_WheelOnly"];
            row.Binding = HotkeyRules.Copy(row.Binding);
            return;
        }
        var binding = HotkeyRules.Copy(row.Binding);
        binding.Mode = change.Mode;
        row.Binding = binding;
        row.Warning = "";
        Assign(row.Action, binding);
        row.RefreshText();
        await SaveAsync().ConfigureAwait(true);
    }

    private async Task SaveAsync()
    {
        var snapshot = _core.Snapshot;
        if (snapshot == null) return;
        var settings = JsonClone.Clone(snapshot.Store.Settings);
        settings.Hotkeys = JsonClone.Clone(_hotkeys);
        try { await _core.SaveSettingsAsync(settings).ConfigureAwait(true); }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    private void ApplyErrors(Snapshot snapshot)
    {
        foreach (var row in Rows)
        {
            var failure = snapshot.Runtime.HotkeyErrors.FirstOrDefault(item => item.Action == row.Action);
            row.Failure = failure == null ? "" : Loc.Instance["Hotkeys_RegisterFailed"];
        }
    }

    private void Set(string action, InputBinding binding)
    {
        var row = Rows.First(item => item.Action == action);
        row.Binding = HotkeyRules.Copy(binding);
        row.RefreshText();
    }

    private void Assign(string action, InputBinding binding)
    {
        var copy = HotkeyRules.Copy(binding);
        switch (action)
        {
            case "NextCrosshair": _hotkeys.NextCrosshair = copy; break;
            case "PreviousCrosshair": _hotkeys.PreviousCrosshair = copy; break;
            case "OpenSettings": _hotkeys.OpenSettings = copy; break;
            case "HoldVisibility": _hotkeys.HoldVisibility = copy; break;
            default: _hotkeys.ToggleVisibility = copy; break;
        }
    }

    private HotkeySettings With(string action, InputBinding candidate)
    {
        var copy = JsonClone.Clone(_hotkeys);
        switch (action)
        {
            case "NextCrosshair": copy.NextCrosshair = candidate; break;
            case "PreviousCrosshair": copy.PreviousCrosshair = candidate; break;
            case "OpenSettings": copy.OpenSettings = candidate; break;
            case "HoldVisibility": copy.HoldVisibility = candidate; break;
            default: copy.ToggleVisibility = candidate; break;
        }
        return copy;
    }

    private IEnumerable<(string Action, InputBinding Binding)> GameBindings()
    {
        var profiles = _core.Snapshot?.Store.GameProfiles ?? [];
        foreach (var profile in profiles)
        {
            if (profile.VisibilityBinding is { Key.Length: > 0 } binding)
                yield return ("GameProfile:" + profile.Id, binding);
        }
    }

    private static string Label(string action) => action switch
    {
        "ToggleVisibility" => Loc.Instance["Hotkeys_ToggleVisibility"],
        "NextCrosshair" => Loc.Instance["Hotkeys_Next"],
        "PreviousCrosshair" => Loc.Instance["Hotkeys_Previous"],
        "OpenSettings" => Loc.Instance["Hotkeys_OpenSettings"],
        "HoldVisibility" => Loc.Instance["Hotkeys_WhileHeld"],
        _ when action.StartsWith("GameProfile:", StringComparison.Ordinal) => Loc.Instance["Games_VisibilityOverride"],
        _ => action,
    };
}

public sealed class HotkeyCommit
{
    public required string Action { get; init; }
    public required InputBinding Binding { get; init; }
}

public sealed class ModeChange
{
    public required string Action { get; init; }
    public BindingMode Mode { get; init; }
}

public sealed record MouseButtonChoice(string Key, string Label);

public sealed class MousePick
{
    public required string Action { get; init; }
    public required string Key { get; init; }
}

internal static class HotkeyProbe
{
    public static bool IsFree(IntPtr hwnd, int modifiers, int virtualKey)
    {
        if (hwnd == IntPtr.Zero || virtualKey == 0) return true;
        const int id = 0x4C11;
        if (!RegisterHotKey(hwnd, id, (uint)(modifiers | 0x4000), (uint)virtualKey))
            return false;
        UnregisterHotKey(hwnd, id);
        return true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
