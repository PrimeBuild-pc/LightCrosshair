using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LightCrosshair.UI.ViewModels;

namespace LightCrosshair.UI.Views;

public partial class StudioPage : UserControl
{
    private Point? _dragStart;

    public StudioPage() => InitializeComponent();

    private void OnPreviewSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is StudioViewModel studio)
            studio.SetPreviewSize(e.NewSize.Width, e.NewSize.Height);
    }

    // A zoom that overflows the preview starts centered on the crosshair, not at the top-left corner.
    private void OnPreviewScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentWidthChange == 0 && e.ExtentHeightChange == 0 && e.ViewportWidthChange == 0 && e.ViewportHeightChange == 0) return;
        PreviewScroll.ScrollToHorizontalOffset(Math.Max(0, (PreviewScroll.ExtentWidth - PreviewScroll.ViewportWidth) / 2));
        PreviewScroll.ScrollToVerticalOffset(Math.Max(0, (PreviewScroll.ExtentHeight - PreviewScroll.ViewportHeight) / 2));
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
