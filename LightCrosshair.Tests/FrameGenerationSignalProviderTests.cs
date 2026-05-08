using System.Threading;
using System.Threading.Tasks;
using LightCrosshair;
using Xunit;

namespace LightCrosshair.Tests
{
    public class FrameGenerationSignalProviderTests
    {
        [Fact]
        public async Task NoOpProvider_Returns_Unavailable_Unverified_Signal()
        {
            IFrameGenerationSignalProvider provider = new NoOpFrameGenerationSignalProvider();

            FrameGenerationProviderSignal signal = await provider.TryGetSignalAsync(
                processId: 1234,
                CancellationToken.None);

            Assert.Equal(FrameGenerationProviderKind.None, provider.Kind);
            Assert.False(provider.IsEnabled);
            Assert.False(signal.IsAvailable);
            Assert.False(signal.IsVerified);
            Assert.False(signal.IsDetected);
            Assert.Equal(FrameGenerationProviderKind.None, signal.ProviderKind);
            Assert.Equal(FrameGenerationProviderCapability.Unsupported, signal.Capability);
        }

        [Fact]
        public void ProviderSignal_ToVerifiedSignal_Returns_Null_When_Unavailable_Or_Unverified()
        {
            var unavailable = FrameGenerationProviderSignal.Unavailable(
                FrameGenerationProviderKind.None,
                "No provider is enabled.");

            var unverified = new FrameGenerationProviderSignal(
                FrameGenerationProviderKind.PresentMon,
                FrameGenerationProviderCapability.EstimatedExternal,
                true,
                false,
                true,
                "NVIDIA",
                "DLSS-G",
                2.0,
                60,
                120,
                "FPS ratio only.");

            Assert.Null(unavailable.ToVerifiedSignal());
            Assert.Null(unverified.ToVerifiedSignal());
        }

        [Fact]
        public void ProviderSignal_ToVerifiedSignal_Maps_Verified_Active_Signal()
        {
            var signal = new FrameGenerationProviderSignal(
                FrameGenerationProviderKind.NativeNgxDlssg,
                FrameGenerationProviderCapability.VerifiedInProcessState,
                true,
                true,
                true,
                "NVIDIA",
                "DLSS-G",
                3.0,
                40,
                120,
                "In-process NGX/Streamline state reported active DLSS-G.");

            FrameGenerationVerifiedSignal? verified = signal.ToVerifiedSignal();

            Assert.True(verified.HasValue);
            Assert.True(verified.Value.IsDetected);
            Assert.Equal("NVIDIA", verified.Value.Vendor);
            Assert.Equal("DLSS-G", verified.Value.Technology);
            Assert.Equal(3.0, verified.Value.EstimatedGeneratedFrameRatio);
            Assert.Equal(40, verified.Value.EstimatedAppFps);
            Assert.Equal(120, verified.Value.PresentedFps);
        }

        [Fact]
        public void ProviderSignal_ToVerifiedSignal_Maps_Verified_Inactive_Signal()
        {
            var signal = new FrameGenerationProviderSignal(
                FrameGenerationProviderKind.PresentMon,
                FrameGenerationProviderCapability.VerifiedExternalFrameType,
                true,
                true,
                false,
                "NVIDIA",
                "DLSS-G",
                null,
                null,
                60,
                "Verified frame-type provider reported no generated frames.");

            FrameGenerationVerifiedSignal? verified = signal.ToVerifiedSignal();

            Assert.True(verified.HasValue);
            Assert.False(verified.Value.IsDetected);
            Assert.Null(verified.Value.EstimatedGeneratedFrameRatio);
            Assert.Equal(60, verified.Value.PresentedFps);
        }
    }
}
