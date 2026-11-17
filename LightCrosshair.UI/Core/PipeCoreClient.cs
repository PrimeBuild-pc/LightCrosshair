using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using LightCrosshair.UI.Services;

namespace LightCrosshair.UI.Core;

public sealed class PipeCoreClientOptions
{
    public string? PipeName { get; init; }
    public Func<CancellationToken, Task>? StartCore { get; init; }
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan RenderTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public int MaxLineBytes { get; init; } = 8 * 1024 * 1024;
    public IUiMarshaler? Marshaler { get; init; }
}

/// <summary>NDJSON named-pipe client for LightCrosshair.exe. See CONTRACT.md §2.</summary>
public sealed class PipeCoreClient : ICoreClient, IAsyncDisposable
{
    private readonly PipeCoreClientOptions _options;
    private readonly IUiMarshaler _marshaler;
    private readonly SemaphoreSlim _write = new(1, 1);
    private readonly object _pendingGate = new();
    private readonly Dictionary<long, TaskCompletionSource<IpcEnvelope>> _pending = new();
    private TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? _run;
    private NamedPipeClientStream? _pipe;
    private long _nextId;
    private int _running;

    public PipeCoreClient(PipeCoreClientOptions? options = null)
    {
        _options = options ?? new PipeCoreClientOptions();
        _marshaler = _options.Marshaler ?? new DispatcherMarshaler();
    }

    public bool IsFake => false;
    public CoreLinkState State { get; private set; } = CoreLinkState.Idle;
    public Snapshot? Snapshot { get; private set; }

    public event EventHandler<CoreLinkState>? StateChanged;
    public event EventHandler<Snapshot>? SnapshotChanged;
    public event EventHandler<RuntimeState>? RuntimeChanged;
    public event EventHandler<NavigateRequest?>? FocusRequested;
    public event EventHandler? CloseRequested;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            return _ready.Task;
        if (_ready.Task.IsCompleted)
            _ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = ObserveAsync(RunAsync(_run.Token));
        return _ready.Task;
    }

    private async Task ObserveAsync(Task run)
    {
        try { await run.ConfigureAwait(false); }
        finally { Interlocked.Exchange(ref _running, 0); }
    }

    public Task<CrosshairDesign> SaveCrosshairAsync(CrosshairDesign design, CancellationToken cancellationToken = default)
        => InvokeRequired<CrosshairDesign>(IpcMethods.SaveCrosshair, design, _options.RequestTimeout, cancellationToken);

    public async Task DeleteCrosshairAsync(string id, CancellationToken cancellationToken = default)
        => await InvokeAsync(IpcMethods.DeleteCrosshair, new IdRequest { Id = id }, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);

    public Task<GameProfile> SaveGameProfileAsync(GameProfile profile, CancellationToken cancellationToken = default)
        => InvokeRequired<GameProfile>(IpcMethods.SaveGameProfile, profile, _options.RequestTimeout, cancellationToken);

    public async Task DeleteGameProfileAsync(string id, CancellationToken cancellationToken = default)
        => await InvokeAsync(IpcMethods.DeleteGameProfile, new IdRequest { Id = id }, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);

    public Task<AppSettings> SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
        => InvokeRequired<AppSettings>(IpcMethods.SaveSettings, settings, _options.RequestTimeout, cancellationToken);

    public async Task PreviewDisplayColorsAsync(DisplayColorSettings? colors, CancellationToken cancellationToken = default)
        => await InvokeAsync(IpcMethods.PreviewDisplayColors, new DisplayColorsPreviewRequest { Colors = colors }, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);

    public async Task PreviewAsync(CrosshairDesign? design, CancellationToken cancellationToken = default)
        => await InvokeAsync(IpcMethods.Preview, new PreviewRequest { Design = design }, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);

    public async Task<string> RenderPngAsync(RenderRequest request, CancellationToken cancellationToken = default)
    {
        var response = await InvokeRequired<RenderResponse>(IpcMethods.Render, request, _options.RenderTimeout, cancellationToken).ConfigureAwait(false);
        return response.PngBase64;
    }

    public async Task<IReadOnlyList<PresetEntry>> GetPresetsAsync(CancellationToken cancellationToken = default)
        => await InvokeRequired<List<PresetEntry>>(IpcMethods.GetPresets, null, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);

    public async Task<string> EncodeShareCodeAsync(CrosshairDesign design, CancellationToken cancellationToken = default)
    {
        var payload = await InvokeRequired<TextPayload>(IpcMethods.EncodeShareCode, design, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);
        return payload.Value;
    }

    public async Task<CrosshairDesign> DecodeShareCodeAsync(string code, CancellationToken cancellationToken = default)
        => await InvokeRequired<CrosshairDesign>(IpcMethods.DecodeShareCode, new TextPayload { Value = code }, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);

    public async Task<string> ImportImageAsync(string path, CancellationToken cancellationToken = default)
    {
        var payload = await InvokeRequired<TextPayload>(IpcMethods.ImportImage, new TextPayload { Value = path }, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);
        return payload.Value;
    }

    public Task ExportCrosshairsAsync(string path, IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
        => InvokeAsync(IpcMethods.ExportCrosshairs, new ExportRequest { Path = path, CrosshairIds = ids.ToList() }, _options.RequestTimeout, cancellationToken);

    public Task<ImportResult> ImportCrosshairsAsync(string path, CancellationToken cancellationToken = default)
        => InvokeRequired<ImportResult>(IpcMethods.ImportCrosshairs, new TextPayload { Value = path }, _options.RequestTimeout, cancellationToken);

    public async Task<IReadOnlyList<ProcessEntry>> ListProcessesAsync(CancellationToken cancellationToken = default)
        => await InvokeRequired<List<ProcessEntry>>(IpcMethods.ListProcesses, null, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);

    public Task<GameProfile> AcceptSuggestionAsync(string processName, CancellationToken cancellationToken = default)
        => InvokeRequired<GameProfile>(IpcMethods.AcceptSuggestion, new TextPayload { Value = processName }, _options.RequestTimeout, cancellationToken);

    public Task DismissSuggestionAsync(string processName, CancellationToken cancellationToken = default)
        => InvokeAsync(IpcMethods.DismissSuggestion, new TextPayload { Value = processName }, _options.RequestTimeout, cancellationToken);

    public Task SetInputCaptureAsync(bool active, CancellationToken cancellationToken = default)
        => InvokeAsync(IpcMethods.SetInputCapture, new InputCaptureRequest { Active = active }, _options.RequestTimeout, cancellationToken);

    public Task QuitAsync(CancellationToken cancellationToken = default)
        => InvokeAsync(IpcMethods.Quit, null, _options.RequestTimeout, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        _run?.Cancel();
        FailPending(ErrorText.Disconnected);
        if (_pipe != null) await _pipe.DisposeAsync().ConfigureAwait(false);
        _write.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var launched = false;
        var everConnected = false;
        var delay = TimeSpan.FromMilliseconds(250);
        var pipeName = _options.PipeName ?? ContractInfo.PipeName(System.Diagnostics.Process.GetCurrentProcess().SessionId);

        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeClientStream? pipe = null;
            try
            {
                SetState(everConnected ? CoreLinkState.Reconnecting : CoreLinkState.Connecting);
                pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.ConnectAsync(300, cancellationToken).ConfigureAwait(false);
                _pipe = pipe;
                using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var reading = ReadLoopAsync(pipe, session.Token);
                try
                {
                    await InvokeRequired<HelloResponse>(IpcMethods.Hello, new HelloRequest
                    {
                        ProtocolVersion = ContractInfo.ProtocolVersion,
                        Client = ContractInfo.UiExeName,
                    }, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);
                    var snapshot = await InvokeRequired<Snapshot>(IpcMethods.GetSnapshot, null, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);
                    Remember(snapshot);
                    everConnected = true;
                    delay = TimeSpan.FromMilliseconds(250);
                    SetState(CoreLinkState.Connected);
                    await reading.ConfigureAwait(false);
                }
                finally
                {
                    session.Cancel();
                    try { await reading.ConfigureAwait(false); } catch { /* the read loop ends with the pipe */ }
                }
                throw new IOException("eof");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (CoreException ex) when (ex.Code == IpcErrorCodes.ProtocolMismatch)
            {
                SetState(CoreLinkState.ProtocolMismatch);
                return;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                FailPending(ErrorText.Disconnected);
                if (!everConnected)
                {
                    if (!launched)
                    {
                        launched = true;
                        try { await (_options.StartCore ?? StartCoreAsync)(cancellationToken).ConfigureAwait(false); }
                        catch { /* a missing executable surfaces as a failed connect */ }
                    }
                    if (DateTime.UtcNow - started >= _options.ConnectTimeout)
                    {
                        SetState(CoreLinkState.Failed);
                        return;
                    }
                }
                try { await Task.Delay(everConnected ? delay : TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                if (everConnected)
                    delay = TimeSpan.FromMilliseconds(Math.Min(2000, delay.TotalMilliseconds * 2));
            }
            finally
            {
                _pipe = null;
                if (pipe != null) await pipe.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task ReadLoopAsync(NamedPipeClientStream pipe, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var line = new MemoryStream();
        var offset = 0;
        var count = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (offset >= count)
            {
                count = await pipe.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                offset = 0;
                if (count == 0) return;
            }
            var value = buffer[offset++];
            if (value == (byte)'\n')
            {
                var text = Encoding.UTF8.GetString(line.GetBuffer(), 0, (int)line.Length).TrimEnd('\r');
                line.SetLength(0);
                HandleLine(text);
                continue;
            }
            line.WriteByte(value);
            if (line.Length > _options.MaxLineBytes)
                throw new CoreException(IpcErrorCodes.BadRequest);
        }
    }

    private void HandleLine(string text)
    {
        IpcEnvelope? envelope;
        try { envelope = JsonSerializer.Deserialize<IpcEnvelope>(text, ContractJson.Options); }
        catch (JsonException) { throw new CoreException(IpcErrorCodes.BadRequest); }
        if (envelope == null) throw new CoreException(IpcErrorCodes.BadRequest);
        if (envelope.Kind == IpcKinds.Response)
        {
            TaskCompletionSource<IpcEnvelope>? pending = null;
            lock (_pendingGate)
            {
                if (_pending.Remove(envelope.Id, out var found)) pending = found;
            }
            pending?.TrySetResult(envelope);
            return;
        }
        if (envelope.Kind == IpcKinds.Event)
            _marshaler.Post(() => Dispatch(envelope));
    }

    private void Dispatch(IpcEnvelope envelope)
    {
        switch (envelope.Method)
        {
            case IpcEvents.SnapshotChanged:
                var snapshot = envelope.Payload?.Deserialize<Snapshot>(ContractJson.Options);
                if (snapshot != null)
                {
                    Snapshot = snapshot;
                    SnapshotChanged?.Invoke(this, snapshot);
                }
                break;
            case IpcEvents.RuntimeChanged:
                var runtime = envelope.Payload?.Deserialize<RuntimeState>(ContractJson.Options);
                if (runtime != null)
                {
                    if (Snapshot != null) Snapshot.Runtime = runtime;
                    RuntimeChanged?.Invoke(this, runtime);
                }
                break;
            case IpcEvents.FocusRequested:
                NavigateRequest? navigation = null;
                if (envelope.Payload is { ValueKind: JsonValueKind.Object })
                    navigation = envelope.Payload.Value.Deserialize<NavigateRequest>(ContractJson.Options);
                FocusRequested?.Invoke(this, navigation);
                break;
            case IpcEvents.CloseRequested:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private async Task<T> InvokeRequired<T>(string method, object? payload, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var value = await InvokeAsync(method, payload, timeout, cancellationToken).ConfigureAwait(false);
        if (value == null) throw new CoreException(IpcErrorCodes.BadRequest);
        return value.Value.Deserialize<T>(ContractJson.Options) ?? throw new CoreException(IpcErrorCodes.BadRequest);
    }

    private async Task<JsonElement?> InvokeAsync(string method, object? payload, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var pipe = _pipe ?? throw new CoreException(ErrorText.Disconnected);
        var id = Interlocked.Increment(ref _nextId);
        var pending = new TaskCompletionSource<IpcEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pendingGate) _pending[id] = pending;
        try
        {
            var envelope = new IpcEnvelope
            {
                Kind = IpcKinds.Request,
                Id = id,
                Method = method,
                Payload = payload == null ? null : JsonSerializer.SerializeToElement(payload, payload.GetType(), ContractJson.Options),
            };
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, ContractJson.Options) + "\n");
            await _write.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await pipe.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally { _write.Release(); }

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);
            IpcEnvelope response;
            try { response = await pending.Task.WaitAsync(timeoutSource.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                throw new CoreException(cancellationToken.IsCancellationRequested ? ErrorText.Disconnected : ErrorText.Timeout);
            }
            if (!response.Ok)
                throw new CoreException(string.IsNullOrEmpty(response.Error?.Code) ? IpcErrorCodes.Internal : response.Error.Code);
            return response.Payload;
        }
        finally
        {
            lock (_pendingGate) _pending.Remove(id);
        }
    }

    private void Remember(Snapshot snapshot)
    {
        _marshaler.Post(() =>
        {
            Snapshot = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
        });
    }

    private void SetState(CoreLinkState state)
    {
        State = state;
        if (state is CoreLinkState.Connected or CoreLinkState.Failed or CoreLinkState.ProtocolMismatch)
            _ready.TrySetResult();
        _marshaler.Post(() => StateChanged?.Invoke(this, state));
    }

    private void FailPending(string code)
    {
        List<TaskCompletionSource<IpcEnvelope>> waiting;
        lock (_pendingGate)
        {
            waiting = _pending.Values.ToList();
            _pending.Clear();
        }
        var failed = new IpcEnvelope { Kind = IpcKinds.Response, Ok = false, Error = new IpcError { Code = code, Message = "" } };
        foreach (var item in waiting) item.TrySetResult(failed);
    }

    public static Task StartCoreAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, ContractInfo.CoreExeName);
        if (!File.Exists(path)) return Task.CompletedTask;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = true,
        });
        return Task.CompletedTask;
    }
}
