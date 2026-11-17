using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal sealed class FpsOverlay : IDisposable
{
    private readonly SystemFpsMonitor _capture = new();
    private readonly FpsWindow _window;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private FpsOverlaySettings _settings = new();
    private string? _monitor;
    private string? _last;
    private string _language = "auto";
    private long _lastGraphPaint;
    internal bool Enabled => _timer.Enabled;
    internal FpsOverlay()
    {
        _window = new(_capture, () => _settings.ShowFrametimeGraph && Enabled) { Text = "LightCrosshair FPS" };
        _timer.Tick += (_, _) => Draw();
    }
    private sealed class FpsWindow(SystemFpsMonitor capture, Func<bool> graph) : LayeredWindow
    {
        protected override void WndProc(ref Message m)
        {
            // External swapchain probe: numeric telemetry without screen capture.
            if (Program.PerfProbesEnabled && m.Msg == 0x806A)
            { m.Result = (IntPtr)(int)(capture.Snapshot().Fps * 100); return; }
            if (Program.PerfProbesEnabled && m.Msg == 0x806B)
            { m.Result = (IntPtr)(capture.Snapshot().HasData ? 0 : 1); return; }
            if (Program.PerfProbesEnabled && m.Msg == 0x8072) { m.Result = (IntPtr)(graph() ? 1 : 0); return; }
            base.WndProc(ref m);
        }
    }
    public void Configure(bool enabled, int pid, FpsOverlaySettings settings, string? crosshairMonitor, string language = "auto")
    {
        if (!enabled)
        {
            _ = StopAsync(); return;
        }
        _settings = settings; _monitor = settings.MonitorDeviceName ?? crosshairMonitor; _language = language;
        _capture.Start(pid); _last = null; _timer.Interval = settings.ShowFrametimeGraph ? 50 : 250; _timer.Start(); Draw(); _window.ReinforceTopmost();
    }
    private void Draw()
    {
        bool graph = _settings.ShowFrametimeGraph;
        long tick = Environment.TickCount64;
        if (graph && _lastGraphPaint != 0 && tick - _lastGraphPaint < 50) return;
        var sample = _capture.Snapshot();
        string text = sample.HasData ? $"{sample.Fps:F0} FPS" : "– FPS";
        if (_settings.ShowFrametime) text += sample.HasData ? $"\n{sample.Frametime:F1} ms" : "\n– ms";
        if (_settings.Show1PercentLow) text += sample.HasData ? $"\n1% {sample.OnePercentLow:F0}" : "\n1% –";
        if (!sample.HasData) text += "\n" + CoreStrings.Get(_language, sample.Reason ?? "FpsNoFrames");
        Rectangle monitor = OverlayMonitorSelector.ResolveBounds(_monitor);
        int textHeight = 12 + 16 * (1 + (_settings.ShowFrametime ? 1 : 0) + (_settings.Show1PercentLow ? 1 : 0) + (!sample.HasData ? 1 : 0));
        var bounds = new Rectangle(monitor.Left + _settings.OffsetX, monitor.Top + _settings.OffsetY, (int)(270 * _settings.Scale), (int)((textHeight + (graph ? 56 : 0)) * _settings.Scale));
        string key = text + bounds + _settings.TextColor + _settings.GraphColor + _settings.BackgroundColor + _settings.BackgroundOpacity;
        if (!graph && key == _last) return;
        var points = graph ? _capture.Graph() : [];
        _window.Present(bounds, g =>
        {
            g.Clear(Color.FromArgb((int)(_settings.BackgroundOpacity * 255), ColorTranslator.FromHtml(_settings.BackgroundColor)));
            using var font = new Font("Segoe UI", (float)(12 * _settings.Scale), FontStyle.Regular, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(ColorTranslator.FromHtml(_settings.TextColor));
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.DrawString(text, font, brush, 5, 5);
            if (graph)
            {
                float left = (float)(6 * _settings.Scale), top = (float)(textHeight * _settings.Scale);
                float width = bounds.Width - left * 2, height = (float)(44 * _settings.Scale);
                var graphColor = ColorTranslator.FromHtml(_settings.GraphColor ?? _settings.TextColor);
                using var grid = new Pen(Color.FromArgb(60, graphColor));
                g.DrawLine(grid, left, top + height, left + width, top + height);
                double maximum = Math.Max(16.667, points.Length == 0 ? 0 : points.Max(p => p.Frametime) * 1.1);
                if (points.Length > 1)
                {
                    var line = points.Select(p => new PointF(left + (float)((2000 - p.AgeMilliseconds) / 2000 * width), top + height - (float)(Math.Clamp(p.Frametime / maximum, 0, 1) * height))).ToArray();
                    using var pen = new Pen(graphColor, (float)_settings.Scale);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.DrawLines(pen, line);
                }
            }
        });
        _window.AccessibleName = text;
        if (!_window.Visible) _window.Show(); _last = key;
        if (graph) _lastGraphPaint = tick;
    }
    internal Task StopAsync() { _timer.Stop(); _window.Hide(); _last = null; _lastGraphPaint = 0; return _capture.StopAsync(); }
    public void Dispose() { _timer.Dispose(); _ = _capture.StopAsync(); _window.Dispose(); }
}
