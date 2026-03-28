using Xunit;
using LightCrosshair;

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
    }
}