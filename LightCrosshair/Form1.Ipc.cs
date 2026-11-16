using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text.Json;
using LightCrosshair.Contracts;
using LightCrosshair.Core;

namespace LightCrosshair;

internal sealed partial class Form1
{
    private PipeServer? _pipe;
    private readonly SemaphoreSlim _mutations = new(1, 1);
    private CancellationTokenSource? _previewTimeout;
    private bool _runtimePending;
    private long _lastRuntimeEvent;
    private void InitializePipe()
    {
        _pipe = new(ContractInfo.PipeName(Process.GetCurrentProcess().SessionId),
            request => OnUiAsync(() => HandleRequestAsync(request)),
            () => OnUiAsync(() => { EndPreview(); return Task.FromResult<object?>(null); }));
        RuntimeUpdated += QueueRuntimeEvent;
    }
    private Task<object?> OnUiAsync(Func<Task<object?>> action)
    {
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (IsDisposed || !IsHandleCreated) return Task.FromResult<object?>(null);
        try
        {
            BeginInvoke(new Action(async () =>
            {
                try { completion.SetResult(await action()); }
                catch (Exception ex) { completion.SetException(ex); }
            }));
        }
        catch (InvalidOperationException) { completion.TrySetResult(null); }
        return completion.Task;
    }
    private Snapshot Snapshot() => new() { Store = Document, Runtime = Runtime };
    private void QueueRuntimeEvent()
    {
        if (_runtimePending || _pipe?.IsConnected != true || _closing) return;
        _runtimePending = true; _ = FlushRuntimeEventAsync();
    }
    private async Task FlushRuntimeEventAsync()
    {
        await Task.Delay((int)Math.Max(1, 100 - (Environment.TickCount64 - _lastRuntimeEvent)));
        _runtimePending = false;
        if (_closing || IsDisposed) return;
        _lastRuntimeEvent = Environment.TickCount64; _pipe?.Publish(IpcEvents.RuntimeChanged, Runtime);
    }
    private static T Payload<T>(IpcEnvelope request)
    {
        if (request.Payload is not { ValueKind: JsonValueKind.Object } element) throw new CoreException(IpcErrorCodes.BadRequest, "Payload required.");
        return element.Deserialize<T>(ContractJson.Options) ?? throw new CoreException(IpcErrorCodes.BadRequest, "Payload required.");
    }
    private async Task<object?> MutateAsync(Func<StoreDocument, Task<object?>> change)
    {
        await _mutations.WaitAsync();
        try
        {
            var next = Validation.Copy(Document);
            object? result = await change(next);
            await Store.SaveAsync(next);
            Document = next; RefreshContext();
            _pipe?.Publish(IpcEvents.SnapshotChanged, Snapshot());
            return result;
        }
        finally { _mutations.Release(); }
    }
    internal async Task<object?> ToggleVisibilityAsync()
    {
        bool desired = !(_manualVisibility ?? Document.Settings.CrosshairVisible);
        _manualVisibility = desired; RefreshOverlay(); QueueRuntimeEvent();
        try
        {
            return await MutateAsync(next => { next.Settings.CrosshairVisible = desired; return Task.FromResult<object?>(null); });
        }
        finally
        {
            if (_manualVisibility == desired) _manualVisibility = null;
            RefreshOverlay(); QueueRuntimeEvent();
        }
    }
    private async Task<object?> HandleRequestAsync(IpcEnvelope request)
    {
        if (_closing) throw new CoreException(IpcErrorCodes.Internal, "Core is stopping.");
        switch (request.Method)
        {
            case IpcMethods.GetSnapshot: return Snapshot();
            case IpcMethods.SaveCrosshair:
                var design = Validation.Design(Payload<CrosshairDesign>(request), Store.ImagesDirectory);
                design.UpdatedAt = DateTimeOffset.UtcNow;
                return await MutateAsync(next =>
                {
                    int index = next.Crosshairs.FindIndex(d => d.Id == design.Id);
                    if (index < 0) next.Crosshairs.Add(design); else next.Crosshairs[index] = design;
                    return Task.FromResult<object?>(design);
                });
            case IpcMethods.DeleteCrosshair:
                string id = Payload<IdRequest>(request).Id;
                return await MutateAsync(next =>
                {
                    if (id == next.Settings.DefaultCrosshairId || next.GameProfiles.Any(g => g.CrosshairId == id)) throw Validation.Invalid("Crosshair is in use.");
                    if (next.Crosshairs.RemoveAll(d => d.Id == id) == 0) throw new CoreException(IpcErrorCodes.NotFound, "Crosshair not found.");
                    return Task.FromResult<object?>(null);
                });
            case IpcMethods.SaveGameProfile:
                return await MutateAsync(async next =>
                {
                    var game = Validation.Game(Payload<GameProfile>(request), next);
                    var old = next.GameProfiles.FirstOrDefault(g => g.Id == game.Id);
                    await ApplyDriverCapAsync(old, game);
                    next.GameProfiles.RemoveAll(g => g.Id == game.Id); next.GameProfiles.Add(game);
                    return game;
                });
            case IpcMethods.DeleteGameProfile:
                return await MutateAsync(async next =>
                {
                    string gameId = Payload<IdRequest>(request).Id;
                    var old = next.GameProfiles.FirstOrDefault(g => g.Id == gameId) ?? throw new CoreException(IpcErrorCodes.NotFound, "Game profile not found.");
                    await ApplyDriverCapAsync(old, null); next.GameProfiles.Remove(old); return null;
                });
            case IpcMethods.SaveSettings:
                var previousSettings = Validation.Copy(Document.Settings);
                object? savedSettings;
                try
                {
                    savedSettings = await MutateAsync(next =>
                    {
                        next.Settings = Validation.Settings(Payload<AppSettings>(request), next.Crosshairs);
                        if (next.Settings.StartWithWindows != previousSettings.StartWithWindows) StartupRegistration.Apply(next.Settings.StartWithWindows);
                        return Task.FromResult<object?>(next.Settings);
                    });
                }
                catch
                {
                    try { StartupRegistration.Apply(previousSettings.StartWithWindows); }
                    catch (Exception ex) { Program.LogError(ex, "Restore autostart registration"); }
                    throw;
                }
                return savedSettings;
            case IpcMethods.Preview:
                var preview = Payload<PreviewRequest>(request).Design;
                if (preview is null) { EndPreview(); return null; }
                PreviewDesign = Validation.Design(preview, Store.ImagesDirectory);
                _previewTimeout?.Cancel(); _previewTimeout?.Dispose(); _previewTimeout = new();
                _ = ExpirePreviewAsync(_previewTimeout.Token); RefreshOverlay(); QueueRuntimeEvent(); return null;
            case IpcMethods.Render:
                var render = Payload<RenderRequest>(request);
                render.Design = Validation.Design(render.Design, Store.ImagesDirectory);
                render.CanvasSize = Math.Clamp(render.CanvasSize, 16, 512);
                if (render.Background is not null) render.Background = Validation.Color(render.Background);
                // Rasterization and PNG compression run off the overlay thread.
                return await Task.Run(() =>
                {
                    using var bitmap = Renderer.Render(render.Design, render.CanvasSize, render.Background);
                    using var png = new MemoryStream(); bitmap.Save(png, ImageFormat.Png);
                    return new RenderResponse { PngBase64 = Convert.ToBase64String(png.ToArray()) };
                });
            case IpcMethods.ListProcesses:
                return await Task.Run(() =>
                {
                    var processes = new List<ProcessEntry>();
                    foreach (var process in Process.GetProcesses())
                        using (process)
                            try
                            {
                                if (process.MainWindowHandle != IntPtr.Zero)
                                    processes.Add(new() { ProcessName = process.ProcessName.ToLowerInvariant() + ".exe", WindowTitle = process.MainWindowTitle, ExePath = process.MainModule?.FileName });
                            }
                            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
                    return processes.OrderBy(p => p.ProcessName).ToList();
                });
            case IpcMethods.AcceptSuggestion:
                return await MutateAsync(next =>
                {
                    string process = Validation.ProcessName(Payload<TextPayload>(request).Value);
                    if (!KnownGames.Entries.TryGetValue(process, out string? name)) throw Validation.Invalid("Unknown suggestion.");
                    var game = Validation.Game(new() { ProcessName = process, Name = name, CrosshairId = next.Settings.DefaultCrosshairId }, next);
                    next.GameProfiles.Add(game); Runtime.PendingSuggestion = null; return Task.FromResult<object?>(game);
                });
            case IpcMethods.DismissSuggestion:
                return await MutateAsync(next =>
                {
                    string process = Validation.ProcessName(Payload<TextPayload>(request).Value);
                    if (!next.Settings.DismissedSuggestions.Contains(process)) next.Settings.DismissedSuggestions.Add(process);
                    Runtime.PendingSuggestion = null; return Task.FromResult<object?>(null);
                });
            case IpcMethods.Quit: BeginInvoke(new Action(Close)); return null;
            default: return await HandleLibraryRequestAsync(request);
        }
    }
    private async Task ExpirePreviewAsync(CancellationToken token)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(10), token); if (!IsDisposed) EndPreview(); }
        catch (OperationCanceledException) { }
    }
    private void EndPreview()
    {
        _previewTimeout?.Cancel(); _previewTimeout?.Dispose(); _previewTimeout = null;
        PreviewDesign = null; if (!_closing && !IsDisposed) { RefreshOverlay(); QueueRuntimeEvent(); }
    }
}
