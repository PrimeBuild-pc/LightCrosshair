namespace LightCrosshair.UI.ViewModels;

// A page projection of the shared settings editor: one debounce/save coordinator
// prevents unrelated settings and FPS edits overwriting each other's snapshots.
public sealed class FpsOverlayViewModel(SettingsViewModel editor)
{
    // CONTRACT-REQUEST: add UiPages.FpsOverlay = "fps". This page-local id keeps
    // navigation additive without editing the shared contract package.
    public const string PageId = "fps";
    public SettingsViewModel Editor { get; } = editor;
}
