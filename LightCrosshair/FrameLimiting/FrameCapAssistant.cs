using System;

namespace LightCrosshair.FrameLimiting
{
    internal readonly record struct FrameCapAssistantStatus(
        double RefreshRateHz,
        int TargetFps,
        bool IsAssistantOnly,
        bool HasActiveLimiterBackend,
        bool CanApplyLimit,
        string StatusText,
        string HelpText);

    internal readonly record struct FrameCapAssistantSettings(
        double RefreshRateHz,
        int TargetFps,
        bool UsedDetectedRefreshRate);

    internal static class FrameCapAssistant
    {
        public const double DefaultRefreshRateHz = 144.0;

        public static int RecommendTargetFps(double refreshRateHz)
        {
            if (!double.IsFinite(refreshRateHz) || refreshRateHz <= 0)
            {
                return 0;
            }

            double offset = refreshRateHz <= 75.0 ? 2.0 : 3.0;
            return Math.Max(15, (int)Math.Round(refreshRateHz - offset, MidpointRounding.AwayFromZero));
        }

        public static FrameCapAssistantSettings InitializeFromDetectedRefreshRate(
            double configuredRefreshRateHz,
            int configuredTargetFps,
            double? detectedRefreshRateHz)
        {
            double refreshRateHz = NormalizeRefreshRate(configuredRefreshRateHz);
            int targetFps = NormalizeTargetFps(configuredTargetFps);

            bool hasDefaultAssistantValues =
                Math.Abs(refreshRateHz - DefaultRefreshRateHz) < 0.01 &&
                targetFps == RecommendTargetFps(DefaultRefreshRateHz);
            if (!hasDefaultAssistantValues ||
                detectedRefreshRateHz == null ||
                !double.IsFinite(detectedRefreshRateHz.Value) ||
                detectedRefreshRateHz.Value <= 0)
            {
                return new FrameCapAssistantSettings(refreshRateHz, targetFps, false);
            }

            double detectedHz = NormalizeRefreshRate(detectedRefreshRateHz.Value);
            return new FrameCapAssistantSettings(
                detectedHz,
                NormalizeTargetFps(RecommendTargetFps(detectedHz)),
                true);
        }

        public static FrameCapAssistantStatus BuildStatus(
            double refreshRateHz,
            int targetFps,
            FrameLimiterCapability capability)
        {
            bool canApplyLimit = capability.CanApplyLimit;
            string statusText = canApplyLimit
                ? "Limiter backend available, but this assistant is not applying a cap."
                : "Assistant only: No active limiter backend.";
            string helpText = canApplyLimit
                ? "Use an approved backend flow before claiming an active frame limit."
                : "Real frame limiting requires an in-game limiter, external tool, driver option, or external or future backend.";

            return new FrameCapAssistantStatus(
                refreshRateHz,
                Math.Clamp(targetFps, 0, 1000),
                IsAssistantOnly: true,
                HasActiveLimiterBackend: false,
                canApplyLimit,
                statusText,
                helpText);
        }

        private static double NormalizeRefreshRate(double value)
        {
            if (!double.IsFinite(value) || value <= 0)
            {
                return DefaultRefreshRateHz;
            }

            return Math.Clamp(value, 30.0, 500.0);
        }

        private static int NormalizeTargetFps(int value)
        {
            if (value <= 0)
            {
                return RecommendTargetFps(DefaultRefreshRateHz);
            }

            return Math.Clamp(value, 15, 1000);
        }
    }
}
