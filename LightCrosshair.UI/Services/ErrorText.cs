using LightCrosshair.UI.Localization;

namespace LightCrosshair.UI.Services;

public static class ErrorText
{
    public const string Timeout = "timeout";
    public const string Disconnected = "disconnected";
    public const string CoreMissing = "core_missing";

    public static string For(string? code) => code switch
    {
        IpcErrorCodes.BadRequest => Loc.Instance["Error_BadRequest"],
        IpcErrorCodes.NotFound => Loc.Instance["Error_NotFound"],
        IpcErrorCodes.Validation => Loc.Instance["Error_Validation"],
        IpcErrorCodes.Unsupported => Loc.Instance["Error_Unsupported"],
        IpcErrorCodes.ProtocolMismatch => Loc.Instance["Error_ProtocolMismatch"],
        Timeout => Loc.Instance["Error_Timeout"],
        Disconnected => Loc.Instance["Error_Disconnected"],
        CoreMissing => Loc.Instance["Error_CoreMissing"],
        _ => Loc.Instance["Error_Internal"],
    };
}
