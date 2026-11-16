using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal sealed class FpsOverlay : IDisposable
{
    private readonly SystemFpsMonitor _capture = new();
    private readonly LayeredWindow _window = new() { Text = "LightCrosshair FPS" };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private FpsOverlaySettings _settings = new();
    private string? _monitor;
    private string? _last;
    internal bool Enabled => _timer.Enabled;
    internal FpsOverlay() => _timer.Tick += (_, _) => Draw();
    public void Configure(bool enabled, int pid, FpsOverlaySettings settings, string? crosshairMonitor)
    {
        if (!enabled)
        {
            _timer.Stop(); _window.Hide(); _ = _capture.StopAsync(); _last = null; return;
        }
        _settings = settings; _monitor = settings.MonitorDeviceName ?? crosshairMonitor;
        _capture.Start(pid); _last = null; _timer.Start(); Draw(); _window.ReinforceTopmost();
    }
    private void Draw()
    {
        var sample = _capture.Snapshot();
        string text = sample.HasData ? $"{sample.Fps:F0} FPS" : "— FPS";
        if (_settings.ShowFrametime) text += sample.HasData ? $"\n{sample.Frametime:F1} ms" : "\n— ms";
        if (_settings.Show1PercentLow) text += sample.HasData ? $"\n1% {sample.OnePercentLow:F0}" : "\n1% —";
        Rectangle monitor = OverlayMonitorSelector.ResolveBounds(_monitor);
        var bounds = new Rectangle(monitor.Left + _settings.OffsetX, monitor.Top + _settings.OffsetY, (int)(180 * _settings.Scale), (int)(80 * _settings.Scale));
        string key = text + bounds + _settings.TextColor + _settings.BackgroundColor + _settings.BackgroundOpacity;
        if (key == _last) return;
        _window.Present(bounds, g =>
        {
            g.Clear(Color.FromArgb((int)(_settings.BackgroundOpacity * 255), ColorTranslator.FromHtml(_settings.BackgroundColor)));
            using var font = new Font("Segoe UI", (float)(12 * _settings.Scale), FontStyle.Regular, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(ColorTranslator.FromHtml(_settings.TextColor));
            g.DrawString(text, font, brush, 5, 5);
        });
        if (!_window.Visible) _window.Show(); _last = key;
    }
    public void Dispose() { _timer.Dispose(); _ = _capture.StopAsync(); _window.Dispose(); }
}
