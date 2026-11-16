using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace LightCrosshair.UI.Localization;

/// <summary>
/// Bindable string source. Bindings listen to <see cref="Version"/> so a language
/// change refreshes every string without restarting.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private readonly ResourceManager _resources = new("LightCrosshair.UI.Resources.Strings", typeof(Loc).Assembly);
    private CultureInfo _culture = CultureInfo.GetCultureInfo("en");
    private string _language = "en";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Changed;

    public int Version { get; private set; }

    public string Language => _language;

    public string this[string key]
    {
        get
        {
            if (string.IsNullOrEmpty(key)) return "";
            return _resources.GetString(key, _culture) ?? key;
        }
    }

    public string Format(string key, params object[] args) => string.Format(_culture, this[key], args);

    public void Apply(string? language)
    {
        var requested = string.IsNullOrWhiteSpace(language) ? "auto" : language;
        var culture = Resolve(requested);
        if (requested == _language && culture.Name == _culture.Name) return;
        _language = requested;
        _culture = culture;
        Version++;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public static CultureInfo Resolve(string language)
    {
        if (!string.Equals(language, "auto", StringComparison.OrdinalIgnoreCase))
        {
            try { return CultureInfo.GetCultureInfo(language); }
            catch (CultureNotFoundException) { return CultureInfo.GetCultureInfo("en"); }
        }

        var name = CultureInfo.CurrentUICulture.Name;
        if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return CultureInfo.GetCultureInfo("zh-Hans");
        foreach (var supported in ContractInfo.SupportedLanguages)
        {
            if (name.Equals(supported, StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(supported + "-", StringComparison.OrdinalIgnoreCase))
                return CultureInfo.GetCultureInfo(supported);
        }
        return CultureInfo.GetCultureInfo("en");
    }
}
