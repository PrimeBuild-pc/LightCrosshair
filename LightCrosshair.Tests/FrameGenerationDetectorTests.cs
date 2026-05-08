using System.Collections.Generic;
using LightCrosshair;
using Xunit;

namespace LightCrosshair.Tests
{
    public class FrameGenerationDetectorTests
    {
        [Fact]
        public void Evaluate_NoData_Returns_Unknown()
        {
            var result = FrameGenerationDetector.Evaluate(System.Array.Empty<double>(), "ETW");

            Assert.Equal(FrameGenerationState.Unknown, result.State);
            Assert.False(result.IsVerifiedSignal);
            Assert.Equal(FrameGenerationEvidence.InsufficientSamples, result.Evidence);
        }

        [Fact]
        public void Evaluate_InsufficientSamples_Returns_Unknown_With_Low_Confidence()
        {
            var result = FrameGenerationDetector.Evaluate(new[] { 16.7, 16.6, 16.8 }, "ETW");

            Assert.Equal(FrameGenerationState.Unknown, result.State);
            Assert.InRange(result.Confidence, 0, 0.1);
        }

        [Fact]
        public void Evaluate_StableNativeHighFps_Does_Not_Suspect_FrameGeneration()
        {
            double[] samples = Repeat(8.333, 90);

            var result = FrameGenerationDetector.Evaluate(samples, "ETW");

            Assert.Equal(FrameGenerationState.NotDetected, result.State);
            Assert.False(result.IsVerifiedSignal);
            Assert.Equal(FrameGenerationEvidence.StableNativeCadence, result.Evidence);
            Assert.Null(result.EstimatedGeneratedFrameRatio);
        }

        [Fact]
        public void Evaluate_AlternatingTwoToOneCadence_Returns_Suspected()
        {
            double[] samples = Alternating(8.333, 16.667, 80);

            var result = FrameGenerationDetector.Evaluate(samples, "ETW");

            Assert.Equal(FrameGenerationState.Suspected, result.State);
            Assert.False(result.IsVerifiedSignal);
            Assert.Equal(FrameGenerationEvidence.AlternatingCadenceRatio, result.Evidence);
            Assert.Equal(2.0, result.EstimatedGeneratedFrameRatio);
            Assert.InRange(result.Confidence, 0.55, 0.72);
            Assert.True(result.GeneratedFrameCount > 0);
        }

        [Fact]
        public void Evaluate_AlternatingThreeToOneCadence_Returns_Suspected()
        {
            double[] samples = Alternating(5.55, 16.667, 80);

            var result = FrameGenerationDetector.Evaluate(samples, "ETW");

            Assert.Equal(FrameGenerationState.Suspected, result.State);
            Assert.Equal(3.0, result.EstimatedGeneratedFrameRatio);
            Assert.False(result.IsVerifiedSignal);
        }

        [Fact]
        public void Evaluate_AlternatingFourToOneCadence_Returns_Suspected_Not_Detected()
        {
            double[] samples = Alternating(4.167, 16.667, 80);

            var result = FrameGenerationDetector.Evaluate(samples, "ETW");

            Assert.Equal(FrameGenerationState.Suspected, result.State);
            Assert.False(result.IsVerifiedSignal);
            Assert.Equal(FrameGenerationEvidence.AlternatingCadenceRatio, result.Evidence);
            Assert.Equal(4.0, result.EstimatedGeneratedFrameRatio);
            Assert.InRange(result.GeneratedFrameCount, 58, 62);
        }

        [Fact]
        public void Evaluate_NoisyOutlierPattern_Does_Not_Return_Suspected()
        {
            double[] samples = Alternating(8.333, 16.667, 80);
            for (int i = 0; i < samples.Length; i += 5)
            {
                samples[i] = 24.0;
            }

            var result = FrameGenerationDetector.Evaluate(samples, "ETW");

            Assert.NotEqual(FrameGenerationState.Suspected, result.State);
            Assert.False(result.IsVerifiedSignal);
        }

        [Fact]
        public void Evaluate_EstimatedAppFpsRatio_Returns_Suspected_But_Not_Detected()
        {
            double[] samples = Repeat(8.333, 90);

            var result = FrameGenerationDetector.Evaluate(samples, "ETW", estimatedAppFps: 60);

            Assert.Equal(FrameGenerationState.Suspected, result.State);
            Assert.False(result.IsVerifiedSignal);
            Assert.Equal(FrameGenerationEvidence.EstimatedAppFpsRatio, result.Evidence);
            Assert.Equal(2.0, result.EstimatedGeneratedFrameRatio);
        }

        [Fact]
        public void Evaluate_EstimatedAppFpsRatioOutsideTolerance_Does_Not_Suspect_FrameGeneration()
        {
            double[] samples = Repeat(8.333, 90);

            var result = FrameGenerationDetector.Evaluate(samples, "ETW", estimatedAppFps: 90);

            Assert.NotEqual(FrameGenerationState.Suspected, result.State);
            Assert.False(result.IsVerifiedSignal);
            Assert.NotEqual(FrameGenerationEvidence.EstimatedAppFpsRatio, result.Evidence);
            Assert.Null(result.EstimatedGeneratedFrameRatio);
            Assert.Equal(0, result.GeneratedFrameCount);
        }

        [Fact]
        public void Evaluate_InvalidAndTooFewValidSamples_Returns_Unknown()
        {
            double[] samples = { double.NaN, double.PositiveInfinity, -1, 0.01, 16.667, 16.667 };

            var result = FrameGenerationDetector.Evaluate(samples, "ETW");

            Assert.Equal(FrameGenerationState.Unknown, result.State);
            Assert.Equal(FrameGenerationEvidence.InsufficientSamples, result.Evidence);
            Assert.InRange(result.PresentedFps, 59.9, 60.1);
            Assert.Equal(0, result.GeneratedFrameCount);
        }

        [Fact]
        public void Evaluate_StableHalfRefreshCap_Returns_NotDetected()
        {
            double[] samples = Repeat(33.333, 90);

            var result = FrameGenerationDetector.Evaluate(samples, "ETW");

            Assert.Equal(FrameGenerationState.NotDetected, result.State);
            Assert.Equal(FrameGenerationEvidence.StableNativeCadence, result.Evidence);
            Assert.Null(result.EstimatedGeneratedFrameRatio);
            Assert.Equal(0, result.GeneratedFrameCount);
        }

        [Fact]
        public void Evaluate_RtssWithoutVerifiedSignal_Returns_Unsupported()
        {
            double[] samples = Repeat(16.667, 60);

            var result = FrameGenerationDetector.Evaluate(samples, "RTSS");

            Assert.Equal(FrameGenerationState.Unsupported, result.State);
            Assert.False(result.IsVerifiedSignal);
            Assert.Equal(FrameGenerationEvidence.UnsupportedTelemetrySource, result.Evidence);
        }

        [Fact]
        public void Evaluate_VerifiedSignal_Is_Required_For_Detected()
        {
            var verified = new FrameGenerationVerifiedSignal(
                true,
                "NVIDIA",
                "DLSS-G",
                2.0,
                60,
                120,
                "PresentMon frame type provider reported generated frames.");

            var result = FrameGenerationDetector.Evaluate(Repeat(8.333, 60), "ETW", verified);

            Assert.Equal(FrameGenerationState.Detected, result.State);
            Assert.True(result.IsVerifiedSignal);
            Assert.Equal(1.0, result.Confidence);
            Assert.Equal("NVIDIA", result.Vendor);
            Assert.Equal("DLSS-G", result.Technology);
            Assert.Equal(2.0, result.EstimatedGeneratedFrameRatio);
        }

        [Fact]
        public void Evaluate_VerifiedInactiveSignal_Returns_NotDetected_Without_Generated_Count()
        {
            var verified = new FrameGenerationVerifiedSignal(
                false,
                "NVIDIA",
                "DLSS-G",
                2.0,
                60,
                120,
                "NGX/Streamline provider reported DLSS-G inactive.");

            var result = FrameGenerationDetector.Evaluate(Repeat(8.333, 60), "ETW", verified);

            Assert.Equal(FrameGenerationState.NotDetected, result.State);
            Assert.True(result.IsVerifiedSignal);
            Assert.Equal(FrameGenerationEvidence.VerifiedExternalSignal, result.Evidence);
            Assert.Equal(0.9, result.Confidence);
            Assert.Null(result.EstimatedGeneratedFrameRatio);
            Assert.Equal(0, result.GeneratedFrameCount);
        }

        [Fact]
        public void Evaluate_VerifiedSignal_Overrides_InsufficientCadenceSamples()
        {
            var verified = new FrameGenerationVerifiedSignal(
                true,
                "NVIDIA",
                "DLSS-G",
                3.0,
                40,
                120,
                "In-process NGX state reported active frame generation.");

            var result = FrameGenerationDetector.Evaluate(new[] { 8.333, 8.333 }, "RTSS", verified);

            Assert.Equal(FrameGenerationState.Detected, result.State);
            Assert.True(result.IsVerifiedSignal);
            Assert.Equal(FrameGenerationEvidence.VerifiedExternalSignal, result.Evidence);
            Assert.Equal(3.0, result.EstimatedGeneratedFrameRatio);
        }

        [Fact]
        public void OverlayFormatter_Uses_Conservative_FrameGeneration_Label_In_Diagnostics()
        {
            var lines = new List<string>();
            var snapshot = CreateSnapshot(FrameGenerationState.Suspected, verified: false, confidence: 0.62);

            FpsOverlayTextFormatter.AppendLines(
                lines,
                snapshot,
                source: "ETW",
                status: "Active",
                new FpsOverlayTextOptions(Show1PercentLows: true, ShowGeneratedFrames: true, ShowDiagnostics: true));

            Assert.Contains("FG: SUSPECT 62%", lines);
            Assert.DoesNotContain("FG: VERIFIED", lines);
        }

        [Fact]
        public void OverlayFormatter_Uses_Estimated_Label_For_NonDiagnostic_Heuristic_Count()
        {
            var lines = new List<string>();
            var snapshot = CreateSnapshot(FrameGenerationState.Suspected, verified: false, confidence: 0.62);

            FpsOverlayTextFormatter.AppendLines(
                lines,
                snapshot,
                source: "ETW",
                status: "Active",
                new FpsOverlayTextOptions(Show1PercentLows: true, ShowGeneratedFrames: true, ShowDiagnostics: false));

            Assert.Contains("GEN EST: 45", lines);
            Assert.DoesNotContain("GEN: 45", lines);
        }

        [Fact]
        public void OverlayFormatter_Detailed_Mode_Does_Not_Verify_Heuristic_FrameGeneration()
        {
            var lines = new List<string>();
            var snapshot = CreateSnapshot(FrameGenerationState.Suspected, verified: false, confidence: 0.62);

            FpsOverlayTextFormatter.AppendLines(
                lines,
                snapshot,
                source: "ETW",
                status: "Active",
                new FpsOverlayTextOptions(Show1PercentLows: true, ShowGeneratedFrames: true, ShowDiagnostics: true)
                {
                    DisplayMode = FpsOverlayDisplayMode.Detailed,
                    ShowFps = true,
                    ShowFrameTime = true,
                    ShowFramePacing = true,
                    ShowGeneratedFrames = true
                });

            Assert.Contains("FG: SUSPECT 62%", lines);
            Assert.DoesNotContain("FG: VERIFIED", lines);
        }

        [Fact]
        public void OverlayFormatter_Shows_Verified_Only_For_Verified_Signal()
        {
            var lines = new List<string>();
            var snapshot = CreateSnapshot(FrameGenerationState.Detected, verified: true, confidence: 1.0);

            FpsOverlayTextFormatter.AppendLines(
                lines,
                snapshot,
                source: "PresentMon",
                status: "Active",
                new FpsOverlayTextOptions(Show1PercentLows: true, ShowGeneratedFrames: true, ShowDiagnostics: true));

            Assert.Contains("FG: VERIFIED DLSS-G x2", lines);
        }

        [Fact]
        public void AddFrame_RtssSamples_Do_Not_Poison_Etw_Cadence_Window()
        {
            var detector = new FrameGenerationDetector();

            for (int i = 0; i < 40; i++)
            {
                var rtss = detector.AddFrame(16.667, "RTSS");
                Assert.Equal(FrameGenerationState.Unsupported, rtss.State);
            }

            FrameGenerationDetectionResult result = FrameGenerationDetectionResult.Unknown;
            for (int i = 0; i < FrameGenerationDetector.MinimumSamples; i++)
            {
                result = detector.AddFrame(8.333, "ETW");
            }

            Assert.Equal(FrameGenerationState.NotDetected, result.State);
            Assert.Equal(FrameGenerationEvidence.StableNativeCadence, result.Evidence);
        }

        [Fact]
        public void AddFrame_EtwAfterRtssFallback_Can_Reach_Suspected_From_Fresh_Etw_Samples()
        {
            var detector = new FrameGenerationDetector();

            for (int i = 0; i < 40; i++)
            {
                detector.AddFrame(16.667, "RTSS");
            }

            FrameGenerationDetectionResult result = FrameGenerationDetectionResult.Unknown;
            for (int i = 0; i < 80; i++)
            {
                result = detector.AddFrame(i % 2 == 0 ? 8.333 : 16.667, "ETW");
            }

            Assert.Equal(FrameGenerationState.Suspected, result.State);
            Assert.Equal(FrameGenerationEvidence.AlternatingCadenceRatio, result.Evidence);
            Assert.Equal(2.0, result.EstimatedGeneratedFrameRatio);
        }

        private static double[] Repeat(double value, int count)
        {
            var samples = new double[count];
            for (int i = 0; i < count; i++)
            {
                samples[i] = value;
            }

            return samples;
        }

        private static double[] Alternating(double first, double second, int count)
        {
            var samples = new double[count];
            for (int i = 0; i < count; i++)
            {
                samples[i] = i % 2 == 0 ? first : second;
            }

            return samples;
        }

        private static FpsMetricsSnapshot CreateSnapshot(FrameGenerationState state, bool verified, double confidence)
        {
            return new FpsMetricsSnapshot(
                true,
                90,
                120,
                118,
                95,
                8.3,
                45,
                true,
                Repeat(8.333, 90))
            {
                PacingStats = FrameTimingStatistics.Calculate(Repeat(8.333, 90), 90),
                FrameGenerationStatus = new FrameGenerationDetectionResult(
                    state,
                    confidence,
                    verified,
                    verified ? FrameGenerationEvidence.VerifiedExternalSignal : FrameGenerationEvidence.EstimatedAppFpsRatio,
                    verified ? "Verified provider signal." : "Presented FPS is close to 2x estimated app FPS.",
                    120,
                    60,
                    60,
                    2.0,
                    45,
                    verified ? "NVIDIA" : string.Empty,
                    verified ? "DLSS-G" : string.Empty)
            };
        }
    }
}
