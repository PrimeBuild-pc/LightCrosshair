using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LightCrosshair;

// Construct/dispose on the UI thread: OUTOFCONTEXT WinEvent callbacks use its message loop.
internal sealed class DisplayManager : IDisposable
{
    private readonly WinEventProc _callback;
    private readonly Action<string?, int> _changed;
    private readonly HashSet<string> _fallbackLogged = new();
    private readonly IntPtr _hook;
    public DisplayManager(Action<string?, int> changed)
    {
        _changed = changed;
        _callback = (_, _, window, _, _, _, _) => Resolve(window);
        _hook = SetWinEventHook(3, 3, IntPtr.Zero, _callback, 0, 0, 0);
        if (_hook == IntPtr.Zero) throw new Win32Exception();
    }
    public void Refresh() => Resolve(GetForegroundWindow());
    private void Resolve(IntPtr window)
    {
        if (window == IntPtr.Zero) { _changed(null, 0); return; }
        GetWindowThreadProcessId(window, out uint pid);
        if (pid == Environment.ProcessId) return;
        string? name = null;
        IntPtr process = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
        try
        {
            if (process != IntPtr.Zero)
            {
                var path = new StringBuilder(32768); int size = path.Capacity;
                if (QueryFullProcessImageName(process, 0, path, ref size)) name = Path.GetFileName(path.ToString()).ToLowerInvariant();
            }
        }
        finally { if (process != IntPtr.Zero) CloseHandle(process); }
        if (name is null)
        {
            try
            {
                using var fallback = Process.GetProcessById((int)pid);
                name = fallback.ProcessName.ToLowerInvariant() + ".exe";
                if (_fallbackLogged.Add(name)) Program.WriteLog("Foreground process-name fallback: " + name);
            }
            catch (Exception ex) when (ex is Win32Exception or ArgumentException or InvalidOperationException) { }
        }
        _changed(name, (int)pid);
    }
    public void Dispose() { if (_hook != IntPtr.Zero) UnhookWinEvent(_hook); GC.KeepAlive(_callback); }
    private delegate void WinEventProc(IntPtr hook, uint type, IntPtr window, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint process);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref int size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
