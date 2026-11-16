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
                var start = DateTime.UtcNow;
                while (viewModel.Studio.PreviewPng == null && DateTime.UtcNow - start < TimeSpan.FromSeconds(3))
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
            await PumpAsync().ConfigureAwait(true);
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
