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

    public override string ToString() => Design.Name;
    [ObservableProperty] private string? _png;
    [ObservableProperty] private bool _inUse;
    [ObservableProperty] private string _usageText = "";
    [ObservableProperty] private bool _renaming;
    [ObservableProperty] private string _renameText = "";
    public string SectionKey => IsPreset ? "Library_Presets" : "Library_Mine";

    public void BeginRename()
    {
        RenameText = Design.Name;
        Renaming = true;
    }
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
    private StoreDocument? _store;
    private RuntimeState? _runtime;

    public LibraryViewModel(ICoreClient core, IUiServices ui, StudioViewModel studio, ThumbnailCache? thumbs = null)
    {
        _core = core;
        _ui = ui;
        _studio = studio;
        _thumbs = thumbs ?? new ThumbnailCache();
        Loc.Instance.Changed += (_, _) => UpdateUsage();
    }

    public ObservableCollection<LibraryCard> Mine { get; } = new();
    public ObservableCollection<LibraryCard> Presets { get; } = new();
    public IReadOnlyList<string> TagKeys { get; } =
        ["Library_TagAll", "Tag_dot", "Tag_cross", "Tag_t", "Tag_x", "Tag_circle", "Tag_box", "Tag_minimal", "Tag_outlined", "Tag_highvisibility"];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _tagKey = "Library_TagAll";
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private string _defaultCrosshairId = "";
    [ObservableProperty] private bool _useOpen;
    [ObservableProperty] private string _useName = "";
    [ObservableProperty] private bool _showUseName = true;
    [ObservableProperty] private bool _showUseExisting;
    private LibraryCard? _useCard;
    private CrosshairDesign? _existingCopy;
    private string? _gameProfileId;

    public IReadOnlyList<LibraryCard> AllPresets => _presets;
    public Action<string, string>? GameCrosshairChanged { get; set; }

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
        _store = snapshot.Store;
        _runtime = snapshot.Runtime;
        DefaultCrosshairId = snapshot.Store.Settings.DefaultCrosshairId;
        var active = snapshot.Runtime.ActiveCrosshairId;
        if (string.IsNullOrEmpty(active)) active = snapshot.Store.Settings.DefaultCrosshairId;
        _mine.Clear();
        foreach (var design in snapshot.Store.Crosshairs.OrderByDescending(item => item.UpdatedAt))
        {
            var card = new LibraryCard(design, false, InferTags(design)) { InUse = design.Id == active };
            _mine.Add(card);
        }
        ApplyFilter();
        UpdateUsage();
    }

    public void SetMinimized(bool minimized) => _minimized = minimized;

    public void NoteActive(string? id)
    {
        foreach (var card in _mine)
            card.InUse = card.Design.Id == id;
        UpdateUsage();
    }

    public void ApplyRuntime(RuntimeState runtime)
    { _runtime = runtime; NoteActive(runtime.ActiveCrosshairId); }

    private void UpdateUsage()
    {
        if (_store is null) return;
        var game = _store.GameProfiles.FirstOrDefault(item => item.Id == _runtime?.ActiveGameProfileId);
        foreach (var card in _mine)
        {
            var labels = new List<string>();
            if (game is not null && game.CrosshairId == card.Design.Id) labels.Add(Loc.Instance.Format("Library_InUseGame", game.Name));
            if (_store.Settings.DefaultCrosshairId == card.Design.Id) labels.Add(Loc.Instance["Library_Default"]);
            card.UsageText = string.Join(" · ", labels);
        }
    }

    public void ReleaseCaches()
    {
        ClearThumbnails();
        _minimized = true;
    }

    /// <summary>Drop thumbnails so they are remade (e.g. at a new screen DPI); unlike ReleaseCaches it does not park the page.</summary>
    public void ClearThumbnails()
    {
        _thumbs.Clear();
        foreach (var card in _mine.Concat(_presets)) card.Png = null;
    }

    public async Task LoadThumbAsync(LibraryCard? card)
    {
        if (card == null || _minimized || card.Png != null) return;
        try
        {
            // The card's 72 px thumbnail area, in screen pixels, at the overlay's 1:1 scale.
            card.Png = await _thumbs.GetAsync(_core, card.Design, (int)Math.Round(72 * UiScale.Dpi), UiScale.Dpi).ConfigureAwait(true);
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
        await ActivateAsync(card.Design.Id, _core.Snapshot?.Runtime.ActiveGameProfileId).ConfigureAwait(true);
    }

    [RelayCommand]
    private Task SetDefaultForAllAppsAsync(LibraryCard? card) => card is null || card.IsPreset ? Task.CompletedTask : ActivateAsync(card.Design.Id, null);

    public void BeginUse(LibraryCard card, string? gameProfileId)
    {
        if (!card.IsPreset) return;
        _useCard = card;
        _gameProfileId = gameProfileId;
        _existingCopy = _mine.Select(item => item.Design).FirstOrDefault(item => item.SourcePresetId == card.Design.Id);
        UseName = card.Design.Name;
        ShowUseExisting = _existingCopy != null;
        ShowUseName = _existingCopy == null;
        UseOpen = true;
    }

    [RelayCommand]
    private void SaveAnotherCopy()
    {
        if (_useCard == null) return;
        ShowUseExisting = false;
        ShowUseName = true;
        UseName = _useCard.Design.Name;
    }

    [RelayCommand]
    private async Task ConfirmUseAsync()
    {
        if (_useCard == null) return;
        if (ShowUseExisting && _existingCopy != null)
        {
            await ActivateAsync(_existingCopy.Id, _gameProfileId ?? _core.Snapshot?.Runtime.ActiveGameProfileId).ConfigureAwait(true);
            CloseUse();
            return;
        }
        var name = (UseName ?? "").Trim();
        if (name.Length == 0) return;
        if (name.Length > 48) name = name[..48];
        var copy = JsonClone.Clone(_useCard.Design);
        copy.Id = Guid.NewGuid().ToString("N");
        copy.SourcePresetId = _useCard.Design.Id;
        copy.Name = name;
        copy.Favorite = false;
        copy.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await SaveDesignAsync(copy).ConfigureAwait(true)) return;
        await ActivateAsync(copy.Id, _gameProfileId ?? _core.Snapshot?.Runtime.ActiveGameProfileId).ConfigureAwait(true);
        CloseUse();
    }

    [RelayCommand]
    private void CancelUse() => CloseUse();

    private void CloseUse()
    {
        UseOpen = false;
        _useCard = null;
        _existingCopy = null;
        _gameProfileId = null;
    }

    [RelayCommand]
    private async Task CommitRenameAsync(LibraryCard? card)
    {
        if (card == null || card.IsPreset) return;
        var name = (card.RenameText ?? "").Trim();
        card.Renaming = false;
        if (name.Length == 0 || name == card.Design.Name) return;
        if (name.Length > 48) name = name[..48];
        var copy = JsonClone.Clone(card.Design);
        copy.Name = name;
        copy.UpdatedAt = DateTimeOffset.UtcNow;
        await SaveDesignAsync(copy).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(LibraryCard? card)
    {
        if (card == null || card.IsPreset) return;
        var copy = JsonClone.Clone(card.Design);
        copy.Favorite = !copy.Favorite;
        await SaveDesignAsync(copy).ConfigureAwait(true);
    }

    /// <summary>Set by the shell: resolve unsaved Studio edits (false = cancel), then show Studio.</summary>
    public Func<Task<bool>>? BeforeEdit { get; set; }
    public Func<Task>? EditOpened { get; set; }

    [RelayCommand]
    private async Task EditAsync(LibraryCard? card)
    {
        if (card == null) return;
        if (BeforeEdit != null && !await BeforeEdit().ConfigureAwait(true)) return;
        Open(card);
        if (EditOpened != null) await EditOpened().ConfigureAwait(true);
    }

    private void Open(LibraryCard card)
    {
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
        var path = _ui.PickOpenFile(Loc.Instance["File_Import"]);
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

    private async Task<bool> SaveDesignAsync(CrosshairDesign design)
    {
        try
        {
            await _core.SaveCrosshairAsync(design).ConfigureAwait(true);
            return true;
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
        return false;
    }

    private async Task ActivateAsync(string id, string? gameProfileId)
    {
        if (!string.IsNullOrEmpty(gameProfileId))
        {
            var snapshot = _core.Snapshot;
            var profile = snapshot?.Store.GameProfiles.FirstOrDefault(item => item.Id == gameProfileId);
            if (profile == null) return;
            var copy = JsonClone.Clone(profile);
            copy.CrosshairId = id;
            try { await _core.SaveGameProfileAsync(copy).ConfigureAwait(true); }
            catch (CoreException ex) { Error = ErrorText.For(ex.Code); return; }
            catch (Exception) { Error = ErrorText.For(null); return; }
            GameCrosshairChanged?.Invoke(gameProfileId, id);
            return;
        }
        await MutateSettingsAsync(settings => settings.DefaultCrosshairId = id).ConfigureAwait(true);
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
    private const int MaxEntries = 48;
    private readonly Dictionary<string, string> _png = new();
    private readonly List<string> _order = new();

    public async Task<string> GetAsync(ICoreClient core, CrosshairDesign design, int box, double dpi, CancellationToken cancellationToken = default)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonClone.Text(design)))) + ":" + box + ":" + dpi.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (_png.TryGetValue(key, out var cached)) return cached;
        var render = await core.RenderPngAsync(new RenderRequest { Design = design, CanvasSize = 256 }, cancellationToken).ConfigureAwait(false);
        var png = await Task.Run(() => PixelThumb.Fit(render, box, dpi), cancellationToken).ConfigureAwait(false);
        _png[key] = png;
        _order.Remove(key);
        _order.Add(key);
        while (_order.Count > MaxEntries)
        {
            var oldest = _order[0];
            _order.RemoveAt(0);
            _png.Remove(oldest);
        }
        return png;
    }

    public void Clear()
    {
        _png.Clear();
        _order.Clear();
    }
}
