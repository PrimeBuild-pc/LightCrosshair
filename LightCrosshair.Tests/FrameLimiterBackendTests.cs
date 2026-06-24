using LightCrosshair.FrameLimiting;
using Xunit;

namespace LightCrosshair.Tests
{
    public class FrameLimiterBackendTests
    {
        [Fact]
        public void NoOpFrameLimiter_Detect_Returns_Unavailable_Capability()
        {
            FrameLimiterCapability capability = NoOpFrameLimiter.Detect();

            Assert.Equal(FrameLimiterBackendKind.None, capability.BackendKind);
            Assert.Equal(FrameLimiterCapabilityLevel.Unsupported, capability.Level);
            Assert.False(capability.IsAvailable);
            Assert.False(capability.CanApplyLimit);
            Assert.False(capability.RequiresExternalTool);
            Assert.False(capability.RequiresNativeComponent);
            Assert.False(capability.RequiresInjection);
            Assert.Contains("not applying a frame cap", capability.EvidenceText);
        }

        [Fact]
        public void NoOpFrameLimiter_Apply_Returns_Unavailable_And_Does_Not_Activate()
        {
            var request = new FrameLimiterRequest(FrameLimiterTarget.ForProcessName("sample.exe"), 120);

            FrameLimiterResult result = NoOpFrameLimiter.Apply(request);

            Assert.False(result.Succeeded);
            Assert.Equal(FrameLimiterResultKind.Unavailable, result.ResultKind);
            Assert.Equal(FrameLimiterStatusKind.Unavailable, result.Status.StatusKind);
            Assert.False(result.Status.IsLimitActive);
            Assert.False(result.Status.IsTelemetryValidated);
            Assert.Null(result.Status.TargetFps);
            Assert.Contains("not applied", result.EvidenceText);
        }

        [Fact]
        public void NoOpFrameLimiter_Clear_Is_Idempotent_NoOp()
        {
            FrameLimiterResult result = NoOpFrameLimiter.Clear();

            Assert.True(result.Succeeded);
            Assert.Equal(FrameLimiterResultKind.ClearNoOp, result.ResultKind);
            Assert.Equal(FrameLimiterStatusKind.Inactive, result.Status.StatusKind);
            Assert.False(result.Status.IsLimitActive);
            Assert.False(result.Status.IsTelemetryValidated);
        }

        [Fact]
        public void NoOpFrameLimiter_GetStatus_Returns_Unavailable_Status()
        {
            FrameLimiterStatus status = NoOpFrameLimiter.GetStatus();

            Assert.Equal(FrameLimiterBackendKind.None, status.BackendKind);
            Assert.Equal(FrameLimiterStatusKind.Unavailable, status.StatusKind);
            Assert.False(status.IsLimitActive);
            Assert.False(status.IsTelemetryValidated);
            Assert.Null(status.TargetFps);
            Assert.Contains("not applying a frame cap", status.EvidenceText);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(14.99)]
        [InlineData(1000.01)]
        public void Apply_InvalidFps_Is_Rejected(double targetFps)
        {
            FrameLimiterResult result = NoOpFrameLimiter.Apply(
                new FrameLimiterRequest(FrameLimiterTarget.ForProcessName("sample.exe"), targetFps));

            Assert.False(result.Succeeded);
            Assert.Equal(FrameLimiterResultKind.RejectedInvalidFps, result.ResultKind);
            Assert.False(result.Status.IsLimitActive);
        }

        [Fact]
        public void Apply_EmptyTarget_Is_Rejected()
        {
            FrameLimiterResult result = NoOpFrameLimiter.Apply(new FrameLimiterRequest(default, 60));

            Assert.False(result.Succeeded);
            Assert.Equal(FrameLimiterResultKind.RejectedInvalidTarget, result.ResultKind);
            Assert.False(result.Status.IsLimitActive);
        }

        [Fact]
        public void FrameLimiterCapability_DiagnosticsOnly_Backend_Cannot_Apply_Limit()
        {
            var capability = FrameLimiterCapability.DiagnosticsOnly(
                FrameLimiterBackendKind.PresentMonValidation,
                "PresentMon validation",
                requiresExternalTool: true,
                "Read-only validation provider can observe frame timing but cannot apply a cap.");

            Assert.Equal(FrameLimiterCapabilityLevel.DiagnosticsOnly, capability.Level);
            Assert.True(capability.IsAvailable);
            Assert.False(capability.CanApplyLimit);
            Assert.True(capability.RequiresExternalTool);
            Assert.False(capability.RequiresNativeComponent);
            Assert.False(capability.RequiresInjection);
        }
    }
}
