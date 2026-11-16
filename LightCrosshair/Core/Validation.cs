using System.Text.Json;
using System.Text.RegularExpressions;
using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal sealed class CoreException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

internal static partial class Validation
{
    public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, ContractJson.Options), ContractJson.Options)!;
    public static CoreException Invalid(string message) => new(IpcErrorCodes.Validation, message);
    public static string Name(string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name)) throw Invalid("A name is required.");
        return name[..Math.Min(48, name.Length)];
    }
    public static string Id(string? id) => string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") :
        id.Length <= 64 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? id : throw Invalid("Invalid id.");
    public static string Color(string? color) => color is not null && ColorPattern().IsMatch(color) ? color.ToUpperInvariant() : throw Invalid("Invalid color.");
    public static double Range(double value, double min, double max) => double.IsFinite(value) ? Math.Clamp(value, min, max) : throw Invalid("Non-finite number.");
    public static string ProcessName(string? process)
    {
        process = process?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(process) || process.Length > 240 || !process.EndsWith(".exe", StringComparison.Ordinal) ||
            process.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || process.Contains('/') || process.Contains('\\') || process == ".exe")
            throw Invalid("Invalid process name.");
        return process;
    }
    public static CrosshairDesign Design(CrosshairDesign? design, string imagesDirectory)
    {
        if (design is null || design.Layers is null || design.Layers.Count > ContractInfo.MaxLayers) throw Invalid("Invalid layer count.");
        design = Copy(design);
        design.Id = Id(design.Id); design.Name = Name(design.Name);
        if (design.SourcePresetId is not null) design.SourcePresetId = Id(design.SourcePresetId);
        var ids = new HashSet<string>();
        foreach (var layer in design.Layers)
        {
            if (layer is null || !Enum.IsDefined(layer.Kind) || ((int)layer.Arms & ~15) != 0) throw Invalid("Invalid layer.");
            layer.Id = Id(layer.Id);
            if (!ids.Add(layer.Id)) throw Invalid("Duplicate layer id.");
            layer.Name = (layer.Name ?? "").Trim();
            layer.Name = layer.Name[..Math.Min(48, layer.Name.Length)];
            if (layer.FillColor is not null) layer.FillColor = Color(layer.FillColor);
            layer.Color = Color(layer.Color); layer.OutlineColor = Color(layer.OutlineColor);
            layer.Opacity = Range(layer.Opacity, 0, 1); layer.Rotation = Range(layer.Rotation, -36000, 36000) % 360;
            layer.ImageScale = Range(layer.ImageScale, .1, 4);
            layer.Length = Math.Clamp(layer.Length, 1, 256); layer.Thickness = Math.Clamp(layer.Thickness, 1, 64);
            layer.Gap = Math.Clamp(layer.Gap, 0, 128); layer.OutlineThickness = Math.Clamp(layer.OutlineThickness, 1, 8);
            layer.OffsetX = Math.Clamp(layer.OffsetX, -1024, 1024); layer.OffsetY = Math.Clamp(layer.OffsetY, -1024, 1024);
            if (layer.Kind == LayerKind.Image && (layer.ImageFile is null || Path.GetFileName(layer.ImageFile) != layer.ImageFile ||
                !Regex.IsMatch(layer.ImageFile, "^[a-f0-9]{32}\\.png$", RegexOptions.CultureInvariant) || !File.Exists(Path.Combine(imagesDirectory, layer.ImageFile))))
                throw Invalid("Image not found.");
        }
        return design;
    }
    public static InputBinding Binding(InputBinding? binding, bool visibility = true)
    {
        if (binding is null || !Enum.IsDefined(binding.Mode) || ((int)binding.Modifiers & ~15) != 0) throw Invalid("Invalid binding.");
        binding = Copy(binding);
        binding.Key = binding.Key?.Trim() ?? "";
        if (binding.Key.Length == 0) return binding;
        bool mouse = Enum.TryParse<BindingInput>(binding.Key, false, out var input) && input != BindingInput.Keyboard;
        if (!mouse && (!Enum.TryParse<Keys>(binding.Key, false, out var key) || !Enum.IsDefined(key) || (int)key <= 6 || (int)key >= 255 || VisibilityBinding.IsModifier((int)key)))
            throw Invalid("Invalid binding key.");
        if ((binding.Key is "WheelUp" or "WheelDown" || !visibility) && binding.Mode != Contracts.BindingMode.Toggle) throw Invalid("Binding must toggle.");
        return binding;
    }
    public static AppSettings Settings(AppSettings? settings, IReadOnlyList<CrosshairDesign> designs)
    {
        if (settings?.Hotkeys is null || settings.FpsOverlay is null || settings.DismissedSuggestions is null ||
            !designs.Any(d => d.Id == settings.DefaultCrosshairId)) throw Invalid("Invalid settings.");
        settings = Copy(settings);
        if (settings.Language != "auto" && !ContractInfo.SupportedLanguages.Contains(settings.Language)) throw Invalid("Invalid language.");
        settings.OffsetX = Math.Clamp(settings.OffsetX, -16384, 16384); settings.OffsetY = Math.Clamp(settings.OffsetY, -16384, 16384);
        settings.Hotkeys.ToggleVisibility = Binding(settings.Hotkeys.ToggleVisibility);
        settings.Hotkeys.NextCrosshair = Binding(settings.Hotkeys.NextCrosshair, false);
        settings.Hotkeys.PreviousCrosshair = Binding(settings.Hotkeys.PreviousCrosshair, false);
        settings.Hotkeys.OpenSettings = Binding(settings.Hotkeys.OpenSettings, false);
        var fps = settings.FpsOverlay;
        fps.Scale = Range(fps.Scale, .5, 3); fps.BackgroundOpacity = Range(fps.BackgroundOpacity, 0, 1);
        fps.TextColor = Color(fps.TextColor); fps.BackgroundColor = Color(fps.BackgroundColor);
        fps.OffsetX = Math.Clamp(fps.OffsetX, 0, 16384); fps.OffsetY = Math.Clamp(fps.OffsetY, 0, 16384);
        settings.DismissedSuggestions = settings.DismissedSuggestions.Select(ProcessName).Distinct().Take(1000).ToList();
        if (settings.MonitorDeviceName?.Length > 128 || fps.MonitorDeviceName?.Length > 128) throw Invalid("Invalid monitor.");
        return settings;
    }
    public static GameProfile Game(GameProfile? game, StoreDocument store)
    {
        if (game is null || !store.Crosshairs.Any(d => d.Id == game.CrosshairId)) throw Invalid("Crosshair not found.");
        game = Copy(game); game.Id = Id(game.Id); game.Name = Name(game.Name); game.ProcessName = ProcessName(game.ProcessName);
        if (store.GameProfiles.Any(g => g.Id != game.Id && g.ProcessName == game.ProcessName)) throw Invalid("Duplicate process.");
        if (game.VisibilityBinding is not null) game.VisibilityBinding = Binding(game.VisibilityBinding);
        if (game.NvidiaFpsCap.HasValue) game.NvidiaFpsCap = Math.Clamp(game.NvidiaFpsCap.Value, 20, 1000);
        if (game.DisplayColors is { } c)
        {
            c.Gamma = Math.Clamp(c.Gamma, 50, 150); c.Contrast = Math.Clamp(c.Contrast, 50, 150);
            c.Brightness = Math.Clamp(c.Brightness, 50, 150); c.Vibrance = Math.Clamp(c.Vibrance, 0, 100);
            if (c.NvidiaDigitalVibrance.HasValue) c.NvidiaDigitalVibrance = Math.Clamp(c.NvidiaDigitalVibrance.Value, 0, 100);
        }
        return game;
    }
    public static StoreDocument Store(StoreDocument? store, string imagesDirectory)
    {
        if (store?.Crosshairs is null || store.GameProfiles is null || store.SchemaVersion != ContractInfo.StoreSchemaVersion ||
            store.Crosshairs.Count is 0 or > 10000 || store.GameProfiles.Count > 10000) throw Invalid("Invalid store.");
        store.Crosshairs = store.Crosshairs.Select(d => Design(d, imagesDirectory)).ToList();
        if (store.Crosshairs.Select(d => d.Id).Distinct().Count() != store.Crosshairs.Count ||
            store.GameProfiles.Select(g => g.Id).Distinct().Count() != store.GameProfiles.Count) throw Invalid("Duplicate id.");
        store.Settings = Settings(store.Settings, store.Crosshairs);
        store.GameProfiles = store.GameProfiles.Select(g => Game(g, store)).ToList();
        if (store.GameProfiles.Select(g => g.ProcessName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != store.GameProfiles.Count) throw Invalid("Duplicate process.");
        return store;
    }
    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant)] private static partial Regex ColorPattern();
}
