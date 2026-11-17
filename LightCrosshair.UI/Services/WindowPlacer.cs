using System.Windows;

namespace LightCrosshair.UI.Services;

public readonly record struct WindowPlace(Rect Bounds, bool Maximized);

/// <summary>First open is centered on the monitor under the mouse. Later opens restore the normal bounds and clamp them onto a visible monitor.</summary>
public static class WindowPlacer
{
    public static WindowPlace Place(WindowPlacement? saved, Point mouse, IReadOnlyList<Rect> workAreas, double preferredWidth, double preferredHeight)
    {
        var areas = workAreas.Where(area => area.Width >= 80 && area.Height >= 80).ToList();
        if (areas.Count == 0)
            areas.Add(new Rect(0, 0, preferredWidth, preferredHeight));
        var home = areas.FirstOrDefault(area => area.Contains(mouse));
        if (home.IsEmpty) home = Nearest(areas, mouse);

        if (saved == null || saved.Width < 400 || saved.Height < 300)
            return new WindowPlace(Center(home, preferredWidth, preferredHeight), false);

        var rect = new Rect(saved.Left, saved.Top, saved.Width, saved.Height);
        var target = BestOverlap(areas, rect) ?? home;
        return new WindowPlace(Clamp(target, rect), saved.Maximized && Overlaps(areas, rect));
    }

    private static Rect Center(Rect work, double width, double height)
    {
        width = Math.Min(width, work.Width);
        height = Math.Min(height, work.Height);
        return new Rect(work.Left + (work.Width - width) / 2, work.Top + (work.Height - height) / 2, width, height);
    }

    private static Rect Clamp(Rect work, Rect window)
    {
        var width = Math.Min(window.Width, work.Width);
        var height = Math.Min(window.Height, work.Height);
        var left = Math.Clamp(window.Left, work.Left, work.Right - width);
        var top = Math.Clamp(window.Top, work.Top, work.Bottom - height);
        return new Rect(left, top, width, height);
    }

    private static Rect? BestOverlap(IReadOnlyList<Rect> areas, Rect window)
    {
        Rect? best = null;
        var bestArea = 0d;
        foreach (var area in areas)
        {
            var overlap = Rect.Intersect(area, window);
            if (overlap.IsEmpty) continue;
            var size = overlap.Width * overlap.Height;
            if (size > bestArea)
            {
                bestArea = size;
                best = area;
            }
        }
        return best;
    }

    private static bool Overlaps(IReadOnlyList<Rect> areas, Rect window) => BestOverlap(areas, window) != null;

    private static Rect Nearest(IReadOnlyList<Rect> areas, Point mouse)
    {
        return areas.OrderBy(area =>
        {
            var cx = area.Left + area.Width / 2;
            var cy = area.Top + area.Height / 2;
            var dx = cx - mouse.X;
            var dy = cy - mouse.Y;
            return dx * dx + dy * dy;
        }).First();
    }
}
