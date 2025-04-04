using System;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;

namespace LightCrosshair;

static class Program
{
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
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            // Create and run the form
            using var form = new Form1();
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

    private static void LogError(Exception ex, string context)
    {
        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            string timestamp = DateTime.Now.ToString("[dd/MM/yyyy HH:mm:ss]");
            string errorMessage = $"{timestamp} {context}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";

            File.AppendAllText(logPath, errorMessage);
            Debug.WriteLine(errorMessage);
        }
        catch
        {
            // If logging fails, we can't do much about it
        }
    }
}
