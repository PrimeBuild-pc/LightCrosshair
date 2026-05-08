using System;

namespace LightCrosshair
{
    public enum FpsOverlayDisplayMode
    {
        Off = 0,
        Minimal = 1,
        Detailed = 2
    }

    internal readonly record struct FpsOverlayRuntimePolicy(
        bool ShouldShow,
        FpsOverlayDisplayMode EffectiveDisplayMode,
        bool UltraLightweight,
        bool ShowFps,
        bool ShowFrameTime,
        bool ShowFramePacing,
        bool ShowGeneratedFrames,
        bool ShowGraph,
        int TimerIntervalMs)
    {
        public const int UltraLightweightRefreshMs = 500;

        public static FpsOverlayRuntimePolicy FromConfig(CrosshairConfig cfg)
        {
            var configuredMode = NormalizeDisplayMode(cfg.FpsOverlayMode);
            bool shouldShow = cfg.EnableFpsOverlay && configuredMode != FpsOverlayDisplayMode.Off;
            bool ultra = cfg.UltraLightweightMode;
            var effectiveMode = ultra && configuredMode == FpsOverlayDisplayMode.Detailed
                ? FpsOverlayDisplayMode.Minimal
                : configuredMode;

            bool showFps = cfg.ShowFps;
            bool showFrameTime = cfg.ShowFrameTime;
            bool showPacing = !ultra && effectiveMode == FpsOverlayDisplayMode.Detailed && (cfg.ShowFramePacing || cfg.ShowFpsDiagnostics);
            bool showGeneratedFrames = !ultra && effectiveMode == FpsOverlayDisplayMode.Detailed && cfg.ShowGenFrames;
            bool showGraph = !ultra && effectiveMode == FpsOverlayDisplayMode.Detailed && cfg.ShowFrametimeGraph;

            int timerInterval = ultra
                ? UltraLightweightRefreshMs
                : showGraph
                    ? CrosshairConfig.NormalizeGraphRefreshRatePreset(cfg.GraphRefreshRateMs)
                    : SystemFpsMonitor.PreferredUiTextRefreshMs;

            return new FpsOverlayRuntimePolicy(
                shouldShow,
                effectiveMode,
                ultra,
                showFps,
                showFrameTime,
                showPacing,
                showGeneratedFrames,
                showGraph,
                Math.Clamp(timerInterval, 33, 1000));
        }

        private static FpsOverlayDisplayMode NormalizeDisplayMode(FpsOverlayDisplayMode mode) =>
            Enum.IsDefined(typeof(FpsOverlayDisplayMode), mode)
                ? mode
                : FpsOverlayDisplayMode.Minimal;
    }
}
