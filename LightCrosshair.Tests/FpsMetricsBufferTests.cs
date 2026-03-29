using Xunit;
using LightCrosshair;
using System.Linq;

namespace LightCrosshair.Tests
{
    public class FpsMetricsBufferTests
    {
        [Fact]
        public void Snapshot_Computes_Instant_And_Average_Fps()
        {
            var buffer = new FpsMetricsBuffer(1000);

            // 10 ms frame time = 100 FPS.
            for (int i = 0; i < 120; i++)
            {
                buffer.AddFrame(10);
            }

            var snapshot = buffer.Snapshot();
            Assert.True(snapshot.HasData);
            Assert.Equal(120, snapshot.SampleCount);
            Assert.InRange(snapshot.InstantFps, 99.9, 100.1);
            Assert.InRange(snapshot.AverageFps, 99.9, 100.1);
        }

        [Fact]
        public void Snapshot_Computes_OnePercentLow_From_Slowest_Frames()
        {
            var buffer = new FpsMetricsBuffer(1000);

            // 99 fast frames + 1 slow frame.
            for (int i = 0; i < 99; i++)
            {
                buffer.AddFrame(10); // 100 FPS
            }
            buffer.AddFrame(50); // 20 FPS

            var snapshot = buffer.Snapshot();
            Assert.True(snapshot.HasData);
            Assert.Equal(100, snapshot.SampleCount);

            // 1% low should focus on slowest sample, close to 20 FPS.
            Assert.InRange(snapshot.OnePercentLowFps, 19.5, 20.5);
        }

        [Fact]
        public void Buffer_Respects_Capacity_And_Drops_Oldest_Samples()
        {
            var buffer = new FpsMetricsBuffer(8);
            buffer.AddFrame(16.0);
            buffer.AddFrame(17.0);
            buffer.AddFrame(18.0);
            buffer.AddFrame(19.0);
            buffer.AddFrame(20.0); // pushes out the first sample
            buffer.AddFrame(21.0);
            buffer.AddFrame(22.0);
            buffer.AddFrame(23.0);
            buffer.AddFrame(24.0); // pushes out 16.0

            var snapshot = buffer.Snapshot();
            Assert.Equal(8, snapshot.SampleCount);
            Assert.Equal(24.0, snapshot.LatestFrameTimeMs);
            Assert.Equal(8, snapshot.RecentFrameTimesMs.Length);
            Assert.DoesNotContain(16.0, snapshot.RecentFrameTimesMs);
        }

        [Fact]
        public void PercentileWindow_Uses_Only_Requested_Window_When_Array_Has_Trailing_Empties()
        {
            var data = new[] { 5.0, 10.0, 1000.0, 0.0, 0.0 };
            var scratch = new double[1000];

            double p95 = FrameTimeGraphMath.PercentileWindow(data, offset: 0, count: 3, percentile: 0.95, scratchBuffer: scratch);

            Assert.InRange(p95, 900.9, 901.1);
        }

        [Fact]
        public void PercentileWindow_Handles_Extremes_And_Offset_Subset_Correctly()
        {
            var data = new[] { -100.0, 0.0, 16.67, 500.0, 1000.0 };
            var scratch = new double[1000];

            double min = FrameTimeGraphMath.PercentileWindow(data, offset: 1, count: 3, percentile: 0.0, scratchBuffer: scratch);
            double max = FrameTimeGraphMath.PercentileWindow(data, offset: 1, count: 3, percentile: 1.0, scratchBuffer: scratch);

            Assert.Equal(0.0, min, 3);
            Assert.Equal(500.0, max, 3);
        }

        [Fact]
        public void FindTimeWindowStart_Selects_Last_Target_Milliseconds()
        {
            var data = Enumerable.Repeat(100.0, 30).ToArray();

            int start = FrameTimeGraphMath.FindTimeWindowStart(data, validCount: 30, targetWindowMs: 2000.0);

            Assert.Equal(10, start);
        }

        [Fact]
        public void FindTimeWindowStart_Tracks_Reactive_And_Stable_Presets()
        {
            var data = Enumerable.Repeat(100.0, 30).ToArray();

            int reactive = FrameTimeGraphMath.FindTimeWindowStart(data, validCount: 30, targetWindowMs: 1500.0);
            int stable = FrameTimeGraphMath.FindTimeWindowStart(data, validCount: 30, targetWindowMs: 3000.0);

            Assert.Equal(15, reactive);
            Assert.Equal(0, stable);
        }

        [Fact]
        public void BuildTimeWindowSamples_Produces_Monotonic_Indices_And_Times()
        {
            var data = Enumerable.Repeat(10.0, 300).ToArray();
            var indexBuffer = new int[120];
            var timeBuffer = new double[120];

            int pointCount = FrameTimeGraphMath.BuildTimeWindowSamples(
                data,
                windowStart: 0,
                windowCount: 300,
                indexBuffer,
                timeBuffer,
                out double totalMs);

            Assert.Equal(120, pointCount);
            Assert.Equal(0, indexBuffer[0]);
            Assert.Equal(299, indexBuffer[pointCount - 1]);
            Assert.True(totalMs > 0);

            for (int i = 1; i < pointCount; i++)
            {
                Assert.True(indexBuffer[i] > indexBuffer[i - 1]);
                Assert.True(timeBuffer[i] >= timeBuffer[i - 1]);
            }
        }

        [Theory]
        [InlineData(-1, 66)]
        [InlineData(1, 33)]
        [InlineData(49, 33)]
        [InlineData(50, 66)]
        [InlineData(83, 66)]
        [InlineData(84, 100)]
        [InlineData(500, 100)]
        public void NormalizeGraphRefreshRatePreset_Returns_Expected_Preset(int input, int expected)
        {
            int normalized = CrosshairConfig.NormalizeGraphRefreshRatePreset(input);
            Assert.Equal(expected, normalized);
        }

        [Theory]
        [InlineData(-1, 2000)]
        [InlineData(1, 1500)]
        [InlineData(1750, 1500)]
        [InlineData(1751, 2000)]
        [InlineData(2500, 2000)]
        [InlineData(2501, 3000)]
        [InlineData(5000, 3000)]
        public void NormalizeGraphTimeWindowPreset_Returns_Expected_Preset(int input, int expected)
        {
            int normalized = CrosshairConfig.NormalizeGraphTimeWindowPreset(input);
            Assert.Equal(expected, normalized);
        }

        [Theory]
        [InlineData(-1, 100)]
        [InlineData(0, 100)]
        [InlineData(1, 50)]
        [InlineData(50, 50)]
        [InlineData(100, 100)]
        [InlineData(250, 250)]
        [InlineData(300, 300)]
        [InlineData(301, 300)]
        [InlineData(500, 300)]
        public void NormalizeFpsOverlayScale_Returns_Expected_Value(int input, int expected)
        {
            int normalized = CrosshairConfig.NormalizeFpsOverlayScale(input);
            Assert.Equal(expected, normalized);
        }
    }
}