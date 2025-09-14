using System;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;

namespace LightCrosshair;

static class Program
{
    private static readonly object _logGate = new object();
    private static readonly string _debugLogPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
    public static bool DebugLoggingEnabled { get; set; } = true;

    [STAThread]
    static void Main()
    {
        try
        {
            // Set up error logging
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.ThreadException += Application_ThreadException;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Enhanced DPI awareness for better performance across different displays
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            bool created;
            using var mutex = new System.Threading.Mutex(true, "LightCrosshair_Singleton", out created);
            if (!created)
            {
                // Another instance running – optionally bring to front via IPC (omitted here)
                return;
            }

            // Initialize profiles centrally
            var ps = ProfileService.Instance;
            ps.InitializeAsync().GetAwaiter().GetResult();

            using var form = new Form1(ps);
            Application.Run(form);
        }
        catch (Exception ex)
        {
            LogError(ex, "Main method");
            MessageBox.Show($"Error: {ex.Message}\n\nCheck error.log for details.", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
    {
        LogError(e.Exception, "Thread exception");
        MessageBox.Show($"Thread error: {e.Exception.Message}\n\nCheck error.log for details.", "Error",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogError(ex, "Unhandled exception");
            MessageBox.Show($"Unhandled error: {ex.Message}\n\nCheck error.log for details.", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static void LogError(Exception ex, string context)
    {
        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            string timestamp = DateTime.Now.ToString("[dd/MM/yyyy HH:mm:ss]");
            string errorMessage = $"{timestamp} {context}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
            lock (_logGate)
            {
                File.AppendAllText(logPath, errorMessage);
            }
            try { Console.WriteLine(errorMessage); } catch { }
            Debug.WriteLine(errorMessage);
        }
        catch
        {
            // If logging fails, we can't do much about it
        }
    }

    public static void LogDebug(string message, string? context = null)
    {
        if (!DebugLoggingEnabled) return;
        try
        {
            string timestamp = DateTime.Now.ToString("[dd/MM/yyyy HH:mm:ss]");
            string line = context is null ? $"{timestamp} {message}\n" : $"{timestamp} {context}: {message}\n";
            lock (_logGate)
            {
                File.AppendAllText(_debugLogPath, line);
            }
            try { Console.WriteLine(line.TrimEnd()); } catch { }
            Debug.WriteLine(line);
        }
        catch { }
    }
}
