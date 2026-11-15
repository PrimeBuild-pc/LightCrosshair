using System.Diagnostics;

namespace LightCrosshair;

// Bootstrap tray shell. The layered renderer replaces this legacy bitmap in C3.
internal sealed class Form1 : Form
{
    private readonly NotifyIcon _tray;
    private readonly CrosshairRenderer _renderer = new();
    private readonly Bitmap _bitmap;
    private bool _shown = true;
    internal Form1()
    {
        Text = "LightCrosshair Core";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        _bitmap = _renderer.RenderIfNeeded(new CrosshairProfile());
        ClientSize = _bitmap.Size;
        BackColor = Color.Black;
        var bounds = OverlayMonitorSelector.ResolveBounds(null);
        Location = new(bounds.Left + bounds.Width / 2 - Width / 2, bounds.Top + bounds.Height / 2 - Height / 2);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show/Hide crosshair", null, (_, _) => { _shown = !_shown; Visible = _shown; });
        menu.Items.Add("Open LightCrosshair", null, (_, _) => OpenSettings());
        menu.Items.Add("Exit", null, (_, _) => Close());
        _tray = new() { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application, Text = "LightCrosshair", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => OpenSettings();
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x20 | 0x80; return cp; } }
    protected override void OnPaint(PaintEventArgs e) => e.Graphics.DrawImageUnscaled(_bitmap, 0, 0);
    private void OpenSettings()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "LightCrosshair.UI.exe");
        if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        else _tray.ShowBalloonTip(3000, "LightCrosshair", "Install the settings app beside LightCrosshair.", ToolTipIcon.Info);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _tray.Visible = false; _tray.ContextMenuStrip?.Dispose(); _tray.Dispose(); _renderer.Dispose(); }
        base.Dispose(disposing);
    }
}
