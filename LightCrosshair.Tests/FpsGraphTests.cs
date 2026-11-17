using System;
using System.Linq;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public class FpsGraphTests
{
    [Fact]
    public void GraphKeepsTwoSecondsAtHighFpsAndRetainsPeaksInBoundedBuckets()
    {
        var history = new FpsGraphHistory(); var start = DateTime.UnixEpoch;
        for (int i = 0; i < 6000; i++) history.Add(start.AddMilliseconds(i), i == 5999 ? 48 : 1);
        var points = history.Snapshot(start.AddMilliseconds(6000));
        Assert.InRange(points.Length, 39, 41);
        Assert.All(points, point => Assert.InRange(point.AgeMilliseconds, 0, 1999.999));
        Assert.Equal(48, points.Last().Frametime);
        Assert.Empty(history.Snapshot(start.AddSeconds(9)));
        history.Clear(); Assert.Empty(history.Snapshot(start));
    }
    [Fact]
    public void SwapchainSwitchAndStaleTelemetryDoNotMixGraphs()
    {
        var tracker = new FpsPresentTracker(); var start = DateTime.Now;
        tracker.Present(1, start); tracker.Present(1, start.AddMilliseconds(10));
        Assert.Equal(10, Assert.Single(tracker.Graph(start.AddMilliseconds(11))).Frametime);
        tracker.Present(2, start.AddSeconds(3)); tracker.Present(2, start.AddSeconds(3.02));
        Assert.Equal(20, Assert.Single(tracker.Graph(start.AddSeconds(3.021))).Frametime);
        Assert.Empty(tracker.Graph(start.AddSeconds(6)));
    }
}
