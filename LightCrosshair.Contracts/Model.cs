namespace LightCrosshair.Contracts;

// Colors are "#RRGGBB" strings. Transparency is expressed only through Opacity (0..1).
// Geometry is in physical screen pixels at the crosshair's monitor (pixel-perfect, no DPI scaling).

public enum LayerKind
{
    /// <summary>Up to four arms around the center. Arms + Rotation 45 = X; Arms without Top = T.</summary>
    Arms,
    /// <summary>Filled circle of diameter Length.</summary>
    Dot,
    /// <summary>Circle outline: outer diameter Length, stroke Thickness.</summary>
    Ring,
    /// <summary>Square outline: side Length, stroke Thickness. Gap > 0 cuts the middle of each side (corner brackets).</summary>
    Box,
    /// <summary>PNG from the images folder, centered, scaled by ImageScale.</summary>
    Image,
}

[Flags]
public enum ArmMask
{
    None = 0,
    Top = 1,
    Right = 2,
    Bottom = 4,
    Left = 8,
    All = Top | Right | Bottom | Left,
}

public sealed class CrosshairLayer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public LayerKind Kind { get; set; } = LayerKind.Arms;
    public bool Visible { get; set; } = true;

    public string Color { get; set; } = "#FFFFFF";
    public double Opacity { get; set; } = 1.0;

    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    /// <summary>Degrees, clockwise.</summary>
    public double Rotation { get; set; }

    /// <summary>Arms: arm length. Dot: diameter. Ring: outer diameter. Box: side. Range 1..256.</summary>
    public int Length { get; set; } = 8;
    /// <summary>Arms: arm width. Ring/Box: stroke. Ignored for Dot/Image. Range 1..64.</summary>
    public int Thickness { get; set; } = 2;
    /// <summary>Arms: distance from center to arm start. Box: cut in the middle of each side. Range 0..128.</summary>
    public int Gap { get; set; } = 4;
    public ArmMask Arms { get; set; } = ArmMask.All;

    /// <summary>Box and Ring only: interior fill "#RRGGBB" drawn beneath the stroke; null = unfilled. Uses the layer Opacity.</summary>
    public string? FillColor { get; set; }

    public bool Outline { get; set; }
    /// <summary>Range 1..8.</summary>
    public int OutlineThickness { get; set; } = 1;
    public string OutlineColor { get; set; } = "#000000";

    /// <summary>Image only: file name inside %AppData%\LightCrosshair\v2\images\.</summary>
    public string? ImageFile { get; set; }
    /// <summary>Image only. Range 0.1..4.</summary>
    public double ImageScale { get; set; } = 1.0;
}

public sealed class CrosshairDesign
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public bool Favorite { get; set; }
    public bool AntiAlias { get; set; } = true;
    /// <summary>Set when the design was created from a built-in preset.</summary>
    public string? SourcePresetId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Drawn in order: index 0 is the bottom layer. Max ContractInfo.MaxLayers.</summary>
    public List<CrosshairLayer> Layers { get; set; } = [];
}

public enum BindingMode
{
    Toggle,
    HoldToHide,
    HoldToShow,
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// Key: a System.Windows.Forms.Keys name ("X", "F5", "D1", "OemTilde") or one of
/// "MiddleClick", "RightClick", "Mouse4", "Mouse5", "WheelUp", "WheelDown".
/// Wheel keys support Toggle only. Empty Key = unbound.
/// </summary>
public sealed class InputBinding
{
    public string Key { get; set; } = "";
    public KeyModifiers Modifiers { get; set; }
    public BindingMode Mode { get; set; } = BindingMode.Toggle;
}

public sealed class HotkeySettings
{
    public InputBinding ToggleVisibility { get; set; } = new() { Key = "X", Modifiers = KeyModifiers.Alt };
    /// <summary>Cycles favorites, or all crosshairs when there are no favorites.</summary>
    public InputBinding NextCrosshair { get; set; } = new() { Key = "C", Modifiers = KeyModifiers.Alt };
    public InputBinding PreviousCrosshair { get; set; } = new() { Key = "V", Modifiers = KeyModifiers.Alt };
    /// <summary>Opens the UI, or closes it when already open.</summary>
    public InputBinding OpenSettings { get; set; } = new() { Key = "L", Modifiers = KeyModifiers.Alt };
}

public sealed class FpsOverlaySettings
{
    public bool Enabled { get; set; }
    /// <summary>Null = same monitor as the crosshair.</summary>
    public string? MonitorDeviceName { get; set; }
    /// <summary>Physical pixels from the monitor's top-left corner.</summary>
    public int OffsetX { get; set; } = 10;
    public int OffsetY { get; set; } = 10;
    /// <summary>Range 0.5..3.</summary>
    public double Scale { get; set; } = 1.0;
    public bool ShowFrametime { get; set; } = true;
    public bool Show1PercentLow { get; set; } = true;
    public string TextColor { get; set; } = "#FFFFFF";
    public string BackgroundColor { get; set; } = "#000000";
    public double BackgroundOpacity { get; set; } = 0.5;
}

/// <summary>Applied while the game is in the foreground, restored when it leaves.</summary>
public sealed class DisplayColorSettings
{
    /// <summary>Software gamma ramp values, 100 = neutral. Range 50..150.</summary>
    public int Gamma { get; set; } = 100;
    public int Contrast { get; set; } = 100;
    public int Brightness { get; set; } = 100;
    /// <summary>Software vibrance, 50 = neutral. Range 0..100.</summary>
    public int Vibrance { get; set; } = 50;
    /// <summary>NVIDIA Digital Vibrance 0..100; null = leave the driver value alone.</summary>
    public int? NvidiaDigitalVibrance { get; set; }
}

public sealed class GameProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    /// <summary>Lower-case executable file name, e.g. "cs2.exe". Unique across profiles.</summary>
    public string ProcessName { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string CrosshairId { get; set; } = "";
    /// <summary>Null = never configured. Values are kept even while DisplayColorsEnabled is false.</summary>
    public DisplayColorSettings? DisplayColors { get; set; }
    /// <summary>Apply DisplayColors for this game. False = keep the values but don't touch display colors.</summary>
    public bool DisplayColorsEnabled { get; set; } = true;
    /// <summary>NVIDIA per-app driver FPS cap, written to the driver on save. Null = no cap managed by LightCrosshair.</summary>
    public int? NvidiaFpsCap { get; set; }
    /// <summary>Null = follow the global FPS overlay setting.</summary>
    public bool? FpsOverlay { get; set; }
    /// <summary>Null = use the global ToggleVisibility binding.</summary>
    public InputBinding? VisibilityBinding { get; set; }
}

public sealed class AppSettings
{
    /// <summary>"auto" or one of ContractInfo.SupportedLanguages.</summary>
    public string Language { get; set; } = "auto";
    public string DefaultCrosshairId { get; set; } = "";
    public bool CrosshairVisible { get; set; } = true;
    /// <summary>When true the crosshair is hidden unless a configured game is in the foreground.</summary>
    public bool ShowOnlyInConfiguredGames { get; set; }
    public bool HideDuringScreenRecording { get; set; }
    /// <summary>Null = primary monitor.</summary>
    public string? MonitorDeviceName { get; set; }
    /// <summary>Global position adjustment in physical pixels from the monitor center.</summary>
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public HotkeySettings Hotkeys { get; set; } = new();
    public FpsOverlaySettings FpsOverlay { get; set; } = new();
    public bool SuggestKnownGames { get; set; } = true;
    /// <summary>Process names the user said "no" to.</summary>
    public List<string> DismissedSuggestions { get; set; } = [];
    public bool StartWithWindows { get; set; }
    public bool CheckForUpdates { get; set; } = true;
}

/// <summary>Root of %AppData%\LightCrosshair\v2\store.json. Core is the only writer.</summary>
public sealed class StoreDocument
{
    public int SchemaVersion { get; set; } = ContractInfo.StoreSchemaVersion;
    public AppSettings Settings { get; set; } = new();
    public List<CrosshairDesign> Crosshairs { get; set; } = [];
    public List<GameProfile> GameProfiles { get; set; } = [];
}

public sealed class HotkeyError
{
    /// <summary>"ToggleVisibility", "NextCrosshair", "PreviousCrosshair", "OpenSettings" or "GameProfile:{id}".</summary>
    public string Action { get; set; } = "";
    public InputBinding Binding { get; set; } = new();
    public string Message { get; set; } = "";
}

public sealed class GameSuggestion
{
    public string ProcessName { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public sealed class GpuCapabilities
{
    public bool SoftwareGamma { get; set; }
    public bool NvidiaDigitalVibrance { get; set; }
    public bool NvidiaFpsCap { get; set; }
    public bool AmdColor { get; set; }
}

public sealed class MonitorInfo
{
    public string DeviceName { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public bool IsPrimary { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int DpiScalePercent { get; set; } = 100;
}

/// <summary>Live state owned by Core, never persisted.</summary>
public sealed class RuntimeState
{
    public string CoreVersion { get; set; } = "";
    public string? ForegroundProcess { get; set; }
    public string? ActiveGameProfileId { get; set; }
    /// <summary>The crosshair currently drawn (game profile's, cycled, or default).</summary>
    public string ActiveCrosshairId { get; set; } = "";
    /// <summary>Effective visibility after settings, game rules, hold bindings and recording detection.</summary>
    public bool CrosshairShown { get; set; }
    public bool PreviewActive { get; set; }
    public List<HotkeyError> HotkeyErrors { get; set; } = [];
    public GameSuggestion? PendingSuggestion { get; set; }
    public GpuCapabilities Gpu { get; set; } = new();
    public List<MonitorInfo> Monitors { get; set; } = [];
}

public sealed class Snapshot
{
    public StoreDocument Store { get; set; } = new();
    public RuntimeState Runtime { get; set; } = new();
}

public sealed class PresetEntry
{
    public CrosshairDesign Design { get; set; } = new();
    /// <summary>Lower-case tags: shape ("dot", "cross", "t", "x", "circle", "box") and style ("minimal", "outlined", "high-visibility").</summary>
    public List<string> Tags { get; set; } = [];
}

public sealed class ProcessEntry
{
    public string ProcessName { get; set; } = "";
    public string? WindowTitle { get; set; }
    public string? ExePath { get; set; }
}

public sealed class ExportedImage
{
    public string FileName { get; set; } = "";
    public string PngBase64 { get; set; } = "";
}

/// <summary>File written by ExportCrosshairs (*.lcx.json).</summary>
public sealed class CrosshairExportFile
{
    public string Format { get; set; } = "lightcrosshair.crosshairs";
    public int Version { get; set; } = 1;
    public List<CrosshairDesign> Crosshairs { get; set; } = [];
    public List<ExportedImage> Images { get; set; } = [];
}
