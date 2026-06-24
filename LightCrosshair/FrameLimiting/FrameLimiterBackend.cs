using System;

namespace LightCrosshair.FrameLimiting
{
    internal enum FrameLimiterBackendKind
    {
        None,
        RtssExternal,
        PresentMonValidation,
        VendorDriverProfile,
        NativeInProcess
    }

    internal enum FrameLimiterCapabilityLevel
    {
        Unsupported,
        DiagnosticsOnly,
        ExternalLimiterAvailable,
        ExternalLimiterConfigurable,
        DriverProfileConfigurable,
        NativeInProcessRequiresApproval
    }

    internal enum FrameLimiterResultKind
    {
        RejectedInvalidTarget,
        RejectedInvalidFps,
        Unavailable,
        Unsupported,
        ApplyFailed,
        ClearNoOp,
        Succeeded
    }

    internal enum FrameLimiterStatusKind
    {
        Unavailable,
        Inactive,
        Configured,
        ValidationPending,
        ActiveValidated,
        ActiveUnvalidated,
        ValidationFailed
    }

    internal readonly record struct FrameLimiterTarget(int? ProcessId, string ProcessName, string ExecutablePath)
    {
        public bool HasTarget =>
            (ProcessId.HasValue && ProcessId.Value > 0) ||
            !string.IsNullOrWhiteSpace(ProcessName) ||
            !string.IsNullOrWhiteSpace(ExecutablePath);

        public static FrameLimiterTarget ForProcessName(string processName) =>
            new(null, processName ?? string.Empty, string.Empty);
    }

    internal readonly record struct FrameLimiterRequest(FrameLimiterTarget Target, double TargetFps)
    {
        public const double MinimumTargetFps = 15.0;
        public const double MaximumTargetFps = 1000.0;

        public FrameLimiterResult? Validate(FrameLimiterBackendKind backendKind = FrameLimiterBackendKind.None)
        {
            if (!Target.HasTarget)
            {
                return FrameLimiterResult.RejectedInvalidTarget(
                    backendKind,
                    "Frame limiter request rejected: no explicit target process or executable was provided.");
            }

            if (double.IsNaN(TargetFps) ||
                double.IsInfinity(TargetFps) ||
                TargetFps < MinimumTargetFps ||
                TargetFps > MaximumTargetFps)
            {
                return FrameLimiterResult.RejectedInvalidFps(
                    backendKind,
                    $"Frame limiter request rejected: target FPS must be finite and between {MinimumTargetFps:0} and {MaximumTargetFps:0}.");
            }

            return null;
        }
    }

    internal readonly record struct FrameLimiterCapability(
        FrameLimiterBackendKind BackendKind,
        FrameLimiterCapabilityLevel Level,
        bool IsAvailable,
        bool CanApplyLimit,
        bool RequiresExternalTool,
        bool RequiresNativeComponent,
        bool RequiresInjection,
        string BackendName,
        string EvidenceText)
    {
        public static FrameLimiterCapability Unavailable(FrameLimiterBackendKind backendKind, string backendName, string evidenceText) =>
            new(backendKind, FrameLimiterCapabilityLevel.Unsupported, false, false, false, false, false, backendName, evidenceText);

        public static FrameLimiterCapability DiagnosticsOnly(
            FrameLimiterBackendKind backendKind,
            string backendName,
            bool requiresExternalTool,
            string evidenceText) =>
            new(backendKind, FrameLimiterCapabilityLevel.DiagnosticsOnly, true, false, requiresExternalTool, false, false, backendName, evidenceText);
    }

    internal readonly record struct FrameLimiterStatus(
        FrameLimiterBackendKind BackendKind,
        FrameLimiterStatusKind StatusKind,
        bool IsLimitConfigured,
        bool IsLimitActive,
        bool IsTelemetryValidated,
        double? TargetFps,
        string EvidenceText)
    {
        public static FrameLimiterStatus Unavailable(FrameLimiterBackendKind backendKind, string evidenceText) =>
            new(backendKind, FrameLimiterStatusKind.Unavailable, false, false, false, null, evidenceText);

        public static FrameLimiterStatus Inactive(FrameLimiterBackendKind backendKind, string evidenceText) =>
            new(backendKind, FrameLimiterStatusKind.Inactive, false, false, false, null, evidenceText);
    }

    internal readonly record struct FrameLimiterResult(
        FrameLimiterBackendKind BackendKind,
        FrameLimiterResultKind ResultKind,
        bool Succeeded,
        FrameLimiterStatus Status,
        string EvidenceText)
    {
        public static FrameLimiterResult RejectedInvalidTarget(FrameLimiterBackendKind backendKind, string evidenceText) =>
            Failure(backendKind, FrameLimiterResultKind.RejectedInvalidTarget, evidenceText);

        public static FrameLimiterResult RejectedInvalidFps(FrameLimiterBackendKind backendKind, string evidenceText) =>
            Failure(backendKind, FrameLimiterResultKind.RejectedInvalidFps, evidenceText);

        public static FrameLimiterResult Unavailable(FrameLimiterBackendKind backendKind, string evidenceText) =>
            Failure(backendKind, FrameLimiterResultKind.Unavailable, evidenceText);

        public static FrameLimiterResult Unsupported(FrameLimiterBackendKind backendKind, string evidenceText) =>
            Failure(backendKind, FrameLimiterResultKind.Unsupported, evidenceText);

        public static FrameLimiterResult ClearNoOp(FrameLimiterBackendKind backendKind, string evidenceText) =>
            new(backendKind, FrameLimiterResultKind.ClearNoOp, true, FrameLimiterStatus.Inactive(backendKind, evidenceText), evidenceText);

        private static FrameLimiterResult Failure(FrameLimiterBackendKind backendKind, FrameLimiterResultKind resultKind, string evidenceText) =>
            new(backendKind, resultKind, false, FrameLimiterStatus.Unavailable(backendKind, evidenceText), evidenceText);
    }

    internal static class NoOpFrameLimiter
    {
        private const string BackendName = "Unavailable frame limiter backend";
        private const string Evidence = "No real frame limiter backend is configured. LightCrosshair is not applying a frame cap.";

        public static FrameLimiterCapability Detect() =>
            FrameLimiterCapability.Unavailable(FrameLimiterBackendKind.None, BackendName, Evidence);

        public static FrameLimiterResult Apply(FrameLimiterRequest request) =>
            request.Validate(FrameLimiterBackendKind.None) ??
            FrameLimiterResult.Unavailable(
                FrameLimiterBackendKind.None,
                "Frame cap was not applied: no supported external, driver, or native limiter backend is available.");

        public static FrameLimiterResult Clear() =>
            FrameLimiterResult.ClearNoOp(
                FrameLimiterBackendKind.None,
                "No frame limiter state was active, so clear completed without changing runtime behavior.");

        public static FrameLimiterStatus GetStatus() =>
            FrameLimiterStatus.Unavailable(FrameLimiterBackendKind.None, Evidence);
    }
}
