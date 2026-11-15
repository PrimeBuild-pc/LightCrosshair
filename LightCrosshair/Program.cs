using LightCrosshair.GpuDriver;
using System.Text;

namespace LightCrosshair;

internal static class Program
{
    private static readonly object LogGate = new();
    internal static string DataRoot => Environment.GetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LightCrosshair", "v2");
    public static bool DebugLoggingEnabled { get; set; } = Environment.GetEnvironmentVariable("LIGHTCROSSHAIR_DEBUG_LOG") == "1";

    [STAThread]
    private static int Main(string[] args)
    {
        if (NvidiaHelperHost.IsHelperInvocation(args)) return NvidiaHelperHost.Run(args);
        using var mutex = new Mutex(true, @"Local\LightCrosshair.Core", out bool created);
        if (!created) return 0;
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (_, e) => LogError(e.Exception, "UI thread");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) LogError(ex, "Unhandled"); };
        try
        {
            using var form = new Form1();
            Application.Run(form);
            return 0;
        }
        catch (Exception ex) { LogError(ex, "Startup"); return 1; }
    }

    public static void LogError(Exception ex, string context) => WriteLog($"{context}: {ex.GetType().Name}");
    public static void LogDebug(string message, string? context = null) { if (DebugLoggingEnabled) WriteLog(context ?? "Debug"); }
    // Legacy driver diagnostics may contain paths. Log the operation only.
    public static void LogLifecycle(string message, string? context = null) => WriteLog(context ?? "Lifecycle");
    internal static void WriteLog(string message)
    {
        try
        {
            lock (LogGate)
            {
                string directory = Path.Combine(DataRoot, "logs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "core.log");
                if (File.Exists(path) && new FileInfo(path).Length >= 1024 * 1024)
                {
                    File.Move(path + ".1", path + ".2", true);
                    File.Move(path, path + ".1", true);
                }
                File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {message}\n", Encoding.UTF8);
            }
        }
        catch { /* Diagnostics cannot prevent restoration or shutdown. */ }
    }
}
