using System.Text.Json;
using System.Text.Json.Serialization;
using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal static class LegacyMigration
{
    private static readonly JsonSerializerOptions LegacyJson = new() { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip, Converters = { new JsonStringEnumConverter() } };
    public static StoreDocument Defaults()
    {
        var design = new CrosshairDesign { Name = "Classic Cross", AntiAlias = false, Layers = [new() { Name = "Arms", Color = "#00FFFF" }] };
        return new() { Crosshairs = [design], Settings = new() { DefaultCrosshairId = design.Id } };
    }
    public static async Task<StoreDocument> LoadAsync(string legacyDirectory, string imagesDirectory)
    {
        var store = Defaults();
        var profiles = await ProfileStore.LoadAsync(Path.Combine(legacyDirectory, "profiles.json")).ConfigureAwait(false);
        var current = await Read<CurrentStateSnapshot>("current_state.json").ConfigureAwait(false);
        var prefs = await Read<AppPreferences>("prefs.json").ConfigureAwait(false);
        CrosshairConfig? config = null;
        string configPath = Path.Combine(legacyDirectory, "crosshair_settings.json");
        if (File.Exists(configPath))
        {
            try { config = CrosshairConfig.DeserializeSettings(await File.ReadAllTextAsync(configPath).ConfigureAwait(false)); }
            catch (JsonException) { Program.WriteLog("Legacy settings invalid"); }
        }
        if (profiles.Count > 0)
        {
            store.Crosshairs = profiles.Select(Convert).ToList();
            foreach (var profile in profiles.Where(p => !string.IsNullOrWhiteSpace(p.DisplayTargetProcessName)))
            {
                try
                {
                    string process = profile.DisplayTargetProcessName.Trim().ToLowerInvariant();
                    if (!process.EndsWith(".exe", StringComparison.Ordinal)) process += ".exe";
                    if (store.GameProfiles.Any(g => g.ProcessName == process)) continue;
                    store.GameProfiles.Add(Validation.Game(new()
                    {
                        Name = profile.Name, ProcessName = process, CrosshairId = store.Crosshairs[profiles.IndexOf(profile)].Id,
                        VisibilityBinding = profile.VisibilityBinding is { IsValid: true } b ? Binding(b) : null,
                        DisplayColors = profile.HasDisplayColorProfile && profile.DisplayEnableGammaOverride ? new()
                        { Gamma = profile.DisplayGammaValue, Contrast = profile.DisplayContrastValue, Brightness = profile.DisplayBrightnessValue, Vibrance = profile.DisplayVibranceValue } : null
                    }, store));
                }
                catch (CoreException) { Program.WriteLog("Legacy game profile invalid"); }
            }
            store.Settings.DefaultCrosshairId = store.Crosshairs.FirstOrDefault(d => d.Id == prefs?.LastProfileId)?.Id ?? store.Crosshairs[0].Id;
        }
        else if (config is not null)
        {
            store.Crosshairs = [Convert(new() { Name = "Migrated crosshair", Shape = config.CrosshairStyle, EnumShape = ShapeNormalizer.ToEnum(config.CrosshairStyle), Size = config.CrosshairSize, Thickness = config.CrosshairThickness, OuterColor = config.CrosshairColor, AntiAlias = config.AntiAlias })];
            store.Settings.DefaultCrosshairId = store.Crosshairs[0].Id;
        }
        if (current?.CurrentProfile is { } active)
        {
            var design = Convert(active);
            string source = string.IsNullOrWhiteSpace(active.SourceProfileId) ? active.Id : active.SourceProfileId;
            int index = store.Crosshairs.FindIndex(d => d.Id == source);
            if (index >= 0) { design.Id = source; store.Crosshairs[index] = design; }
            else store.Crosshairs.Add(design);
            store.Settings.DefaultCrosshairId = design.Id;
            store.Settings.HideDuringScreenRecording = active.HideDuringScreenRecording;
        }
        if (config is not null)
        {
            var s = store.Settings;
            s.CrosshairVisible = prefs?.OverlayVisible ?? config.Visible;
            s.MonitorDeviceName = CrosshairConfig.ResolveMonitorDeviceName(config.CrosshairMonitorDeviceName, config.OverlayMonitorDeviceName);
            s.Hotkeys.ToggleVisibility = Binding(config.ResolveVisibilityBinding());
            s.Hotkeys.NextCrosshair = Key(config.CycleProfileHotkeyKey, config.CycleProfileHotkeyUseAlt, config.CycleProfileHotkeyUseControl, config.CycleProfileHotkeyUseShift, config.CycleProfileHotkeyUseWin);
            s.Hotkeys.PreviousCrosshair = Key(config.CycleProfilePrevHotkeyKey, config.CycleProfilePrevHotkeyUseAlt, config.CycleProfilePrevHotkeyUseControl, config.CycleProfilePrevHotkeyUseShift, config.CycleProfilePrevHotkeyUseWin);
            s.Hotkeys.OpenSettings = Key(config.SettingsWindowHotkeyKey, config.SettingsWindowHotkeyUseAlt, config.SettingsWindowHotkeyUseControl, config.SettingsWindowHotkeyUseShift, config.SettingsWindowHotkeyUseWin);
            s.FpsOverlay = new()
            {
                Enabled = config.EnableFpsOverlay, MonitorDeviceName = CrosshairConfig.ResolveMonitorDeviceName(config.FpsOverlayMonitorDeviceName, config.OverlayMonitorDeviceName),
                OffsetX = config.FpsOverlayX, OffsetY = config.FpsOverlayY, Scale = config.FpsOverlayScale / 100.0,
                ShowFrametime = config.ShowFrameTime, Show1PercentLow = config.Show1PercentLows,
                TextColor = LegacyColor(config.FpsOverlayColorSerialized, "#FFFFFF"), BackgroundColor = LegacyColor(config.FpsOverlayBgColorSerialized, "#000000")
            };
        }
        // Before migration, preserve every source verbatim in v2; never write any v1 file.
        string backup = Path.Combine(Path.GetDirectoryName(imagesDirectory)!, "backups", "migration");
        foreach (string name in new[] { "profiles.json", "crosshair_settings.json", "prefs.json", "current_state.json" })
        {
            string source = Path.Combine(legacyDirectory, name);
            if (!File.Exists(source)) continue;
            Directory.CreateDirectory(backup);
            if (!File.Exists(Path.Combine(backup, name))) File.Copy(source, Path.Combine(backup, name), false);
        }
        return Validation.Store(store, imagesDirectory);

        async Task<T?> Read<T>(string name)
        {
            string path = Path.Combine(legacyDirectory, name);
            if (!File.Exists(path)) return default;
            try { return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path).ConfigureAwait(false), LegacyJson); }
            catch (JsonException) { Program.WriteLog("Legacy file invalid"); return default; }
        }
    }
    public static InputBinding Binding(VisibilityBinding binding) => new()
    { Key = binding.Input == BindingInput.Keyboard ? ((Keys)binding.Key).ToString() : binding.Input.ToString(), Modifiers = (KeyModifiers)binding.Modifiers, Mode = (Contracts.BindingMode)binding.Mode };
    private static InputBinding Key(Keys key, bool alt, bool ctrl, bool shift, bool win) => new() { Key = key == Keys.None ? "" : key.ToString(), Modifiers = (KeyModifiers)((alt ? 1 : 0) | (ctrl ? 2 : 0) | (shift ? 4 : 0) | (win ? 8 : 0)) };
    private static string LegacyColor(string value, string fallback)
    {
        var p = value.Split(',');
        return p.Length == 3 && p.All(x => byte.TryParse(x, out _)) ? $"#{byte.Parse(p[0]):X2}{byte.Parse(p[1]):X2}{byte.Parse(p[2]):X2}" : fallback;
    }
    public static CrosshairDesign Convert(CrosshairProfile p)
    {
        var design = new CrosshairDesign { Id = Validation.Id(p.Id), Name = string.IsNullOrWhiteSpace(p.Name) ? "Migrated crosshair" : Validation.Name(p.Name), AntiAlias = p.AntiAlias };
        string shape = p.Shape;
        if (shape is "CircleDot" or "CircleCross" or "CirclePlus" or "CircleX" or "CrossDot")
        {
            Add(shape == "CrossDot" ? "Cross" : "Circle", p.Size, p.Thickness, p.GapSize, p.EdgeColor.A > 0 ? p.EdgeColor : p.OuterColor, false);
            Add(shape == "CrossDot" ? "Dot" : shape[6..].Replace("Plus", "Cross"), p.InnerSize, p.InnerThickness, p.InnerGapSize, p.InnerShapeColor, false);
        }
        else
        {
            shape = p.EnumShape == CrosshairShape.Custom ? "Cross" : p.EnumShape.ToString();
            Add(shape, p.Size, p.Thickness, p.GapSize, p.OuterColor.A > 0 ? p.OuterColor : p.InnerColor, p.OutlineEnabled || shape is "CrossOutlined" or "CircleOutlined" or "GapCross");
        }
        return design;
        void Add(string kind, int size, int thickness, int gap, Color color, bool outline)
        {
            bool arms = kind is "Cross" or "CrossOutlined" or "GapCross" or "T" or "X";
            int radius = Math.Max(1, (int)Math.Round(size / 2.0));
            gap = Math.Clamp(gap, 0, Math.Max(0, radius - 1));
            design.Layers.Add(new()
            {
                Name = kind, Kind = kind switch { "Dot" => LayerKind.Dot, "Circle" or "CircleOutlined" => LayerKind.Ring, "Box" => LayerKind.Box, _ => LayerKind.Arms },
                Length = Math.Clamp(arms ? radius - gap : size, 1, 256), Thickness = Math.Clamp(p.EdgeThickness > 0 && !arms ? p.EdgeThickness : thickness, 1, 64),
                Gap = arms ? gap : 0, Arms = kind == "T" ? ArmMask.Left | ArmMask.Right | ArmMask.Bottom : ArmMask.All,
                Rotation = kind == "X" ? 45 : 0, Color = $"#{color.R:X2}{color.G:X2}{color.B:X2}", Opacity = color.A / 255.0,
                Outline = outline, OutlineThickness = Math.Clamp(thickness / 2, 1, 8), OutlineColor = $"#{p.EdgeColor.R:X2}{p.EdgeColor.G:X2}{p.EdgeColor.B:X2}"
            });
            if (kind == "Box" && p.FillColor.A > 0)
            {
                // CONTRACT-REQUEST: Filled boxes. A stack of inset box strokes preserves the fill without a new layer kind.
                design.Layers.Insert(design.Layers.Count - 1, new() { Name = "Fill", Kind = LayerKind.Box, Length = Math.Clamp(size, 1, 256), Thickness = Math.Clamp(size / 2 + 1, 1, 64), Gap = 0, Color = $"#{p.FillColor.R:X2}{p.FillColor.G:X2}{p.FillColor.B:X2}", Opacity = p.FillColor.A / 255.0 });
            }
        }
    }
}
