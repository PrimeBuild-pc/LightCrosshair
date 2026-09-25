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

        [Fact]
        public void OverlayMonitor_UsesConfiguredDisplay_WhenAvailable()
        {
            int index = OverlayMonitorSelector.ResolveIndex(
                @"\\.\display2",
                new[] { @"\\.\DISPLAY1", @"\\.\DISPLAY2" },
                primaryIndex: 0);

            Assert.Equal(1, index);
        }

        [Fact]
        public void OverlayMonitor_FallsBackToPrimary_WhenConfiguredDisplayIsUnavailable()
        {
            int index = OverlayMonitorSelector.ResolveIndex(
                @"\\.\DISPLAY3",
                new[] { @"\\.\DISPLAY1", @"\\.\DISPLAY2" },
                primaryIndex: 1);

            Assert.Equal(1, index);
        }

        [Theory]
        [InlineData(@"\\.\DISPLAY1", @"\\.\DISPLAY2", @"\\.\DISPLAY1")]
        [InlineData("", @"\\.\DISPLAY2", @"\\.\DISPLAY2")]
        public void MonitorConfig_PrefersDistinctSetting_AndMigratesLegacySetting(
            string configuredDeviceName,
            string legacyDeviceName,
            string expected)
        {
            Assert.Equal(expected, CrosshairConfig.ResolveMonitorDeviceName(configuredDeviceName, legacyDeviceName));
        }
    }
}
