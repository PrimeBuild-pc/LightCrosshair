using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LightCrosshair.UI.Localization;
using LightCrosshair.UI.Services;
using LightCrosshair.UI.ViewModels;
using LightCrosshair.UI.Views;

namespace LightCrosshair.UI;

public partial class MainWindow : Window
{
    private DispatcherTimer? _parkTimer;
    internal TimeSpan ParkDuration { get; set; } = ParkPolicy.Duration;

    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) =>
        {
            MaxGlyph.Text = WindowState == WindowState.Maximized ? Icons.Restore : Icons.Maximize;
            System.Windows.Automation.AutomationProperties.SetName(MaxButton, WindowState == WindowState.Maximized ? Loc.Instance["Window_Restore"] : Loc.Instance["Window_Maximize"]);
            if (DataContext is MainViewModel viewModel)
                viewModel.SetMinimized(WindowState == WindowState.Minimized);
        };
        Loaded += (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ActivateRequested += (_, _) =>
                {
                    var requested = Environment.TickCount64;
                    if (!IsVisible) Show();
                    if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                    Wake();
                    Activate();
                    Dispatcher.BeginInvoke(DispatcherPriority.Render, () => OpenTiming.NoteWake(requested));
                };
                viewModel.CloseRequested += (_, _) =>
                {
                    viewModel.AllowExit = true;
                    Close();
                };
            }
        };
        Activated += (_, _) =>
        {
            if (IsVisible) Wake();
        };
        ContentRendered += (_, _) => OpenTiming.NoteFirstFrame();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) FindChild<LibraryPage>(this)?.ReloadVisible();
        };
        Closing += OnClosing;
        SourceInitialized += (_, _) =>
        {
            var source = (HwndSource?)PresentationSource.FromVisual(this);
            source?.AddHook(WndProc);
        };
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    public void ShowUsePopup(FrameworkElement anchor) => UsePopup.PlacementTarget = anchor;

    public void AllowClose()
    {
        if (DataContext is MainViewModel viewModel) viewModel.AllowExit = true;
    }

    private void OnUseOpened(object sender, EventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.Library.ShowUseName)
        {
            UseNameBox.Focus();
            UseNameBox.SelectAll();
        }
        else if (UsePopup.Child is UIElement child)
            child.Focus();
    }

    private void OnUseKey(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        if (e.Key == Key.Escape)
        {
            viewModel.Library.CancelUseCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            viewModel.Library.ConfirmUseCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        WindowPlacementStore.Save(this);
        if (DataContext is not MainViewModel viewModel || viewModel.AllowExit || Application.Current?.MainWindow != this) return;
        e.Cancel = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (!IsVisible) return;
            Hide();
            _parkTimer?.Stop();
            viewModel.Park();
            _parkTimer = new DispatcherTimer { Interval = ParkDuration };
            _parkTimer.Tick += (_, _) =>
            {
                _parkTimer?.Stop();
                viewModel.AllowExit = true;
                Close();
            };
            _parkTimer.Start();
            if (Environment.GetEnvironmentVariable("LIGHTCROSSHAIR_TIMING") == "1")
                ScheduleTimingWake();
        });
    }

    private void ScheduleTimingWake()
    {
        var wake = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        wake.Tick += (_, _) =>
        {
            wake.Stop();
            var requested = Environment.TickCount64;
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Wake();
            Activate();
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, () => OpenTiming.NoteWake(requested));
        };
        wake.Start();
    }

    private void Wake()
    {
        _parkTimer?.Stop();
        if (DataContext is MainViewModel viewModel)
            viewModel.Wake();
    }

    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found) return found;
            var nested = FindChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0024)
        {
            ApplyMaxBounds(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static void ApplyMaxBounds(IntPtr hwnd, IntPtr lParam)
    {
        var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var monitor = MonitorFromWindow(hwnd, 2);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo)) return;
        var work = monitorInfo.Work;
        var bounds = monitorInfo.Monitor;
        info.MaxPosition.X = Math.Abs(work.Left - bounds.Left);
        info.MaxPosition.Y = Math.Abs(work.Top - bounds.Top);
        info.MaxSize.X = Math.Abs(work.Right - work.Left);
        info.MaxSize.Y = Math.Abs(work.Bottom - work.Top);
        info.MaxTrack.X = info.MaxSize.X;
        info.MaxTrack.Y = info.MaxSize.Y;
        Marshal.StructureToPtr(info, lParam, true);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrack;
        public Point MaxTrack;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }
}
