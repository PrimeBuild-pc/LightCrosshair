using System.Drawing;
using Xunit;
using LightCrosshair;

namespace LightCrosshair.Tests
{
    public class GapConfigurationTests
    {
        [Fact]
        public void CrossDot_OuterGap_Respects_GapSize_And_Regenerates()
        {
            var renderer = new CrosshairRenderer();
            renderer.AntiAlias = false;
            var p = new CrosshairProfile
            {
                EnumShape = CrosshairShape.Custom,
                Shape = "CrossDot",
                Size = 64,
                InnerSize = 6,
                EdgeColor = Color.White,
                EdgeThickness = 2,
                InnerShapeEdgeColor = Color.Lime,
                InnerThickness = 2,
                GapSize = 0,            // outer cross gap
                AntiAlias = false
            };

            using var bmp0 = renderer.RenderIfNeeded(p);
            Assert.NotNull(bmp0);
            var c0 = bmp0.GetPixel(bmp0.Width / 2, bmp0.Height / 2);
            Assert.True(c0.A > 0); // with gap=0, cross passes through center until clamped min; dot also at center

            p.GapSize = 8; // increase outer gap
            using var bmp1 = renderer.RenderIfNeeded(p);
            Assert.NotNull(bmp1);
            Assert.False(object.ReferenceEquals(bmp0, bmp1)); // geometry changed => new bitmap
            var c1 = bmp1.GetPixel(bmp1.Width / 2, bmp1.Height / 2);
            Assert.True(c1.A > 0); // dot still visible
            // Sample right of center strictly inside the configured gap (avoid end pixel and dot radius)
            var sampleX = bmp1.Width / 2 + (p.GapSize - 1);
            var rightOfCenter = bmp1.GetPixel(sampleX, bmp1.Height / 2);
            Assert.Equal(0, rightOfCenter.A); // within the horizontal gap region
        }

        [Fact]
        public void CircleCross_InnerGap_Respects_InnerGapSize_And_Regenerates()
        {
            var renderer = new CrosshairRenderer();
            var p = new CrosshairProfile
            {
                EnumShape = CrosshairShape.Custom,
                Shape = "CircleCross",
                Size = 64,              // outer circle
                InnerSize = 18,         // inner cross radius
                EdgeColor = Color.White,
                EdgeThickness = 2,
                InnerShapeEdgeColor = Color.Lime,
                InnerThickness = 3,
                InnerGapSize = 0,
                AntiAlias = true
            };

            using var bmp0 = renderer.RenderIfNeeded(p);
            Assert.NotNull(bmp0);
            var c0 = bmp0.GetPixel(bmp0.Width / 2, bmp0.Height / 2);
            // With innerGap=0, our renderer clamps to >=1 for visibility, so center should be transparent
            Assert.Equal(0, c0.A);

            p.InnerGapSize = 8; // widen inner gap
            using var bmp1 = renderer.RenderIfNeeded(p);
            Assert.NotNull(bmp1);
            Assert.False(object.ReferenceEquals(bmp0, bmp1));
            var c1 = bmp1.GetPixel(bmp1.Width / 2, bmp1.Height / 2);
            Assert.Equal(0, c1.A); // still transparent at center
            // Sample just right of center to ensure gap is wider => still transparent
            var rightOfCenter = bmp1.GetPixel(bmp1.Width / 2 + 2, bmp1.Height / 2);
            Assert.Equal(0, rightOfCenter.A);
        }

        [Fact]
        public void CirclePlus_InnerGap_Respects_InnerGapSize_And_Regenerates()
        {
            var renderer = new CrosshairRenderer();
            var p = new CrosshairProfile
            {
                EnumShape = CrosshairShape.Custom,
                Shape = "CirclePlus",
                Size = 64,              // outer circle
                InnerSize = 16,         // inner plus radius
                EdgeColor = Color.White,
                EdgeThickness = 2,
                InnerShapeEdgeColor = Color.Red,
                InnerThickness = 2,
                InnerGapSize = 2,
                AntiAlias = true
            };

            using var bmp0 = renderer.RenderIfNeeded(p);
            Assert.NotNull(bmp0);
            var c0 = bmp0.GetPixel(bmp0.Width / 2, bmp0.Height / 2);
            Assert.Equal(0, c0.A); // plus has center gap

            p.InnerGapSize = 10; // increase inner plus gap
            using var bmp1 = renderer.RenderIfNeeded(p);
            Assert.NotNull(bmp1);
            Assert.False(object.ReferenceEquals(bmp0, bmp1));
            var c1 = bmp1.GetPixel(bmp1.Width / 2, bmp1.Height / 2);
            Assert.Equal(0, c1.A);
            var rightOfCenter = bmp1.GetPixel(bmp1.Width / 2 + 2, bmp1.Height / 2);
            Assert.Equal(0, rightOfCenter.A);
        }
    }
}

