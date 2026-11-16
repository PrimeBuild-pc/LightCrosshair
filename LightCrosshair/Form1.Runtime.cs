using LightCrosshair.Contracts;
using LightCrosshair.Core;
using LightCrosshair.GpuDriver;

namespace LightCrosshair;

internal sealed partial class Form1
{
    private readonly ColorSession _colors = new(Program.DataRoot);
    private readonly FpsOverlay _fps = new();
    private DisplayManager? _foreground;
    private GameContext? _context;
    private string? _contextProcess, _cycledId, _colorsKey;
    private int _foregroundPid;
    private bool? _holdOverride;
    private readonly HashSet<string> _suggested = new();
    private bool _closing, _allowClose;
    internal event Action? RuntimeUpdated;
    private void InitializeRuntime()
    {
        var vendor = GpuDetectionService.DetectVendor(out _);
        Runtime.Gpu = new()
        {
            SoftwareGamma = true,
            NvidiaDigitalVibrance = vendor == GpuVendorKind.Nvidia,
            NvidiaFpsCap = vendor == GpuVendorKind.Nvidia,
            AmdColor = vendor == GpuVendorKind.Amd
        };
        _foreground = new(OnForeground);
        _foreground.Refresh(); RefreshContext();
    }
    private void OnForeground(string? process, int pid)
    {
        if (IsDisposed || _closing) return;
        Runtime.ForegroundProcess = process;
        if (process == "lightcrosshair.ui.exe") { RuntimeUpdated?.Invoke(); return; }
        if (process != _contextProcess) { _cycledId = null; _holdOverride = null; }
        _contextProcess = process; _foregroundPid = pid;
        RefreshContext();
        if (process is not null && Document.Settings.SuggestKnownGames && KnownGames.Entries.TryGetValue(process, out string? name) &&
            !Document.GameProfiles.Any(g => g.ProcessName == process) && !Document.Settings.DismissedSuggestions.Contains(process) && _suggested.Add(process))
        {
            Runtime.PendingSuggestion = new() { ProcessName = process, DisplayName = name };
            _tray.ShowBalloonTip(4000, "LightCrosshair", $"Create a profile for {name}?", ToolTipIcon.Info);
            RuntimeUpdated?.Invoke();
        }
    }
    internal void RefreshContext()
    {
        if (!_ready || _closing) return;
        _context = GameRules.Evaluate(Document, _contextProcess);
        Runtime.ActiveGameProfileId = _context.Game?.Id;
        Runtime.ActiveCrosshairId = _cycledId is not null && Document.Crosshairs.Any(d => d.Id == _cycledId) ? _cycledId : _context.CrosshairId;
        Runtime.Monitors = Screen.AllScreens.Select(s => new MonitorInfo { DeviceName = s.DeviceName, FriendlyName = s.DeviceName, IsPrimary = s.Primary, Width = s.Bounds.Width, Height = s.Bounds.Height }).ToList();
        var colors = _context.Game?.DisplayColors;
        string colorsKey = System.Text.Json.JsonSerializer.Serialize(colors, ContractJson.Options);
        if (_colorsKey != colorsKey) { _colorsKey = colorsKey; _ = _colors.SetAsync(colors); }
        _fps.Configure(_context.FpsEnabled, _foregroundPid, Document.Settings.FpsOverlay, Document.Settings.MonitorDeviceName);
        RefreshOverlay(); RuntimeUpdated?.Invoke();
    }
    internal async Task ApplyDriverCapAsync(GameProfile? old, GameProfile? next)
    {
        if (old?.NvidiaFpsCap == next?.NvidiaFpsCap && old?.ProcessName == next?.ProcessName) return;
        if (old?.NvidiaFpsCap is null && next?.NvidiaFpsCap is null) return;
        if (!Runtime.Gpu.NvidiaFpsCap) throw new CoreException(IpcErrorCodes.Unsupported, "NVIDIA FPS cap unavailable.");
        await Task.Run(() =>
        {
            var helper = new NvidiaHelperProcessClient();
            if (old?.NvidiaFpsCap is not null && (next?.NvidiaFpsCap is null || old.ProcessName != next.ProcessName) && !helper.Run(NvidiaHelperRequest.ClearFpsCap(old.ProcessName)).Success)
                throw new CoreException(IpcErrorCodes.Internal, "Unable to clear the previous driver cap.");
            if (next?.NvidiaFpsCap is not null && !helper.Run(NvidiaHelperRequest.SetFpsCap(next.ProcessName, next.NvidiaFpsCap.Value)).Success)
            {
                if (old?.NvidiaFpsCap is not null) helper.Run(NvidiaHelperRequest.SetFpsCap(old.ProcessName, old.NvidiaFpsCap.Value));
                throw new CoreException(IpcErrorCodes.Internal, "Unable to apply the driver cap.");
            }
        });
    }
    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        if (_allowClose) { base.OnFormClosing(e); return; }
        e.Cancel = true; base.OnFormClosing(e);
        if (_closing) return;
        _closing = true; _foreground?.Dispose(); _foreground = null;
        _fps.Configure(false, 0, Document.Settings.FpsOverlay, null);
        try { await Store.SaveAsync(Document); }
        catch (Exception ex) { Program.LogError(ex, "Shutdown store"); }
        await _colors.SetAsync(null);
        _allowClose = true; Close();
    }
}
