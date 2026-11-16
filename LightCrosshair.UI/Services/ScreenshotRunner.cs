using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LightCrosshair.UI.ViewModels;

namespace LightCrosshair.UI.Services;

public static class ScreenshotRunner
{
    public static async Task CaptureAsync(Window window, MainViewModel viewModel, string directory)
    {
        Directory.CreateDirectory(directory);
        window.WindowState = WindowState.Normal;
        window.Width = 1180;
        window.Height = 740;
        await PumpAsync().ConfigureAwait(true);
        foreach (var page in new[] { "studio", "library", "games", "hotkeys", "settings" })
        {
            if (viewModel.Page != page)
                await viewModel.NavigateCommand.ExecuteAsync(page).ConfigureAwait(true);
            await PumpAsync().ConfigureAwait(true);
            if (page == "studio")
            {
                var seen = viewModel.Studio.PreviewPng;
                viewModel.Studio.SetBackgroundCommand.Execute("mid");
                var start = DateTime.UtcNow;
                while ((viewModel.Studio.PreviewPng == null || ReferenceEquals(viewModel.Studio.PreviewPng, seen))
                    && DateTime.UtcNow - start < TimeSpan.FromSeconds(3))
                    await PumpAsync().ConfigureAwait(true);
            }
            if (page == "library")
            {
                var start = DateTime.UtcNow;
                while (DateTime.UtcNow - start < TimeSpan.FromSeconds(3))
                {
                    var pending = viewModel.Library.Mine.Concat(viewModel.Library.Presets).Any(card => card.Png == null);
                    if (!pending) break;
                    await PumpAsync().ConfigureAwait(true);
                }
            }
            HideScrollBars(window);
            for (var pass = 0; pass < 6 && HideCutRows(window); pass++)
                window.UpdateLayout();
            Save(window, Path.Combine(directory, "ui-" + page + ".png"));
        }
    }

    private static void HideScrollBars(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is System.Windows.Controls.ScrollViewer viewer)
            {
                viewer.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Hidden;
                viewer.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Hidden;
            }
            HideScrollBars(child);
        }
    }

    /// <summary>Hides a row that crosses the window edge so the capture ends on a whole control.</summary>
    private static bool HideCutRows(Window window)
    {
        var limit = window.ActualHeight - 12;
        return HideCutRows(window, window, limit);
    }

    private static bool HideCutRows(Window window, DependencyObject root, double limit)
    {
        var hid = false;
        if (root is System.Windows.Controls.Panel panel)
        {
            var cut = -1;
            for (var i = 0; i < panel.Children.Count; i++)
            {
                if (panel.Children[i] is not FrameworkElement child || child.ActualHeight is < 8 or > 220 || !child.IsVisible) continue;
                var origin = child.TransformToAncestor(window).Transform(new System.Windows.Point(0, 0));
                var bottom = origin.Y + child.ActualHeight + child.Margin.Bottom;
                if (origin.Y < limit && bottom > limit)
                {
                    cut = i;
                    if (i > 0 && panel.Children[i - 1] is System.Windows.Controls.TextBlock heading && heading.ActualHeight < 64)
                        cut = i - 1;
                    break;
                }
            }
            if (cut >= 0)
            {
                for (var i = cut; i < panel.Children.Count; i++)
                {
                    if (panel.Children[i] is UIElement child && child.Visibility != Visibility.Collapsed)
                    {
                        child.Visibility = Visibility.Collapsed;
                        hid = true;
                    }
                }
            }
        }
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
            hid |= HideCutRows(window, VisualTreeHelper.GetChild(root, i), limit);
        return hid;
    }

    private static void Save(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1180, 740, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static async Task PumpAsync()
    {
        await Dispatcher.Yield(DispatcherPriority.Background);
        await Task.Delay(40).ConfigureAwait(true);
    }
}
