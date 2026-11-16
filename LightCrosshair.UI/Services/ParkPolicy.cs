using System.Diagnostics;

namespace LightCrosshair.UI.Services;

public static class ParkPolicy
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(5);

    public static ProcessPriorityClass Priority(bool parked, bool gameForeground) =>
        parked || gameForeground ? ProcessPriorityClass.BelowNormal : ProcessPriorityClass.Normal;

    public static bool GameIsForeground(Snapshot? snapshot)
    {
        var runtime = snapshot?.Runtime;
        if (runtime == null) return false;
        if (!string.IsNullOrEmpty(runtime.ActiveGameProfileId)) return true;
        var process = runtime.ForegroundProcess;
        if (string.IsNullOrEmpty(process)) return false;
        return snapshot!.Store.GameProfiles.Any(profile => string.Equals(profile.ProcessName, process, StringComparison.OrdinalIgnoreCase));
    }
}

public static class ProcessDuty
{
    public static void Apply(ProcessPriorityClass priority)
    {
        try { Process.GetCurrentProcess().PriorityClass = priority; }
        catch (Exception) { /* a restricted process keeps the priority Core gave it */ }
    }

    public static void TrimWorkingSet()
    {
        try { SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1)); }
        catch (Exception) { }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimum, IntPtr maximum);
}

public static class OpenTiming
{
    private static readonly long Started = Environment.TickCount64;
    private static int _coldReported;

    public static void NoteFirstFrame()
    {
        if (Interlocked.Exchange(ref _coldReported, 1) != 0) return;
        Write("cold", Environment.TickCount64 - Started);
    }

    public static void NoteWake(long requestedAt) => Write("wake", Environment.TickCount64 - requestedAt);

    public static void NoteParkedMemory()
    {
        if (Environment.GetEnvironmentVariable("LIGHTCROSSHAIR_TIMING") != "1") return;
        var bytes = Process.GetCurrentProcess().WorkingSet64;
        Write("parked-bytes", bytes);
    }

    private static void Write(string name, long value)
    {
        if (Environment.GetEnvironmentVariable("LIGHTCROSSHAIR_TIMING") != "1") return;
        try
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "lc-ui-timing.txt"), name + " " + value + Environment.NewLine);
        }
        catch (Exception) { }
    }
}
