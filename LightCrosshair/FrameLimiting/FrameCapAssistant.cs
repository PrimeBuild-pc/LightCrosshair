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
    }
}
