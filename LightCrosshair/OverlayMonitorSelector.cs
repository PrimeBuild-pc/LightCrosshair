using System;
using System.Drawing;
using System.Windows.Forms;

namespace LightCrosshair
{
    internal static class OverlayMonitorSelector
    {
        internal static int ResolveIndex(string? configuredDeviceName, string[] deviceNames, int primaryIndex)
        {
            if (!string.IsNullOrWhiteSpace(configuredDeviceName))
            {
                for (int i = 0; i < deviceNames.Length; i++)
                {
                    if (string.Equals(deviceNames[i], configuredDeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            return primaryIndex >= 0 && primaryIndex < deviceNames.Length ? primaryIndex : 0;
        }

        public static Rectangle ResolveBounds(string? configuredDeviceName)
        {
            Screen[] screens = Screen.AllScreens;
            if (screens.Length == 0)
            {
                return Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            }

            var deviceNames = new string[screens.Length];
            int primaryIndex = 0;
            for (int i = 0; i < screens.Length; i++)
            {
                deviceNames[i] = screens[i].DeviceName;
                if (screens[i].Primary)
                {
                    primaryIndex = i;
                }
            }

            return screens[ResolveIndex(configuredDeviceName, deviceNames, primaryIndex)].Bounds;
        }
    }
}
