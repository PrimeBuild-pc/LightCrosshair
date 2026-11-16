using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightCrosshair.UI.Core;
using LightCrosshair.UI.Localization;

namespace LightCrosshair.UI.Services;

public enum UnsavedChoice { Save, Discard, Cancel }

public sealed class DialogButton
{
    public required string Text { get; init; }
    public required IRelayCommand Command { get; init; }
    public bool IsPrimary { get; init; }
    public bool IsDanger { get; init; }
}

public sealed partial class DialogHost : ObservableObject
{
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _message = "";
    public ObservableCollection<DialogButton> Buttons { get; } = new();

    public Task<int> ShowAsync(string title, string message, IReadOnlyList<(string Text, bool Primary, bool Danger)> buttons)
    {
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Buttons.Clear();
        for (var i = 0; i < buttons.Count; i++)
        {
            var index = i;
            Buttons.Add(new DialogButton
            {
                Text = buttons[i].Text,
                IsPrimary = buttons[i].Primary,
                IsDanger = buttons[i].Danger,
                Command = new RelayCommand(() =>
                {
                    IsOpen = false;
                    source.TrySetResult(index);
                }),
            });
        }
        Title = title;
        Message = message;
        IsOpen = true;
        return source.Task;
    }
}

public sealed class ToastNote
{
    public string Message { get; init; } = "";
}

public interface IUiServices
{
    DialogHost Dialogs { get; }
    ObservableCollection<ToastNote> Toasts { get; }
    IntPtr WindowHandle { get; }
    string? PickOpenFile(string filter);
    string? PickSaveFile(string filter, string suggestedName);
    Task<UnsavedChoice> AskUnsavedAsync();
    Task<bool> ConfirmAsync(string title, string message, string confirmText);
    void Toast(string message);
    void Copy(string text);
    string? Paste();
    void OpenUrl(string url);
}

public sealed class AppUi : IUiServices
{
    private readonly IUiMarshaler _marshaler;

    public AppUi(IUiMarshaler? marshaler = null) => _marshaler = marshaler ?? new DispatcherMarshaler();

    public DialogHost Dialogs { get; } = new();
    public ObservableCollection<ToastNote> Toasts { get; } = new();
    public IntPtr WindowHandle { get; set; }

    public string? PickOpenFile(string filter)
    {
        var title = filter.Split('|')[0];
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = filter, Title = title, CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSaveFile(string filter, string suggestedName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = filter, FileName = suggestedName, AddExtension = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public async Task<UnsavedChoice> AskUnsavedAsync()
    {
        var choice = await Dialogs.ShowAsync(Loc.Instance["Unsaved_Title"], Loc.Instance["Unsaved_Message"],
        [
            (Loc.Instance["Save"], true, false),
            (Loc.Instance["Discard"], false, false),
            (Loc.Instance["Cancel"], false, false),
        ]).ConfigureAwait(true);
        return choice switch { 0 => UnsavedChoice.Save, 1 => UnsavedChoice.Discard, _ => UnsavedChoice.Cancel };
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var choice = await Dialogs.ShowAsync(title, message,
        [
            (confirmText, false, true),
            (Loc.Instance["Cancel"], false, false),
        ]).ConfigureAwait(true);
        return choice == 0;
    }

    public void Toast(string message)
    {
        var note = new ToastNote { Message = message };
        _marshaler.Post(() =>
        {
            Toasts.Add(note);
            while (Toasts.Count > 3) Toasts.RemoveAt(0);
        });
        _ = RemoveLater(note);
    }

    public void Copy(string text)
    {
        try { Clipboard.SetText(text); }
        catch (Exception) { Toast(Loc.Instance["Error_Internal"]); }
    }

    public string? Paste()
    {
        try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
        catch (Exception) { return null; }
    }

    public void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception) { Toast(Loc.Instance["Error_Internal"]); }
    }

    private async Task RemoveLater(ToastNote note)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(4)).ConfigureAwait(false); }
        catch (Exception) { return; }
        _marshaler.Post(() => Toasts.Remove(note));
    }
}

public sealed class FakeUi : IUiServices
{
    public DialogHost Dialogs { get; } = new();
    public ObservableCollection<ToastNote> Toasts { get; } = new();
    public IntPtr WindowHandle { get; set; }
    public UnsavedChoice UnsavedAnswer { get; set; } = UnsavedChoice.Discard;
    public bool ConfirmAnswer { get; set; } = true;
    public string? NextOpenFile { get; set; }
    public string? NextSaveFile { get; set; }
    public string? Clipboard { get; set; }
    public List<string> OpenedUrls { get; } = new();

    public string? PickOpenFile(string filter) => NextOpenFile;
    public string? PickSaveFile(string filter, string suggestedName) => NextSaveFile;
    public Task<UnsavedChoice> AskUnsavedAsync() => Task.FromResult(UnsavedAnswer);
    public Task<bool> ConfirmAsync(string title, string message, string confirmText) => Task.FromResult(ConfirmAnswer);
    public void Toast(string message) => Toasts.Add(new ToastNote { Message = message });
    public void Copy(string text) => Clipboard = text;
    public string? Paste() => Clipboard;
    public void OpenUrl(string url) => OpenedUrls.Add(url);
}
