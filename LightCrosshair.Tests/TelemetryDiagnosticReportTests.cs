using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using LightCrosshair.Diagnostics;
using LightCrosshair.FrameLimiting;
using Xunit;

namespace LightCrosshair.Tests
{
    public class TelemetryDiagnosticReportTests
    {
        [Fact]
        public void Build_EmptySnapshot_Returns_StatusOnly_Report()
        {
            var capturedAt = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

            TelemetryDiagnosticReport report = TelemetryDiagnosticReportBuilder.Build(
                new FpsMetricsSnapshot(false, 0, 0, 0, 0, 0, 0, false, Array.Empty<double>()),
                telemetrySource: "",
                telemetryStatus: "",
                capturedAt: capturedAt);

            IReadOnlyList<string> lines = report.ToLines();

            Assert.Equal(capturedAt, report.CapturedAt);
            Assert.Equal("None", report.TelemetrySource);
            Assert.Equal("Unknown", report.TelemetryStatus);
            Assert.False(report.HasData);
            Assert.Contains("FPS: N/A", lines);
            Assert.Contains("Frame Pacing: N/A", lines);
            Assert.Contains("Frame Limiter Active: No", lines);
            Assert.DoesNotContain(lines, line => line.Contains("(verified,", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Build_Includes_FramePacing_Stats_With_Stable_Field_Order()
        {
            TelemetryDiagnosticReport report = TelemetryDiagnosticReportBuilder.Build(
                CreateSnapshot(FrameGenerationDetectionResult.Unknown),
                "ETW",
                "ETW active",
                FrameLimiterStatus.Unavailable(
                    FrameLimiterBackendKind.None,
                    "No real limiter backend is available."),
                new DateTimeOffset(2026, 5, 8, 12, 30, 0, TimeSpan.Zero));

            IReadOnlyList<string> lines = report.ToLines();

            Assert.Equal("Captured: 2026-05-08T12:30:00.0000000+00:00", lines[0]);
            Assert.Equal("Telemetry Source: ETW", lines[1]);
            Assert.Equal("Telemetry Status: ETW active", lines[2]);
            Assert.Contains("FPS: 120.0", lines);
            Assert.Contains("Average FPS: 118.0", lines);
            Assert.Contains("1% Low FPS: 95.0", lines);
            Assert.Contains("0.1% Low FPS: 82.0", lines);
            Assert.Contains("Latest Frame Time: 8.33 ms", lines);
            Assert.Contains("Average Frame Time: 8.40 ms", lines);
            Assert.Contains("Jitter: 0.40 ms", lines);
            Assert.Contains("Frame Time SD: 0.30 ms", lines);
            Assert.Contains("Hitches: 1", lines);
            Assert.Contains("Pacing Stability: 97.0", lines);
        }

        [Fact]
        public void Build_Labels_FrameGeneration_Suspected_Not_Verified()
        {
            var suspected = new FrameGenerationDetectionResult(
                FrameGenerationState.Suspected,
                0.62,
                false,
                FrameGenerationEvidence.EstimatedAppFpsRatio,
                "Presented FPS is close to 2x the estimated app FPS.",
                120,
                60,
                60,
                2.0,
                45,
                string.Empty,
                string.Empty);

            TelemetryDiagnosticReport report = TelemetryDiagnosticReportBuilder.Build(
                CreateSnapshot(suspected),
                "ETW",
                "Active");

            string text = report.ToMultilineText();

            Assert.Contains("Frame Generation: Suspected (not verified, confidence 62%)", text);
            Assert.Contains("Presented FPS is close to 2x the estimated app FPS.", text);
            Assert.DoesNotContain("FG: VERIFIED", text);
            Assert.False(report.IsFrameGenerationVerified);
        }

        [Fact]
        public void Build_Labels_Verified_FrameGeneration_With_Source_Metadata()
        {
            var verified = new FrameGenerationDetectionResult(
                FrameGenerationState.Detected,
                1.0,
                true,
                FrameGenerationEvidence.VerifiedExternalSignal,
                "PresentMon frame type provider reported generated frames.",
                120,
                60,
                60,
                2.0,
                45,
                "NVIDIA",
                "DLSS-G");

            TelemetryDiagnosticReport report = TelemetryDiagnosticReportBuilder.Build(
                CreateSnapshot(verified),
                "PresentMon",
                "Read-only validation");

            IReadOnlyDictionary<string, string> fields = report.ToExportFields();

            Assert.Equal("Detected", fields["frame_generation_state"]);
            Assert.Equal("true", fields["frame_generation_verified"]);
            Assert.Equal("1", fields["frame_generation_confidence"]);
            Assert.Equal("45", fields["generated_frame_count"]);
            Assert.Equal("2", fields["estimated_generated_frame_ratio"]);
            Assert.Equal("NVIDIA", fields["frame_generation_vendor"]);
            Assert.Equal("DLSS-G", fields["frame_generation_technology"]);
            Assert.Contains("PresentMon frame type provider", fields["frame_generation_evidence"]);
            Assert.Contains("Frame Generation Technology: NVIDIA DLSS-G x2", report.ToLines());
            Assert.Contains("Generated Frames: 45", report.ToLines());
            Assert.True(report.IsFrameGenerationVerified);
        }

        [Fact]
        public void Build_Reports_Rtss_FrameGeneration_As_Unsupported()
        {
            var unsupported = new FrameGenerationDetectionResult(
                FrameGenerationState.Unsupported,
                0,
                false,
                FrameGenerationEvidence.UnsupportedTelemetrySource,
                "RTSS does not expose verified generated-frame data.",
                0,
                null,
                null,
                null,
                0,
                string.Empty,
                string.Empty);

            TelemetryDiagnosticReport report = TelemetryDiagnosticReportBuilder.Build(
                CreateSnapshot(unsupported),
                "RTSS",
                "Fallback source active");

            Assert.Equal("RTSS", report.TelemetrySource);
            Assert.Equal(FrameGenerationState.Unsupported, report.FrameGenerationState);
            Assert.Contains("RTSS does not expose verified generated-frame data.", report.ToMultilineText());
            Assert.False(report.IsFrameGenerationVerified);
        }

        [Fact]
        public async Task Build_FrameLimiter_NoOp_Does_Not_Report_Active_Cap()
        {
            IFrameLimiterBackend backend = new NoOpFrameLimiterBackend();
            FrameLimiterStatus limiterStatus = await backend.GetStatusAsync(
                FrameLimiterTarget.ForProcessName("sample.exe"),
                CancellationToken.None);

            TelemetryDiagnosticReport report = TelemetryDiagnosticReportBuilder.Build(
                CreateSnapshot(FrameGenerationDetectionResult.Unknown),
                "ETW",
                "Active",
                limiterStatus);

            Assert.Equal(FrameLimiterBackendKind.None, report.FrameLimiterBackendKind);
            Assert.Equal(FrameLimiterStatusKind.Unavailable, report.FrameLimiterStatusKind);
            Assert.False(report.IsFrameLimiterActive);
            Assert.False(report.IsFrameLimiterTelemetryValidated);
            Assert.Contains("Frame Limiter Active: No", report.ToLines());
            Assert.Contains("not applying a frame cap", report.FrameLimiterEvidence);
        }

        [Fact]
        public void ToCsv_Escapes_Commas_Quotes_And_NewLines()
        {
            TelemetryDiagnosticReport report = TelemetryDiagnosticReportBuilder.Build(
                CreateSnapshot(FrameGenerationDetectionResult.Unknown),
                "ETW, PresentMon",
                "Status \"quoted\"",
                FrameLimiterStatus.Unavailable(
                    FrameLimiterBackendKind.None,
                    "Line one\r\nLine two"),
                new DateTimeOffset(2026, 5, 8, 13, 0, 0, TimeSpan.Zero));

            string csv = TelemetryDiagnosticReportBuilder.ToCsv(report);

            Assert.Contains("\"ETW, PresentMon\"", csv);
            Assert.Contains("\"Status \"\"quoted\"\"\"", csv);
            Assert.Contains("\"Line one\r\nLine two\"", csv);
        }

        [Fact]
        public void ToExportFields_Uses_Invariant_Decimal_Formatting()
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("it-IT");
                CultureInfo.CurrentUICulture = new CultureInfo("it-IT");

                TelemetryDiagnosticReport report = TelemetryDiagnosticReportBuilder.Build(
                    CreateSnapshot(FrameGenerationDetectionResult.Unknown),
                    "ETW",
                    "Active");

                IReadOnlyDictionary<string, string> fields = report.ToExportFields();

                Assert.Equal("120", fields["instant_fps"]);
                Assert.Equal("8.33", fields["latest_frame_time_ms"]);
                Assert.Equal("0.4", fields["jitter_ms"]);
                Assert.DoesNotContain(",", fields["latest_frame_time_ms"]);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }

        private static FpsMetricsSnapshot CreateSnapshot(FrameGenerationDetectionResult frameGeneration)
        {
            return new FpsMetricsSnapshot(
                true,
                90,
                120,
                118,
                95,
                8.33,
                frameGeneration.GeneratedFrameCount,
                frameGeneration.State != FrameGenerationState.Unsupported,
                Repeat(8.33, 90))
            {
                PacingStats = new FramePacingStats(
                    true,
                    90,
                    8.4,
                    8.0,
                    18.0,
                    0.3,
                    0.09,
                    0.4,
                    95,
                    82,
                    1,
                    FrameTimingStatistics.DefaultHitchThresholdMs,
                    97),
                FrameGenerationStatus = frameGeneration
            };
        }

        private static double[] Repeat(double value, int count)
        {
            var values = new double[count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = value;
            }

            return values;
        }
    }
}
