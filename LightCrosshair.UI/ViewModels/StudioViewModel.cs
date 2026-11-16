using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightCrosshair.UI.Core;
using LightCrosshair.UI.Localization;
using LightCrosshair.UI.Services;

namespace LightCrosshair.UI.ViewModels;

public sealed partial class LayerRow : ObservableObject
{
    public LayerRow(CrosshairLayer layer) => Layer = layer;

    public CrosshairLayer Layer { get; }

    public string Name => string.IsNullOrWhiteSpace(Layer.Name) ? Layer.Kind.ToString() : Layer.Name;

    public override string ToString() => Name;
    public bool Visible => Layer.Visible;
    public string KindKey => "Kind_" + Layer.Kind;

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Visible));
        OnPropertyChanged(nameof(KindKey));
    }
}

public sealed partial class StudioViewModel : ObservableObject
{
    private readonly ICoreClient _core;
    private readonly IUiServices _ui;
    private readonly IDelay _delay;
    private readonly Func<DateTime> _clock;
    private readonly PreviewScheduler<CrosshairDesign?> _previews;
    private readonly PreviewScheduler<CrosshairDesign> _renders;
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private CrosshairDesign _design = new();
    private string _baseline = "";
    private bool _suspend;
    private bool _coalesce;
    private bool _pushed;
    private int _renderGeneration;
    private bool _live;
    private bool _entered = true;
    private bool _loaded;

    public StudioViewModel(ICoreClient core, IUiServices ui, IDelay? delay = null, Func<DateTime>? clock = null, TimeSpan? interval = null)
    {
        _core = core;
        _ui = ui;
        _delay = delay ?? new TaskDelay();
        _clock = clock ?? (() => DateTime.UtcNow);
        var gap = interval ?? TimeSpan.FromMilliseconds(33);
        _previews = new PreviewScheduler<CrosshairDesign?>(gap);
        _renders = new PreviewScheduler<CrosshairDesign>(gap);
        Loc.Instance.Changed += (_, _) => OnPropertyChanged(nameof(LengthLabelKey));
    }

    public ObservableCollection<LayerRow> Rows { get; } = new();

    [ObservableProperty] private LayerRow? _selected;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private string? _previewPng;
    [ObservableProperty] private int _zoom = 2;
    [ObservableProperty] private string _background = "dark";
    [ObservableProperty] private string _customBackground = "#3A3D42";
    [ObservableProperty] private bool _isMinimized;
    [ObservableProperty] private bool _addOpen;

    public bool LiveInGame
    {
        get => _live;
        set
        {
            if (_live == value) return;
            _live = value;
            OnPropertyChanged();
            if (_live && _entered && !IsMinimized) SchedulePreview();
            else _ = SendPreviewAsync(null);
        }
    }

    public bool CanAddLayer => _design.Layers.Count < ContractInfo.MaxLayers;
    public bool HasLayer => Selected != null;
    public bool ShowArms => Selected?.Layer.Kind == LayerKind.Arms;
    public bool ShowGap => Selected?.Layer.Kind is LayerKind.Arms or LayerKind.Box;
    public bool ShowThickness => Selected?.Layer.Kind is LayerKind.Arms or LayerKind.Ring or LayerKind.Box;
    public bool ShowOutline => Selected?.Layer.Kind != LayerKind.Image;
    public bool ShowImage => Selected?.Layer.Kind == LayerKind.Image;
    public bool ShowShape => Selected != null && Selected.Layer.Kind != LayerKind.Image;
    public bool ShowFill => Selected?.Layer.Kind is LayerKind.Ring or LayerKind.Box;
    public string LengthLabelKey => Selected?.Layer.Kind switch
    {
        LayerKind.Dot or LayerKind.Ring => "Studio_Diameter",
        LayerKind.Box => "Studio_Side",
        _ => "Studio_Length",
    };

    public string DesignName
    {
        get => _design.Name;
        set => Edit(() => _design.Name = value.Length > 48 ? value[..48] : value);
    }

    public bool AntiAlias
    {
        get => _design.AntiAlias;
        set => Edit(() => _design.AntiAlias = value);
    }

    public string LayerName
    {
        get => Selected?.Layer.Name ?? "";
        set => Edit(() => { if (Selected != null) Selected.Layer.Name = value.Length > 48 ? value[..48] : value; });
    }

    public int Length
    {
        get => Selected?.Layer.Length ?? 1;
        set => Edit(() => { if (Selected != null) Selected.Layer.Length = Math.Clamp(value, 1, 256); });
    }

    public int Thickness
    {
        get => Selected?.Layer.Thickness ?? 1;
        set => Edit(() => { if (Selected != null) Selected.Layer.Thickness = Math.Clamp(value, 1, 64); });
    }

    public int Gap
    {
        get => Selected?.Layer.Gap ?? 0;
        set => Edit(() => { if (Selected != null) Selected.Layer.Gap = Math.Clamp(value, 0, 128); });
    }

    public double OpacityPercent
    {
        get => (Selected?.Layer.Opacity ?? 1) * 100;
        set => Edit(() => { if (Selected != null) Selected.Layer.Opacity = Math.Clamp(value, 0, 100) / 100.0; });
    }

    public int OffsetX
    {
        get => Selected?.Layer.OffsetX ?? 0;
        set => Edit(() => { if (Selected != null) Selected.Layer.OffsetX = value; });
    }

    public int OffsetY
    {
        get => Selected?.Layer.OffsetY ?? 0;
        set => Edit(() => { if (Selected != null) Selected.Layer.OffsetY = value; });
    }

    public double Rotation
    {
        get => Selected?.Layer.Rotation ?? 0;
        set => Edit(() => { if (Selected != null) Selected.Layer.Rotation = Math.Clamp(value, -180, 180); });
    }

    public string Color
    {
        get => Selected?.Layer.Color ?? "#FFFFFF";
        set => EditColor(value, color => { if (Selected != null) Selected.Layer.Color = color; });
    }

    public bool Fill
    {
        get => !string.IsNullOrEmpty(Selected?.Layer.FillColor);
        set => Edit(() =>
        {
            if (Selected == null) return;
            Selected.Layer.FillColor = value ? Selected.Layer.FillColor ?? Selected.Layer.Color : null;
        });
    }

    public string FillColor
    {
        get => Selected?.Layer.FillColor ?? Selected?.Layer.Color ?? "#FFFFFF";
        set => EditColor(value, color => { if (Selected != null) Selected.Layer.FillColor = color; });
    }

    public bool Outline
    {
        get => Selected?.Layer.Outline ?? false;
        set => Edit(() => { if (Selected != null) Selected.Layer.Outline = value; });
    }

    public int OutlineThickness
    {
        get => Selected?.Layer.OutlineThickness ?? 1;
        set => Edit(() => { if (Selected != null) Selected.Layer.OutlineThickness = Math.Clamp(value, 1, 8); });
    }

    public string OutlineColor
    {
        get => Selected?.Layer.OutlineColor ?? "#000000";
        set => EditColor(value, color => { if (Selected != null) Selected.Layer.OutlineColor = color; });
    }

    public double ImageScale
    {
        get => Selected?.Layer.ImageScale ?? 1;
        set => Edit(() => { if (Selected != null) Selected.Layer.ImageScale = Math.Clamp(value, 0.1, 4); });
    }

    public bool ArmTop { get => HasArm(ArmMask.Top); set => SetArm(ArmMask.Top, value); }
    public bool ArmRight { get => HasArm(ArmMask.Right); set => SetArm(ArmMask.Right, value); }
    public bool ArmBottom { get => HasArm(ArmMask.Bottom); set => SetArm(ArmMask.Bottom, value); }
    public bool ArmLeft { get => HasArm(ArmMask.Left); set => SetArm(ArmMask.Left, value); }

    public bool CheckerPreview => Background == "checker";

    public bool AllLayersHidden => Rows.Count > 0 && Rows.All(row => !row.Layer.Visible);

    public string PreviewFill => Background switch
    {
        "mid" => "#808080",
        "light" => "#E8EAED",
        "custom" => NormalizeHex(CustomBackground) ?? "#3A3D42",
        _ => "#0E0F11",
    };

    public void Load(CrosshairDesign design, bool saved = true)
    {
        _suspend = true;
        _design = JsonClone.Clone(design);
        if (saved) _baseline = JsonClone.Text(_design);
        _undo.Clear();
        _redo.Clear();
        IsDirty = JsonClone.Text(_design) != _baseline;
        Error = "";
        RebuildRows(_design.Layers.Count - 1);
        _suspend = false;
        _loaded = true;
        NotifyAll();
        ScheduleRender(final: true);
        if (LiveInGame && _entered && !IsMinimized) SchedulePreview(final: true);
    }

    public bool IsLoaded => _loaded;

    public void Revert()
    {
        if (string.IsNullOrEmpty(_baseline)) return;
        var design = System.Text.Json.JsonSerializer.Deserialize<CrosshairDesign>(_baseline, ContractJson.Options);
        if (design != null) Load(design);
    }

    public void OnSnapshot(Snapshot snapshot)
    {
        if (string.IsNullOrEmpty(_design.Id)) return;
        if (IsDirty) return;
        var stored = snapshot.Store.Crosshairs.FirstOrDefault(item => item.Id == _design.Id);
        if (stored != null && JsonClone.Text(stored) != _baseline)
            Load(stored);
    }

    public CrosshairDesign? Current => string.IsNullOrEmpty(_design.Id) && _design.Layers.Count == 0 ? null : _design;

    public void Enter()
    {
        _entered = true;
        if (LiveInGame && !IsMinimized) SchedulePreview(final: true);
        ScheduleRender(final: true);
    }

    public async Task LeaveAsync()
    {
        _entered = false;
        if (LiveInGame) await SendPreviewAsync(null).ConfigureAwait(false);
    }

    public void SetMinimized(bool minimized)
    {
        IsMinimized = minimized;
        if (!minimized)
        {
            ScheduleRender(final: true);
            if (LiveInGame && _entered) SchedulePreview(final: true);
        }
    }

    [RelayCommand]
    private void BeginEdit()
    {
        _coalesce = true;
        _pushed = false;
    }

    [RelayCommand]
    private void EndEdit()
    {
        _coalesce = false;
        _pushed = false;
        SchedulePreview(final: true);
        ScheduleRender(final: true);
        NotifyUndo();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Restore(_undo, _redo);

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Restore(_redo, _undo);

    private bool CanUndo() => _undo.Count > 0 && !_coalesce;
    private bool CanRedo() => _redo.Count > 0 && !_coalesce;

    [RelayCommand]
    private Task SaveAsync() => SaveCoreAsync(newId: false);

    [RelayCommand]
    private Task SaveAsNewAsync() => SaveCoreAsync(newId: true);

    [RelayCommand]
    private void AddLayer(string? kindName)
    {
        AddOpen = false;
        if (!Enum.TryParse<LayerKind>(kindName, out var kind)) return;
        if (kind == LayerKind.Image)
        {
            _ = ImportImageAsLayerAsync();
            return;
        }
        if (_design.Layers.Count >= ContractInfo.MaxLayers)
        {
            Error = Loc.Instance["Studio_MaxLayers"];
            return;
        }
        Edit(() => _design.Layers.Add(NewLayer(kind)));
        RebuildRows(_design.Layers.Count - 1);
    }

    [RelayCommand]
    private void DuplicateLayer(LayerRow? row)
    {
        if (row != null) Selected = row;
        if (Selected == null || _design.Layers.Count >= ContractInfo.MaxLayers)
        {
            if (Selected != null) Error = Loc.Instance["Studio_MaxLayers"];
            return;
        }
        var index = _design.Layers.IndexOf(Selected.Layer);
        Edit(() =>
        {
            var copy = JsonClone.Clone(Selected.Layer);
            copy.Id = Guid.NewGuid().ToString("N");
            _design.Layers.Insert(Math.Max(0, index + 1), copy);
        });
        RebuildRows(Math.Min(_design.Layers.Count - 1, index + 1));
    }

    [RelayCommand]
    private void DeleteLayer(LayerRow? row)
    {
        if (row != null) Selected = row;
        if (Selected == null) return;
        var index = _design.Layers.IndexOf(Selected.Layer);
        Edit(() => _design.Layers.RemoveAt(index));
        RebuildRows(Math.Clamp(index, 0, _design.Layers.Count - 1));
    }

    [RelayCommand]
    private void ToggleLayer(LayerRow? row)
    {
        if (row == null) return;
        Edit(() => row.Layer.Visible = !row.Layer.Visible);
        row.Refresh();
    }

    [RelayCommand]
    private void MoveUp(LayerRow? row)
    {
        if (row == null) return;
        MoveDisplay(Rows.IndexOf(row), Rows.IndexOf(row) - 1);
    }

    [RelayCommand]
    private void MoveDown(LayerRow? row)
    {
        if (row == null) return;
        MoveDisplay(Rows.IndexOf(row), Rows.IndexOf(row) + 1);
    }

    [RelayCommand]
    private void MoveLayer(MoveRequest? request)
    {
        if (request == null) return;
        MoveDisplay(request.From, request.To);
    }

    [RelayCommand]
    private async Task CopyCodeAsync()
    {
        try
        {
            var code = await _core.EncodeShareCodeAsync(JsonClone.Clone(_design)).ConfigureAwait(true);
            _ui.Copy(code);
            _ui.Toast(Loc.Instance["Toast_Copied"]);
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    [RelayCommand]
    private async Task PasteCodeAsync() => await PasteCodeAsync(_ui.Paste()).ConfigureAwait(true);

    public async Task PasteCodeAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            _ui.Toast(Loc.Instance["Toast_NoClipboard"]);
            return;
        }
        try
        {
            var design = await _core.DecodeShareCodeAsync(code).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(design.Name)) design.Name = Loc.Instance["Name_NewCrosshair"];
            Load(design, saved: false);
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    [RelayCommand]
    private async Task ImportImageAsync()
    {
        if (Selected?.Layer.Kind != LayerKind.Image)
        {
            await ImportImageAsLayerAsync().ConfigureAwait(true);
            return;
        }
        var path = _ui.PickOpenFile(Loc.Instance["File_Png"]);
        if (path == null) return;
        try
        {
            var file = await _core.ImportImageAsync(path).ConfigureAwait(true);
            Edit(() => Selected.Layer.ImageFile = file);
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    [RelayCommand]
    private void SetZoom(string? zoom)
    {
        if (int.TryParse(zoom, out var value) && value is 1 or 2 or 4)
            Zoom = value;
    }

    [RelayCommand]
    private void SetBackground(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        Background = name;
        ScheduleRender(final: true);
    }

    partial void OnCustomBackgroundChanged(string value)
    {
        OnPropertyChanged(nameof(PreviewFill));
        ScheduleRender(final: true);
    }

    partial void OnBackgroundChanged(string value)
    {
        OnPropertyChanged(nameof(ShowCustomBackground));
        OnPropertyChanged(nameof(CheckerPreview));
        OnPropertyChanged(nameof(PreviewFill));
    }
    public bool ShowCustomBackground => Background == "custom";
    partial void OnZoomChanged(int value) => OnPropertyChanged(nameof(ShowPixelGrid));
    public bool ShowPixelGrid => Zoom == 4;
    partial void OnSelectedChanged(LayerRow? value) => NotifyAll();

    public void MoveDisplay(int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= Rows.Count || to >= Rows.Count) return;
        var id = Rows[from].Layer.Id;
        Edit(() =>
        {
            var modelFrom = _design.Layers.Count - 1 - from;
            var modelTo = _design.Layers.Count - 1 - to;
            var layer = _design.Layers[modelFrom];
            _design.Layers.RemoveAt(modelFrom);
            _design.Layers.Insert(modelTo, layer);
        });
        RebuildRows();
        Selected = Rows.FirstOrDefault(row => row.Layer.Id == id);
    }

    public async Task<bool> SaveCoreAsync(bool newId)
    {
        try
        {
            var copy = JsonClone.Clone(_design);
            if (string.IsNullOrWhiteSpace(copy.Name)) copy.Name = Loc.Instance["Name_NewCrosshair"];
            copy.Name = copy.Name.Trim();
            if (copy.Name.Length > 48) copy.Name = copy.Name[..48];
            if (newId)
            {
                copy.Id = Guid.NewGuid().ToString("N");
                copy.SourcePresetId = null;
            }
            var saved = await _core.SaveCrosshairAsync(copy).ConfigureAwait(true);
            Load(saved);
            _ui.Toast(Loc.Instance["Toast_Saved"]);
            return true;
        }
        catch (CoreException ex)
        {
            Error = ErrorText.For(ex.Code);
            return false;
        }
        catch (Exception)
        {
            Error = ErrorText.For(null);
            return false;
        }
    }

    private async Task ImportImageAsLayerAsync()
    {
        if (_design.Layers.Count >= ContractInfo.MaxLayers)
        {
            Error = Loc.Instance["Studio_MaxLayers"];
            return;
        }
        var path = _ui.PickOpenFile(Loc.Instance["File_Png"]);
        if (path == null) return;
        try
        {
            var file = await _core.ImportImageAsync(path).ConfigureAwait(true);
            Edit(() =>
            {
                var layer = NewLayer(LayerKind.Image);
                layer.ImageFile = file;
                _design.Layers.Add(layer);
            });
            RebuildRows(_design.Layers.Count - 1);
        }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    private void Edit(Action change)
    {
        if (_suspend)
        {
            change();
            return;
        }
        var before = JsonClone.Text(_design);
        change();
        var after = JsonClone.Text(_design);
        if (before == after) return;
        if (_coalesce)
        {
            if (!_pushed)
            {
                _undo.Push(before);
                _redo.Clear();
                _pushed = true;
            }
        }
        else
        {
            _undo.Push(before);
            _redo.Clear();
        }
        IsDirty = after != _baseline;
        Error = "";
        Selected?.Refresh();
        NotifyAll();
        SchedulePreview();
        ScheduleRender();
    }

    private void EditColor(string value, Action<string> apply)
    {
        var hex = NormalizeHex(value);
        if (hex == null) return;
        Edit(() => apply(hex));
    }

    private void Restore(Stack<string> from, Stack<string> to)
    {
        if (from.Count == 0) return;
        to.Push(JsonClone.Text(_design));
        var json = from.Pop();
        _suspend = true;
        _design = System.Text.Json.JsonSerializer.Deserialize<CrosshairDesign>(json, ContractJson.Options) ?? _design;
        var selected = Selected?.Layer.Id;
        RebuildRows();
        Selected = Rows.FirstOrDefault(row => row.Layer.Id == selected) ?? Rows.FirstOrDefault();
        IsDirty = JsonClone.Text(_design) != _baseline;
        _suspend = false;
        NotifyAll();
        NotifyUndo();
        SchedulePreview(final: true);
        ScheduleRender(final: true);
    }

    private void RebuildRows(int modelIndex = -1)
    {
        var selectedId = modelIndex >= 0 && modelIndex < _design.Layers.Count
            ? _design.Layers[modelIndex].Id
            : Selected?.Layer.Id;
        Rows.Clear();
        for (var i = _design.Layers.Count - 1; i >= 0; i--)
            Rows.Add(new LayerRow(_design.Layers[i]));
        Selected = Rows.FirstOrDefault(row => row.Layer.Id == selectedId) ?? Rows.FirstOrDefault();
        OnPropertyChanged(nameof(CanAddLayer));
    }

    private void SchedulePreview(bool final = false)
    {
        if (!LiveInGame || !_entered || IsMinimized || _suspend) return;
        var now = _clock();
        if (final)
        {
            _previews.SuppressPending(now);
            _ = SendPreviewAsync(JsonClone.Clone(_design));
            return;
        }
        var send = _previews.Submit(JsonClone.Clone(_design), now, out var wait);
        if (send != null) _ = SendPreviewAsync(send);
        else _ = WaitPreviewAsync(wait, _previews.Version);
    }

    private async Task WaitPreviewAsync(TimeSpan wait, int version)
    {
        try { await _delay.Delay(wait, CancellationToken.None).ConfigureAwait(true); }
        catch (Exception) { return; }
        var send = _previews.TakePending(_clock(), version);
        if (send != null) await SendPreviewAsync(send).ConfigureAwait(true);
    }

    private async Task SendPreviewAsync(CrosshairDesign? design)
    {
        try { await _core.PreviewAsync(design).ConfigureAwait(true); }
        catch (CoreException ex) { Error = ErrorText.For(ex.Code); }
        catch (Exception) { Error = ErrorText.For(null); }
    }

    private void ScheduleRender(bool final = false)
    {
        if (IsMinimized || _suspend) return;
        var now = _clock();
        if (final)
        {
            _renders.SuppressPending(now);
            _ = SendRenderAsync(JsonClone.Clone(_design));
            return;
        }
        var send = _renders.Submit(JsonClone.Clone(_design), now, out var wait);
        if (send != null) _ = SendRenderAsync(send);
        else _ = WaitRenderAsync(wait, _renders.Version);
    }

    private async Task WaitRenderAsync(TimeSpan wait, int version)
    {
        try { await _delay.Delay(wait, CancellationToken.None).ConfigureAwait(true); }
        catch (Exception) { return; }
        var send = _renders.TakePending(_clock(), version);
        if (send != null) await SendRenderAsync(send).ConfigureAwait(true);
    }

    private async Task SendRenderAsync(CrosshairDesign design)
    {
        var generation = ++_renderGeneration;
        try
        {
            var png = await _core.RenderPngAsync(new RenderRequest
            {
                Design = design,
                CanvasSize = 256,
                Background = null,
            }).ConfigureAwait(true);
            if (generation == _renderGeneration) PreviewPng = png;
        }
        catch (CoreException ex) { if (generation == _renderGeneration) Error = ErrorText.For(ex.Code); }
        catch (Exception) { if (generation == _renderGeneration) Error = ErrorText.For(null); }
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(DesignName));
        OnPropertyChanged(nameof(AntiAlias));
        OnPropertyChanged(nameof(LayerName));
        OnPropertyChanged(nameof(Length));
        OnPropertyChanged(nameof(Thickness));
        OnPropertyChanged(nameof(Gap));
        OnPropertyChanged(nameof(OpacityPercent));
        OnPropertyChanged(nameof(OffsetX));
        OnPropertyChanged(nameof(OffsetY));
        OnPropertyChanged(nameof(Rotation));
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(Fill));
        OnPropertyChanged(nameof(FillColor));
        OnPropertyChanged(nameof(Outline));
        OnPropertyChanged(nameof(OutlineThickness));
        OnPropertyChanged(nameof(OutlineColor));
        OnPropertyChanged(nameof(ImageScale));
        OnPropertyChanged(nameof(ArmTop));
        OnPropertyChanged(nameof(ArmRight));
        OnPropertyChanged(nameof(ArmBottom));
        OnPropertyChanged(nameof(ArmLeft));
        OnPropertyChanged(nameof(HasLayer));
        OnPropertyChanged(nameof(ShowArms));
        OnPropertyChanged(nameof(ShowGap));
        OnPropertyChanged(nameof(ShowThickness));
        OnPropertyChanged(nameof(ShowOutline));
        OnPropertyChanged(nameof(ShowImage));
        OnPropertyChanged(nameof(ShowShape));
        OnPropertyChanged(nameof(ShowFill));
        OnPropertyChanged(nameof(LengthLabelKey));
        OnPropertyChanged(nameof(CanAddLayer));
        OnPropertyChanged(nameof(AllLayersHidden));
        NotifyUndo();
    }

    private void NotifyUndo()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private bool HasArm(ArmMask mask) => Selected?.Layer.Arms.HasFlag(mask) == true;

    private void SetArm(ArmMask mask, bool on) => Edit(() =>
    {
        if (Selected == null) return;
        Selected.Layer.Arms = on ? Selected.Layer.Arms | mask : Selected.Layer.Arms & ~mask;
    });

    private static CrosshairLayer NewLayer(LayerKind kind) => kind switch
    {
        LayerKind.Dot => new CrosshairLayer { Name = Loc.Instance["Kind_Dot"], Kind = kind, Length = 4, Thickness = 2, Gap = 0 },
        LayerKind.Ring => new CrosshairLayer { Name = Loc.Instance["Kind_Ring"], Kind = kind, Length = 16, Thickness = 2, Gap = 0 },
        LayerKind.Box => new CrosshairLayer { Name = Loc.Instance["Kind_Box"], Kind = kind, Length = 16, Thickness = 2, Gap = 6 },
        LayerKind.Image => new CrosshairLayer { Name = Loc.Instance["Kind_Image"], Kind = kind },
        _ => new CrosshairLayer { Name = Loc.Instance["Kind_Arms"], Kind = LayerKind.Arms, Length = 8, Thickness = 2, Gap = 4 },
    };

    internal static string? NormalizeHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim();
        if (!text.StartsWith('#')) text = "#" + text;
        if (text.Length != 7) return null;
        foreach (var ch in text[1..])
        {
            if (!Uri.IsHexDigit(ch)) return null;
        }
        return text.ToUpperInvariant();
    }
}

public sealed class MoveRequest
{
    public int From { get; init; }
    public int To { get; init; }
}
