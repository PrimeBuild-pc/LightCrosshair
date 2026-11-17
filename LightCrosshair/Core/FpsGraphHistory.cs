namespace LightCrosshair.Core;

internal readonly record struct FpsGraphPoint(double AgeMilliseconds, double Frametime);

// At most one peak per 50 ms bucket: bounded history independent of game FPS.
internal sealed class FpsGraphHistory
{
    private readonly DateTime[] _times = new DateTime[48];
    private readonly double[] _peaks = new double[48];
    private int _next, _count;
    internal void Clear() { _next = _count = 0; }
    internal void Add(DateTime time, double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds <= 0 || milliseconds >= 2000) return;
        int last = (_next + _times.Length - 1) % _times.Length;
        if (_count > 0 && time.Ticks / 500_000 == _times[last].Ticks / 500_000)
        { _peaks[last] = Math.Max(_peaks[last], milliseconds); return; }
        _times[_next] = time; _peaks[_next] = milliseconds;
        _next = (_next + 1) % _times.Length; _count = Math.Min(_count + 1, _times.Length);
    }
    internal FpsGraphPoint[] Snapshot(DateTime now)
    {
        var result = new List<FpsGraphPoint>(41);
        for (int i = _count; i > 0; i--)
        {
            int index = (_next + _times.Length - i) % _times.Length;
            double age = (now - _times[index]).TotalMilliseconds;
            if (age is >= 0 and < 2000) result.Add(new(age, _peaks[index]));
        }
        return result.ToArray();
    }
}
