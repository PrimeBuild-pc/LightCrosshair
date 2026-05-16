#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LightCrosshair.FrameLimiting
{
    internal static class DisplayRefreshRateDetector
    {
        private const int EnumCurrentSettings = -1;

        public static double? TryGetPrimaryDisplayRefreshRateHz()
        {
            try
            {
                string? deviceName = Screen.PrimaryScreen?.DeviceName;
                if (string.IsNullOrWhiteSpace(deviceName))
                {
                    return null;
                }

                var mode = new DevMode
                {
                    dmSize = (short)Marshal.SizeOf<DevMode>()
                };

                if (!EnumDisplaySettings(deviceName, EnumCurrentSettings, ref mode))
                {
                    return null;
                }

                return mode.dmDisplayFrequency > 1 ? mode.dmDisplayFrequency : null;
            }
            catch
            {
                return null;
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DevMode devMode);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DevMode
        {
            private const int CchDevName = 32;
            private const int CchFormName = 32;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchDevName)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchFormName)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
        }
    }
}
