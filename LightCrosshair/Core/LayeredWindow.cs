using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LightCrosshair.Core;

internal class LayeredWindow : Form
{
    public long RedrawCount { get; private set; }
    public event Action? DisplayChanged;
    public LayeredWindow()
    {
        Text = "LightCrosshair Core"; FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None; ClientSize = new(1, 1);
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x20 | 0x8 | 0x80 | 0x08000000; return cp; }
    }
    public void Present(Rectangle bounds, Action<Graphics> draw)
    {
        IntPtr screen = GetDC(IntPtr.Zero), dc = IntPtr.Zero, dib = IntPtr.Zero, previous = IntPtr.Zero;
        try
        {
            dc = CreateCompatibleDC(screen);
            var info = new BitmapInfo { Size = (uint)Marshal.SizeOf<BitmapInfo>(), Width = bounds.Width, Height = -bounds.Height, Planes = 1, BitCount = 32, SizeImage = (uint)(bounds.Width * bounds.Height * 4) };
            dib = CreateDIBSection(screen, ref info, 0, out var pixels, IntPtr.Zero, 0);
            if (dc == IntPtr.Zero || dib == IntPtr.Zero) throw new Win32Exception();
            previous = SelectObject(dc, dib);
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height, bounds.Width * 4, PixelFormat.Format32bppPArgb, pixels))
            using (var graphics = Graphics.FromImage(bitmap))
            { graphics.Clear(Color.Transparent); draw(graphics); graphics.Flush(); }
            Point destination = bounds.Location, source = Point.Empty;
            Size size = bounds.Size;
            var blend = new Blend { SourceConstantAlpha = 255, AlphaFormat = 1 }; // AC_SRC_OVER=0, AC_SRC_ALPHA=1
            if (!UpdateLayeredWindow(Handle, screen, ref destination, ref size, dc, ref source, 0, ref blend, 2)) throw new Win32Exception();
            RedrawCount++;
        }
        finally
        {
            if (previous != IntPtr.Zero) SelectObject(dc, previous);
            if (dib != IntPtr.Zero) DeleteObject(dib);
            if (dc != IntPtr.Zero) DeleteDC(dc);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }
    public void ReinforceTopmost() => SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x1 | 0x2 | 0x10);
    protected override void WndProc(ref Message m)
    {
        // Read-only local perf probes; no polling is scheduled inside the application.
        if (m.Msg == 0x8064) { m.Result = (IntPtr)RedrawCount; return; }
        if (m.Msg == 0x8065) { m.Result = (IntPtr)(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name is "PresentationFramework" or "PresentationCore") ? 1 : 0); return; }
        if (m.Msg == 0x8069) { m.Result = (IntPtr)0x4C433230; return; }
        if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; } // HTTRANSPARENT
        if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE
        base.WndProc(ref m);
        if (m.Msg is 0x7E or 0x2E0) DisplayChanged?.Invoke();
    }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, SizeImage; public int XPels, YPels; public uint ColorsUsed, Important; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct Blend { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDc, ref Point destination, ref Size size, IntPtr sourceDc, ref Point source, uint colorKey, ref Blend blend, uint flags);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
