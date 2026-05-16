using LightCrosshair.FrameLimiting;
using Xunit;

namespace LightCrosshair.Tests
{
    public class FrameCapAssistantTests
    {
        [Theory]
        [InlineData(360, 357)]
        [InlineData(240, 237)]
        [InlineData(144, 141)]
        [InlineData(120, 117)]
        [InlineData(60, 58)]
        public void RecommendTargetFps_Stays_Slightly_Below_RefreshRate(double refreshRateHz, int expected)
        {
            int recommendation = FrameCapAssistant.RecommendTargetFps(refreshRateHz);

            Assert.Equal(expected, recommendation);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void RecommendTargetFps_InvalidRefreshRate_Returns_Zero(double refreshRateHz)
        {
            int recommendation = FrameCapAssistant.RecommendTargetFps(refreshRateHz);

            Assert.Equal(0, recommendation);
        }

        [Fact]
        public void InitializeFromDetectedRefreshRate_ReplacesDefaultAssistantValues()
        {
            var settings = FrameCapAssistant.InitializeFromDetectedRefreshRate(144, 141, 360);

            Assert.True(settings.UsedDetectedRefreshRate);
            Assert.Equal(360, settings.RefreshRateHz);
            Assert.Equal(357, settings.TargetFps);
        }

        [Fact]
        public void InitializeFromDetectedRefreshRate_PreservesUserCustomizedValues()
        {
            var settings = FrameCapAssistant.InitializeFromDetectedRefreshRate(240, 200, 360);

            Assert.False(settings.UsedDetectedRefreshRate);
            Assert.Equal(240, settings.RefreshRateHz);
            Assert.Equal(200, settings.TargetFps);
        }

        [Fact]
        public void InitializeFromDetectedRefreshRate_PreservesDefaultsWhenDetectionUnavailable()
        {
            var settings = FrameCapAssistant.InitializeFromDetectedRefreshRate(144, 141, null);

            Assert.False(settings.UsedDetectedRefreshRate);
            Assert.Equal(144, settings.RefreshRateHz);
            Assert.Equal(141, settings.TargetFps);
        }

        [Fact]
        public void BuildStatus_NoOpBackend_Is_AssistantOnly_And_Does_Not_Claim_ActiveLimiter()
        {
            var capability = FrameLimiterCapability.Unavailable(
                FrameLimiterBackendKind.None,
                "No limiter backend",
                "No active limiter backend; assistant only.");

            FrameCapAssistantStatus status = FrameCapAssistant.BuildStatus(360, 357, capability);

            Assert.Equal(360, status.RefreshRateHz);
            Assert.Equal(357, status.TargetFps);
            Assert.True(status.IsAssistantOnly);
            Assert.False(status.HasActiveLimiterBackend);
            Assert.False(status.CanApplyLimit);
            Assert.Contains("No active limiter backend", status.StatusText);
            Assert.Contains("external or future backend", status.HelpText);
        }
    }
}
