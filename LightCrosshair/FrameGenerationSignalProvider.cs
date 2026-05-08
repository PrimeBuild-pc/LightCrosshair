using System.Threading;
using System.Threading.Tasks;

namespace LightCrosshair
{
    internal enum FrameGenerationProviderKind
    {
        None,
        PresentMon,
        Rtss,
        NativeNgxDlssg,
        NativeStreamlineDlssg,
        VendorApi,
        GameCooperative
    }

    internal enum FrameGenerationProviderCapability
    {
        Unsupported,
        HeuristicOnly,
        EstimatedExternal,
        VerifiedExternalFrameType,
        VerifiedInProcessState,
        VerifiedCooperativeAppSignal
    }

    internal readonly record struct FrameGenerationProviderSignal(
        FrameGenerationProviderKind ProviderKind,
        FrameGenerationProviderCapability Capability,
        bool IsAvailable,
        bool IsVerified,
        bool IsDetected,
        string Vendor,
        string Technology,
        double? EstimatedGeneratedFrameRatio,
        double? EstimatedAppFps,
        double? PresentedFps,
        string EvidenceText)
    {
        public static FrameGenerationProviderSignal Unavailable(
            FrameGenerationProviderKind providerKind,
            string evidenceText) =>
            new(
                providerKind,
                FrameGenerationProviderCapability.Unsupported,
                false,
                false,
                false,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                evidenceText);

        public FrameGenerationVerifiedSignal? ToVerifiedSignal()
        {
            if (!IsAvailable || !IsVerified)
            {
                return null;
            }

            return new FrameGenerationVerifiedSignal(
                IsDetected,
                Vendor,
                Technology,
                IsDetected ? EstimatedGeneratedFrameRatio : null,
                IsDetected ? EstimatedAppFps : null,
                PresentedFps,
                EvidenceText);
        }
    }

    internal interface IFrameGenerationSignalProvider
    {
        FrameGenerationProviderKind Kind { get; }
        bool IsEnabled { get; }

        ValueTask<FrameGenerationProviderSignal> TryGetSignalAsync(
            int? processId,
            CancellationToken cancellationToken);
    }

    internal sealed class NoOpFrameGenerationSignalProvider : IFrameGenerationSignalProvider
    {
        public FrameGenerationProviderKind Kind => FrameGenerationProviderKind.None;
        public bool IsEnabled => false;

        public ValueTask<FrameGenerationProviderSignal> TryGetSignalAsync(
            int? processId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(FrameGenerationProviderSignal.Unavailable(
                Kind,
                "No verified frame-generation signal provider is enabled."));
        }
    }
}
