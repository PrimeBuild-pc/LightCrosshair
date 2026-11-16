using System.Diagnostics;
using System.Text.Json;
using LightCrosshair.Contracts;
using LightCrosshair.Core;

namespace LightCrosshair;

internal sealed partial class Form1 : LayeredWindow
{
    private readonly NotifyIcon _tray;
    internal readonly CoreStore Store = new(Program.DataRoot);
    internal StoreDocument Document = LegacyMigration.Defaults();
    internal RuntimeState Runtime = new() { CoreVersion = "2.0.0" };
    internal readonly LayerRenderer Renderer;
    internal CrosshairDesign? PreviewDesign;
    private string? _renderKey;
    private bool _ready;
    internal Form1()
    {
        Renderer = new(Store.ImagesDirectory);
        var menu = new ContextMenuStrip();
        menu.Items.Add(CoreStrings.Get("auto", "HideCrosshair"), null, async (_, _) =>
        {
            try { await ToggleVisibilityAsync(); }
            catch (Exception ex) { Program.LogError(ex, "Toggle visibility"); }
        });
        menu.Items.Add(CoreStrings.Get("auto", "Open"), null, (_, _) => OpenSettings());
        menu.Items.Add(CoreStrings.Get("auto", "Exit"), null, (_, _) => Close());
        _tray = new() { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application, Text = "LightCrosshair", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => OpenSettings();
        _colors.Failed += restore =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(new Action(() => { if (!IsDisposed) _tray.ShowBalloonTip(5000, "LightCrosshair", TextFor(restore ? "ColorsRestoreFailed" : "ColorsApplyFailed"), ToolTipIcon.Warning); })); }
            catch (InvalidOperationException) { }
        };
        _tray.BalloonTipClicked += (_, _) => OpenSettings(Runtime.PendingSuggestion is null ? null : new() { Page = UiPages.Games, SuggestProcess = Runtime.PendingSuggestion.ProcessName });
        DisplayChanged += OnDisplayChanged;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += SystemDisplayChanged;
    }
    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            Document = await Store.LoadAsync();
            await _colors.RecoverAsync();
            if (IsDisposed) return;
            _ready = true; Runtime.ActiveCrosshairId = Document.Settings.DefaultCrosshairId;
            InitializeRuntime();
            InitializePipe();
            if (Store.LoadError is not null)
            {
                string message = TextFor("LoadFailed");
                _tray.ShowBalloonTip(5000, "LightCrosshair", message, ToolTipIcon.Warning);
                Program.WriteLog("Load failure notification: " + message);
            }
            else if (Store.Recovered) _tray.ShowBalloonTip(3000, "LightCrosshair", TextFor("Recovered"), ToolTipIcon.Info);
        }
        catch (Exception ex) { Program.LogError(ex, "Load store"); Close(); }
    }
    private void SystemDisplayChanged(object? sender, EventArgs e)
    { if (!IsDisposed && IsHandleCreated) BeginInvoke(OnDisplayChanged); }
    private void OnDisplayChanged() { _renderKey = null; RefreshContext(); }
    internal void RefreshOverlay()
    {
        if (!_ready || IsDisposed) return;
        var settings = Document.Settings;
        var design = PreviewDesign ?? Document.Crosshairs.FirstOrDefault(d => d.Id == Runtime.ActiveCrosshairId) ?? Document.Crosshairs.First(d => d.Id == settings.DefaultCrosshairId);
        bool visible = PreviewDesign is not null || ((_holdOverride ?? _manualVisibility ?? settings.CrosshairVisible) &&
            (_context?.Game is not null || !settings.ShowOnlyInConfiguredGames) && !_recording);
        Runtime.CrosshairShown = visible; Runtime.PreviewActive = PreviewDesign is not null;
        if (!visible) { Hide(); return; }
        Rectangle screen = OverlayMonitorSelector.ResolveBounds(settings.MonitorDeviceName);
        Rectangle geometry = Renderer.Bounds(design);
        Point origin = new(screen.Left + screen.Width / 2 + settings.OffsetX, screen.Top + screen.Height / 2 + settings.OffsetY);
        Rectangle bounds = new(origin.X + geometry.X, origin.Y + geometry.Y, geometry.Width, geometry.Height);
        string key = JsonSerializer.Serialize(design, ContractJson.Options) + bounds.ToString();
        if (key != _renderKey)
        {
            Present(bounds, g => Renderer.Draw(g, design, geometry.Size, new(-geometry.X, -geometry.Y)));
            _renderKey = key;
        }
        if (!Visible) Show();
        ReinforceTopmost();
    }
    private void OpenSettings(NavigateRequest? navigate = null)
    {
        if (_pipe?.IsConnected == true) { _pipe.Publish(IpcEvents.FocusRequested, navigate); return; }
        string path = Path.Combine(AppContext.BaseDirectory, ContractInfo.UiExeName);
        try
        {
            if (File.Exists(path))
            {
                var start = new ProcessStartInfo(path) { UseShellExecute = true };
                if (navigate is not null)
                {
                    start.ArgumentList.Add("--page"); start.ArgumentList.Add(navigate.Page);
                    if (navigate.SuggestProcess is not null) { start.ArgumentList.Add("--suggest"); start.ArgumentList.Add(navigate.SuggestProcess); }
                }
                Process.Start(start);
            }
            else _tray.ShowBalloonTip(3000, "LightCrosshair", TextFor("MissingUi"), ToolTipIcon.Info);
        }
        catch (Exception ex) { Program.LogError(ex, "Open settings"); }
    }
    private string TextFor(string key, params object[] args) => CoreStrings.Get(Document.Settings.Language, key, args);
    private void UpdateTrayText()
    {
        var items = _tray.ContextMenuStrip!.Items;
        items[0].Text = TextFor(Document.Settings.CrosshairVisible ? "HideCrosshair" : "ShowCrosshair");
        items[1].Text = TextFor("Open"); items[2].Text = TextFor("Exit");
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SystemDisplayChanged;
            _previewTimeout?.Cancel(); _previewTimeout?.Dispose(); _pipe?.Dispose();
            _captureTimer?.Dispose();
            _recordingTimer?.Dispose(); _input?.Dispose(); HotkeyManager.Instance.UnregisterAll();
            _foreground?.Dispose(); _fps.Dispose();
            _tray.Visible = false; _tray.ContextMenuStrip?.Dispose(); _tray.Icon?.Dispose(); _tray.Dispose();
        }
        base.Dispose(disposing);
    }
}
