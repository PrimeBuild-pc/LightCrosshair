using Xunit;
using LightCrosshair;

namespace LightCrosshair.Tests
{
    public class OverlayCompositionTests
    {
        [Fact]
        public void FpsOverlay_BecomingVisible_ReinforcesCrosshair_WhenCrosshairShouldDisplay()
        {
            Assert.True(Form1.ShouldReinforceCrosshairAfterFpsOverlayUpdate(
                shouldDisplayCrosshairOverlay: true,
                shouldShowFpsOverlay: true,
                fpsOverlayBecameVisible: true));
        }

        [Theory]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        public void FpsOverlay_DoesNotReinforceCrosshair_WhenCompositionConditionIsMissing(
            bool shouldDisplayCrosshairOverlay,
            bool shouldShowFpsOverlay,
            bool fpsOverlayBecameVisible)
        {
            Assert.False(Form1.ShouldReinforceCrosshairAfterFpsOverlayUpdate(
                shouldDisplayCrosshairOverlay,
                shouldShowFpsOverlay,
                fpsOverlayBecameVisible));
        }
    }
}
