using System;

namespace LightCrosshair
{
    internal static class WpfSettingsHost
    {
        private static SettingsWindow? _window;

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

