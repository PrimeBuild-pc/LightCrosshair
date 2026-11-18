using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace LightCrosshair;

/// <summary>Tray menu in the app's dark graphite tokens (docs/v2/DESIGN.md), WinForms only.</summary>
internal static class TrayMenu
{
    internal static readonly Color Surface = ColorTranslator.FromHtml("#16181B");
    internal static readonly Color SurfaceRaised = ColorTranslator.FromHtml("#1D2024");
    internal static readonly Color Border = ColorTranslator.FromHtml("#26292E");
    internal static readonly Color TextPrimary = ColorTranslator.FromHtml("#E8EAED");
    internal static readonly Color TextDisabled = ColorTranslator.FromHtml("#5F6368");
    internal static readonly Color Accent = ColorTranslator.FromHtml("#3DDC97");

    internal static ContextMenuStrip Create()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new Renderer(),
            ShowImageMargin = false,
            BackColor = Surface,
            ForeColor = TextPrimary,
            Padding = new Padding(4),
            // Points scale with the menu's DPI; Segoe UI Variable falls back to Segoe UI on Windows 10.
            Font = new Font(FontFamily.Families.Any(f => f.Name == "Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI", 9.75f),
        };
        menu.HandleCreated += (_, _) => RoundCorners(menu.Handle);
        return menu;
    }

    internal static ToolStripMenuItem Item(string text, EventHandler onClick) =>
        new(text, null, onClick) { Padding = new Padding(8, 7, 12, 7), ForeColor = TextPrimary };

    private static void RoundCorners(IntPtr hwnd)
    {
        // Windows 11 only; older builds ignore unknown attributes.
        int round = 2; // DWMWCP_ROUND
        int border = Border.R | Border.G << 8 | Border.B << 16;
        _ = DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, 34, ref border, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private sealed class Renderer() : ToolStripProfessionalRenderer(new Palette())
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Surface);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            var bounds = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
            using var raised = new SolidBrush(SurfaceRaised);
            using var path = Rounded(bounds, 4);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(raised, path);
            // A small accent marker, not a full accent fill.
            using var accent = new SolidBrush(Accent);
            e.Graphics.FillRectangle(accent, bounds.X + 2, bounds.Y + bounds.Height / 2 - 6, 2, 12);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? TextPrimary : TextDisabled;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Border);
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private sealed class Palette : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => SurfaceRaised;
        public override Color MenuItemSelected => SurfaceRaised;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Surface;
    }
}
