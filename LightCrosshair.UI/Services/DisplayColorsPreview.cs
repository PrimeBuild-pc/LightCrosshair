using LightCrosshair.UI.Core;

namespace LightCrosshair.UI.Services;

// UI-thread coordinator: one in-flight request and one replaceable latest value.
public sealed class DisplayColorsPreview(ICoreClient core)
{
    private DisplayColorSettings? _latest;
    private Task _pending = Task.CompletedTask;
    private long _lastSent;
    private bool _active, _draining;
    public event Action? Failed;
    public void Begin() => _active = true;
    public void Update(DisplayColorSettings colors)
    {
        if (!_active) return;
        _latest = JsonClone.Clone(colors);
        if (!_draining) _pending = DrainAsync();
    }
    private async Task DrainAsync()
    {
        _draining = true;
        try
        {
            while (_latest is not null)
            {
                int delay = (int)Math.Max(0, 50 - (Environment.TickCount64 - _lastSent));
                if (delay > 0) await Task.Delay(delay).ConfigureAwait(true);
                var colors = _latest; _latest = null;
                _lastSent = Environment.TickCount64;
                await core.PreviewDisplayColorsAsync(colors).ConfigureAwait(true);
            }
        }
        catch (Exception) { _latest = null; Failed?.Invoke(); }
        finally { _draining = false; }
    }
    public async Task EndAsync()
    {
        if (!_active) return;
        _active = false;
        await _pending.ConfigureAwait(true); // Drain the final value before ending.
        if (_active) return;
        int delay = (int)Math.Max(0, 50 - (Environment.TickCount64 - _lastSent));
        if (delay > 0) await Task.Delay(delay).ConfigureAwait(true);
        if (_active) return;
        _lastSent = Environment.TickCount64;
        try { await core.PreviewDisplayColorsAsync(null).ConfigureAwait(true); }
        catch (Exception) { Failed?.Invoke(); }
    }
}
