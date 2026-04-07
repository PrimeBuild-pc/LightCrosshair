using System;
using System.Runtime.InteropServices;

namespace LightCrosshair
{
    internal static class WpfSettingsHost
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        private static SettingsWindow? _window;
        // Bridge for small interactions (e.g., pixel nudges)
    public static Action<int,int>? OnNudge;
    public static Func<System.Drawing.Point>? GetPosition; // screen coords
    public static Action? ResetCenter;

        public static void Nudge(int dx, int dy)
        {
            try { OnNudge?.Invoke(dx, dy); }
            catch (Exception ex)
            {
                Program.LogError(ex, "WpfSettingsHost.Nudge failed");
            }
        }

        public static System.Drawing.Point? QueryPosition()
        {
            try { return GetPosition?.Invoke(); } catch { return null; }
        }

        public static void ResetToCenter()
        {
            try { ResetCenter?.Invoke(); }
            catch (Exception ex)
            {
                Program.LogError(ex, "WpfSettingsHost.ResetToCenter failed");
            }
        }

        public static void Show(IProfileService profiles)
        {
            if (System.Windows.Application.Current == null)
            {
                // Create a WPF Application without shutting down the process when last window closes
                _ = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            }

            try
            {
                if (_window == null || !_window.IsLoaded)
                {
                    _window = new SettingsWindow(profiles);
                }

                _window.SyncDisplaySettingsFromConfig();
                if (_window.WindowState == System.Windows.WindowState.Minimized)
                {
                    _window.WindowState = System.Windows.WindowState.Normal;
                }

                BringToFront(_window);
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "WpfSettingsHost.Show primary path failed");

                // Recovery path for stale window state (prevents hotkey from becoming a no-op).
                try
                {
                    _window = new SettingsWindow(profiles);
                    _window.SyncDisplaySettingsFromConfig();
                    BringToFront(_window);
                }
                catch (Exception retryEx)
                {
                    Program.LogError(retryEx, "WpfSettingsHost.Show recovery path failed");
                }
            }
        }

        private static void BringToFront(SettingsWindow window)
        {
            if (!window.IsVisible)
            {
                window.Show();
            }

            window.Activate();
            window.Focus();

            try
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero)
                {
                    _ = ShowWindow(handle, SW_RESTORE);
                    _ = SetForegroundWindow(handle);
                }
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "WpfSettingsHost.BringToFront failed");
            }
        }

        public static void RefreshDisplayUiFromConfig()
        {
            try
            {
                if (_window == null || !_window.IsLoaded)
                {
                    return;
                }

                var dispatcher = _window.Dispatcher;
                if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                {
                    return;
                }

                _ = dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_window != null && _window.IsLoaded)
                        {
                            _window.SyncDisplaySettingsFromConfig();
                        }
                    }
                    catch (Exception ex)
                    {
                        Program.LogError(ex, "WpfSettingsHost.RefreshDisplayUiFromConfig invoke failed");
                    }
                }));
            }
            catch (Exception ex)
            {
                Program.LogError(ex, "WpfSettingsHost.RefreshDisplayUiFromConfig failed");
            }
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

