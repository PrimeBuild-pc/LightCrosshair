using System;
using LightCrosshair.Diagnostics.PresentMon;
using Xunit;

namespace LightCrosshair.Tests
{
    public class PresentMonCsvParserTests
    {
        [Fact]
        public void Parse_MinimalPresentMonCsv_ComputesBasicSummary()
        {
            string csv = string.Join(Environment.NewLine,
                "Application,ProcessID,MsBetweenPresents",
                "Game.exe,123,16.6",
                "Game.exe,123,17.0",
                "Game.exe,123,16.4");

            PresentMonValidationResult result = PresentMonCsvParser.Parse(csv);

            Assert.True(result.IsSupportedCapture);
            Assert.Equal(3, result.Summary.SampleCount);
            Assert.Equal("Game.exe", result.Summary.ApplicationName);
            Assert.InRange(result.Summary.AverageFrameTimeMs!.Value, 16.66, 16.67);
            Assert.Equal(PresentMonFrameGenerationClassification.Unavailable, result.Summary.FrameGenerationClassification);
            Assert.Contains("No dedicated", result.Summary.FrameGenerationEvidence);
        }

        [Fact]
        public void Parse_MissingOptionalColumns_StillSummarizesFrameTiming()
        {
            string csv = string.Join(Environment.NewLine,
                "Application,FrameTime",
                "Sample.exe,10",
                "Sample.exe,20");

            PresentMonValidationResult result = PresentMonCsvParser.Parse(csv);

            Assert.True(result.IsSupportedCapture);
            Assert.Equal(2, result.Summary.SampleCount);
            Assert.Equal(15, result.Summary.AverageFrameTimeMs);
            Assert.Empty(result.Summary.PresentModeDistribution);
            Assert.Equal(0, result.Summary.DroppedSampleCount);
        }

        [Fact]
        public void Parse_UnknownColumns_ArePreserved_AndIgnoredForSummary()
        {
            string csv = string.Join(Environment.NewLine,
                "Application,FrameTime,UnknownColumn",
                "Game.exe,12.5,extra");

            PresentMonValidationResult result = PresentMonCsvParser.Parse(csv);

            Assert.Single(result.Samples);
            Assert.Equal("extra", result.Samples[0].RawColumns["UnknownColumn"]);
            Assert.Equal(12.5, result.Summary.AverageFrameTimeMs);
        }

        [Fact]
        public void Parse_ComputesP95AndP99FrameTimes()
        {
            string csv = string.Join(Environment.NewLine,
                "FrameTime",
                "1",
                "2",
                "3",
                "4",
                "100");

            PresentMonCaptureSummary summary = PresentMonCsvParser.Parse(csv).Summary;

            Assert.Equal(80.8, summary.P95FrameTimeMs!.Value, 1);
            Assert.Equal(96.16, summary.P99FrameTimeMs!.Value, 2);
        }

        [Fact]
        public void Parse_ComputesPresentModeDistribution()
        {
            string csv = string.Join(Environment.NewLine,
                "PresentMode,FrameTime",
                "Hardware: Independent Flip,8",
                "Composed: Flip,9",
                "Hardware: Independent Flip,8");

            PresentMonCaptureSummary summary = PresentMonCsvParser.Parse(csv).Summary;

            Assert.Equal(2, summary.PresentModeDistribution["Hardware: Independent Flip"]);
            Assert.Equal(1, summary.PresentModeDistribution["Composed: Flip"]);
        }

        [Fact]
        public void Parse_ExplicitFrameTypeGeneratedValue_IsVerifiedSignalPresent()
        {
            string csv = string.Join(Environment.NewLine,
                "Application,FrameTime,FrameType",
                "Game.exe,8.3,Application",
                "Game.exe,8.3,Generated",
                "Game.exe,8.3,Interpolated");

            PresentMonCaptureSummary summary = PresentMonCsvParser.Parse(csv).Summary;

            Assert.Equal(2, summary.ExplicitGeneratedFrameSampleCount);
            Assert.Equal(PresentMonFrameGenerationClassification.VerifiedSignalPresent, summary.FrameGenerationClassification);
            Assert.Contains("FrameType explicitly reported 2", summary.FrameGenerationEvidence);
        }

        [Fact]
        public void Parse_FrameTypeWithoutGeneratedValues_IsInconclusive()
        {
            string csv = string.Join(Environment.NewLine,
                "FrameTime,FrameType",
                "8.3,Application",
                "8.4,Rendered");

            PresentMonCaptureSummary summary = PresentMonCsvParser.Parse(csv).Summary;

            Assert.Equal(0, summary.ExplicitGeneratedFrameSampleCount);
            Assert.Equal(PresentMonFrameGenerationClassification.Inconclusive, summary.FrameGenerationClassification);
            Assert.Contains("FrameType column is present", summary.FrameGenerationEvidence);
        }

        [Fact]
        public void Parse_FpsRatioOrCadenceWithoutFrameType_RemainsHeuristicOnly()
        {
            string csv = string.Join(Environment.NewLine,
                "Application,FPS,MsBetweenPresents",
                "Game.exe,120,16.67",
                "Game.exe,120,8.33",
                "Game.exe,120,16.67",
                "Game.exe,120,8.33");

            PresentMonCaptureSummary summary = PresentMonCsvParser.Parse(csv).Summary;

            Assert.Equal(PresentMonFrameGenerationClassification.HeuristicOnly, summary.FrameGenerationClassification);
            Assert.NotEqual(PresentMonFrameGenerationClassification.VerifiedSignalPresent, summary.FrameGenerationClassification);
            Assert.Contains("heuristic only", summary.FrameGenerationEvidence);
        }

        [Fact]
        public void Parse_MalformedRows_AddWarnings_AndKeepsValidValues()
        {
            string csv = string.Join(Environment.NewLine,
                "Application,ProcessID,FrameTime,Dropped",
                "Game.exe,abc,not-a-number,maybe",
                "Game.exe,123,12.5,1");

            PresentMonValidationResult result = PresentMonCsvParser.Parse(csv);

            Assert.Equal(2, result.Samples.Count);
            Assert.Equal(12.5, result.Summary.AverageFrameTimeMs);
            Assert.Equal(1, result.Summary.DroppedSampleCount);
            Assert.Contains(result.Warnings, warning => warning.Contains("invalid integer", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Warnings, warning => warning.Contains("invalid numeric", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Warnings, warning => warning.Contains("invalid boolean", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Parse_QuotedCsvFields_AreHandled()
        {
            string csv = string.Join(Environment.NewLine,
                "Application,FrameTime,PresentMode",
                "\"Game, Demo.exe\",16.7,\"Composed: Flip\"");

            PresentMonValidationResult result = PresentMonCsvParser.Parse(csv);

            Assert.Equal("Game, Demo.exe", result.Summary.ApplicationName);
            Assert.Equal(16.7, result.Summary.AverageFrameTimeMs);
            Assert.Equal(1, result.Summary.PresentModeDistribution["Composed: Flip"]);
        }
    }
}
