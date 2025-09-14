using System;

namespace LightCrosshair
{
    internal static class WpfSettingsHost
    {
        private static SettingsWindow? _window;
        // Bridge for small interactions (e.g., pixel nudges)
    public static Action<int,int>? OnNudge;
    public static Func<System.Drawing.Point>? GetPosition; // screen coords
    public static Action? ResetCenter;

        public static void Nudge(int dx, int dy)
        {
            try { OnNudge?.Invoke(dx, dy); } catch { }
        }

        public static System.Drawing.Point? QueryPosition()
        {
            try { return GetPosition?.Invoke(); } catch { return null; }
        }

        public static void ResetToCenter()
        {
            try { ResetCenter?.Invoke(); } catch { }
        }

        public static void Show(IProfileService profiles)
        {
            if (System.Windows.Application.Current == null)
            {
                // Create a WPF Application without shutting down the process when last window closes
                _ = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            }

            if (_window == null || !_window.IsLoaded)
            {
                _window = new SettingsWindow(profiles);
            }

            _window.Show();
            _window.Activate();
            _window.Focus();
        }
    }
}

