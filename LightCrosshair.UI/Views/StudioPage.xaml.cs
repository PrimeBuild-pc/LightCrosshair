using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LightCrosshair.UI.ViewModels;

namespace LightCrosshair.UI.Views;

public partial class StudioPage : UserControl
{
    private Point? _dragStart;

    private StudioViewModel? _studio;

    public StudioPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_studio != null) _studio.PropertyChanged -= OnStudioChanged;
            _studio = DataContext as StudioViewModel;
            if (_studio != null) _studio.PropertyChanged += OnStudioChanged;
            UpdatePreviewLayout();
        };
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        ReportPreviewSize();
        UpdatePreviewLayout();
    }

    private void OnStudioChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StudioViewModel.Zoom)) UpdatePreviewLayout();
    }

    private double Dpi => VisualTreeHelper.GetDpi(this).DpiScaleX;

    // Fit compares the 256 px render against the preview in screen pixels.
    private void ReportPreviewSize() => _studio?.SetPreviewSize(PreviewScroll.ActualWidth * Dpi, PreviewScroll.ActualHeight * Dpi);

    private void OnPreviewSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ReportPreviewSize();
        UpdatePreviewLayout();
    }

    /// <summary>
    /// One render pixel = Zoom screen pixels at any DPI, and the image starts on a whole screen pixel.
    /// A half-pixel centering offset (odd free space) made nearest-neighbor scaling double or drop rows,
    /// so the preview looked crooked at some window sizes while the overlay was exact.
    /// </summary>
    internal void UpdatePreviewLayout()
    {
        if (_studio == null) return;
        double dpi = Dpi, size = 256d * Math.Max(1, _studio.Zoom) / dpi;
        PreviewCanvas.Width = PreviewCanvas.Height = size;
        PreviewShift.X = Math.Floor(Math.Max(0, PreviewScroll.ViewportWidth - size) * dpi / 2) / dpi;
        PreviewShift.Y = Math.Floor(Math.Max(0, PreviewScroll.ViewportHeight - size) * dpi / 2) / dpi;
        if (PixelGrid.Fill is DrawingBrush grid && !grid.IsFrozen) grid.Viewport = new Rect(0, 0, 4 / dpi, 4 / dpi);
    }

    // A zoom that overflows the preview starts centered on the crosshair, not at the top-left corner.
    private void OnPreviewScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        double dpi = Dpi;
        double Snap(double value) => Math.Round(value * dpi) / dpi; // scroll by whole screen pixels only
        if (e.ExtentWidthChange != 0 || e.ExtentHeightChange != 0 || e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)
        {
            if (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0) UpdatePreviewLayout();
            PreviewScroll.ScrollToHorizontalOffset(Snap(Math.Max(0, (PreviewScroll.ExtentWidth - PreviewScroll.ViewportWidth) / 2)));
            PreviewScroll.ScrollToVerticalOffset(Snap(Math.Max(0, (PreviewScroll.ExtentHeight - PreviewScroll.ViewportHeight) / 2)));
            return;
        }
        if (Math.Abs(Snap(e.HorizontalOffset) - e.HorizontalOffset) > 1e-6) PreviewScroll.ScrollToHorizontalOffset(Snap(e.HorizontalOffset));
        if (Math.Abs(Snap(e.VerticalOffset) - e.VerticalOffset) > 1e-6) PreviewScroll.ScrollToVerticalOffset(Snap(e.VerticalOffset));
    }

    private void OnLayerPress(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) != null) return;
        _dragStart = e.GetPosition(LayerList);
    }

    private void OnLayerDrag(object sender, MouseEventArgs e)
    {
        if (_dragStart == null || e.LeftButton != MouseButtonState.Pressed) return;
        var position = e.GetPosition(LayerList);
        if (Math.Abs(position.Y - _dragStart.Value.Y) < 8) return;
        if (ItemAt(_dragStart.Value) is not LayerRow row) return;
        _dragStart = null;
        DragDrop.DoDragDrop(LayerList, row, DragDropEffects.Move);
    }

    private void OnLayerDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(LayerRow)) is not LayerRow row) return;
        if (ItemAt(e.GetPosition(LayerList)) is not LayerRow target) return;
        if (DataContext is not StudioViewModel studio) return;
        studio.MoveDisplay(studio.Rows.IndexOf(row), studio.Rows.IndexOf(target));
    }

    private LayerRow? ItemAt(Point position)
    {
        var element = LayerList.InputHitTest(position) as DependencyObject;
        var item = FindAncestor<ListBoxItem>(element);
        return item?.DataContext as LayerRow;
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node != null)
        {
            if (node is T match) return match;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }
}
