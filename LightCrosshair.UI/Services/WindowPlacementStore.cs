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

    public static void Restore(Window window, WindowPlacement? placement)
    {
        if (placement == null || placement.Width < 400 || placement.Height < 300 || !IntersectsVirtualScreen(placement))
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Width = 1180;
            window.Height = 740;
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = placement.Left;
        window.Top = placement.Top;
        window.Width = Math.Max(window.MinWidth, placement.Width);
        window.Height = Math.Max(window.MinHeight, placement.Height);
        if (placement.Maximized) window.WindowState = WindowState.Maximized;
    }

    private static bool IntersectsVirtualScreen(WindowPlacement placement)
    {
        var rect = new Rect(placement.Left, placement.Top, placement.Width, placement.Height);
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        rect.Intersect(screen);
        return rect.Width > 80 && rect.Height > 80;
    }
}
