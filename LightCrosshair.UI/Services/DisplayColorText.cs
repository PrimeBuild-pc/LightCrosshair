using LightCrosshair.UI.Localization;

namespace LightCrosshair.UI.Services;

public static class DisplayColorText
{
    public static string For(string? code) => code switch
    {
        "apply_failed" => Loc.Instance["Games_ColorApplyFailed"],
        "restore_failed" => Loc.Instance["Games_ColorRestoreFailed"],
        "unsupported" => Loc.Instance["Games_ColorUnsupported"],
        _ => "",
    };
}
