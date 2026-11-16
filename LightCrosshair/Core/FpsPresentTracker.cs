namespace LightCrosshair.Core;

internal sealed class FpsPresentTracker
{
    private sealed class Stream
    {
        internal readonly FpsBuffer Buffer = new();
        internal readonly FpsGraphHistory Graph = new();
        internal DateTime Last;
        internal int Count;
    }
    private readonly Dictionary<ulong, Stream> _streams = [];
    internal void Clear() => _streams.Clear();
    internal void Present(ulong swapchain, DateTime time)
    {
        if (!_streams.TryGetValue(swapchain, out var stream))
        {
            if (_streams.Count == 16) _streams.Remove(_streams.MinBy(pair => pair.Value.Last).Key);
            _streams[swapchain] = stream = new();
        }
        double interval = (time - stream.Last).TotalMilliseconds;
        if (stream.Last != default && interval is > 0 and < 2000)
        { stream.Buffer.Add(interval); stream.Graph.Add(time, interval); stream.Count = Math.Min(600, stream.Count + 1); }
        else { stream.Buffer.Clear(); stream.Graph.Clear(); stream.Count = 0; }
        stream.Last = time;
    }
    internal FpsSample Snapshot(DateTime now)
    {
        // Separate swapchains: summing launcher/video/overlay presents inflates FPS.
        var best = Best(now);
        return best?.Buffer.Snapshot() ?? default;
    }
    private Stream? Best(DateTime now) => _streams.Values.Where(s => now - s.Last < TimeSpan.FromSeconds(2))
        .OrderByDescending(s => s.Count).ThenByDescending(s => s.Last).FirstOrDefault();
    internal FpsGraphPoint[] Graph(DateTime now) => Best(now)?.Graph.Snapshot(now) ?? [];
}
