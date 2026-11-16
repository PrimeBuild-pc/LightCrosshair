using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace LightCrosshair.UI.Controls;

/// <summary>Uniform wrap panel that generates only the rows intersecting the viewport.</summary>
public class UniformWrapPanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(nameof(ItemWidth), typeof(double), typeof(UniformWrapPanel), new PropertyMetadata(128d, (d, _) => ((UniformWrapPanel)d).Reset()));
    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(nameof(ItemHeight), typeof(double), typeof(UniformWrapPanel), new PropertyMetadata(168d, (d, _) => ((UniformWrapPanel)d).Reset()));

    private Size _extent;
    private Size _viewport = new(1, 1);
    private Point _offset;
    private Size _lastMeasure = new(1, 1);
    private int _measureDepth;
    private bool _scrollQueued;

    public double ItemWidth { get => (double)GetValue(ItemWidthProperty); set => SetValue(ItemWidthProperty, value); }
    public double ItemHeight { get => (double)GetValue(ItemHeightProperty); set => SetValue(ItemHeightProperty, value); }

    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; } = true;
    public double ExtentWidth => _extent.Width;
    public double ExtentHeight => _extent.Height;
    public double ViewportWidth => _viewport.Width;
    public double ViewportHeight => _viewport.Height;
    public double HorizontalOffset => _offset.X;
    public double VerticalOffset => _offset.Y;
    public ScrollViewer? ScrollOwner { get; set; }

    public void LineUp() => SetVerticalOffset(VerticalOffset - 16);
    public void LineDown() => SetVerticalOffset(VerticalOffset + 16);
    public void LineLeft() { }
    public void LineRight() { }
    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);
    public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);
    public void PageLeft() { }
    public void PageRight() { }
    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - ItemHeight);
    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + ItemHeight);
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }
    public void SetHorizontalOffset(double offset) { }

    public void SetVerticalOffset(double offset)
    {
        var max = Math.Max(0, ExtentHeight - ViewportHeight);
        var next = Math.Clamp(offset, 0, max);
        if (Math.Abs(next - _offset.Y) < 0.1) return;
        _offset.Y = next;
        InvalidateMeasure();
        QueueScrollInvalidation();
    }

    public Rect MakeVisible(Visual visual, Rect rectangle) => rectangle;

    private void Reset() => InvalidateMeasure();

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_measureDepth > 0) return _lastMeasure;
        _measureDepth++;
        try
        {
            var count = ItemsControl.GetItemsOwner(this)?.Items.Count ?? 0;
            var width = availableSize.Width;
            if (double.IsInfinity(width) || width <= 0) width = Math.Max(ItemWidth, ScrollOwner?.ViewportWidth ?? ItemWidth);
            if (ScrollOwner != null && ScrollOwner.ViewportWidth > 1)
                width = Math.Min(width, ScrollOwner.ViewportWidth);
            var columns = Math.Max(1, (int)Math.Floor(width / ItemWidth));
            var rows = count == 0 ? 0 : (int)Math.Ceiling(count / (double)columns);
            _extent = new Size(columns * ItemWidth, rows * ItemHeight);

            var viewportHeight = ScrollOwner?.ViewportHeight ?? 0;
            if (viewportHeight <= 0 || double.IsInfinity(viewportHeight))
                viewportHeight = double.IsInfinity(availableSize.Height) || availableSize.Height <= 0 ? _extent.Height : availableSize.Height;
            _viewport = new Size(width, Math.Max(1, viewportHeight));

            var realizeAll = ScrollOwner == null || viewportHeight >= _extent.Height;
            var firstRow = realizeAll ? 0 : Math.Max(0, (int)Math.Floor(_offset.Y / ItemHeight));
            var visibleRows = realizeAll ? Math.Max(rows, 0) : Math.Max(1, (int)Math.Ceiling(_viewport.Height / ItemHeight) + 1);
            var first = Math.Min(count, firstRow * columns);
            var last = realizeAll ? count : Math.Min(count, (firstRow + visibleRows) * columns);
            Realize(first, last);

            _lastMeasure = new Size(width, realizeAll ? _extent.Height : _viewport.Height);
            QueueScrollInvalidation();
            return _lastMeasure;
        }
        finally
        {
            _measureDepth--;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, (int)(Math.Max(finalSize.Width, ItemWidth) / ItemWidth));
        foreach (UIElement child in InternalChildren)
        {
            var index = GeneratorImpl.IndexFromContainer(child);
            if (index < 0) continue;
            var column = index % columns;
            var row = index / columns;
            child.Arrange(new Rect(column * ItemWidth, row * ItemHeight - _offset.Y, ItemWidth, ItemHeight));
        }
        return finalSize;
    }

    private ItemContainerGenerator GeneratorImpl => (ItemContainerGenerator)ItemContainerGenerator;

    private void Realize(int first, int last)
    {
        var generator = (IItemContainerGenerator)ItemContainerGenerator;
        for (var i = InternalChildren.Count - 1; i >= 0; i--)
        {
            var index = GeneratorImpl.IndexFromContainer(InternalChildren[i]);
            if (index >= first && index < last) continue;
            if (index >= 0)
                generator.Remove(generator.GeneratorPositionFromIndex(index), 1);
            RemoveInternalChildRange(i, 1);
        }

        for (var index = first; index < last; index++)
        {
            if (HasContainer(index)) continue;
            UIElement? child = null;
            var position = generator.GeneratorPositionFromIndex(index);
            using (generator.StartAt(position, GeneratorDirection.Forward, true))
            {
                child = generator.GenerateNext(out var fresh) as UIElement;
                if (child != null && fresh)
                {
                    AddInternalChild(child);
                    generator.PrepareItemContainer(child);
                }
            }
            child?.Measure(new Size(ItemWidth, ItemHeight));
        }
    }

    private bool HasContainer(int index)
    {
        foreach (UIElement child in InternalChildren)
        {
            if (GeneratorImpl.IndexFromContainer(child) == index) return true;
        }
        return false;
    }

    private void QueueScrollInvalidation()
    {
        if (ScrollOwner == null || _scrollQueued) return;
        _scrollQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            _scrollQueued = false;
            ScrollOwner?.InvalidateScrollInfo();
        });
    }
}
