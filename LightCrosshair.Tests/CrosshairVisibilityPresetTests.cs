using System.Drawing;
using LightCrosshair;
using Xunit;

namespace LightCrosshair.Tests
{
    public class CrosshairVisibilityPresetTests
    {
        [Theory]
        [InlineData(CrosshairVisibilityPresetKind.NeonCyan)]
        [InlineData(CrosshairVisibilityPresetKind.Lime)]
        [InlineData(CrosshairVisibilityPresetKind.Magenta)]
        [InlineData(CrosshairVisibilityPresetKind.Yellow)]
        public void Apply_VisibilityPreset_UsesOpaqueMainColor_And_EnablesBlackOutline(CrosshairVisibilityPresetKind kind)
        {
            var profile = new CrosshairProfile
            {
                Name = "Test",
                OutlineEnabled = false,
                OuterColor = Color.Red,
                InnerColor = Color.Red,
                EdgeColor = Color.Transparent,
                InnerShapeColor = Color.Transparent
            };

            CrosshairVisibilityPreset.Apply(profile, kind);

            Assert.True(profile.OutlineEnabled);
            Assert.Equal(255, profile.OuterColor.A);
            Assert.Equal(profile.OuterColor.ToArgb(), profile.InnerColor.ToArgb());
            Assert.Equal(Color.Black.ToArgb(), profile.EdgeColor.ToArgb());
            Assert.Equal(Color.Black.ToArgb(), profile.InnerShapeColor.ToArgb());
        }

        [Fact]
        public void Apply_DefaultVisibilityPreset_Does_Not_Change_Shape_Or_Size()
        {
            var profile = new CrosshairProfile
            {
                Shape = "Circle",
                EnumShape = CrosshairShape.Circle,
                Size = 33,
                Thickness = 4,
                GapSize = 7
            };

            CrosshairVisibilityPreset.Apply(profile, CrosshairVisibilityPresetKind.NeonCyan);

            Assert.Equal("Circle", profile.Shape);
            Assert.Equal(CrosshairShape.Circle, profile.EnumShape);
            Assert.Equal(33, profile.Size);
            Assert.Equal(4, profile.Thickness);
            Assert.Equal(7, profile.GapSize);
        }
    }
}
