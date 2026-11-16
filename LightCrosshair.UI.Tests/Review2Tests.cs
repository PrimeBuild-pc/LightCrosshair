using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LightCrosshair.UI;
using LightCrosshair.UI.Controls;
using LightCrosshair.UI.Core;
using LightCrosshair.UI.Services;
using LightCrosshair.UI.ViewModels;
using LightCrosshair.UI.Views;
using Xunit;

namespace LightCrosshair.UI.Tests;

public class Review2Tests
{
    [Fact]
    public void PlacementFileFollowsTheDataRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "lc-ui-place-" + Guid.NewGuid().ToString("N"));
        var real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LightCrosshair", "v2", "ui-placement.json");
        var previous = Environment.GetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT");
        var realStamp = File.Exists(real) ? File.GetLastWriteTimeUtc(real) : (DateTime?)null;
        try
        {
            Environment.SetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT", "  ");
            Assert.Equal(real, WindowPlacementStore.FilePath);

            Environment.SetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT", root);
            Assert.Equal(Path.Combine(root, "ui-placement.json"), WindowPlacementStore.FilePath);
            Sta(() =>
            {
                var window = new Window { Left = 30, Top = 40, Width = 900, Height = 640 };
                WindowPlacementStore.Save(window);
                var loaded = WindowPlacementStore.Load();
                Assert.NotNull(loaded);
                Assert.Equal(30, loaded.Left);
                Assert.Equal(40, loaded.Top);
                Assert.Equal(900, loaded.Width);
                Assert.Equal(640, loaded.Height);
                Assert.False(loaded.Maximized);
            });
            Assert.True(File.Exists(Path.Combine(root, "ui-placement.json")));
            if (realStamp == null) Assert.False(File.Exists(real));
            else Assert.Equal(realStamp.Value, File.GetLastWriteTimeUtc(real));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LIGHTCROSSHAIR_DATA_ROOT", previous);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void FitPicksTheLargestIntegerZoomThatFits()
    {
        Assert.Equal(1, StudioViewModel.FitZoom(0, 0));
        Assert.Equal(1, StudioViewModel.FitZoom(511, 800));
        Assert.Equal(2, StudioViewModel.FitZoom(520, 700));
        Assert.Equal(3, StudioViewModel.FitZoom(800, 780));
        Assert.Equal(4, StudioViewModel.FitZoom(1100, 1024));
    }

    [Fact]
    public async Task FitStaysInsideThePreviewAtTheReviewSizes()
    {
        var core = new FakeCoreClient(new InlineMarshaler());
        await core.ConnectAsync();
        Sta(() =>
        {
            var main = new MainViewModel(core, new FakeUi(), new LaunchOptions(UiPages.Studio, null, true, null));
            var window = new MainWindow { DataContext = main, ShowInTaskbar = false };
            window.Show();
            try
            {
                main.StartAsync().GetAwaiter().GetResult();
                AssertFit(window, main, 1000, 640);
                AssertFit(window, main, 1180, 740);
                window.WindowState = WindowState.Maximized;
                Settle(window);
                var scroll = Preview(window);
                Assert.Equal("fit", main.Studio.ZoomMode);
                Assert.Equal(StudioViewModel.FitZoom(scroll.ActualWidth, scroll.ActualHeight), main.Studio.Zoom);
                Assert.NotEqual(Visibility.Visible, scroll.ComputedVerticalScrollBarVisibility);
                Assert.NotEqual(Visibility.Visible, scroll.ComputedHorizontalScrollBarVisibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SidebarMarkUsesTheDpiFrameAndHardEdges()
    {
        Assert.Equal(20, CrispIcon.FrameForScale(1));
        Assert.Equal(24, CrispIcon.FrameForScale(1.25));
        Assert.Equal(32, CrispIcon.FrameForScale(1.5));
        Assert.Equal(40, CrispIcon.FrameForScale(2));
        Sta(() =>
        {
            foreach (var scale in new[] { 1d, 1.5 })
            {
                var icon = new CrispIcon();
                icon.Apply(scale);
                var source = (BitmapSource)icon.Source;
                Assert.Equal(CrispIcon.FrameForScale(scale), source.PixelWidth);
                var pixels = new byte[source.PixelWidth * source.PixelHeight * 4];
                source.CopyPixels(pixels, source.PixelWidth * 4, 0);
                var colored = 0;
                for (var i = 0; i < pixels.Length; i += 4)
                {
                    Assert.True(pixels[i + 3] is 0 or 255);
                    if (pixels[i + 3] == 0) continue;
                    colored++;
                    Assert.Equal(42, pixels[i]);
                    Assert.Equal(42, pixels[i + 1]);
                    Assert.Equal(255, pixels[i + 2]);
                }
                Assert.True(colored > 10);
            }
        });
    }

    [Fact]
    public async Task PreviewScrollsOnlyWhenZoomExceedsTheArea()
    {
        var core = new FakeCoreClient(new InlineMarshaler());
        await core.ConnectAsync();
        Sta(() =>
        {
            var studio = new StudioViewModel(core, new FakeUi());
            studio.Load(core.Snapshot!.Store.Crosshairs[0]);
            var page = new StudioPage { DataContext = studio };
            var window = new Window
            {
                Content = page,
                Width = 1180,
                Height = 652,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
            };
            window.Show();
            try
            {
                Settle(page);
                var scroll = (ScrollViewer)page.FindName("PreviewScroll")!;
                AssertBarsFollowExtent(scroll, page, studio, 1);
                AssertBarsFollowExtent(scroll, page, studio, 2);
                Assert.True(scroll.ExtentHeight > scroll.ViewportHeight);
                Assert.Equal(Visibility.Visible, scroll.ComputedVerticalScrollBarVisibility);
                Assert.NotEqual(Visibility.Visible, scroll.ComputedHorizontalScrollBarVisibility);
                AssertBarsFollowExtent(scroll, page, studio, 4);
                Assert.Equal(Visibility.Visible, scroll.ComputedVerticalScrollBarVisibility);
                Assert.Equal(Visibility.Visible, scroll.ComputedHorizontalScrollBarVisibility);

                window.Height = 900;
                AssertBarsFollowExtent(scroll, page, studio, 2);
                Assert.True(scroll.ExtentHeight <= scroll.ViewportHeight + 1);
                Assert.NotEqual(Visibility.Visible, scroll.ComputedVerticalScrollBarVisibility);
                Assert.NotEqual(Visibility.Visible, scroll.ComputedHorizontalScrollBarVisibility);

                Assert.False(studio.AllLayersHidden);
                foreach (var row in studio.Rows.ToList()) studio.ToggleLayerCommand.Execute(row);
                Settle(page);
                Assert.True(studio.AllLayersHidden);
                var hint = Find<TextBlock>(page).Single(block => block.Text == "All layers are hidden");
                Assert.True(hint.IsVisible);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void AssertFit(MainWindow window, MainViewModel main, double width, double height)
    {
        window.WindowState = WindowState.Normal;
        window.Width = width;
        window.Height = height;
        Settle(window);
        var scroll = Preview(window);
        Assert.Equal("fit", main.Studio.ZoomMode);
        Assert.Equal(StudioViewModel.FitZoom(scroll.ActualWidth, scroll.ActualHeight), main.Studio.Zoom);
        Assert.NotEqual(Visibility.Visible, scroll.ComputedVerticalScrollBarVisibility);
        Assert.NotEqual(Visibility.Visible, scroll.ComputedHorizontalScrollBarVisibility);
    }

    private static ScrollViewer Preview(MainWindow window)
    {
        var page = Find<StudioPage>(window).Single();
        return (ScrollViewer)page.FindName("PreviewScroll")!;
    }

    private static void AssertBarsFollowExtent(ScrollViewer scroll, StudioPage page, StudioViewModel studio, int zoom)
    {
        studio.SetZoomCommand.Execute(zoom.ToString());
        Settle(page);
        var detail = $"zoom {zoom} viewport {scroll.ViewportWidth:0}x{scroll.ViewportHeight:0} extent {scroll.ExtentWidth:0}x{scroll.ExtentHeight:0}";
        var vertical = scroll.ExtentHeight > scroll.ViewportHeight + 1;
        var horizontal = scroll.ExtentWidth > scroll.ViewportWidth + 1;
        Assert.True((scroll.ComputedVerticalScrollBarVisibility == Visibility.Visible) == vertical, detail);
        Assert.True((scroll.ComputedHorizontalScrollBarVisibility == Visibility.Visible) == horizontal, detail);
        var image = Find<Image>(scroll).Single();
        var center = image.TranslatePoint(new Point(image.ActualWidth / 2, image.ActualHeight / 2), scroll);
        if (!horizontal) Assert.InRange(Math.Abs(center.X - scroll.ViewportWidth / 2), 0, 2);
        if (!vertical) Assert.InRange(Math.Abs(center.Y - scroll.ViewportHeight / 2), 0, 2);
    }

    private static void Settle(FrameworkElement element)
    {
        for (var i = 0; i < 6; i++)
        {
            element.UpdateLayout();
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
    }

    private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Find<T>(child)) yield return nested;
        }
    }

    private static Dispatcher? _dispatcher;

    private static void Sta(Action action)
    {
        if (_dispatcher == null)
        {
            using var ready = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                var app = new LightCrosshair.UI.App();
                try { app.InitializeComponent(); }
                catch (InvalidOperationException) { }
                if (!app.Resources.Contains("Vis"))
                    throw new InvalidOperationException("App resources are not loaded.");
                _dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            ready.Wait();
        }

        Exception? error = null;
        _dispatcher!.Invoke(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        if (error != null) throw error;
    }
}
