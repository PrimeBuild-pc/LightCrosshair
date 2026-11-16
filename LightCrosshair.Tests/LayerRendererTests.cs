using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using LightCrosshair.Contracts;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public class LayerRendererTests
{
    private readonly LayerRenderer _renderer = new(Path.GetTempPath());
    [Theory]
    [InlineData(1, 0)] [InlineData(2, 0)] [InlineData(3, -1)] [InlineData(4, -1)]
    public void OddAndEvenStrokesUseTheSpecifiedCenterPixel(int thickness, int start)
    {
        var d = new CrosshairDesign { Name = "Stroke", AntiAlias = false, Layers = [new() { Arms = ArmMask.Right, Gap = 3, Length = 5, Thickness = thickness }] };
        using var b = _renderer.Render(d, 32);
        Assert.Equal(start, LayerRenderer.StrokeStart(thickness));
        for (int y = 0; y < 32; y++) Assert.Equal(y >= 16 + start && y < 16 + start + thickness ? 255 : 0, b.GetPixel(20, y).A);
    }
    [Theory]
    [InlineData(0)] [InlineData(90)] [InlineData(180)] [InlineData(270)]
    public void QuarterTurnsKeepIntegerEdgesAndEvenBias(double rotation)
    {
        var l = new CrosshairLayer { Arms = ArmMask.All, Thickness = 2, Length = 5, Gap = 3, Rotation = rotation };
        using var p = _renderer.Geometry(l);
        Assert.All(p.PathPoints, point => { Assert.Equal(MathF.Round(point.X), point.X); Assert.Equal(MathF.Round(point.Y), point.Y); });
        using var b = _renderer.Render(new() { AntiAlias = false, Layers = [l] }, 32);
        Assert.Equal(255, b.GetPixel(20, 16).A); Assert.Equal(255, b.GetPixel(20, 17).A); Assert.Equal(0, b.GetPixel(20, 15).A);
    }
    [Theory]
    [InlineData(LayerKind.Arms)] [InlineData(LayerKind.Dot)] [InlineData(LayerKind.Ring)] [InlineData(LayerKind.Box)]
    public void GeometryAndOutlineFitTheirBoundsAndRenderDeterministically(LayerKind kind)
    {
        var d = new CrosshairDesign { AntiAlias = true, Layers = [new() { Kind = kind, Length = 17, Gap = 2, Thickness = 3, Outline = true, OutlineThickness = 2, Rotation = 37, OffsetX = 3, OffsetY = -5 }] };
        var bounds = _renderer.Bounds(d);
        using var a = _renderer.Render(d, 128); using var b = _renderer.Render(d, 128);
        Assert.Equal(PixelFormat.Format32bppPArgb, a.PixelFormat);
        int count = 0;
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
        {
            Assert.Equal(a.GetPixel(x, y), b.GetPixel(x, y));
            if (a.GetPixel(x, y).A == 0) continue;
            count++; Assert.True(bounds.Contains(x - 64, y - 64));
        }
        Assert.True(count > 0);
    }
    [Fact]
    public void TransparentOutlineHasNoDoubleAlphaAndRingHasAHole()
    {
        using var ring = _renderer.Render(new() { AntiAlias = false, Layers = [new() { Kind = LayerKind.Ring, Length = 20, Thickness = 2, Outline = true, Opacity = .5 }] }, 64);
        Assert.Equal(0, ring.GetPixel(32, 32).A);
        Assert.InRange(ring.GetPixel(32, 23).A, 126, 128);
        using var cross = _renderer.Render(new() { AntiAlias = false, Layers = [new() { Gap = 0, Length = 8, Thickness = 3, Opacity = .5 }] }, 64);
        Assert.InRange(cross.GetPixel(32, 32).A, 126, 128);
    }
    [Fact]
    public void ImagesUseTheSameBoundsAndAlphaRenderer()
    {
        string directory = Path.Combine(Path.GetTempPath(), "lc2-render-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            string name = Guid.NewGuid().ToString("N") + ".png";
            using (var image = new Bitmap(4, 6)) { using var g = Graphics.FromImage(image); g.Clear(Color.Red); image.Save(Path.Combine(directory, name), ImageFormat.Png); }
            var renderer = new LayerRenderer(directory);
            var d = new CrosshairDesign { AntiAlias = false, Layers = [new() { Kind = LayerKind.Image, ImageFile = name, ImageScale = 2, Rotation = 90 }] };
            Assert.Equal(new Size(12, 8), renderer.Bounds(d).Size);
            using var rendered = renderer.Render(d, 64);
            Assert.Equal(255, rendered.GetPixel(32, 32).A);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact]
    public void ThickRotatedOutlinesStayInsideTheOverlayDib()
    {
        var d = new CrosshairDesign { AntiAlias = false, Layers = [new() { Length = 20, Thickness = 12, Rotation = 45, Outline = true, OutlineThickness = 8 }] };
        var bounds = _renderer.Bounds(d); using var b = _renderer.Render(d, 128);
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            if (b.GetPixel(x, y).A > 0) Assert.True(bounds.Contains(x - 64, y - 64));
    }
}
