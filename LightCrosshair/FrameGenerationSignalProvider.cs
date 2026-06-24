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
        public static FrameGenerationProviderSignal Unavailable(FrameGenerationProviderKind providerKind, string evidenceText) =>
            new(providerKind, FrameGenerationProviderCapability.Unsupported, false, false, false, string.Empty, string.Empty, null, null, null, evidenceText);

        public FrameGenerationVerifiedSignal? ToVerifiedSignal() =>
            IsAvailable && IsVerified
                ? new FrameGenerationVerifiedSignal(
                    IsDetected,
                    Vendor,
                    Technology,
                    IsDetected ? EstimatedGeneratedFrameRatio : null,
                    IsDetected ? EstimatedAppFps : null,
                    PresentedFps,
                    EvidenceText)
                : null;
    }

    internal static class NoOpFrameGenerationSignalProvider
    {
        public const FrameGenerationProviderKind Kind = FrameGenerationProviderKind.None;
        public const bool IsEnabled = false;

        public static FrameGenerationProviderSignal GetSignal() =>
            FrameGenerationProviderSignal.Unavailable(
                Kind,
                "No verified frame-generation signal provider is enabled.");
    }
}
