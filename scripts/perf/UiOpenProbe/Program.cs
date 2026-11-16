using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Tracing.Session;

// External observer: first WPF UCE Present-end timestamp, never HWND creation time.
if (args.Length != 5) throw new ArgumentException("core-pid probe-hwnd cycles output.csv stage");
using var core = Process.GetProcessById(int.Parse(args[0], CultureInfo.InvariantCulture));
IntPtr foreground = new(long.Parse(args[1], CultureInfo.InvariantCulture));
int cycles = int.Parse(args[2], CultureInfo.InvariantCulture);
string output = args[3], stage = args[4];
if (Process.GetProcessesByName("LightCrosshair.UI").Length != 0) throw new InvalidOperationException("Close other LightCrosshair UI processes first.");
IntPtr coreWindow = Native.Find(core.Id, "LightCrosshair Core");
if (coreWindow == IntPtr.Zero) throw new InvalidOperationException("Core window missing.");
var presents = new ConcurrentQueue<(int Pid, DateTime Time)>();
var observed = new ConcurrentDictionary<int, int>();
using var trace = new TraceEventSession($"LightCrosshair.UiProbe.{Environment.ProcessId}") { StopOnDispose = true };
Guid wpf = new("E13B77A8-14B6-11DE-8069-001B212B5009");
trace.EnableProvider(wpf, Microsoft.Diagnostics.Tracing.TraceEventLevel.Verbose, 0x1002);
void Observe(Microsoft.Diagnostics.Tracing.TraceEvent data)
{
    if (data.ProviderGuid != wpf) return;
    observed.AddOrUpdate((int)data.ID, 1, (_, count) => count + 1);
    if ((int)data.ID != 11035) return;
    // Correlate with the PID observed at launch, even if the process has exited
    // before this real-time ETW buffer arrives. Never resolve names retrospectively.
    presents.Enqueue((data.ProcessID, data.TimeStamp));
}
trace.Source.AllEvents += Observe;
trace.Source.UnhandledEvents += Observe;
var capture = Task.Run(() => trace.Source.Process());
var rows = new List<(int Cycle, int Pid, DateTime Start, double VisibleMs, double CpuMs, double IntervalMs, string Priority, int Io)>();
try
{
    for (int cycle = 1; cycle <= cycles; cycle++)
    {
        // Foreground focus is an explicit part of this interactive game test.
        var focusStart = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        foreach (string argument in new[] { "-NoProfile", "-Command", "agent-desktop --headed focus-window --app SwapchainProbe" }) focusStart.ArgumentList.Add(argument);
        using (var focus = Process.Start(focusStart)!) { string result = await focus.StandardOutput.ReadToEndAsync(); await focus.WaitForExitAsync(); if (focus.ExitCode != 0) throw new InvalidOperationException(result); }
        await Task.Delay(300);
        if (!Native.IsWindowVisible(foreground) || Native.GetForegroundWindow() != foreground) throw new InvalidOperationException($"3D focus invalid: visible={Native.IsWindowVisible(foreground)}, foreground={Native.GetForegroundWindow()}, expected={foreground}.");
        core.Refresh(); double cpu = core.TotalProcessorTime.TotalMilliseconds;
        DateTime start = DateTime.Now; var elapsed = Stopwatch.StartNew();
        Native.PostMessage(coreWindow, 0x312, new(9003), IntPtr.Zero);
        Process? ui = null; double visible = double.NaN; string priority = "unknown"; int io = -1;
        try
        {
            while (elapsed.ElapsedMilliseconds < 2000)
            {
                if (ui is null)
                {
                    var candidates = Process.GetProcessesByName("LightCrosshair.UI");
                    ui = candidates.FirstOrDefault(); foreach (var other in candidates.Skip(1)) other.Dispose();
                    if (ui is not null) { priority = ui.PriorityClass.ToString(); Native.NtQueryInformationProcess(ui.Handle, 33, out io, 4, out _); }
                }
                if (ui is not null && double.IsNaN(visible)) { ui.Refresh(); if (ui.MainWindowHandle != IntPtr.Zero && Native.IsWindowVisible(ui.MainWindowHandle)) visible = elapsed.Elapsed.TotalMilliseconds; }
                await Task.Delay(10);
            }
            core.Refresh();
            rows.Add((cycle, ui?.Id ?? 0, start, visible, core.TotalProcessorTime.TotalMilliseconds - cpu, elapsed.Elapsed.TotalMilliseconds, priority, io));
            if (ui is null) throw new InvalidOperationException("UI did not launch.");
            ui.CloseMainWindow();
            if (!ui.WaitForExit(10000)) throw new InvalidOperationException("UI parked instead of exiting; use a warm-session benchmark for this UI build.");
        }
        finally { ui?.Dispose(); }
        await Task.Delay(1200);
    }
    // Receive the last real-time ETW buffer before selecting original event timestamps.
    await Task.Delay(1500);
    var records = presents.ToArray();
    using var writer = new StreamWriter(output, false, new UTF8Encoding(false));
    writer.WriteLine("Stage,Cycle,UiPid,FirstPresentMs,VisibleWindowMs,CoreCpuMs,ObservedMs,CoreCpuOneCorePercent,LaunchPriority,IoPriority");
    foreach (var row in rows)
    {
        DateTime first = records.Where(p => p.Pid == row.Pid && p.Time >= row.Start).Select(p => p.Time).DefaultIfEmpty().Min();
        double frame = first == default ? double.NaN : (first - row.Start).TotalMilliseconds;
        writer.WriteLine(FormattableString.Invariant($"{stage},{row.Cycle},{row.Pid},{frame:F3},{row.VisibleMs:F3},{row.CpuMs:F3},{row.IntervalMs:F3},{100 * row.CpuMs / row.IntervalMs:F3},{row.Priority},{row.Io}"));
        Console.WriteLine(FormattableString.Invariant($"{stage} {row.Cycle}: first Present {frame:F1} ms; Core {row.CpuMs:F3} CPU ms / {row.IntervalMs:F0} ms; launch {row.Priority}, I/O {row.Io}"));
    }
    File.WriteAllLines(output + ".events.csv", new[] { "WpfEventId,Count" }.Concat(observed.OrderBy(p => p.Key).Select(p => $"{p.Key},{p.Value}")));
    if (rows.Any(row => !records.Any(p => p.Pid == row.Pid && p.Time >= row.Start))) throw new InvalidOperationException("No UI presentation event captured; do not substitute window visibility for first frame.");
}
finally { trace.Stop(); trace.Source.StopProcessing(); await capture; }

static class Native
{
    private delegate bool EnumProc(IntPtr window, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
    [DllImport("ntdll.dll")] internal static extern int NtQueryInformationProcess(IntPtr process, int info, out int value, int size, out int returned);
    internal static IntPtr Find(int pid, string title)
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows((h, _) => { GetWindowThreadProcessId(h, out uint owner); var text = new StringBuilder(256); GetWindowText(h, text, text.Capacity); if (owner == pid && text.ToString() == title) result = h; return true; }, IntPtr.Zero);
        return result;
    }
}
