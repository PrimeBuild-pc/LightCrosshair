using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LightCrosshair.FrameLimiting;

namespace LightCrosshair.Diagnostics
{
    internal readonly record struct TelemetryDiagnosticReport(
        DateTimeOffset CapturedAt,
        string TelemetrySource,
        string TelemetryStatus,
        bool HasData,
        int SampleCount,
        double InstantFps,
        double AverageFps,
        double OnePercentLowFps,
        double PointOnePercentLowFps,
        double LatestFrameTimeMs,
        double AverageFrameTimeMs,
        double JitterMs,
        double StandardDeviationFrameTimeMs,
        int HitchCount,
        double StabilityScore,
        FrameGenerationState FrameGenerationState,
        bool IsFrameGenerationVerified,
        double FrameGenerationConfidence,
        int GeneratedFrameCount,
        double? EstimatedGeneratedFrameRatio,
        string FrameGenerationVendor,
        string FrameGenerationTechnology,
        string FrameGenerationEvidence,
        FrameLimiterBackendKind FrameLimiterBackendKind,
        FrameLimiterStatusKind FrameLimiterStatusKind,
        bool IsFrameLimiterActive,
        bool IsFrameLimiterTelemetryValidated,
        string FrameLimiterEvidence)
    {
        public IReadOnlyList<string> ToLines()
        {
            var lines = new List<string>(24)
            {
                $"Captured: {CapturedAt:O}",
                $"Telemetry Source: {TelemetrySource}",
                $"Telemetry Status: {TelemetryStatus}",
                $"Has Data: {FormatBool(HasData)}",
                $"Samples: {SampleCount.ToString(CultureInfo.InvariantCulture)}"
            };

            if (!HasData)
            {
                lines.Add("FPS: N/A");
                lines.Add("Frame Pacing: N/A");
            }
            else
            {
                lines.Add($"FPS: {InstantFps.ToString("0.0", CultureInfo.InvariantCulture)}");
                lines.Add($"Average FPS: {AverageFps.ToString("0.0", CultureInfo.InvariantCulture)}");
                lines.Add($"1% Low FPS: {OnePercentLowFps.ToString("0.0", CultureInfo.InvariantCulture)}");
                lines.Add($"0.1% Low FPS: {PointOnePercentLowFps.ToString("0.0", CultureInfo.InvariantCulture)}");
                lines.Add($"Latest Frame Time: {LatestFrameTimeMs.ToString("0.00", CultureInfo.InvariantCulture)} ms");
                lines.Add($"Average Frame Time: {AverageFrameTimeMs.ToString("0.00", CultureInfo.InvariantCulture)} ms");
                lines.Add($"Jitter: {JitterMs.ToString("0.00", CultureInfo.InvariantCulture)} ms");
                lines.Add($"Frame Time SD: {StandardDeviationFrameTimeMs.ToString("0.00", CultureInfo.InvariantCulture)} ms");
                lines.Add($"Hitches: {HitchCount.ToString(CultureInfo.InvariantCulture)}");
                lines.Add($"Pacing Stability: {StabilityScore.ToString("0.0", CultureInfo.InvariantCulture)}");
            }

            lines.Add($"Frame Generation: {FormatFrameGenerationState()}");
            if (IsFrameGenerationVerified && !string.IsNullOrWhiteSpace(FrameGenerationTechnology))
            {
                string ratio = EstimatedGeneratedFrameRatio.HasValue
                    ? $" x{EstimatedGeneratedFrameRatio.Value.ToString("0.#", CultureInfo.InvariantCulture)}"
                    : string.Empty;
                lines.Add($"Frame Generation Technology: {FrameGenerationVendor} {FrameGenerationTechnology}{ratio}".Trim());
                lines.Add($"Generated Frames: {GeneratedFrameCount.ToString(CultureInfo.InvariantCulture)}");
            }
            lines.Add($"Frame Generation Evidence: {FrameGenerationEvidence}");
            lines.Add($"Frame Limiter Backend: {FrameLimiterBackendKind}");
            lines.Add($"Frame Limiter Status: {FrameLimiterStatusKind}");
            lines.Add($"Frame Limiter Active: {FormatBool(IsFrameLimiterActive)}");
            lines.Add($"Frame Limiter Validated: {FormatBool(IsFrameLimiterTelemetryValidated)}");
            lines.Add($"Frame Limiter Evidence: {FrameLimiterEvidence}");

            return lines;
        }

        public string ToMultilineText() =>
            string.Join(Environment.NewLine, ToLines());

        public IReadOnlyDictionary<string, string> ToExportFields()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["captured_at"] = CapturedAt.ToString("O", CultureInfo.InvariantCulture),
                ["telemetry_source"] = TelemetrySource,
                ["telemetry_status"] = TelemetryStatus,
                ["has_data"] = HasData ? "true" : "false",
                ["sample_count"] = SampleCount.ToString(CultureInfo.InvariantCulture),
                ["instant_fps"] = FormatDouble(InstantFps),
                ["average_fps"] = FormatDouble(AverageFps),
                ["one_percent_low_fps"] = FormatDouble(OnePercentLowFps),
                ["point_one_percent_low_fps"] = FormatDouble(PointOnePercentLowFps),
                ["latest_frame_time_ms"] = FormatDouble(LatestFrameTimeMs),
                ["average_frame_time_ms"] = FormatDouble(AverageFrameTimeMs),
                ["jitter_ms"] = FormatDouble(JitterMs),
                ["frame_time_sd_ms"] = FormatDouble(StandardDeviationFrameTimeMs),
                ["hitch_count"] = HitchCount.ToString(CultureInfo.InvariantCulture),
                ["stability_score"] = FormatDouble(StabilityScore),
                ["frame_generation_state"] = FrameGenerationState.ToString(),
                ["frame_generation_verified"] = IsFrameGenerationVerified ? "true" : "false",
                ["frame_generation_confidence"] = FormatDouble(FrameGenerationConfidence),
                ["generated_frame_count"] = GeneratedFrameCount.ToString(CultureInfo.InvariantCulture),
                ["estimated_generated_frame_ratio"] = EstimatedGeneratedFrameRatio.HasValue ? FormatDouble(EstimatedGeneratedFrameRatio.Value) : string.Empty,
                ["frame_generation_vendor"] = FrameGenerationVendor,
                ["frame_generation_technology"] = FrameGenerationTechnology,
                ["frame_generation_evidence"] = FrameGenerationEvidence,
                ["frame_limiter_backend"] = FrameLimiterBackendKind.ToString(),
                ["frame_limiter_status"] = FrameLimiterStatusKind.ToString(),
                ["frame_limiter_active"] = IsFrameLimiterActive ? "true" : "false",
                ["frame_limiter_validated"] = IsFrameLimiterTelemetryValidated ? "true" : "false",
                ["frame_limiter_evidence"] = FrameLimiterEvidence
            };
        }

        private string FormatFrameGenerationState()
        {
            string verified = IsFrameGenerationVerified ? "verified" : "not verified";
            return $"{FrameGenerationState} ({verified}, confidence {FrameGenerationConfidence.ToString("0%", CultureInfo.InvariantCulture)})";
        }

        private static string FormatBool(bool value) => value ? "Yes" : "No";

        private static string FormatDouble(double value) =>
            (double.IsNaN(value) || double.IsInfinity(value))
                ? string.Empty
                : value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    internal static class TelemetryDiagnosticReportBuilder
    {
        public static TelemetryDiagnosticReport Build(
            FpsMetricsSnapshot snapshot,
            string telemetrySource,
            string telemetryStatus,
            FrameLimiterStatus? frameLimiterStatus = null,
            DateTimeOffset? capturedAt = null)
        {
            FramePacingStats pacing = snapshot.PacingStats;
            FrameGenerationDetectionResult frameGeneration = snapshot.FrameGenerationStatus;
            FrameLimiterStatus limiter = frameLimiterStatus ?? FrameLimiterStatus.Unavailable(
                FrameLimiterBackendKind.None,
                "No frame limiter status provider was supplied. LightCrosshair is not reporting an active cap.");

            return new TelemetryDiagnosticReport(
                capturedAt ?? DateTimeOffset.UtcNow,
                NormalizeText(telemetrySource, "None"),
                NormalizeText(telemetryStatus, "Unknown"),
                snapshot.HasData,
                snapshot.SampleCount,
                snapshot.HasData ? snapshot.InstantFps : 0,
                snapshot.HasData ? snapshot.AverageFps : 0,
                snapshot.HasData ? snapshot.OnePercentLowFps : 0,
                pacing.HasData ? pacing.PointOnePercentLowFps : 0,
                snapshot.HasData ? snapshot.LatestFrameTimeMs : 0,
                pacing.HasData ? pacing.AverageFrameTimeMs : 0,
                pacing.HasData ? pacing.JitterMs : 0,
                pacing.HasData ? pacing.StandardDeviationFrameTimeMs : 0,
                pacing.HasData ? pacing.HitchCount : 0,
                pacing.HasData ? pacing.StabilityScore : 0,
                frameGeneration.State,
                frameGeneration.IsVerifiedSignal,
                Math.Clamp(frameGeneration.Confidence, 0, 1),
                frameGeneration.GeneratedFrameCount,
                frameGeneration.EstimatedGeneratedFrameRatio,
                NormalizeText(frameGeneration.Vendor, string.Empty),
                NormalizeText(frameGeneration.Technology, string.Empty),
                NormalizeText(frameGeneration.EvidenceText, "No frame generation evidence available."),
                limiter.BackendKind,
                limiter.StatusKind,
                limiter.IsLimitActive,
                limiter.IsTelemetryValidated,
                NormalizeText(limiter.EvidenceText, "No frame limiter evidence available."));
        }

        public static string ToCsv(TelemetryDiagnosticReport report)
        {
            var fields = report.ToExportFields();
            string header = string.Join(",", fields.Keys.Select(EscapeCsv));
            string values = string.Join(",", fields.Values.Select(EscapeCsv));
            return header + Environment.NewLine + values;
        }

        private static string NormalizeText(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static string EscapeCsv(string value)
        {
            bool requiresQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (!requiresQuotes)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }
    }
}
