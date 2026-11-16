using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightCrosshair.UI.Core;

/// <summary>
/// Placeholder crosshair drawing used only by <see cref="FakeCoreClient"/>.
/// The real UI asks Core to render; this exists so Studio and the library work without it.
/// </summary>
internal static class FakeRenderer
{
    public static string Render(CrosshairDesign design, int canvas, string? background)
    {
        canvas = Math.Clamp(canvas, 16, 512);
        var pixels = new byte[canvas * canvas * 4];
        if (TryParse(background, out var br, out var bg, out var bb))
        {
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = br;
                pixels[i + 1] = bg;
                pixels[i + 2] = bb;
                pixels[i + 3] = 255;
            }
        }

        foreach (var layer in design.Layers)
            DrawLayer(pixels, canvas, layer, design.AntiAlias);

        return Convert.ToBase64String(EncodePng(pixels, canvas, canvas));
    }

    private static void DrawLayer(byte[] pixels, int canvas, CrosshairLayer layer, bool antiAlias)
    {
        if (!layer.Visible) return;
        if (!TryParse(layer.Color, out var r, out var g, out var b)) return;
        TryParse(layer.OutlineColor, out var or, out var og, out var ob);
        var opacity = Math.Clamp(layer.Opacity, 0, 1);
        if (opacity <= 0) return;
        var cx = canvas / 2.0 + layer.OffsetX;
        var cy = canvas / 2.0 + layer.OffsetY;
        var rad = layer.Rotation * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var outline = layer.Outline ? Math.Clamp(layer.OutlineThickness, 1, 8) : 0;
        byte fr = 0, fg = 0, fb = 0;
        var hasFill = layer.Kind is LayerKind.Ring or LayerKind.Box && TryParse(layer.FillColor, out fr, out fg, out fb);

        for (var y = 0; y < canvas; y++)
        {
            for (var x = 0; x < canvas; x++)
            {
                var dx = x + 0.5 - cx;
                var dy = y + 0.5 - cy;
                var lx = dx * cos + dy * sin;
                var ly = -dx * sin + dy * cos;
                if (hasFill && Inside(layer, lx, ly))
                    Blend(pixels, (y * canvas + x) * 4, fr, fg, fb, opacity);
                var cover = Coverage(layer, lx, ly, outline, antiAlias);
                if (cover == 0) continue;
                var useOutline = cover < 0;
                var alpha = Math.Clamp(Math.Abs(cover), 0, 1) * opacity;
                Blend(pixels, (y * canvas + x) * 4, useOutline ? or : r, useOutline ? og : g, useOutline ? ob : b, alpha);
            }
        }
    }

    /// <summary>Positive coverage is fill, negative is outline.</summary>
    private static double Coverage(CrosshairLayer layer, double x, double y, int outline, bool antiAlias)
    {
        var filled = Hit(layer, x, y, 0);
        if (filled) return antiAlias ? EdgeAlpha(layer, x, y) : 1;
        if (outline > 0 && Hit(layer, x, y, outline)) return antiAlias ? -EdgeAlpha(layer, x, y) : -1;
        return 0;
    }

    private static double EdgeAlpha(CrosshairLayer layer, double x, double y)
    {
        var inside = 0;
        foreach (var (ox, oy) in new[] { (-0.25, -0.25), (0.25, -0.25), (-0.25, 0.25), (0.25, 0.25) })
        {
            if (Hit(layer, x + ox, y + oy, 0)) inside++;
        }
        return inside == 0 ? 1 : inside / 4.0;
    }

    private static bool Inside(CrosshairLayer layer, double x, double y)
    {
        var length = Math.Clamp(layer.Length, 1, 256);
        var thickness = Math.Clamp(layer.Thickness, 1, 64);
        var inner = length / 2.0 - thickness;
        if (inner <= 0) return false;
        if (layer.Kind == LayerKind.Ring) return x * x + y * y <= inner * inner;
        return Math.Abs(x) < inner && Math.Abs(y) < inner;
    }

    private static bool Hit(CrosshairLayer layer, double x, double y, int grow)
    {
        var length = Math.Clamp(layer.Length, 1, 256);
        var thickness = Math.Clamp(layer.Thickness, 1, 64);
        var gap = Math.Clamp(layer.Gap, 0, 128);
        switch (layer.Kind)
        {
            case LayerKind.Dot:
            {
                var radius = length / 2.0 + grow;
                return x * x + y * y <= radius * radius;
            }
            case LayerKind.Ring:
            {
                var outer = length / 2.0 + grow;
                var inner = Math.Max(0, length / 2.0 - thickness - (grow > 0 ? 0 : 0));
                if (grow > 0) inner = Math.Max(0, length / 2.0 - thickness);
                var d = x * x + y * y;
                var hole = grow > 0 ? Math.Max(0, length / 2.0 - thickness - grow) : inner;
                return d <= outer * outer && d >= hole * hole;
            }
            case LayerKind.Box:
                return HitBox(x, y, length, thickness, gap, grow);
            case LayerKind.Image:
            {
                var side = Math.Clamp(16 * layer.ImageScale, 2, 128) + grow;
                return Math.Abs(x) <= side / 2 && Math.Abs(y) <= side / 2;
            }
            default:
                return HitArms(layer.Arms, x, y, length, thickness, gap, grow);
        }
    }

    private static bool HitArms(ArmMask arms, double x, double y, int length, int thickness, int gap, int grow)
    {
        var half = thickness / 2.0 + grow;
        var start = Math.Max(0, gap - grow);
        var end = gap + length + grow;
        if (arms.HasFlag(ArmMask.Right) && x >= start && x <= end && Math.Abs(y) <= half) return true;
        if (arms.HasFlag(ArmMask.Left) && x <= -start && x >= -end && Math.Abs(y) <= half) return true;
        if (arms.HasFlag(ArmMask.Bottom) && y >= start && y <= end && Math.Abs(x) <= half) return true;
        if (arms.HasFlag(ArmMask.Top) && y <= -start && y >= -end && Math.Abs(x) <= half) return true;
        return false;
    }

    private static bool HitBox(double x, double y, int length, int thickness, int gap, int grow)
    {
        var outer = length / 2.0 + grow;
        var inner = Math.Max(0, length / 2.0 - thickness - grow);
        var ax = Math.Abs(x);
        var ay = Math.Abs(y);
        if (ax > outer || ay > outer) return false;
        if (ax < inner && ay < inner) return false;
        var cut = gap / 2.0;
        if (ay >= inner && ax < cut) return false;
        if (ax >= inner && ay < cut) return false;
        return true;
    }

    private static void Blend(byte[] pixels, int i, byte r, byte g, byte b, double alpha)
    {
        var srcA = alpha;
        var dstA = pixels[i + 3] / 255.0;
        var outA = srcA + dstA * (1 - srcA);
        if (outA <= 0) return;
        pixels[i] = (byte)Math.Clamp((r * srcA + pixels[i] * dstA * (1 - srcA)) / outA, 0, 255);
        pixels[i + 1] = (byte)Math.Clamp((g * srcA + pixels[i + 1] * dstA * (1 - srcA)) / outA, 0, 255);
        pixels[i + 2] = (byte)Math.Clamp((b * srcA + pixels[i + 2] * dstA * (1 - srcA)) / outA, 0, 255);
        pixels[i + 3] = (byte)Math.Clamp(outA * 255, 0, 255);
    }

    private static bool TryParse(string? hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (hex == null || hex.Length != 7 || hex[0] != '#') return false;
        if (!byte.TryParse(hex.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out r)) return false;
        if (!byte.TryParse(hex.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out g)) return false;
        if (!byte.TryParse(hex.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out b)) return false;
        return true;
    }

    private static byte[] EncodePng(byte[] rgba, int width, int height)
    {
        var bgra = new byte[rgba.Length];
        for (var i = 0; i < rgba.Length; i += 4)
        {
            bgra[i] = rgba[i + 2];
            bgra[i + 1] = rgba[i + 1];
            bgra[i + 2] = rgba[i];
            bgra[i + 3] = rgba[i + 3];
        }

        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), bgra, width * 4, 0);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
