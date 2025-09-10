using System;
using System.Drawing;
using Xunit;
using LightCrosshair;

namespace LightCrosshair.Tests
{
    public class RendererCompositeHashChangeTests
    {
        [Fact]
        public void ChangingCompositeInnerComponent_RegeneratesBitmap_AndCenterVisibilityMaintained()
        {
            var renderer = new CrosshairRenderer();

            var p = new CrosshairProfile
            {
                EnumShape = CrosshairShape.Custom,
                Shape = "CircleDot",
                Size = 60,
                InnerSize = 8,
                EdgeColor = Color.Lime,
                EdgeThickness = 2,
                InnerShapeEdgeColor = Color.Red,
                InnerThickness = 2,
                AntiAlias = true
            };

            using var bmp1 = renderer.RenderIfNeeded(p);
            Assert.NotNull(bmp1);

            // Center pixel for CircleDot should be filled (dot is filled)
            var c1 = bmp1.GetPixel(bmp1.Width / 2, bmp1.Height / 2);
            Assert.True(c1.A > 0);

            // Switch inner to Cross; renderer should regenerate and center must be transparent
            p.Shape = "CircleCross";
            using var bmp2 = renderer.RenderIfNeeded(p);
            Assert.NotNull(bmp2);
            Assert.False(object.ReferenceEquals(bmp1, bmp2));

            var c2 = bmp2.GetPixel(bmp2.Width / 2, bmp2.Height / 2);
            Assert.Equal(0, c2.A);
        }
    }
}

