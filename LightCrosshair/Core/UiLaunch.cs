using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LightCrosshair.Core;

internal static class UiLaunch
{
    internal static bool ReducePriority(bool configuredGame, bool fullscreen) => configuredGame || fullscreen;
    internal static bool ForegroundIsFullscreen()
    {
        IntPtr window = GetForegroundWindow();
        var monitor = new Monitor { Size = Marshal.SizeOf<Monitor>() };
        if (window == IntPtr.Zero || !GetWindowRect(window, out var rect) || !GetMonitorInfo(MonitorFromWindow(window, 2), ref monitor)) return false;
        return rect.Left <= monitor.Bounds.Left && rect.Top <= monitor.Bounds.Top && rect.Right >= monitor.Bounds.Right && rect.Bottom >= monitor.Bounds.Bottom;
    }
    internal static bool IsParked(uint pid)
    {
        if (pid == 0) return false;
        try { using var process = Process.GetProcessById((int)pid); if (process.ProcessName != "LightCrosshair.UI") return false; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception) { return true; }
        bool visible = false;
        EnumWindows((window, _) => { GetWindowThreadProcessId(window, out uint owner); if (owner == pid && IsWindowVisible(window) && GetWindow(window, 4) == IntPtr.Zero) visible = true; return !visible; }, IntPtr.Zero);
        return !visible;
    }
    internal static void Start(ProcessStartInfo start, bool reduced)
    {
        if (!reduced) { using var process = Process.Start(start); return; }
        // Set both priorities before managed startup can execute. Inherit the Core's
        // environment, including an isolated data root. No shell or elevation.
        var command = new StringBuilder(Quote(start.FileName));
        foreach (string argument in start.ArgumentList) command.Append(' ').Append(Quote(argument));
        var startup = new Startup { Size = Marshal.SizeOf<Startup>() };
        if (!CreateProcess(start.FileName, command, IntPtr.Zero, IntPtr.Zero, false, 0x4004, IntPtr.Zero, AppContext.BaseDirectory, ref startup, out var info)) throw new Win32Exception();
        try
        {
            int io = 1; // IoPriorityLow; ProcessIoPriority = 33.
            int status = NtSetInformationProcess(info.Process, 33, ref io, sizeof(int));
            if (status != 0) Program.WriteLog($"UI low I/O priority unavailable: NTSTATUS 0x{status:X8}");
            if (ResumeThread(info.Thread) == uint.MaxValue) { TerminateProcess(info.Process, 1); throw new Win32Exception(); }
        }
        catch { TerminateProcess(info.Process, 1); throw; }
        finally { CloseHandle(info.Thread); CloseHandle(info.Process); }
    }
    internal static string Quote(string value)
    {
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            if (c == '"') result.Append('\\', slashes * 2 + 1).Append(c);
            else result.Append('\\', slashes).Append(c);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    private delegate bool EnumProc(IntPtr window, IntPtr state);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Monitor { public int Size; public Rect Bounds, WorkArea; public uint Flags; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Startup
    { public int Size; public string? Reserved, Desktop, Title; public uint X, Y, XSize, YSize, XChars, YChars, Fill, Flags; public ushort Show, ReservedSize; public IntPtr Reserved2, Input, Output, Error; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, bool inherit, uint flags, IntPtr environment, string directory, ref Startup startup, out ProcessInfo process);
    [DllImport("ntdll.dll")] private static extern int NtSetInformationProcess(IntPtr process, int info, ref int value, int length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll")] private static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref Monitor info);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint relation);
}
