using System;
using System.Threading;
using System.Threading.Tasks;
using LightCrosshair.FrameLimiting;
using Xunit;

namespace LightCrosshair.Tests
{
    public class FrameLimiterBackendTests
    {
        [Fact]
        public async Task NoOpBackend_DetectAsync_Returns_Unavailable_Capability()
        {
            IFrameLimiterBackend backend = new NoOpFrameLimiterBackend();

            FrameLimiterCapability capability = await backend.DetectAsync(CancellationToken.None);

            Assert.Equal(FrameLimiterBackendKind.None, backend.Kind);
            Assert.False(string.IsNullOrWhiteSpace(backend.Name));
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
        public async Task NoOpBackend_ApplyAsync_Returns_Unavailable_And_Does_Not_Activate()
        {
            IFrameLimiterBackend backend = new NoOpFrameLimiterBackend();
            var request = new FrameLimiterRequest(
                FrameLimiterTarget.ForProcessName("sample.exe"),
                120);

            FrameLimiterResult result = await backend.ApplyAsync(request, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(FrameLimiterResultKind.Unavailable, result.ResultKind);
            Assert.Equal(FrameLimiterStatusKind.Unavailable, result.Status.StatusKind);
            Assert.False(result.Status.IsLimitConfigured);
            Assert.False(result.Status.IsLimitActive);
            Assert.False(result.Status.IsTelemetryValidated);
            Assert.Null(result.Status.TargetFps);
            Assert.Contains("not applied", result.EvidenceText);
        }

        [Fact]
        public async Task NoOpBackend_ClearAsync_Is_Idempotent_NoOp()
        {
            IFrameLimiterBackend backend = new NoOpFrameLimiterBackend();

            FrameLimiterResult result = await backend.ClearAsync(
                FrameLimiterTarget.ForProcessName("sample.exe"),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Equal(FrameLimiterResultKind.ClearNoOp, result.ResultKind);
            Assert.Equal(FrameLimiterStatusKind.Inactive, result.Status.StatusKind);
            Assert.False(result.Status.IsLimitConfigured);
            Assert.False(result.Status.IsLimitActive);
            Assert.False(result.Status.IsTelemetryValidated);
        }

        [Fact]
        public async Task NoOpBackend_GetStatusAsync_Returns_Inactive_Unavailable_Status()
        {
            IFrameLimiterBackend backend = new NoOpFrameLimiterBackend();

            FrameLimiterStatus status = await backend.GetStatusAsync(
                FrameLimiterTarget.ForProcessName("sample.exe"),
                CancellationToken.None);

            Assert.Equal(FrameLimiterBackendKind.None, status.BackendKind);
            Assert.Equal(FrameLimiterStatusKind.Unavailable, status.StatusKind);
            Assert.False(status.IsLimitConfigured);
            Assert.False(status.IsLimitActive);
            Assert.False(status.IsTelemetryValidated);
            Assert.Null(status.TargetFps);
            Assert.Contains("not applying a frame cap", status.EvidenceText);
        }

        [Fact]
        public async Task NoOpBackend_Operations_Honor_Cancellation()
        {
            IFrameLimiterBackend backend = new NoOpFrameLimiterBackend();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await backend.DetectAsync(cts.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await backend.ApplyAsync(
                    new FrameLimiterRequest(FrameLimiterTarget.ForProcessName("sample.exe"), 60),
                    cts.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await backend.ClearAsync(FrameLimiterTarget.ForProcessName("sample.exe"), cts.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await backend.GetStatusAsync(FrameLimiterTarget.ForProcessName("sample.exe"), cts.Token));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(14.99)]
        [InlineData(1000.01)]
        public async Task FrameLimiterController_InvalidFps_Is_Rejected_Before_Backend_Apply(double targetFps)
        {
            var backend = new CountingBackend();
            var controller = new FrameLimiterController(backend);

            FrameLimiterResult result = await controller.ApplyAsync(
                new FrameLimiterRequest(FrameLimiterTarget.ForProcessName("sample.exe"), targetFps),
                CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(FrameLimiterResultKind.RejectedInvalidFps, result.ResultKind);
            Assert.Equal(0, backend.ApplyCount);
            Assert.False(result.Status.IsLimitActive);
        }

        [Fact]
        public async Task FrameLimiterController_EmptyTarget_Is_Rejected_Before_Backend_Apply()
        {
            var backend = new CountingBackend();
            var controller = new FrameLimiterController(backend);

            FrameLimiterResult result = await controller.ApplyAsync(
                new FrameLimiterRequest(default, 60),
                CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(FrameLimiterResultKind.RejectedInvalidTarget, result.ResultKind);
            Assert.Equal(0, backend.ApplyCount);
            Assert.False(result.Status.IsLimitActive);
        }

        [Fact]
        public async Task FrameLimiterController_NoAvailableBackend_Does_Not_Report_Active()
        {
            var controller = new FrameLimiterController(new NoOpFrameLimiterBackend());

            FrameLimiterResult result = await controller.ApplyAsync(
                new FrameLimiterRequest(FrameLimiterTarget.ForProcessName("sample.exe"), 60),
                CancellationToken.None);
            FrameLimiterStatus status = await controller.GetStatusAsync(
                FrameLimiterTarget.ForProcessName("sample.exe"),
                CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(FrameLimiterResultKind.Unavailable, result.ResultKind);
            Assert.False(result.Status.IsLimitActive);
            Assert.Equal(FrameLimiterStatusKind.Unavailable, status.StatusKind);
            Assert.False(status.IsLimitActive);
            Assert.False(status.IsTelemetryValidated);
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

        private sealed class CountingBackend : IFrameLimiterBackend
        {
            public int ApplyCount { get; private set; }
            public FrameLimiterBackendKind Kind => FrameLimiterBackendKind.RtssExternal;
            public string Name => "Counting backend";

            public ValueTask<FrameLimiterCapability> DetectAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(FrameLimiterCapability.DiagnosticsOnly(
                    Kind,
                    Name,
                    requiresExternalTool: true,
                    "Counting backend test capability."));
            }

            public ValueTask<FrameLimiterResult> ApplyAsync(
                FrameLimiterRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ApplyCount++;
                return ValueTask.FromResult(FrameLimiterResult.Unsupported(
                    Kind,
                    "Counting backend does not implement a real limiter."));
            }

            public ValueTask<FrameLimiterResult> ClearAsync(
                FrameLimiterTarget target,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(FrameLimiterResult.ClearNoOp(Kind, "Nothing to clear."));
            }

            public ValueTask<FrameLimiterStatus> GetStatusAsync(
                FrameLimiterTarget target,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(FrameLimiterStatus.Inactive(Kind, "Inactive test backend."));
            }
        }
    }
}
