using System;
using System.Drawing;
using System.IO;
using LightCrosshair.Contracts;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public class CrosshairSymmetryTests
{
    private readonly LayerRenderer _renderer = new(Path.GetTempPath());

    // Every built-in shape must mirror exactly around its own center, for odd and even strokes.
    [Theory]
    [InlineData(LayerKind.Arms, false)] [InlineData(LayerKind.Arms, true)]
    [InlineData(LayerKind.Dot, false)] [InlineData(LayerKind.Dot, true)]
    [InlineData(LayerKind.Ring, false)] [InlineData(LayerKind.Ring, true)]
    [InlineData(LayerKind.Box, false)] [InlineData(LayerKind.Box, true)]
    public void ShapesAreMirrorSymmetricAroundTheirCenter(LayerKind kind, bool smooth)
    {
        foreach (int thickness in new[] { 1, 2, 3, 4, 5, 6 })
            foreach (int gap in new[] { 0, 1, 2, 3, 4, 7 })
                foreach (int length in new[] { 5, 8, 11, 16 })
                {
                    var layer = new CrosshairLayer { Kind = kind, Thickness = thickness, Gap = gap, Length = length, Outline = gap % 2 == 1 };
                    using var bitmap = _renderer.Render(new CrosshairDesign { AntiAlias = smooth, Layers = [layer] }, 96);
                    // Center in pixel units: 48 + .5 (odd) or 48 + 1 (even); a mirrored pixel index is 2c - 1 - x.
                    int twice = 2 * 48 + (int)(LayerRenderer.Center(layer) * 2) - 1;
                    for (int y = 0; y < 96; y++)
                        for (int x = 0; x < 96; x++)
                        {
                            int mx = twice - x, my = twice - y;
                            if (mx is < 0 or >= 96 || my is < 0 or >= 96) continue;
                            int a = bitmap.GetPixel(x, y).A;
                            Assert.True(Math.Abs(a - bitmap.GetPixel(mx, y).A) <= 2, $"{kind} t={thickness} gap={gap} len={length} smooth={smooth}: x mirror at ({x},{y})");
                            Assert.True(Math.Abs(a - bitmap.GetPixel(x, my).A) <= 2, $"{kind} t={thickness} gap={gap} len={length} smooth={smooth}: y mirror at ({x},{y})");
                        }
                }
    }

    [Fact]
    public void EvenStrokeArmsKeepTheSameGapOnBothSides()
    {
        var layer = new CrosshairLayer { Thickness = 2, Gap = 4, Length = 6, Arms = ArmMask.Left | ArmMask.Right };
        using var bitmap = _renderer.Render(new CrosshairDesign { AntiAlias = false, Layers = [layer] }, 64);
        // The 2 px stroke covers rows 32-33, so the center is the corner at x = 33: four empty columns each side.
        int lastLeft = -1, firstRight = -1;
        for (int x = 0; x < 64; x++) if (bitmap.GetPixel(x, 32).A > 0) { if (x < 33) lastLeft = x; else if (firstRight < 0) firstRight = x; }
        Assert.Equal(33 - 4 - 1, lastLeft);
        Assert.Equal(33 + 4, firstRight);
    }

    [Fact]
    public void SmoothEdgesRoundTheArmEnds()
    {
        var layer = new CrosshairLayer { Thickness = 4, Gap = 3, Length = 10 };
        using var crisp = _renderer.Render(new CrosshairDesign { AntiAlias = false, Layers = [layer] }, 64);
        using var smooth = _renderer.Render(new CrosshairDesign { AntiAlias = true, Layers = [layer] }, 64);
        int differences = 0;
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) if (crisp.GetPixel(x, y).A != smooth.GetPixel(x, y).A) differences++;
        Assert.True(differences >= 16, $"Smooth edges changed only {differences} pixels");
        // The outer corner of the right arm is rounded off; the middle of the arm stays solid.
        Assert.Equal(255, crisp.GetPixel(32 + 1 + 3 + 9, 31).A);
        Assert.True(smooth.GetPixel(32 + 1 + 3 + 9, 31).A < 255);
        Assert.Equal(255, smooth.GetPixel(32 + 1 + 3 + 5, 32).A);
    }
}
