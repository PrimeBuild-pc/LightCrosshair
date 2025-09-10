using System.Drawing;
using Xunit;
using LightCrosshair;

namespace LightCrosshair.Tests
{
    public class CompositeGeometryCalculationsTests
    {
        [Theory]
        [InlineData("CircleCross")]
        [InlineData("CirclePlus")]
        [InlineData("CircleX")]
        public void Composite_NoObstructionAtCenter_ExceptDot(string shape)
        {
            var renderer = new CrosshairRenderer();
            var p = new CrosshairProfile
            {
                EnumShape = CrosshairShape.Custom,
                Shape = shape,
                Size = 64,
                InnerSize = 14,
                EdgeColor = Color.White,
                EdgeThickness = 2,
                InnerShapeEdgeColor = Color.Lime,
                InnerThickness = 3,
                InnerGapSize = 6,
                AntiAlias = true
            };

            using var bmp = renderer.RenderIfNeeded(p);
            var center = bmp.GetPixel(bmp.Width / 2, bmp.Height / 2);
            Assert.Equal(0, center.A); // center must be transparent
        }

        [Fact]
        public void Composite_CircleDot_HasDotAtCenter()
        {
            var renderer = new CrosshairRenderer();
            var p = new CrosshairProfile
            {
                EnumShape = CrosshairShape.Custom,
                Shape = "CircleDot",
                Size = 64,
                InnerSize = 8,
                EdgeColor = Color.White,
                EdgeThickness = 2,
                InnerShapeEdgeColor = Color.Red,
                InnerThickness = 2,
                AntiAlias = true
            };

            using var bmp = renderer.RenderIfNeeded(p);
            var center = bmp.GetPixel(bmp.Width / 2, bmp.Height / 2);
            Assert.True(center.A > 0); // dot should be filled
        }
    }
}

