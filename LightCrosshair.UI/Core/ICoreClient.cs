namespace LightCrosshair.UI.Core;

public enum CoreLinkState
{
    Idle,
    Connecting,
    Connected,
    Reconnecting,
    Failed,
    ProtocolMismatch,
}

public interface ICoreClient
{
    bool IsFake { get; }
    CoreLinkState State { get; }
    Snapshot? Snapshot { get; }

    event EventHandler<CoreLinkState>? StateChanged;
    event EventHandler<Snapshot>? SnapshotChanged;
    event EventHandler<RuntimeState>? RuntimeChanged;
    event EventHandler<NavigateRequest?>? FocusRequested;
    event EventHandler? CloseRequested;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task<CrosshairDesign> SaveCrosshairAsync(CrosshairDesign design, CancellationToken cancellationToken = default);
    Task DeleteCrosshairAsync(string id, CancellationToken cancellationToken = default);
    Task<GameProfile> SaveGameProfileAsync(GameProfile profile, CancellationToken cancellationToken = default);
    Task DeleteGameProfileAsync(string id, CancellationToken cancellationToken = default);
    Task<AppSettings> SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task PreviewDisplayColorsAsync(DisplayColorSettings? colors, CancellationToken cancellationToken = default);
    Task PreviewAsync(CrosshairDesign? design, CancellationToken cancellationToken = default);
    Task<string> RenderPngAsync(RenderRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PresetEntry>> GetPresetsAsync(CancellationToken cancellationToken = default);
    Task<string> EncodeShareCodeAsync(CrosshairDesign design, CancellationToken cancellationToken = default);
    Task<CrosshairDesign> DecodeShareCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<string> ImportImageAsync(string path, CancellationToken cancellationToken = default);
    Task ExportCrosshairsAsync(string path, IReadOnlyList<string> ids, CancellationToken cancellationToken = default);
    Task<ImportResult> ImportCrosshairsAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcessEntry>> ListProcessesAsync(CancellationToken cancellationToken = default);
    Task<GameProfile> AcceptSuggestionAsync(string processName, CancellationToken cancellationToken = default);
    Task DismissSuggestionAsync(string processName, CancellationToken cancellationToken = default);
    Task SetInputCaptureAsync(bool active, CancellationToken cancellationToken = default);
    Task QuitAsync(CancellationToken cancellationToken = default);
}

public interface IUiMarshaler
{
    void Post(Action action);
}

public sealed class InlineMarshaler : IUiMarshaler
{
    public void Post(Action action) => action();
}

public sealed class DispatcherMarshaler : IUiMarshaler
{
    public void Post(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
