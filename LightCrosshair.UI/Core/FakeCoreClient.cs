using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;
using LightCrosshair.UI.Services;

namespace LightCrosshair.UI.Core;

/// <summary>In-memory Core used by development, screenshots and view-model tests.</summary>
public sealed class FakeCoreClient : ICoreClient
{
    private static readonly Regex ColorPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private readonly IUiMarshaler _marshaler;
    private readonly object _gate = new();
    private readonly Dictionary<string, byte[]> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PresetEntry> _presets = BuildPresets();
    private StoreDocument _store;
    private RuntimeState _runtime;
    public FakeCoreClient(IUiMarshaler? marshaler = null)
    {
        _marshaler = marshaler ?? new InlineMarshaler();
        var classic = Classic();
        _store = new StoreDocument
        {
            Settings = new AppSettings
            {
                DefaultCrosshairId = classic.Id,
                Language = "en",
                Hotkeys = new HotkeySettings(),
            },
            Crosshairs = [classic, DotOnly()],
            GameProfiles =
            [
                new GameProfile
                {
                    Name = "Sample Game",
                    ProcessName = "samplegame.exe",
                    CrosshairId = classic.Id,
                },
            ],
        };
        _runtime = new RuntimeState
        {
            CoreVersion = "2.0.0-fake",
            ActiveCrosshairId = classic.Id,
            CrosshairShown = true,
            Gpu = new GpuCapabilities
            {
                SoftwareGamma = true,
                NvidiaDigitalVibrance = true,
                NvidiaFpsCap = true,
                AmdColor = true,
            },
            Monitors =
            [
                new MonitorInfo { DeviceName = @"\\.\DISPLAY1", FriendlyName = "Primary", IsPrimary = true, Width = 1920, Height = 1080, DpiScalePercent = 100 },
                new MonitorInfo { DeviceName = @"\\.\DISPLAY2", FriendlyName = "Side", IsPrimary = false, Width = 2560, Height = 1440, DpiScalePercent = 150 },
            ],
        };
    }

    public bool IsFake => true;
    public bool InputCapture { get; private set; }
    public CoreLinkState State { get; private set; } = CoreLinkState.Idle;
    public Snapshot? Snapshot { get; private set; }
    public int PreviewCount { get; private set; }
    public int RenderCount { get; private set; }
    public CrosshairDesign? LastPreview { get; private set; }

    public event EventHandler<CoreLinkState>? StateChanged;
    public event EventHandler<Snapshot>? SnapshotChanged;
    public event EventHandler<RuntimeState>? RuntimeChanged;
    public event EventHandler<NavigateRequest?>? FocusRequested;
    public event EventHandler? CloseRequested;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        SetState(CoreLinkState.Connected);
        Publish();
        return Task.CompletedTask;
    }

    public void SetGpu(GpuCapabilities gpu)
    {
        lock (_gate) _runtime.Gpu = gpu;
        Publish();
    }

    public void SetHotkeyErrors(IReadOnlyList<HotkeyError> errors)
    {
        lock (_gate) _runtime.HotkeyErrors = errors.ToList();
        PublishRuntime();
    }

    public void RaiseFocus(NavigateRequest? request) => _marshaler.Post(() => FocusRequested?.Invoke(this, request));

    public void RaiseClose() => _marshaler.Post(() => CloseRequested?.Invoke(this, EventArgs.Empty));

    public Task<CrosshairDesign> SaveCrosshairAsync(CrosshairDesign design, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var saved = Normalize(JsonClone.Clone(design));
            var index = _store.Crosshairs.FindIndex(item => item.Id == saved.Id);
            if (index >= 0) _store.Crosshairs[index] = saved;
            else _store.Crosshairs.Add(saved);
            if (string.IsNullOrEmpty(_store.Settings.DefaultCrosshairId))
                _store.Settings.DefaultCrosshairId = saved.Id;
            Publish();
            return Task.FromResult(JsonClone.Clone(saved));
        }
    }

    public Task DeleteCrosshairAsync(string id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_store.Settings.DefaultCrosshairId == id || _store.GameProfiles.Exists(profile => profile.CrosshairId == id))
                throw new CoreException(IpcErrorCodes.Validation);
            if (_store.Crosshairs.RemoveAll(item => item.Id == id) == 0)
                throw new CoreException(IpcErrorCodes.NotFound);
            Publish();
            return Task.CompletedTask;
        }
    }

    public Task<GameProfile> SaveGameProfileAsync(GameProfile profile, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var saved = Normalize(JsonClone.Clone(profile));
            var index = _store.GameProfiles.FindIndex(item => item.Id == saved.Id);
            if (index >= 0) _store.GameProfiles[index] = saved;
            else _store.GameProfiles.Add(saved);
            Publish();
            return Task.FromResult(JsonClone.Clone(saved));
        }
    }

    public Task DeleteGameProfileAsync(string id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_store.GameProfiles.RemoveAll(item => item.Id == id) == 0)
                throw new CoreException(IpcErrorCodes.NotFound);
            if (_runtime.ActiveGameProfileId == id) _runtime.ActiveGameProfileId = null;
            Publish();
            return Task.CompletedTask;
        }
    }

    public Task<AppSettings> SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var saved = Normalize(JsonClone.Clone(settings));
            _store.Settings = saved;
            _runtime.CrosshairShown = saved.CrosshairVisible && !saved.ShowOnlyInConfiguredGames;
            Publish();
            return Task.FromResult(JsonClone.Clone(saved));
        }
    }

    public Task PreviewAsync(CrosshairDesign? design, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            PreviewCount++;
            LastPreview = design == null ? null : JsonClone.Clone(design);
            _runtime.PreviewActive = design != null;
            PublishRuntime();
            return Task.CompletedTask;
        }
    }

    public Task<string> RenderPngAsync(RenderRequest request, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            RenderCount++;
            if (request.Design == null) throw new CoreException(IpcErrorCodes.BadRequest);
            return Task.FromResult(FakeRenderer.Render(request.Design, request.CanvasSize, request.Background));
        }
    }

    public Task<IReadOnlyList<PresetEntry>> GetPresetsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult<IReadOnlyList<PresetEntry>>(_presets.Select(JsonClone.Clone).ToList());
    }

    public Task<string> EncodeShareCodeAsync(CrosshairDesign design, CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult(ShareCodes.Encode(design));
    }

    public Task<CrosshairDesign> DecodeShareCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        lock (_gate) return Task.FromResult(Normalize(ShareCodes.Decode(code)));
    }

    public async Task<string> ImportImageAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0 || bytes.Length > ContractInfo.MaxImageBytes || bytes.Length < 24 || bytes[0] != 0x89)
            throw new CoreException(IpcErrorCodes.Validation);
        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20));
        if (width <= 0 || height <= 0 || width > ContractInfo.MaxImageSidePx || height > ContractInfo.MaxImageSidePx)
            throw new CoreException(IpcErrorCodes.Validation);
        var name = Guid.NewGuid().ToString("N") + ".png";
        lock (_gate) _images[name] = bytes;
        return name;
    }

    public Task ExportCrosshairsAsync(string path, IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var chosen = _store.Crosshairs.Where(item => ids.Contains(item.Id)).Select(JsonClone.Clone).ToList();
            if (chosen.Count != ids.Count) throw new CoreException(IpcErrorCodes.NotFound);
            var images = new List<ExportedImage>();
            foreach (var file in chosen.SelectMany(item => item.Layers).Select(layer => layer.ImageFile).Where(name => !string.IsNullOrEmpty(name)).Distinct())
            {
                if (file != null && _images.TryGetValue(file, out var bytes))
                    images.Add(new ExportedImage { FileName = file, PngBase64 = Convert.ToBase64String(bytes) });
            }
            var document = new CrosshairExportFile { Crosshairs = chosen, Images = images };
            File.WriteAllText(path, JsonSerializer.Serialize(document, ContractJson.IndentedOptions));
            return Task.CompletedTask;
        }
    }

    public Task<ImportResult> ImportCrosshairsAsync(string path, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            CrosshairExportFile? file;
            try { file = JsonSerializer.Deserialize<CrosshairExportFile>(File.ReadAllText(path), ContractJson.Options); }
            catch (JsonException) { throw new CoreException(IpcErrorCodes.Validation); }
            if (file == null) throw new CoreException(IpcErrorCodes.Validation);
            foreach (var image in file.Images)
            {
                try { _images[image.FileName] = Convert.FromBase64String(image.PngBase64); }
                catch (FormatException) { /* counted below as a warning */ }
            }
            var result = new ImportResult();
            foreach (var design in file.Crosshairs)
            {
                try
                {
                    var saved = Normalize(design);
                    saved.Id = Guid.NewGuid().ToString("N");
                    _store.Crosshairs.Add(saved);
                    result.Imported.Add(JsonClone.Clone(saved));
                }
                catch (CoreException)
                {
                    result.Warnings.Add("skipped");
                }
            }
            Publish();
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<ProcessEntry>> ListProcessesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProcessEntry> list =
        [
            new ProcessEntry { ProcessName = "notepad.exe", WindowTitle = "Notes", ExePath = null },
            new ProcessEntry { ProcessName = "samplegame.exe", WindowTitle = "Sample Game", ExePath = null },
        ];
        return Task.FromResult(list);
    }

    public Task<GameProfile> AcceptSuggestionAsync(string processName, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var name = (processName ?? "").Trim().ToLowerInvariant();
            var existing = _store.GameProfiles.FirstOrDefault(item => item.ProcessName == name);
            if (existing != null)
            {
                _runtime.PendingSuggestion = null;
                Publish();
                return Task.FromResult(JsonClone.Clone(existing));
            }
            var created = Normalize(new GameProfile
            {
                Name = string.IsNullOrEmpty(_runtime.PendingSuggestion?.DisplayName) ? name : _runtime.PendingSuggestion.DisplayName,
                ProcessName = name,
                CrosshairId = _store.Settings.DefaultCrosshairId,
            });
            _store.GameProfiles.Add(created);
            _runtime.PendingSuggestion = null;
            Publish();
            return Task.FromResult(JsonClone.Clone(created));
        }
    }

    public Task DismissSuggestionAsync(string processName, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var name = (processName ?? "").Trim().ToLowerInvariant();
            if (!_store.Settings.DismissedSuggestions.Contains(name, StringComparer.OrdinalIgnoreCase))
                _store.Settings.DismissedSuggestions.Add(name);
            if (_runtime.PendingSuggestion?.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                _runtime.PendingSuggestion = null;
            Publish();
            return Task.CompletedTask;
        }
    }

    public Task SetInputCaptureAsync(bool active, CancellationToken cancellationToken = default)
    {
        InputCapture = active;
        return Task.CompletedTask;
    }

    public Task QuitAsync(CancellationToken cancellationToken = default)
    {
        SetState(CoreLinkState.Idle);
        return Task.CompletedTask;
    }

    public void SetSuggestion(GameSuggestion? suggestion)
    {
        lock (_gate) _runtime.PendingSuggestion = suggestion;
        PublishRuntime();
    }

    private void Publish()
    {
        Snapshot snap;
        lock (_gate)
        {
            snap = new Snapshot { Store = JsonClone.Clone(_store), Runtime = JsonClone.Clone(_runtime) };
        }
        _marshaler.Post(() =>
        {
            Snapshot = snap;
            SnapshotChanged?.Invoke(this, snap);
        });
    }

    private void PublishRuntime()
    {
        RuntimeState runtime;
        lock (_gate) runtime = JsonClone.Clone(_runtime);
        _marshaler.Post(() =>
        {
            if (Snapshot != null) Snapshot.Runtime = runtime;
            RuntimeChanged?.Invoke(this, runtime);
        });
    }

    private void SetState(CoreLinkState state)
    {
        State = state;
        _marshaler.Post(() => StateChanged?.Invoke(this, state));
    }

    private CrosshairDesign Normalize(CrosshairDesign design)
    {
        if (design.Layers.Count > ContractInfo.MaxLayers)
            throw new CoreException(IpcErrorCodes.Validation);
        if (string.IsNullOrWhiteSpace(design.Id)) design.Id = Guid.NewGuid().ToString("N");
        design.Name = Trim(design.Name, "Crosshair");
        design.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var layer in design.Layers)
        {
            if (string.IsNullOrWhiteSpace(layer.Id)) layer.Id = Guid.NewGuid().ToString("N");
            layer.Name = Trim(layer.Name, layer.Kind.ToString());
            RequireColor(layer.Color);
            RequireColor(layer.OutlineColor);
            layer.Opacity = Math.Clamp(layer.Opacity, 0, 1);
            layer.Length = Math.Clamp(layer.Length, 1, 256);
            layer.Thickness = Math.Clamp(layer.Thickness, 1, 64);
            layer.Gap = Math.Clamp(layer.Gap, 0, 128);
            layer.OutlineThickness = Math.Clamp(layer.OutlineThickness, 1, 8);
            layer.ImageScale = Math.Clamp(layer.ImageScale, 0.1, 4);
            if (layer.Kind == LayerKind.Image && (layer.ImageFile == null || !_images.ContainsKey(layer.ImageFile)))
                throw new CoreException(IpcErrorCodes.Validation);
        }
        return design;
    }

    private GameProfile Normalize(GameProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id)) profile.Id = Guid.NewGuid().ToString("N");
        profile.Name = Trim(profile.Name, profile.ProcessName);
        profile.ProcessName = (profile.ProcessName ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(profile.ProcessName))
            throw new CoreException(IpcErrorCodes.Validation);
        if (_store.GameProfiles.Exists(item => item.Id != profile.Id && item.ProcessName == profile.ProcessName))
            throw new CoreException(IpcErrorCodes.Validation);
        if (_store.Crosshairs.TrueForAll(item => item.Id != profile.CrosshairId))
            throw new CoreException(IpcErrorCodes.Validation);
        if (profile.DisplayColors != null)
        {
            var colors = profile.DisplayColors;
            colors.Gamma = Math.Clamp(colors.Gamma, 50, 150);
            colors.Contrast = Math.Clamp(colors.Contrast, 50, 150);
            colors.Brightness = Math.Clamp(colors.Brightness, 50, 150);
            colors.Vibrance = Math.Clamp(colors.Vibrance, 0, 100);
            if (colors.NvidiaDigitalVibrance is { } vibrance)
                colors.NvidiaDigitalVibrance = Math.Clamp(vibrance, 0, 100);
        }
        if (profile.VisibilityBinding != null && HotkeyRules.IsWheel(profile.VisibilityBinding.Key))
            profile.VisibilityBinding.Mode = BindingMode.Toggle;
        return profile;
    }

    private AppSettings Normalize(AppSettings settings)
    {
        settings.Language = string.IsNullOrWhiteSpace(settings.Language) ? "auto" : settings.Language;
        if (_store.Crosshairs.TrueForAll(item => item.Id != settings.DefaultCrosshairId) && _store.Crosshairs.Count > 0)
            settings.DefaultCrosshairId = _store.Crosshairs[0].Id;
        settings.FpsOverlay.Scale = Math.Clamp(settings.FpsOverlay.Scale, 0.5, 3);
        settings.FpsOverlay.BackgroundOpacity = Math.Clamp(settings.FpsOverlay.BackgroundOpacity, 0, 1);
        RequireColor(settings.FpsOverlay.TextColor);
        RequireColor(settings.FpsOverlay.BackgroundColor);
        NormalizeBinding(settings.Hotkeys.ToggleVisibility);
        NormalizeBinding(settings.Hotkeys.NextCrosshair);
        NormalizeBinding(settings.Hotkeys.PreviousCrosshair);
        NormalizeBinding(settings.Hotkeys.OpenSettings);
        return settings;
    }

    private static void NormalizeBinding(InputBinding binding)
    {
        if (HotkeyRules.IsWheel(binding.Key)) binding.Mode = BindingMode.Toggle;
    }

    private static string Trim(string? value, string fallback)
    {
        var text = (value ?? "").Trim();
        if (text.Length == 0) text = fallback;
        return text.Length <= 48 ? text : text[..48];
    }

    private static void RequireColor(string? value)
    {
        if (!ColorPattern.IsMatch(value ?? ""))
            throw new CoreException(IpcErrorCodes.Validation);
    }

    public void UseReadmeSeed()
    {
        lock (_gate)
        {
            var now = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
            var classic = ReadmeClassic();
            classic.UpdatedAt = now;
            var precision = new CrosshairDesign
            {
                Name = "Precision",
                Favorite = true,
                UpdatedAt = now.AddMinutes(-1),
                Layers = [new CrosshairLayer { Name = "Dot", Kind = LayerKind.Dot, Length = 2, Color = "#FFFFFF" }],
            };
            var tactical = new CrosshairDesign
            {
                Name = "Tactical",
                UpdatedAt = now.AddMinutes(-2),
                Layers = [new CrosshairLayer { Name = "T", Kind = LayerKind.Arms, Length = 12, Thickness = 2, Gap = 4, Arms = ArmMask.Left | ArmMask.Right | ArmMask.Bottom, Color = "#E8EAED" }],
            };
            var ring = new CrosshairDesign
            {
                Name = "Circle",
                UpdatedAt = now.AddMinutes(-3),
                Layers = [new CrosshairLayer { Name = "Ring", Kind = LayerKind.Ring, Length = 16, Thickness = 1, Color = "#7EB6FF" }],
            };
            _store = new StoreDocument
            {
                Settings = new AppSettings
                {
                    DefaultCrosshairId = classic.Id,
                    Language = "en",
                    Hotkeys = new HotkeySettings
                    {
                        ToggleVisibility = new InputBinding { Key = "Mouse5", Mode = BindingMode.HoldToShow },
                    },
                },
                Crosshairs = [classic, precision, tactical, ring],
                GameProfiles =
                [
                    new GameProfile { Name = "Counter-Strike 2", ProcessName = "cs2.exe", CrosshairId = classic.Id },
                    new GameProfile { Name = "VALORANT", ProcessName = "valorant-win64-shipping.exe", CrosshairId = classic.Id },
                ],
            };
            _runtime.ActiveCrosshairId = classic.Id;
            _runtime.PendingSuggestion = null;
            _runtime.HotkeyErrors = [];
        }
    }

    private static CrosshairDesign ReadmeClassic() => new()
    {
        Name = "Classic",
        Favorite = true,
        AntiAlias = false,
        Layers =
        [
            new CrosshairLayer { Name = "Ring", Kind = LayerKind.Ring, Length = 22, Thickness = 2, Color = "#FFFFFF" },
            new CrosshairLayer { Name = "Cross", Kind = LayerKind.Arms, Length = 10, Thickness = 2, Gap = 4, Color = "#FFFFFF", Outline = true },
            new CrosshairLayer { Name = "Dot", Kind = LayerKind.Dot, Length = 2, Color = "#3DDC97" },
        ],
    };

    private static CrosshairDesign Classic() => new()
    {
        Name = "Classic",
        Favorite = true,
        Layers =
        [
            new CrosshairLayer { Name = "Cross", Kind = LayerKind.Arms, Length = 10, Thickness = 2, Gap = 4, Color = "#FFFFFF" },
            new CrosshairLayer { Name = "Dot", Kind = LayerKind.Dot, Length = 2, Color = "#3DDC97" },
            new CrosshairLayer { Name = "Ring", Kind = LayerKind.Ring, Length = 18, Thickness = 1, Color = "#FFFFFF", Opacity = 0.85 },
        ],
    };

    private static CrosshairDesign DotOnly() => new()
    {
        Name = "Tiny dot",
        Layers = [new CrosshairLayer { Name = "Dot", Kind = LayerKind.Dot, Length = 3, Color = "#FFFFFF" }],
    };

    private static List<PresetEntry> BuildPresets()
    {
        PresetEntry Preset(string name, string id, string[] tags, params CrosshairLayer[] layers) => new()
        {
            Tags = [.. tags],
            Design = new CrosshairDesign { Id = id, Name = name, SourcePresetId = id, Layers = [.. layers] },
        };

        CrosshairLayer Arms(int length, int thickness, int gap, ArmMask arms, string color, bool outline = false, double rotation = 0) => new()
        {
            Name = "Arms",
            Kind = LayerKind.Arms,
            Length = length,
            Thickness = thickness,
            Gap = gap,
            Arms = arms,
            Color = color,
            Outline = outline,
            Rotation = rotation,
        };

        return
        [
            Preset("Fine cross", "fine-cross", ["cross", "minimal"], Arms(8, 1, 3, ArmMask.All, "#FFFFFF")),
            Preset("Open cross", "open-cross", ["cross", "minimal"], Arms(12, 2, 6, ArmMask.All, "#FFFFFF")),
            Preset("Bold cross", "bold-cross", ["cross", "high-visibility"], Arms(14, 4, 4, ArmMask.All, "#FFFFFF", true)),
            Preset("Marked T", "marked-t", ["t", "minimal"], Arms(10, 2, 4, ArmMask.Left | ArmMask.Right | ArmMask.Bottom, "#FFFFFF")),
            Preset("Wide T", "wide-t", ["t"], Arms(16, 2, 5, ArmMask.Left | ArmMask.Right | ArmMask.Bottom, "#E8EAED")),
            Preset("Saltire", "saltire", ["x", "minimal"], Arms(10, 2, 3, ArmMask.All, "#FFFFFF", rotation: 45)),
            Preset("Outlined saltire", "outlined-x", ["x", "outlined"], Arms(12, 2, 4, ArmMask.All, "#FFFFFF", true, 45)),
            Preset("Pin dot", "pin-dot", ["dot", "minimal"], new CrosshairLayer { Name = "Dot", Kind = LayerKind.Dot, Length = 2, Color = "#FFFFFF" }),
            Preset("Green dot", "green-dot", ["dot", "high-visibility"], new CrosshairLayer { Name = "Dot", Kind = LayerKind.Dot, Length = 4, Color = "#3DDC97", Outline = true }),
            Preset("Thin ring", "thin-ring", ["circle", "minimal"], new CrosshairLayer { Name = "Ring", Kind = LayerKind.Ring, Length = 16, Thickness = 1, Color = "#FFFFFF" }),
            Preset("Dot in ring", "dot-ring", ["dot", "circle"], new CrosshairLayer { Name = "Ring", Kind = LayerKind.Ring, Length = 20, Thickness = 1, Color = "#FFFFFF" }, new CrosshairLayer { Name = "Dot", Kind = LayerKind.Dot, Length = 2, Color = "#FFFFFF" }),
            Preset("Corner box", "corner-box", ["box", "minimal"], new CrosshairLayer { Name = "Box", Kind = LayerKind.Box, Length = 16, Thickness = 2, Gap = 8, Color = "#FFFFFF" }),
        ];
    }
}
