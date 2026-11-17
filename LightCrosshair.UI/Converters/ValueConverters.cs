using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LightCrosshair.UI.Localization;

namespace LightCrosshair.UI.Converters;

public sealed class BoolToVisConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var on = value is true;
        if (Invert) on = !on;
        return on ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible ^ Invert;
}

public sealed class NullToVisConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var on = value switch
        {
            null => false,
            string text => text.Length > 0,
            _ => true,
        };
        if (Invert) on = !on;
        return on ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class KeyToTextConverter : IMultiValueConverter
{
    public static readonly KeyToTextConverter Instance = new();

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = values.Length > 1 ? values[1] as string : null;
        return Loc.Instance[key ?? ""];
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class KeyTextSingleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Loc.Instance[value as string ?? ""];

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class EqualsParameterConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    // Radio segments: the checked one writes its parameter back; the ones being unchecked write nothing.
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter != null ? parameter : Binding.DoNothing;
}

public sealed class Base64ImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || text.Length == 0) return null;
        try
        {
            var bytes = System.Convert.FromBase64String(text);
            var image = new BitmapImage();
            using var stream = new System.IO.MemoryStream(bytes);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            if (parameter != null && int.TryParse(parameter.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var edge) && edge > 0)
                image.DecodePixelWidth = edge;
            image.EndInit();
            image.Freeze();
            if ("screen".Equals(parameter)) return AtScreenDpi(image);
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // PNG stores DPI as whole pixels per meter (96 reads back as 95.987); retag exactly so
    // Stretch="None" draws one bitmap pixel per screen pixel.
    private static BitmapSource AtScreenDpi(BitmapSource image)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Pbgra32, null, 0);
        var pixels = new int[converted.PixelWidth * converted.PixelHeight];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        var dpi = 96 * LightCrosshair.UI.Services.UiScale.Dpi;
        var exact = BitmapSource.Create(converted.PixelWidth, converted.PixelHeight, dpi, dpi, PixelFormats.Pbgra32, null, pixels, converted.PixelWidth * 4);
        exact.Freeze();
        return exact;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class HexBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value as string ?? "#0E0F11")!);
            brush.Freeze();
            return brush;
        }
        catch (Exception)
        {
            return Brushes.Transparent;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class StatusBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var state = value?.ToString();
        var key = state switch
        {
            "Connected" => "AccentBrush",
            "Reconnecting" or "Connecting" => "WarningBrush",
            _ => "DangerBrush",
        };
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
