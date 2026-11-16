using System.Reflection;
using System.Text.Json;
using LightCrosshair;

// Snapshot through the existing baseline API. No new color values are requested:
// its ADL writability probes reapply each channel's current value. No explicit
// TryApply/TryRestore calls are made here; compare snapshots after the Core test.
if (args.Length == 2 && args[0] == "--gamma-readback") { GammaReadback.Read(args[1]); return; }
if (args.Length != 2) throw new ArgumentException("snapshot.json isolated-data-root");
Environment.SetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT", args[1]);
var factory = typeof(ColorRecoverySnapshot).Assembly.GetType("LightCrosshair.GpuColorManagerFactory", true)!;
var manager = (IGpuColorManager)factory.GetMethod("CreateDefault", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, null)!;
if (!manager.TryCaptureOriginalState(out string? error)) throw new InvalidOperationException(error ?? "Cannot read color baseline");
var snapshot = manager.CaptureRecovery(); snapshot.Vendor = manager.Vendor.ToString();
snapshot.Displays = snapshot.Displays.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
snapshot.AmdChannels = snapshot.AmdChannels.OrderBy(c => c.Adapter).ThenBy(c => c.Display).ThenBy(c => c.Type).ToList();
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
File.WriteAllText(args[0], JsonSerializer.Serialize(snapshot));
Console.WriteLine($"Read {snapshot.Vendor}: {snapshot.AmdChannels.Count} AMD channels, {snapshot.Global?.Red.Length ?? 0} global ramp entries; {manager.BackendName}");
