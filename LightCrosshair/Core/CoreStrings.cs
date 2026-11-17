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

/// <summary>Start with Windows: a Run key for the ZIP/installer build, the manifest StartupTask when packaged (MSIX).</summary>
internal static class StartupRegistration
{
    internal const string TaskId = "LightCrosshairStartup";

    /// <summary>Applies the request and returns whether Windows will start the app at sign-in.</summary>
    public static async Task<bool> ApplyAsync(bool enabled)
    {
        if (!AppPackage.IsPackaged)
        {
            using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (enabled) run.SetValue("LightCrosshair", $"\"{Environment.ProcessPath}\"");
            else run.DeleteValue("LightCrosshair", false);
            return enabled;
        }
        // Packaged HKCU writes are virtualized, so a Run key would never be seen by Windows.
        return IsOn(await StartupTaskAbi.RunAsync(TaskId, enabled ? StartupTaskAbi.Action.Enable : StartupTaskAbi.Action.Disable));
    }

    /// <summary>The real StartupTask state when packaged (the user can change it in Task Manager); null otherwise.</summary>
    public static async Task<bool?> PackagedStateAsync()
    {
        if (!AppPackage.IsPackaged) return null;
        return IsOn(await StartupTaskAbi.RunAsync(TaskId, StartupTaskAbi.Action.Read));
    }

    /// <summary>StartupTaskState: Disabled 0, DisabledByUser 1, Enabled 2, DisabledByPolicy 3, EnabledByPolicy 4.</summary>
    internal static bool IsOn(int state) => state is 2 or 4;

    /// <summary>True when the stored switch must change to match Windows.</summary>
    internal static bool NeedsSync(bool stored, bool? actual) => actual is bool on && on != stored;
}

internal static class AppPackage
{
    internal static readonly bool IsPackaged = Detect();

    private static bool Detect()
    {
        int length = 0;
        return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
    }

    private const int AppModelErrorNoPackage = 15700;
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int length, char[]? name);
}
