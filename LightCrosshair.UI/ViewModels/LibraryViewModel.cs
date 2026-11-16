using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightCrosshair.UI.Core;
using LightCrosshair.UI.Localization;
using LightCrosshair.UI.Services;

namespace LightCrosshair.UI.ViewModels;

public sealed partial class LibraryCard : ObservableObject
{
    public LibraryCard(CrosshairDesign design, bool preset, IReadOnlyList<string> tags)
    {
        Design = design;
        IsPreset = preset;
        Tags = tags;
    }

    public CrosshairDesign Design { get; }
    public bool IsPreset { get; }
    public IReadOnlyList<string> Tags { get; }
    [ObservableProperty] private string? _png;
    public string SectionKey => IsPreset ? "Library_Presets" : "Library_Mine";
}

public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly ICoreClient _core;
    private readonly IUiServices _ui;
    private readonly StudioViewModel _studio;
    private readonly ThumbnailCache _thumbs;
    private readonly List<LibraryCard> _mine = new();
    private readonly List<LibraryCard> _presets = new();
    private bool _minimized;

    public LibraryViewModel(ICoreClient core, IUiServices ui, StudioViewModel studio, ThumbnailCache? thumbs = null)
    {
        _core = core;
        _ui = ui;
        _studio = studio;
        _thumbs = thumbs ?? new ThumbnailCache();
    }

    public ObservableCollection<LibraryCard> Mine { get; } = new();
    public ObservableCollection<LibraryCard> Presets { get; } = new();
    public IReadOnlyList<string> TagKeys { get; } =
        ["Library_TagAll", "Tag_dot", "Tag_cross", "Tag_t", "Tag_x", "Tag_circle", "Tag_box", "Tag_minimal", "Tag_outlined", "Tag_highvisibility"];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _tagKey = "Library_TagAll";
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private string _defaultCrosshairId = "";

    partial void OnSearchChanged(string value) => ApplyFilter();
    partial void OnTagKeyChanged(string value) => ApplyFilter();

    public async Task LoadPresetsAsync()
    {
        try
        {
            var presets = await _core.GetPresetsAsync().ConfigureAwait(true);
            _presets.Clear();
            foreach (var preset in presets)
                _presets.Add(new LibraryCard(preset.Design, true, preset.Tags));
            ApplyFilter();
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    public void Apply(Snapshot snapshot)
    {
        DefaultCrosshairId = snapshot.Store.Settings.DefaultCrosshairId;
        _mine.Clear();
        foreach (var design in snapshot.Store.Crosshairs.OrderByDescending(item => item.UpdatedAt))
            _mine.Add(new LibraryCard(design, false, InferTags(design)));
        ApplyFilter();
    }

    public void SetMinimized(bool minimized) => _minimized = minimized;

    public async Task LoadThumbAsync(LibraryCard? card)
    {
        if (card == null || _minimized || card.Png != null) return;
        try
        {
            card.Png = await _thumbs.GetAsync(_core, card.Design, 96).ConfigureAwait(true);
        }
        catch (CoreException) { /* the card stays blank; the page error is for actions */ }
    }

    [RelayCommand]
    private void SetTag(string? key)
    {
        if (!string.IsNullOrEmpty(key)) TagKey = key;
    }

    [RelayCommand]
    private async Task ApplyDefaultAsync(LibraryCard? card)
    {
        if (card == null || card.IsPreset) return;
        await MutateSettingsAsync(settings => settings.DefaultCrosshairId = card.Design.Id).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(LibraryCard? card)
    {
        if (card == null || card.IsPreset) return;
        var copy = JsonClone.Clone(card.Design);
        copy.Favorite = !copy.Favorite;
        await SaveDesignAsync(copy).ConfigureAwait(true);
    }

    [RelayCommand]
    private void Edit(LibraryCard? card)
    {
        if (card == null) return;
        if (card.IsPreset)
        {
            var copy = JsonClone.Clone(card.Design);
            copy.Id = Guid.NewGuid().ToString("N");
            copy.SourcePresetId = card.Design.Id;
            copy.Name = copy.Name;
            _studio.Load(copy, saved: false);
        }
        else _studio.Load(card.Design);
    }

    [RelayCommand]
    private async Task DuplicateAsync(LibraryCard? card)
    {
        if (card == null) return;
        var copy = JsonClone.Clone(card.IsPreset ? card.Design : card.Design);
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Favorite = false;
        if (card.IsPreset) copy.SourcePresetId = card.Design.Id;
        var name = copy.Name + " 2";
        copy.Name = name.Length <= 48 ? name : copy.Name;
        await SaveDesignAsync(copy).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteAsync(LibraryCard? card)
    {
        if (card == null || card.IsPreset) return;
        var ok = await _ui.ConfirmAsync(Loc.Instance["DeleteCrosshair_Title"], Loc.Instance.Format("DeleteCrosshair_Message", card.Design.Name), Loc.Instance["Delete"]).ConfigureAwait(true);
        if (!ok) return;
        try { await _core.DeleteCrosshairAsync(card.Design.Id).ConfigureAwait(true); }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    [RelayCommand]
    private async Task ExportAsync(LibraryCard? card)
    {
        if (card == null || card.IsPreset) return;
        var path = _ui.PickSaveFile(Loc.Instance["File_Export"], card.Design.Name + ".lcx.json");
        if (path == null) return;
        try { await _core.ExportCrosshairsAsync(path, [card.Design.Id]).ConfigureAwait(true); }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    [RelayCommand]
    private async Task ImportFileAsync()
    {
        var path = _ui.PickOpenFile(Loc.Instance["File_Export"]);
        if (path == null) return;
        try
        {
            var result = await _core.ImportCrosshairsAsync(path).ConfigureAwait(true);
            _ui.Toast(Loc.Instance.Format("Library_Imported", result.Imported.Count, result.Warnings.Count));
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    [RelayCommand]
    private async Task PasteCodeAsync()
    {
        var code = _ui.Paste();
        if (string.IsNullOrWhiteSpace(code))
        {
            _ui.Toast(Loc.Instance["Toast_NoClipboard"]);
            return;
        }
        try
        {
            var design = await _core.DecodeShareCodeAsync(code).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(design.Name)) design.Name = Loc.Instance["Name_NewCrosshair"];
            await _core.SaveCrosshairAsync(design).ConfigureAwait(true);
            _ui.Toast(Loc.Instance["Toast_Saved"]);
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    private async Task SaveDesignAsync(CrosshairDesign design)
    {
        try { await _core.SaveCrosshairAsync(design).ConfigureAwait(true); }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    private async Task MutateSettingsAsync(Action<AppSettings> change)
    {
        var snapshot = _core.Snapshot;
        if (snapshot == null) return;
        var settings = JsonClone.Clone(snapshot.Store.Settings);
        change(settings);
        try { await _core.SaveSettingsAsync(settings).ConfigureAwait(true); }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    private void ApplyFilter()
    {
        Fill(Mine, _mine);
        Fill(Presets, _presets);
    }

    private void Fill(ObservableCollection<LibraryCard> target, List<LibraryCard> source)
    {
        var tag = TagKey == "Library_TagAll" ? null : TagKey["Tag_".Length..];
        if (tag == "highvisibility") tag = "high-visibility";
        target.Clear();
        foreach (var card in source)
        {
            if (tag != null && !card.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(Search) && !card.Design.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            target.Add(card);
        }
    }

    private static List<string> InferTags(CrosshairDesign design)
    {
        var tags = new List<string>();
        if (design.Layers.Exists(layer => layer.Kind == LayerKind.Dot)) tags.Add("dot");
        if (design.Layers.Exists(layer => layer.Kind == LayerKind.Ring)) tags.Add("circle");
        if (design.Layers.Exists(layer => layer.Kind == LayerKind.Box)) tags.Add("box");
        if (design.Layers.Exists(layer => layer.Kind == LayerKind.Arms && Math.Abs(layer.Rotation % 180) is > 20 and < 70)) tags.Add("x");
        if (design.Layers.Exists(layer => layer.Kind == LayerKind.Arms && layer.Arms.HasFlag(ArmMask.Left) && layer.Arms.HasFlag(ArmMask.Right) && layer.Arms.HasFlag(ArmMask.Bottom) && !layer.Arms.HasFlag(ArmMask.Top))) tags.Add("t");
        if (design.Layers.Exists(layer => layer.Kind == LayerKind.Arms && layer.Arms == ArmMask.All)) tags.Add("cross");
        if (design.Layers.Exists(layer => layer.Outline)) tags.Add("outlined");
        if (design.Layers.Count <= 2) tags.Add("minimal");
        return tags;
    }
}

public sealed class ThumbnailCache
{
    private readonly Dictionary<string, string> _png = new();

    public async Task<string> GetAsync(ICoreClient core, CrosshairDesign design, int size, CancellationToken cancellationToken = default)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonClone.Text(design)))) + ":" + size;
        if (_png.TryGetValue(key, out var cached)) return cached;
        var png = await core.RenderPngAsync(new RenderRequest { Design = design, CanvasSize = size, Background = "#16181B" }, cancellationToken).ConfigureAwait(false);
        _png[key] = png;
        return png;
    }
}
