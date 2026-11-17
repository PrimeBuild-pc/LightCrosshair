using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;

namespace LightCrosshair.UI.Services;

public sealed class WindowPlacement
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Maximized { get; set; }
}

public static class WindowPlacementStore
{
    internal static string FilePath
    {
        get
        {
            var root = Environment.GetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT");
            if (string.IsNullOrWhiteSpace(root))
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LightCrosshair", "v2");
            return Path.Combine(root, "ui-placement.json");
        }
    }

    public static WindowPlacement? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            return JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(FilePath));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Save(Window window)
    {
        try
        {
            var bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var placement = new WindowPlacement
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                Maximized = window.WindowState == WindowState.Maximized,
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(placement));
        }
        catch (Exception)
        {
            // Placement is convenience state. A failed write must not block closing.
        }
    }

    public static bool Restore(Window window, WindowPlacement? placement)
    {
        var place = WindowPlacer.Place(placement, Cursor(), WorkAreas(), 1180, 740);
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.WindowState = WindowState.Normal;
        window.Left = place.Bounds.Left;
        window.Top = place.Bounds.Top;
        window.Width = Math.Max(window.MinWidth, place.Bounds.Width);
        window.Height = Math.Max(window.MinHeight, place.Bounds.Height);
        // Always open as a normal window at its last normal size (1180x740 the first time), never maximized.
        return false;
    }

    private static Point Cursor()
    {
        return GetCursorPos(out var point) ? new Point(point.X, point.Y) : new Point(0, 0);
    }

    private static IReadOnlyList<Rect> WorkAreas()
    {
        var areas = new List<Rect>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                var work = info.Work;
                areas.Add(new Rect(work.Left, work.Top, Math.Max(0, work.Right - work.Left), Math.Max(0, work.Bottom - work.Top)));
            }
            return true;
        }, IntPtr.Zero);
        return areas;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }
}
