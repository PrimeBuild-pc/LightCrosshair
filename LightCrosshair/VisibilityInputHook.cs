using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LightCrosshair;

// Callbacks only decode input and post a message. All actions run on the main UI thread.
internal sealed class VisibilityInputHook : IDisposable
{
    internal const int InputMessage = 0x8000 + 30, ResetMessage = 0x8000 + 31;
    private const int ConfigureMessage = 0x8000 + 32, StopMessage = 0x8000 + 33;
    private sealed record Configuration(VisibilityBinding Binding, bool Capture, int Generation);
    private Configuration _configuration = new(new(), false, 0);
    private readonly IntPtr _target;
    private readonly Thread _thread;
    private HookWindow? _window;
    private IntPtr _mouseHook, _keyboardHook;
    private readonly HookProc _mouseProc, _keyboardProc;
    private HookHealth _mouseHealth = new(), _keyboardHealth = new();
    private bool _wantMouse, _wantKeyboard;
    private volatile bool _disposed;
    internal int Generation { get; private set; }

    public VisibilityInputHook(IntPtr target)
    {
        _target = target;
        _mouseProc = MouseCallback;
        _keyboardProc = KeyboardCallback;
        _thread = new Thread(Run) { IsBackground = true, Name = "Visibility input" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public void Configure(VisibilityBinding binding, bool capture)
    {
        Generation++;
        Volatile.Write(ref _configuration, new(binding, capture, Generation));
        var window = Volatile.Read(ref _window);
        if (window != null) PostMessage(window.Handle, ConfigureMessage, IntPtr.Zero, IntPtr.Zero);
    }

    private void Run()
    {
        try
        {
            Volatile.Write(ref _window, new HookWindow(this));
            if (_disposed) return;
            ApplyConfiguration();
            System.Windows.Forms.Application.Run();
        }
        catch (Exception ex) { Program.LogError(ex, "Visibility input thread"); }
        finally { RemoveHooks(); _window?.DestroyHandle(); }
    }

    private void ApplyConfiguration()
    {
        var cfg = Volatile.Read(ref _configuration);
        bool mouse = cfg.Capture || cfg.Binding.Input != BindingInput.Keyboard;
        bool keyboard = cfg.Capture || (cfg.Binding.Mode != BindingMode.Toggle &&
            (cfg.Binding.Input == BindingInput.Keyboard || cfg.Binding.Modifiers != 0));
        if (mouse == _wantMouse && keyboard == _wantKeyboard) return;
        RemoveHooks();
        _wantMouse = mouse; _wantKeyboard = keyboard;
        RegisterRaw(mouse, keyboard);
        InstallMissingHooks();
    }

    private void InstallMissingHooks()
    {
        if (_wantMouse && _mouseHook == IntPtr.Zero)
        {
            _mouseHealth = new();
            _mouseHook = SetWindowsHookEx(14, _mouseProc, GetModuleHandle(null), 0);
            if (_mouseHook == IntPtr.Zero) Program.LogError(new Win32Exception(), "Install mouse hook");
        }
        if (_wantKeyboard && _keyboardHook == IntPtr.Zero)
        {
            _keyboardHealth = new();
            _keyboardHook = SetWindowsHookEx(13, _keyboardProc, GetModuleHandle(null), 0);
            if (_keyboardHook == IntPtr.Zero) Program.LogError(new Win32Exception(), "Install keyboard hook");
        }
    }

    private void CheckHealth()
    {
        bool reset = false;
        if (_wantMouse && _mouseHealth.NeedsReinstall(Environment.TickCount64))
        {
            UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; reset = true;
        }
        if (_wantKeyboard && _keyboardHealth.NeedsReinstall(Environment.TickCount64))
        {
            UnhookWindowsHookEx(_keyboardHook); _keyboardHook = IntPtr.Zero; reset = true;
        }
        if (reset)
        {
            Program.LogDebug("Input hook stopped reporting activity; reinstalling.", nameof(VisibilityInputHook));
            PostMessage(_target, ResetMessage, IntPtr.Zero, IntPtr.Zero);
        }
        InstallMissingHooks();
        var cfg = Volatile.Read(ref _configuration);
        // Recover a lost release (desktop switches, hook removal) without persisting a hold state.
        if (cfg.Binding.Mode != BindingMode.Toggle && !cfg.Capture)
        {
            int key = cfg.Binding.Input switch
            {
                BindingInput.Keyboard => cfg.Binding.Key, BindingInput.RightClick => 2,
                BindingInput.MiddleClick => 4, BindingInput.Mouse4 => 5, BindingInput.Mouse5 => 6, _ => 0
            };
            if (key != 0)
            {
                int modifiers = ReadModifiers();
                if ((GetAsyncKeyState(key) & 0x8000) == 0)
                    PostInput(cfg, cfg.Binding.Input, cfg.Binding.Key, modifiers, false);
                else if ((modifiers & cfg.Binding.Modifiers) != cfg.Binding.Modifiers)
                    PostInput(cfg, BindingInput.Keyboard, 0, modifiers, false);
            }
        }
    }

    private IntPtr MouseCallback(int code, IntPtr message, IntPtr data)
    {
        try
        {
            if (code >= 0)
            {
                _mouseHealth.HookCalled(Environment.TickCount64);
                int msg = (int)message;
                BindingInput? input = msg switch
                {
                    0x204 or 0x205 => BindingInput.RightClick,
                    0x207 or 0x208 => BindingInput.MiddleClick,
                    0x20B or 0x20C => (Marshal.ReadInt32(data, 8) >> 16) == 1 ? BindingInput.Mouse4 : BindingInput.Mouse5,
                    0x20A => (short)(Marshal.ReadInt32(data, 8) >> 16) > 0 ? BindingInput.WheelUp : BindingInput.WheelDown,
                    _ => null
                };
                if (input.HasValue) Forward(input.Value, 0, msg is not (0x205 or 0x208 or 0x20C));
            }
        }
        catch { /* Never let a managed exception escape into the hook chain. */ }
        return CallNextHookEx(IntPtr.Zero, code, message, data);
    }

    private IntPtr KeyboardCallback(int code, IntPtr message, IntPtr data)
    {
        try
        {
            if (code >= 0)
            {
                _keyboardHealth.HookCalled(Environment.TickCount64);
                Forward(BindingInput.Keyboard, Marshal.ReadInt32(data), (int)message is 0x100 or 0x104);
            }
        }
        catch { /* Always preserve the original input. */ }
        return CallNextHookEx(IntPtr.Zero, code, message, data);
    }

    private void Forward(BindingInput input, int key, bool down)
    {
        var cfg = Volatile.Read(ref _configuration);
        bool modifier = input == BindingInput.Keyboard && VisibilityBinding.IsModifier(key);
        if (cfg.Capture ? (!down || modifier) :
            !(cfg.Binding.Input == input && (input != BindingInput.Keyboard || cfg.Binding.Key == key)) && !modifier)
            return;
        int modifiers = ReadModifiers();
        // LL keyboard callbacks run before GetAsyncKeyState reflects this key transition.
        if (modifier && !down)
        {
            int flag = key switch { 16 or 160 or 161 => 4, 17 or 162 or 163 => 2, 18 or 164 or 165 => 1, _ => 8 };
            int sibling = key switch { 160 => 161, 161 => 160, 162 => 163, 163 => 162, 164 => 165, 165 => 164, 91 => 92, 92 => 91, _ => 0 };
            if (sibling == 0 || (GetAsyncKeyState(sibling) & 0x8000) == 0) modifiers &= ~flag;
        }
        PostInput(cfg, input, key, modifiers, down);
    }

    private void PostInput(Configuration cfg, BindingInput input, int key, int modifiers, bool down)
    {
        int packed = (int)input | (key << 8) | (modifiers << 16) | (down ? 1 << 20 : 0);
        PostMessage(_target, InputMessage, (IntPtr)packed, (IntPtr)cfg.Generation);
    }

    private static int ReadModifiers() =>
        (((GetAsyncKeyState(18) & 0x8000) != 0) ? 1 : 0) |
        (((GetAsyncKeyState(17) & 0x8000) != 0) ? 2 : 0) |
        (((GetAsyncKeyState(16) & 0x8000) != 0) ? 4 : 0) |
        ((((GetAsyncKeyState(91) | GetAsyncKeyState(92)) & 0x8000) != 0) ? 8 : 0);

    private void RegisterRaw(bool mouse, bool keyboard)
    {
        RawDevice[] devices =
        [new() { Page = 1, Usage = 2, Flags = mouse ? 0x100u : 1u, Target = mouse ? _window!.Handle : IntPtr.Zero },
         new() { Page = 1, Usage = 6, Flags = keyboard ? 0x100u : 1u, Target = keyboard ? _window!.Handle : IntPtr.Zero }];
        if (!RegisterRawInputDevices(devices, 2, (uint)Marshal.SizeOf<RawDevice>()))
            Program.LogError(new Win32Exception(), "Register input hook health monitor");
    }

    private void RemoveHooks()
    {
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        _mouseHook = _keyboardHook = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_window != null) PostMessage(_window.Handle, StopMessage, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(3));
    }

    private sealed class HookWindow : NativeWindow
    {
        private readonly VisibilityInputHook _owner;
        private readonly System.Windows.Forms.Timer _timer;
        public HookWindow(VisibilityInputHook owner)
        {
            _owner = owner;
            CreateHandle(new CreateParams { Caption = "LightCrosshair input", Parent = new IntPtr(-3) });
            _timer = new() { Interval = 500 };
            _timer.Tick += (_, _) => owner.CheckHealth();
            _timer.Start();
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == ConfigureMessage) _owner.ApplyConfiguration();
            else if (m.Msg == StopMessage)
            {
                _timer.Dispose(); _owner.RemoveHooks(); _owner.RegisterRaw(false, false);
                System.Windows.Forms.Application.ExitThread();
            }
            else if (m.Msg == 0xFF)
            {
                uint size = (uint)Marshal.SizeOf<RawHeader>();
                if (GetRawInputData(m.LParam, 0x10000005, out var header, ref size, size) != uint.MaxValue)
                {
                    if (header.Type == 0) _owner._mouseHealth.RawInput(Environment.TickCount64);
                    if (header.Type == 1) _owner._keyboardHealth.RawInput(Environment.TickCount64);
                }
            }
            base.WndProc(ref m);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct RawDevice { public ushort Page, Usage; public uint Flags; public IntPtr Target; }
    [StructLayout(LayoutKind.Sequential)] private struct RawHeader { public uint Type, Size; public IntPtr Device, Param; }
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr target, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(RawDevice[] devices, uint count, uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(IntPtr input, uint command, out RawHeader data, ref uint size, uint headerSize);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
}
