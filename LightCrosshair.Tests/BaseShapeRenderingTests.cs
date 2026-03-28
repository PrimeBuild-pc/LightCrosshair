using System.Drawing;
using Xunit;
using LightCrosshair;

namespace LightCrosshair.Tests
{
    public class BaseShapeRenderingTests
    {
        [Fact]
        public void Cross_WithGap_KeepsCenterTransparent()
        {
            var renderer = new CrosshairRenderer();
            renderer.AntiAlias = false;

            var p = new CrosshairProfile
            {
                EnumShape = CrosshairShape.Cross,
                Shape = "Cross",
                Size = 48,
                Thickness = 4,
                GapSize = 8,
                OutlineEnabled = false,
                OuterColor = Color.White,
                EdgeColor = Color.Red
            };

            using var bmp = renderer.RenderIfNeeded(p);
            var center = bmp.GetPixel(bmp.Width / 2, bmp.Height / 2);
            Assert.Equal(0, center.A);
        }

        [Fact]
        public void Cross_Outline_UsesSecondaryEdgeColor()
        {
            var renderer = new CrosshairRenderer();
            renderer.AntiAlias = false;

            var p = new CrosshairProfile
            {
                EnumShape = CrosshairShape.Cross,
                Shape = "Cross",
                Size = 48,
                Thickness = 2,
                GapSize = 0,
                OutlineEnabled = false,
                OuterColor = Color.White,
                EdgeColor = Color.Red
            };

            using var noOutline = renderer.RenderIfNeeded(p);
            int cx = noOutline.Width / 2;
            int cy = noOutline.Height / 2;
            var probeWithout = noOutline.GetPixel(cx + 10, cy + 2);
            Assert.Equal(0, probeWithout.A);

            p.OutlineEnabled = true;
            using var withOutline = renderer.RenderIfNeeded(p);
            var probeWith = withOutline.GetPixel(cx + 10, cy + 2);
            Assert.True(probeWith.A > 0);
            Assert.Equal(Color.Red.R, probeWith.R);
            Assert.Equal(Color.Red.G, probeWith.G);
            Assert.Equal(Color.Red.B, probeWith.B);
        }
    }
}
