using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using LightCrosshair.UI.Localization;
using LightCrosshair.UI.Services;
using LightCrosshair.UI.ViewModels;

namespace LightCrosshair.UI;

public partial class MainWindow : Window
{
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
                    if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                    Activate();
                };
                viewModel.CloseRequested += (_, _) => Close();
            }
        };
        Closing += (_, _) => WindowPlacementStore.Save(this);
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
