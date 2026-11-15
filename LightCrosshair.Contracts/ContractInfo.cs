namespace LightCrosshair.Contracts;

public static class ContractInfo
{
    /// <summary>Bumped on any breaking change to IPC messages or payloads.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>Schema of %AppData%\LightCrosshair\v2\store.json. v1.x profiles were schema 1-5.</summary>
    public const int StoreSchemaVersion = 6;

    public const int MaxLayers = 8;
    public const int MaxShareCodeLength = 2048;
    public const int MaxImageSidePx = 512;
    public const long MaxImageBytes = 2 * 1024 * 1024;

    public const string CoreExeName = "LightCrosshair.exe";
    public const string UiExeName = "LightCrosshair.UI.exe";

    /// <summary>Share code prefix: "LC2:" + base64url(deflate(utf8 json of CrosshairDesign)).</summary>
    public const string ShareCodePrefix = "LC2:";

    /// <summary>"auto" follows the Windows display language, falling back to "en".</summary>
    public static readonly string[] SupportedLanguages = ["en", "it", "es", "zh-Hans", "ru", "ja"];

    /// <summary>One pipe per interactive Windows session; created with PipeOptions.CurrentUserOnly.</summary>
    public static string PipeName(int windowsSessionId) => $"LightCrosshair.v2.{windowsSessionId}";
}
