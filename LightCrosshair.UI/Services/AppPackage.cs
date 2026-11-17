using System.Runtime.InteropServices;

namespace LightCrosshair.UI.Services;

/// <summary>True when running from the MSIX (Microsoft Store) package. Core launches the UI from the same package.</summary>
public static class AppPackage
{
    public static readonly bool IsPackaged = Detect();

    private static bool Detect()
    {
        int length = 0;
        return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
    }

    private const int AppModelErrorNoPackage = 15700;
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int length, char[]? name);
}
