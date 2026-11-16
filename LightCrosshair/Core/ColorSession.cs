using System.Text.Json;
using LightCrosshair.Contracts;
using LightCrosshair.GpuDriver;

namespace LightCrosshair
{

public sealed class RampSnapshot
{
    public ushort[] Red { get; set; } = [];
    public ushort[] Green { get; set; } = [];
    public ushort[] Blue { get; set; } = [];
    internal static RampSnapshot From(Win32GammaColorManager.GammaRamp ramp) => new() { Red = ramp.Red, Green = ramp.Green, Blue = ramp.Blue };
    internal Win32GammaColorManager.GammaRamp ToRamp()
    {
        if (Red?.Length != 256 || Green?.Length != 256 || Blue?.Length != 256) throw new InvalidDataException("Invalid recovery ramp");
        return new() { Red = Red, Green = Green, Blue = Blue };
    }
}
public sealed record AmdChannelSnapshot(int Adapter, int Display, int Type, int Value);
public sealed class ColorRecoverySnapshot
{
    public string Vendor { get; set; } = "";
    public RampSnapshot? Global { get; set; }
    public Dictionary<string, RampSnapshot> Displays { get; set; } = new();
    public List<AmdChannelSnapshot> AmdChannels { get; set; } = [];
    public int? NvidiaVibrance { get; set; }
    public NvidiaVibranceState? NvidiaVibranceState { get; set; }
}

namespace Core
{
    internal static class ColorWriteGuard
    {
        [ThreadStatic] internal static Action? BeforeWrite;
    }
    // The durable marker IS the original state. It is flushed before the first write,
    // survives a killed process, and is removed only when every restoration succeeds.
    internal sealed class ColorSession(string directory, Func<IGpuColorManager>? create = null, Func<NvidiaHelperRequest, NvidiaHelperResponse>? helper = null)
    {
        public event Action<long, string?>? StatusChanged;
        internal long Revision => Volatile.Read(ref _revision);
        private Task _tail = Task.CompletedTask;
        private long _revision;
        private readonly object _gate = new();
        private IGpuColorManager? _manager;
        private ColorRecoverySnapshot? _original;
        private string? _lastFailure;
        private sealed class BackendFailure(string code, string message) : Exception(message) { internal string Code { get; } = code; }
        private readonly Func<IGpuColorManager> _create = create ?? GpuColorManagerFactory.CreateDefault;
        private readonly Func<NvidiaHelperRequest, NvidiaHelperResponse> _helper = helper ?? (request => new NvidiaHelperProcessClient().Run(request));
        private string Marker => Path.Combine(directory, "colors-applied.json");
        public Task SetAsync(DisplayColorSettings? colors)
        {
            var copy = colors is null ? null : Validation.Copy(colors);
            lock (_gate)
            {
                long revision = ++_revision;
                return _tail = _tail.ContinueWith(_ =>
                {
                    // A slow vendor call must not leave a backlog of obsolete game colors.
                    if (revision == Volatile.Read(ref _revision)) Transition(copy, revision);
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            }
        }
        public Task RecoverAsync() => SetAsync(null);
        private void Notify(long revision, string? code)
        { if (revision == Revision) StatusChanged?.Invoke(revision, code); }
        private void Transition(DisplayColorSettings? colors, long revision, bool rollback = false)
        {
            bool restoring = colors is null;
            try
            {
                if (File.Exists(Marker))
                {
                    restoring = true;
                    _manager ??= _create();
                    _original ??= JsonSerializer.Deserialize<ColorRecoverySnapshot>(File.ReadAllText(Marker), ContractJson.Options) ?? throw new InvalidDataException();
                    if (_original.Vendor != _manager.Vendor.ToString()) throw new InvalidOperationException("Recovery adapter changed");
                    _manager.LoadRecovery(_original);
                    bool gammaOk = _manager.TryRestore(out string? restoreError);
                    bool vibranceOk = _original.NvidiaVibranceState is { } raw
                        ? _helper(new() { Operation = NvidiaHelperOperations.RestoreVibrance, VibranceState = raw }).Success
                        : !_original.NvidiaVibrance.HasValue || _helper(NvidiaHelperRequest.SetVibrance(_original.NvidiaVibrance.Value)).Success;
                    if (!gammaOk || !vibranceOk) throw new BackendFailure("restore_failed", $"{_manager.BackendName}: Color restoration failed: {restoreError ?? "gamma restored"}; NVIDIA vibrance restored={vibranceOk}");
                    File.Delete(Marker); _original = null; _manager.Dispose(); _manager = null;
                }
                if (colors is null)
                {
                    if (!rollback) { _lastFailure = null; Notify(revision, null); }
                    return;
                }
                restoring = false;
                _manager?.Dispose();
                _manager = _create();
                if (!_manager.TryCaptureOriginalState(out string? captureError)) throw new BackendFailure("unsupported", $"{_manager.BackendName}: Color baseline unavailable: {captureError ?? "driver returned no details"}");
                _original = _manager.CaptureRecovery(); _original.Vendor = _manager.Vendor.ToString();
                if (colors.NvidiaDigitalVibrance.HasValue)
                {
                    var baseline = _helper(new() { Operation = NvidiaHelperOperations.GetVibrance });
                    if (!baseline.Success || baseline.VibranceState is null) throw new BackendFailure("unsupported", "NVIDIA vibrance baseline unavailable");
                    _original.NvidiaVibranceState = baseline.VibranceState;
                }
                Directory.CreateDirectory(directory);
                string temp = Marker + ".tmp";
                using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                { JsonSerializer.Serialize(output, _original, ContractJson.Options); output.Flush(true); }
                File.Move(temp, Marker, true);
                ColorWriteGuard.BeforeWrite = () =>
                {
                    // Some legacy backends capture extra endpoints lazily. Refresh the journal
                    // immediately before each hardware write, including those extra baselines.
                    var updated = _manager.CaptureRecovery();
                    updated.Vendor = _original.Vendor; updated.NvidiaVibrance = _original.NvidiaVibrance; updated.NvidiaVibranceState = _original.NvidiaVibranceState;
                    using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                    { JsonSerializer.Serialize(stream, updated, ContractJson.Options); stream.Flush(true); }
                    File.Move(temp, Marker, true); _original = updated;
                };
                try
                {
                    if (!_manager.TryApply(new(colors.Gamma, colors.Contrast, colors.Brightness, colors.Vibrance), out string? applyError))
                        throw new BackendFailure("unsupported", $"{_manager.BackendName}: Color apply failed: {applyError ?? "driver refused the request without details"}");
                    if (colors.NvidiaDigitalVibrance.HasValue && !_helper(NvidiaHelperRequest.SetVibrance(colors.NvidiaDigitalVibrance.Value)).Success)
                        throw new BackendFailure("unsupported", "NVIDIA vibrance apply failed");
                }
                finally { ColorWriteGuard.BeforeWrite = null; }
                _lastFailure = null; Notify(revision, null);
            }
            catch (Exception ex)
            {
                string code = restoring ? "restore_failed" : ex is BackendFailure backend ? backend.Code : "apply_failed";
                string signature = code + ":" + ex.Message;
                if (_lastFailure != signature) { Program.LogError(ex, "Display color transaction (" + code + ")", directory); _lastFailure = signature; }
                Notify(revision, code);
                // Partial apply must immediately try to restore. Leave the marker on failure.
                if (colors is not null && !restoring && File.Exists(Marker)) Transition(null, revision, rollback: true);
                if (!File.Exists(Marker)) { _manager?.Dispose(); _manager = null; }
            }
        }
    }
}

}
