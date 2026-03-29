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

        public static bool IsVisible
        {
            get
            {
                try
                {
                    return _window != null && _window.IsLoaded && _window.IsVisible;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void Hide()
        {
            try
            {
                if (_window != null && _window.IsLoaded)
                {
                    _window.Hide();
                }
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "WpfSettingsHost: Hide Failed");
            }
        }

        public static void Toggle(IProfileService profiles)
        {
            if (IsVisible)
            {
                Hide();
                return;
            }

            Show(profiles);
        }

        public static void Shutdown()
        {
            try
            {
                if (_window != null)
                {
                    _window.AllowCloseForShutdown();
                    _window.Close();
                    _window = null;
                }
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "WpfSettingsHost: Window Close Failed");
            }

            try
            {
                var app = System.Windows.Application.Current;
                if (app != null)
                {
                    app.Dispatcher.Invoke(() => app.Shutdown());
                }
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "WpfSettingsHost: App Shutdown Failed");
            }

            OnNudge = null;
            GetPosition = null;
            ResetCenter = null;
        }
    }
}

