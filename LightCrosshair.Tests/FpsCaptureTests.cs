using System;
using System.Threading;
using System.Threading.Tasks;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public class FpsCaptureTests
{
    [Fact]
    public void SeparateSwapchainsNeverDoubleCountAndDisappearAfterTwoSeconds()
    {
        var tracker = new FpsPresentTracker(); var start = DateTime.Now;
        for (int i = 0; i <= 100; i++)
        { tracker.Present(1, start.AddMilliseconds(i * 10)); tracker.Present(2, start.AddMilliseconds(i * 10 + 1)); }
        var sample = tracker.Snapshot(start.AddSeconds(1));
        Assert.Equal(100, sample.Fps, 3); Assert.Equal(10, sample.Frametime, 3);
        Assert.False(tracker.Snapshot(start.AddSeconds(3.1)).HasData);
        tracker.Clear(); Assert.False(tracker.Snapshot(start).HasData);
    }

    [Fact]
    public void NewSwapchainRecoversAfterThePreviousOneStops()
    {
        var tracker = new FpsPresentTracker(); var start = DateTime.Now;
        for (int i = 0; i <= 100; i++) tracker.Present(1, start.AddMilliseconds(i * 10));
        tracker.Present(2, start.AddSeconds(4)); tracker.Present(2, start.AddSeconds(4.02));
        Assert.Equal(50, tracker.Snapshot(start.AddSeconds(4.02)).Fps, 3);
    }

    [Theory]
    [InlineData(42, true)] [InlineData(55, true)] [InlineData(43, false)] [InlineData(56, false)]
    public void AcceptDxgiAndMultiplanePresentStartsOnly(int id, bool expected)
    {
        Assert.Equal(expected, SystemFpsMonitor.IsPresentStart(new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9"), id));
        Assert.False(SystemFpsMonitor.IsPresentStart(Guid.Empty, id));
        Assert.True(SystemFpsMonitor.IsPresentStart(new("783ACA0A-FA53-4457-BB8E-11327BDDE440"), 1));
    }

    [Fact]
    public async Task PidChangesRejectOtherProcessesAndOldSessionsCannotWriteAfterRestart()
    {
        Action<int, ulong, DateTime>? publish = null;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var now = DateTime.Now;
        var monitor = new SystemFpsMonitor((handler, token) => { publish = handler; ready.TrySetResult(); token.WaitHandle.WaitOne(); }, () => now);
        monitor.Start(11);
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
            publish!(22, 1, now.AddMilliseconds(-10)); publish(22, 1, now);
            Assert.False(monitor.Snapshot().HasData);
            publish(11, 1, now.AddMilliseconds(-10)); publish(11, 1, now);
            Assert.Equal(100, monitor.Snapshot().Fps);
            monitor.Start(22); Assert.False(monitor.Snapshot().HasData);
            publish(11, 1, now.AddMilliseconds(-10)); publish(11, 1, now); Assert.False(monitor.Snapshot().HasData);
            await monitor.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            publish(22, 1, now.AddMilliseconds(-10)); publish(22, 1, now); Assert.False(monitor.Snapshot().HasData);
        }
        finally { await monitor.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Fact]
    public async Task AccessFailureReportsAReasonAndTheSameMonitorRetriesSuccessfully()
    {
        int attempts = 0; var now = DateTime.Now;
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var monitor = new SystemFpsMonitor((publish, token) =>
        {
            if (Interlocked.Increment(ref attempts) == 1) { failed.TrySetResult(); throw new UnauthorizedAccessException("Simulated permission failure"); }
            publish(11, 1, now.AddMilliseconds(-10)); publish(11, 1, now); recovered.TrySetResult(); token.WaitHandle.WaitOne();
        }, () => now);
        monitor.Start(11);
        try
        {
            await failed.Task.WaitAsync(TimeSpan.FromSeconds(3));
            for (int i = 0; i < 100 && monitor.Snapshot().Reason != "FpsAccessDenied"; i++) await Task.Delay(10);
            Assert.Equal("FpsAccessDenied", monitor.Snapshot().Reason);
            await recovered.Task.WaitAsync(TimeSpan.FromSeconds(8));
            Assert.True(monitor.Snapshot().HasData); Assert.Null(monitor.Snapshot().Reason);
            now = now.AddSeconds(2); Assert.Equal("FpsNoFrames", monitor.Snapshot().Reason);
        }
        finally { await monitor.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Theory]
    [InlineData("en")] [InlineData("it")] [InlineData("es")] [InlineData("ru")] [InlineData("ja")] [InlineData("zh-Hans")]
    public void EveryMissingDataReasonIsLocalized(string language)
    {
        foreach (string key in new[] { "FpsNoFrames", "FpsAccessDenied", "FpsUnavailable", "FpsNoForeground" }) Assert.NotEqual(key, CoreStrings.Get(language, key));
    }
}
