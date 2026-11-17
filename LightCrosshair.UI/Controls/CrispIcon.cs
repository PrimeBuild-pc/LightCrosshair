using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightCrosshair.UI.Controls;

/// <summary>Sidebar mark drawn from the ICO frame that matches the current DPI, one bitmap pixel per device pixel.</summary>
public sealed class CrispIcon : System.Windows.Controls.Image
{
    private static readonly int[] Frames = [20, 24, 32, 40];
    private static Dictionary<int, BitmapSource>? _frames;

    public CrispIcon()
    {
        Stretch = Stretch.Fill;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        Width = 20;
        Height = 20;
        Loaded += (_, _) => Apply(VisualTreeHelper.GetDpi(this).DpiScaleX);
    }

    public static int FrameForScale(double dpiScale)
    {
        var target = 20 * (dpiScale <= 0 ? 1 : dpiScale);
        var best = Frames[0];
        var bestDistance = double.MaxValue;
        foreach (var frame in Frames)
        {
            var distance = Math.Abs(frame - target);
            if (distance < bestDistance)
            {
                best = frame;
                bestDistance = distance;
            }
        }
        return best;
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Apply(newDpi.DpiScaleX);
    }

    internal void Apply(double dpiScale)
    {
        var frame = FrameForScale(dpiScale);
        var scale = dpiScale <= 0 ? 1 : dpiScale;
        Width = frame / scale;
        Height = frame / scale;
        Source = Frame(frame);
    }

    private static BitmapSource Frame(int size)
    {
        _frames ??= LoadFrames();
        if (_frames.TryGetValue(size, out var found)) return found;
        var drawn = Draw(size);
        _frames[size] = drawn;
        return drawn;
    }

    private static Dictionary<int, BitmapSource> LoadFrames()
    {
        var frames = new Dictionary<int, BitmapSource>();
        try
        {
            var decoder = BitmapDecoder.Create(
                new Uri("pack://application:,,,/assets/icon.ico", UriKind.Absolute),
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            foreach (var frame in decoder.Frames)
            {
                if (!Frames.Contains(frame.PixelWidth) || frame.PixelWidth != frame.PixelHeight) continue;
                BitmapSource copy = frame.Format == PixelFormats.Bgra32 ? frame : new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                if (copy.CanFreeze) copy.Freeze();
                if (!IsHardEdged(copy)) continue;
                frames[frame.PixelWidth] = copy;
            }
        }
        catch (Exception)
        {
            // A decoder miss falls through to the frames drawn like scripts/assets/make-icon.py.
        }
        return frames;
    }

    private static bool IsHardEdged(BitmapSource source)
    {
        var pixels = new byte[source.PixelWidth * source.PixelHeight * 4];
        source.CopyPixels(pixels, source.PixelWidth * 4, 0);
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] is not 0 and not 255) return false;
        }
        return true;
    }

    internal static WriteableBitmap Draw(int n)
    {
        var thickness = Math.Max(2, 2 * (int)Math.Round(n / 24.0, MidpointRounding.ToEven));
        var gap = Math.Max(1, (int)Math.Round(n * 8 / 96.0, MidpointRounding.ToEven));
        var margin = (int)Math.Round(n * 10 / 96.0, MidpointRounding.ToEven);
        var origin = n / 2 - thickness / 2;
        var bitmap = new WriteableBitmap(n, n, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[n * n * 4];
        void Fill(int x0, int y0, int x1, int y1)
        {
            for (var y = y0; y < y1; y++)
            {
                for (var x = x0; x < x1; x++)
                {
                    if (x < 0 || y < 0 || x >= n || y >= n) continue;
                    var i = (y * n + x) * 4;
                    pixels[i] = 42;
                    pixels[i + 1] = 42;
                    pixels[i + 2] = 255;
                    pixels[i + 3] = 255;
                }
            }
        }
        Fill(origin, origin, origin + thickness, origin + thickness);
        Fill(origin, margin, origin + thickness, origin - gap);
        Fill(origin, origin + thickness + gap, origin + thickness, n - margin);
        Fill(margin, origin, origin - gap, origin + thickness);
        Fill(origin + thickness + gap, origin, n - margin, origin + thickness);
        bitmap.WritePixels(new Int32Rect(0, 0, n, n), pixels, n * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }
}
