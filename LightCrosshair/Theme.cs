using Microsoft.Win32;
using System;
using System.Drawing;

namespace LightCrosshair
{
    /// <summary>
    /// Central theme manager (phase 1). Light/dark detection via registry and static palette.
    /// Future: raise ThemeChanged event and allow runtime switching + high contrast mode.
    /// </summary>
    public static class Theme
    {
        public static event EventHandler? ThemeChanged;

        private static bool? _cachedIsDark;
        private static DateTime _lastCheck = DateTime.MinValue;
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(10);

        public static bool IsDark
        {
            get
            {
                if (_cachedIsDark == null || (DateTime.UtcNow - _lastCheck) > _cacheDuration)
                {
                    _cachedIsDark = DetectDarkMode();
                    _lastCheck = DateTime.UtcNow;
                }
                return _cachedIsDark.Value;
            }
        }

        public static Color BackColor => IsDark ? Color.FromArgb(32,32,34) : Color.White;
        public static Color PanelColor => IsDark ? Color.FromArgb(45,45,48) : Color.FromArgb(245,245,245);
        public static Color Accent => Color.FromArgb(0, 122, 204); // consistent accent
        public static Color TextPrimary => IsDark ? Color.White : Color.Black;
        public static Color TextSecondary => IsDark ? Color.FromArgb(180,180,185) : Color.FromArgb(90,90,95);
        public static Color Border => IsDark ? Color.FromArgb(60,60,64) : Color.FromArgb(210,210,215);

        /// <summary>
        /// Simple registry-based detection. Returns true when system app theme = dark.
        /// </summary>
        private static bool DetectDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int light)
                {
                    return light == 0; // 0 = dark, 1 = light
                }
            }
            catch { }
            return false; // fallback to light
        }

        public static void ForceRefresh()
        {
            _cachedIsDark = null;
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
