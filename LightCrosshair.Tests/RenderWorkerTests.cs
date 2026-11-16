using System;
using System.Threading;
using System.Threading.Tasks;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public class RenderWorkerTests
{
    [Fact]
    public async Task QueueRunsOnOneBelowNormalThreadAndDropsDisconnectedOrParkedJobs()
    {
        using var worker = new RenderWorker();
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        int renders = 0, thread = 0;
        var active = worker.Enqueue(() => { thread = Environment.CurrentManagedThreadId; entered.Set(); release.Wait(TimeSpan.FromSeconds(5)); Interlocked.Increment(ref renders); return Thread.CurrentThread.Priority; }, CancellationToken.None);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var dropped = worker.Enqueue(() => Interlocked.Increment(ref renders), CancellationToken.None);
        worker.DropPending();
        var parked = worker.Enqueue(() => Interlocked.Increment(ref renders), CancellationToken.None, () => false);
        using var stop = new CancellationTokenSource();
        var disconnected = worker.Enqueue(() => Interlocked.Increment(ref renders), stop.Token); stop.Cancel();
        release.Set();
        Assert.Equal(ThreadPriority.BelowNormal, await active);
        await Assert.ThrowsAsync<CoreException>(() => dropped);
        await Assert.ThrowsAsync<CoreException>(() => parked);
        await Assert.ThrowsAsync<CoreException>(() => disconnected);
        Assert.Equal(1, renders);
        Assert.Equal(thread, await worker.Enqueue(() => Environment.CurrentManagedThreadId, CancellationToken.None));
        worker.Dispose();
        await Assert.ThrowsAsync<CoreException>(() => worker.Enqueue(() => 0, CancellationToken.None));
    }
    [Theory]
    [InlineData(false, false, false)] [InlineData(true, false, true)]
    [InlineData(false, true, true)] [InlineData(true, true, true)]
    public void LaunchPriorityFollowsGameOrFullscreen(bool game, bool fullscreen, bool reduced)
        => Assert.Equal(reduced, UiLaunch.ReducePriority(game, fullscreen));
    [Theory]
    [InlineData("hello world", "\"hello world\"")]
    [InlineData("C:\\folder\\", "\"C:\\folder\\\\\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    public void LaunchArgumentsUseWindowsQuoting(string argument, string expected) => Assert.Equal(expected, UiLaunch.Quote(argument));
}
