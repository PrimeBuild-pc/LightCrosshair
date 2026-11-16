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
        menu.Items.Add("Show/Hide crosshair", null, async (_, _) =>
        {
            try { Document.Settings.CrosshairVisible = !Document.Settings.CrosshairVisible; RefreshContext(); await Store.SaveAsync(Document); }
            catch (Exception ex) { Program.LogError(ex, "Toggle visibility"); }
        });
        menu.Items.Add("Open LightCrosshair", null, (_, _) => OpenSettings());
        menu.Items.Add("Exit", null, (_, _) => Close());
        _tray = new() { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application, Text = "LightCrosshair", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => OpenSettings();
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
            if (Store.Recovered) _tray.ShowBalloonTip(3000, "LightCrosshair", "Your library was recovered from a backup.", ToolTipIcon.Info);
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
        bool visible = PreviewDesign is not null || (_holdOverride ?? (_context?.Visible ?? settings.CrosshairVisible));
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
            else _tray.ShowBalloonTip(3000, "LightCrosshair", "Install the settings app beside LightCrosshair.", ToolTipIcon.Info);
        }
        catch (Exception ex) { Program.LogError(ex, "Open settings"); }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SystemDisplayChanged;
            _foreground?.Dispose(); _fps.Dispose();
            _tray.Visible = false; _tray.ContextMenuStrip?.Dispose(); _tray.Icon?.Dispose(); _tray.Dispose();
        }
        base.Dispose(disposing);
    }
}
