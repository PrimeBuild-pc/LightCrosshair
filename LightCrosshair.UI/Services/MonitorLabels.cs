using LightCrosshair.UI.Localization;

namespace LightCrosshair.UI.Services;

public static class MonitorLabels
{
    public static int Number(string deviceName, int index)
    {
        var digits = new string((deviceName ?? "").Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out var number) && number > 0 ? number : index + 1;
    }

    public static string Format(string? friendlyName, int number, bool primary)
    {
        var name = string.IsNullOrWhiteSpace(friendlyName) ? Loc.Instance["Settings_Display"] : friendlyName.Trim();
        var label = Loc.Instance.Format("Settings_MonitorName", name, number);
        return primary ? label + " · " + Loc.Instance["Settings_Primary"] : label;
    }
}
