using System.Drawing;
using System.Windows.Forms;
using Xunit;

namespace LightCrosshair.Tests;

public class TrayMenuTests
{
    [Fact]
    public void TrayMenuDrawsInTheAppsDarkTokensWithTallItems()
    {
        using var menu = TrayMenu.Create();
        menu.Items.Add(TrayMenu.Item("Hide crosshair", (_, _) => { }));
        menu.Items.Add(TrayMenu.Item("Open", (_, _) => { }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(TrayMenu.Item("Exit", (_, _) => { }));
        menu.PerformLayout();
        using var bitmap = new Bitmap(menu.Width, menu.Height);
        menu.DrawToBitmap(bitmap, new Rectangle(Point.Empty, menu.Size));
        Assert.Equal(TrayMenu.Surface.ToArgb(), bitmap.GetPixel(menu.Width - 6, menu.Items[0].Bounds.Top + 2).ToArgb());
        Assert.Equal(TrayMenu.Border.ToArgb(), bitmap.GetPixel(0, menu.Height / 2).ToArgb());
        Assert.True(menu.Items[0].Height >= 30, $"item height {menu.Items[0].Height}");
    }
}
