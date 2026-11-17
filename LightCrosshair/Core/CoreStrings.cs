using System.Globalization;
using System.Resources;
using Microsoft.Win32;

namespace LightCrosshair.Core;

internal static class CoreStrings
{
    private static readonly ResourceManager Resources = new("LightCrosshair.Resources.Strings", typeof(CoreStrings).Assembly);
    public static string Get(string language, string key, params object[] args)
    {
        CultureInfo culture = language == "auto" ? CultureInfo.CurrentUICulture : CultureInfo.GetCultureInfo(language);
        string value = Resources.GetString(key, culture) ?? key;
        return args.Length == 0 ? value : string.Format(culture, value, args);
    }
}

internal static class StartupRegistration
{
    public static void Apply(bool enabled)
    {
        using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) run.SetValue("LightCrosshair", $"\"{Environment.ProcessPath}\"");
        else run.DeleteValue("LightCrosshair", false);
    }
}
