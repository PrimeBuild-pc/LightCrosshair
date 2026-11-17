using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightCrosshair.UI.Services;

/// <summary>Screen scale of the main window (1 = 100%). Set by MainWindow on load and DPI changes.</summary>
public static class UiScale
{
    public static double Dpi { get; set; } = 1;
}

/// <summary>
/// Library thumbnails at the overlay's real pixels: the crosshair is cropped 1:1 from a transparent
/// render into a box the size of the card area in screen pixels, and the PNG carries that screen DPI
/// so WPF draws it without resampling. Squeezing a 96 px render into 72 px dropped every fourth row
/// and column, so arms looked uneven. Only designs larger than the box are reduced, by a whole factor
/// with box averaging.
/// </summary>
public static class PixelThumb
{
    public static string Fit(string pngBase64, int box, double dpi)
    {
        var decoder = BitmapDecoder.Create(new MemoryStream(Convert.FromBase64String(pngBase64)), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var source = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Pbgra32, null, 0);
        int width = source.PixelWidth, height = source.PixelHeight;
        var pixels = new uint[width * height];
        source.CopyPixels(pixels, width * 4, 0);
        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (pixels[y * width + x] >> 24 != 0) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
        var output = new uint[box * box];
        if (maxX >= 0)
        {
            int content = Math.Max(maxX - minX + 1, maxY - minY + 1);
            int factor = Math.Max(1, (int)Math.Ceiling(content / (double)Math.Max(1, box - 2)));
            int span = box * factor;
            // Center the visible pixels (not the canvas), so odd and even shapes both sit in the middle.
            int x0 = (minX + maxX + 1 - span) / 2, y0 = (minY + maxY + 1 - span) / 2;
            for (int y = 0; y < box; y++)
                for (int x = 0; x < box; x++)
                {
                    long a = 0, r = 0, g = 0, b = 0;
                    for (int dy = 0; dy < factor; dy++)
                        for (int dx = 0; dx < factor; dx++)
                        {
                            int sx = x0 + x * factor + dx, sy = y0 + y * factor + dy;
                            if (sx < 0 || sy < 0 || sx >= width || sy >= height) continue;
                            uint p = pixels[sy * width + sx];
                            a += p >> 24; r += (p >> 16) & 255; g += (p >> 8) & 255; b += p & 255;
                        }
                    int n = factor * factor;
                    output[y * box + x] = (uint)(a / n) << 24 | (uint)(r / n) << 16 | (uint)(g / n) << 8 | (uint)(b / n);
                }
        }
        var bitmap = BitmapSource.Create(box, box, 96 * dpi, 96 * dpi, PixelFormats.Pbgra32, null, output, box * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return Convert.ToBase64String(stream.ToArray());
    }
}
