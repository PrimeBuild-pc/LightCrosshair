using System.Windows.Data;
using System.Windows.Markup;

namespace LightCrosshair.UI.Localization;

/// <summary>Markup extension that stays live across language changes.</summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }

    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(Loc.Version))
        {
            Source = Loc.Instance,
            Mode = System.Windows.Data.BindingMode.OneWay,
            Converter = TrConverter.Instance,
            ConverterParameter = Key,
        };
        return binding.ProvideValue(serviceProvider);
    }
}

public sealed class TrConverter : IValueConverter
{
    public static readonly TrConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => Loc.Instance[parameter as string ?? ""];

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
