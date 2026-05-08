using System;
using System.Collections.Generic;
using System.Linq;

namespace LightCrosshair
{
    public enum FrameGenerationState
    {
        Unknown,
        Unsupported,
        NotDetected,
        Suspected,
        Detected
    }

    public enum FrameGenerationEvidence
    {
        None,
        InsufficientSamples,
        UnsupportedTelemetrySource,
        StableNativeCadence,
        NoPlausiblePattern,
        AlternatingCadenceRatio,
        EstimatedAppFpsRatio,
        VerifiedExternalSignal
    }

    public readonly record struct FrameGenerationVerifiedSignal(
        bool IsDetected,
        string Vendor,
        string Technology,
        double? EstimatedGeneratedFrameRatio,
        double? EstimatedAppFps,
        double? PresentedFps,
        string EvidenceText);

    public readonly record struct FrameGenerationDetectionResult(
        FrameGenerationState State,
        double Confidence,
        bool IsVerifiedSignal,
        FrameGenerationEvidence Evidence,
        string EvidenceText,
        double PresentedFps,
        double? EstimatedAppFps,
        double? RenderedFps,
        double? EstimatedGeneratedFrameRatio,
        int GeneratedFrameCount,
        string Vendor,
        string Technology)
    {
        public static FrameGenerationDetectionResult Unknown { get; } = new(
            FrameGenerationState.Unknown,
            0,
            false,
            FrameGenerationEvidence.None,
            "No frame generation evidence available.",
            0,
            null,
            null,
            null,
            0,
            string.Empty,
            string.Empty);
    }

    internal sealed class FrameGenerationDetector
    {
        internal const int MinimumSamples = 24;
        private const int MaxSamples = 180;
        private readonly Queue<double> _frameTimesMs = new(MaxSamples);

        public void Reset()
        {
            _frameTimesMs.Clear();
        }

        public FrameGenerationDetectionResult AddFrame(
            double frameTimeMs,
            string telemetrySource,
            FrameGenerationVerifiedSignal? verifiedSignal = null,
            double? estimatedAppFps = null)
        {
            if (!verifiedSignal.HasValue && !IsCadenceTelemetrySource(telemetrySource))
            {
                return Evaluate(Array.Empty<double>(), telemetrySource);
            }

            if (!double.IsNaN(frameTimeMs) && !double.IsInfinity(frameTimeMs) && frameTimeMs > 0.05 && frameTimeMs <= 500)
            {
                if (_frameTimesMs.Count == MaxSamples)
                {
                    _frameTimesMs.Dequeue();
                }

                _frameTimesMs.Enqueue(frameTimeMs);
            }

            return Evaluate(_frameTimesMs.ToArray(), telemetrySource, verifiedSignal, estimatedAppFps);
        }

        public static FrameGenerationDetectionResult Evaluate(
            IReadOnlyList<double> frameTimesMs,
            string telemetrySource,
            FrameGenerationVerifiedSignal? verifiedSignal = null,
            double? estimatedAppFps = null)
        {
            if (verifiedSignal.HasValue)
            {
                return FromVerifiedSignal(verifiedSignal.Value, frameTimesMs);
            }

            if (!IsCadenceTelemetrySource(telemetrySource))
            {
                return new FrameGenerationDetectionResult(
                    FrameGenerationState.Unsupported,
                    0,
                    false,
                    FrameGenerationEvidence.UnsupportedTelemetrySource,
                    $"{telemetrySource} does not expose verified generated-frame data.",
                    CalculatePresentedFps(frameTimesMs),
                    null,
                    null,
                    null,
                    0,
                    string.Empty,
                    string.Empty);
            }

            var validSamples = frameTimesMs
                .Where(v => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0.05 && v <= 500)
                .ToArray();

            double presentedFps = CalculatePresentedFps(validSamples);
            if (validSamples.Length < MinimumSamples)
            {
                return new FrameGenerationDetectionResult(
                    FrameGenerationState.Unknown,
                    0.05,
                    false,
                    FrameGenerationEvidence.InsufficientSamples,
                    $"Need at least {MinimumSamples} samples for cadence analysis.",
                    presentedFps,
                    null,
                    null,
                    null,
                    0,
                    string.Empty,
                    string.Empty);
            }

            if (estimatedAppFps.HasValue && estimatedAppFps.Value > 1)
            {
                var appRatioResult = EvaluateEstimatedAppRatio(validSamples, estimatedAppFps.Value, presentedFps);
                if (appRatioResult.State == FrameGenerationState.Suspected)
                {
                    return appRatioResult;
                }
            }

            var alternatingResult = EvaluateAlternatingCadence(validSamples, presentedFps);
            if (alternatingResult.State == FrameGenerationState.Suspected)
            {
                return alternatingResult;
            }

            var stats = FrameTimingStatistics.Calculate(validSamples, validSamples.Length);
            bool stableNativeCadence =
                stats.HasData &&
                stats.AverageFrameTimeMs > 0 &&
                stats.StandardDeviationFrameTimeMs <= stats.AverageFrameTimeMs * 0.035 &&
                stats.JitterMs <= stats.AverageFrameTimeMs * 0.03;

            return new FrameGenerationDetectionResult(
                FrameGenerationState.NotDetected,
                stableNativeCadence ? 0.3 : 0.15,
                false,
                stableNativeCadence ? FrameGenerationEvidence.StableNativeCadence : FrameGenerationEvidence.NoPlausiblePattern,
                stableNativeCadence
                    ? "Stable presented cadence without a separate app/render FPS signal is treated as native rendering."
                    : "No plausible generated-frame cadence pattern was found.",
                presentedFps,
                null,
                null,
                null,
                0,
                string.Empty,
                string.Empty);
        }

        private static FrameGenerationDetectionResult FromVerifiedSignal(FrameGenerationVerifiedSignal signal, IReadOnlyList<double> frameTimesMs)
        {
            double presentedFps = signal.PresentedFps.GetValueOrDefault(CalculatePresentedFps(frameTimesMs));
            double? ratio = signal.IsDetected ? NormalizeRatio(signal.EstimatedGeneratedFrameRatio) : null;
            int generatedCount = ratio.HasValue
                ? EstimateGeneratedFrames(frameTimesMs.Count, ratio.Value)
                : 0;

            return new FrameGenerationDetectionResult(
                signal.IsDetected ? FrameGenerationState.Detected : FrameGenerationState.NotDetected,
                signal.IsDetected ? 1.0 : 0.9,
                true,
                FrameGenerationEvidence.VerifiedExternalSignal,
                string.IsNullOrWhiteSpace(signal.EvidenceText)
                    ? "Verified external frame-generation signal."
                    : signal.EvidenceText,
                presentedFps,
                signal.IsDetected ? signal.EstimatedAppFps : null,
                signal.IsDetected ? signal.EstimatedAppFps : null,
                ratio,
                generatedCount,
                signal.Vendor ?? string.Empty,
                signal.Technology ?? string.Empty);
        }

        private static FrameGenerationDetectionResult EvaluateEstimatedAppRatio(double[] validSamples, double estimatedAppFps, double presentedFps)
        {
            double observedRatio = presentedFps / estimatedAppFps;
            double? ratio = MatchGenerationRatio(observedRatio, tolerance: 0.18);
            if (!ratio.HasValue)
            {
                return FrameGenerationDetectionResult.Unknown;
            }

            var stats = FrameTimingStatistics.Calculate(validSamples, validSamples.Length);
            double stability = stats.StabilityScore / 100.0;
            double confidence = Math.Clamp(0.5 + (stability * 0.25), 0.5, 0.75);

            return new FrameGenerationDetectionResult(
                FrameGenerationState.Suspected,
                confidence,
                false,
                FrameGenerationEvidence.EstimatedAppFpsRatio,
                $"Presented FPS is close to {ratio.Value:0.#}x the estimated app FPS.",
                presentedFps,
                estimatedAppFps,
                estimatedAppFps,
                ratio,
                EstimateGeneratedFrames(validSamples.Length, ratio.Value),
                string.Empty,
                string.Empty);
        }

        private static FrameGenerationDetectionResult EvaluateAlternatingCadence(double[] validSamples, double presentedFps)
        {
            int pairs = validSamples.Length - 1;
            if (pairs <= 0)
            {
                return FrameGenerationDetectionResult.Unknown;
            }

            int matchedPairs = 0;
            int ratio2Count = 0;
            int ratio3Count = 0;
            int ratio4Count = 0;
            int directionChanges = 0;
            int lastDirection = 0;

            for (int i = 1; i < validSamples.Length; i++)
            {
                double previous = validSamples[i - 1];
                double current = validSamples[i];
                double small = Math.Min(previous, current);
                double large = Math.Max(previous, current);
                if (small <= 0) continue;

                double observed = large / small;
                double? matched = MatchGenerationRatio(observed, tolerance: 0.16);
                if (matched.HasValue)
                {
                    matchedPairs++;
                    if (Math.Abs(matched.Value - 2.0) < 0.01) ratio2Count++;
                    else if (Math.Abs(matched.Value - 3.0) < 0.01) ratio3Count++;
                    else if (Math.Abs(matched.Value - 4.0) < 0.01) ratio4Count++;
                }

                int direction = current > previous ? 1 : current < previous ? -1 : 0;
                if (direction != 0 && lastDirection != 0 && direction != lastDirection)
                {
                    directionChanges++;
                }

                if (direction != 0)
                {
                    lastDirection = direction;
                }
            }

            double pairSupport = matchedPairs / (double)pairs;
            double alternationSupport = pairs > 1 ? directionChanges / (double)(pairs - 1) : 0;
            if (matchedPairs == 0)
            {
                return FrameGenerationDetectionResult.Unknown;
            }

            int dominantCount = ratio2Count;
            double bestRatio = 2.0;
            if (ratio3Count > dominantCount)
            {
                dominantCount = ratio3Count;
                bestRatio = 3.0;
            }
            if (ratio4Count > dominantCount)
            {
                dominantCount = ratio4Count;
                bestRatio = 4.0;
            }

            pairSupport = dominantCount / (double)pairs;
            if (pairSupport < 0.65 || alternationSupport < 0.55)
            {
                return FrameGenerationDetectionResult.Unknown;
            }

            double confidence = Math.Clamp(0.35 + (pairSupport * 0.25) + (alternationSupport * 0.2), 0.45, 0.72);
            double estimatedAppFps = bestRatio > 0 ? presentedFps / bestRatio : 0;

            return new FrameGenerationDetectionResult(
                FrameGenerationState.Suspected,
                confidence,
                false,
                FrameGenerationEvidence.AlternatingCadenceRatio,
                $"Alternating presented-frame cadence is close to {bestRatio:0.#}x.",
                presentedFps,
                estimatedAppFps > 0 ? estimatedAppFps : null,
                estimatedAppFps > 0 ? estimatedAppFps : null,
                bestRatio,
                EstimateGeneratedFrames(validSamples.Length, bestRatio),
                string.Empty,
                string.Empty);
        }

        private static bool IsCadenceTelemetrySource(string telemetrySource)
        {
            return string.Equals(telemetrySource, "ETW", StringComparison.OrdinalIgnoreCase)
                || string.Equals(telemetrySource, "PresentMon", StringComparison.OrdinalIgnoreCase);
        }

        private static double CalculatePresentedFps(IReadOnlyList<double> frameTimesMs)
        {
            if (frameTimesMs.Count == 0)
            {
                return 0;
            }

            double sum = 0;
            int count = 0;
            for (int i = 0; i < frameTimesMs.Count; i++)
            {
                double value = frameTimesMs[i];
                if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.05 || value > 500)
                {
                    continue;
                }

                sum += value;
                count++;
            }

            return count == 0 || sum <= 0 ? 0 : count * 1000.0 / sum;
        }

        private static double? MatchGenerationRatio(double observedRatio, double tolerance)
        {
            double[] plausibleRatios = { 2.0, 3.0, 4.0 };
            for (int i = 0; i < plausibleRatios.Length; i++)
            {
                double target = plausibleRatios[i];
                if (Math.Abs(observedRatio - target) <= target * tolerance)
                {
                    return target;
                }
            }

            return null;
        }

        private static double? NormalizeRatio(double? ratio)
        {
            if (!ratio.HasValue || double.IsNaN(ratio.Value) || double.IsInfinity(ratio.Value) || ratio.Value < 1.5)
            {
                return null;
            }

            return ratio.Value;
        }

        private static int EstimateGeneratedFrames(int sampleCount, double ratio)
        {
            if (sampleCount <= 0 || ratio <= 1)
            {
                return 0;
            }

            double generatedFraction = (ratio - 1.0) / ratio;
            return Math.Max(0, (int)Math.Round(sampleCount * generatedFraction));
        }
    }
}
