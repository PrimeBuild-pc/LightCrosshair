using System.Text.Json;
using LightCrosshair.Contracts;
using LightCrosshair.Core;

namespace LightCrosshair;

internal sealed partial class Form1
{
    private VisibilityInputHook? _input;
    private List<BoundAction> _bindings = [];
    private readonly Dictionary<int, VisibilityBindingState> _bindingStates = new();
    private string? _bindingsKey;
    private bool? _manualVisibility;
    private System.Windows.Forms.Timer? _recordingTimer;
    private bool _recording;
    private void ConfigureInput()
    {
        if (_context is null) return;
        var bindings = InputBindings.Resolve(Document.Settings, _context);
        string key = Document.Settings.Language + JsonSerializer.Serialize(bindings.Select(b => new { b.Action, b.Input }), ContractJson.Options);
        if (_bindingsKey == key) return;
        _bindingsKey = key; _holdOverride = null; _bindingStates.Clear();
        _input ??= new(Handle);
        var manager = HotkeyManager.Instance;
        manager.SetWindowHandle(Handle); manager.UnregisterAll();
        Runtime.HotkeyErrors.Clear();
        var conflicts = InputBindings.Conflicts(bindings);
        _bindings = [];
        foreach (var action in bindings)
        {
            if (conflicts.Contains(action.HotkeyId))
            { Runtime.HotkeyErrors.Add(new() { Action = action.Action, Binding = action.Input, Message = TextFor("HotkeyConflict") }); continue; }
            bool systemHotkey = action.Binding.Input == BindingInput.Keyboard && action.Binding.Mode == BindingMode.Toggle;
            if (systemHotkey && !manager.RegisterHotkeyWithId(action.HotkeyId, action.Binding.Modifiers, action.Binding.Key))
            { Runtime.HotkeyErrors.Add(new() { Action = action.Action, Binding = action.Input, Message = TextFor("HotkeyRegistrationFailed") }); continue; }
            _bindings.Add(action); _bindingStates[action.HotkeyId] = new();
        }
        _input.Configure(_bindings.Where(a => a.Binding.Input != BindingInput.Keyboard || a.Binding.Mode != BindingMode.Toggle).Select(a => a.Binding).ToArray(), false);
    }
    private void ConfigureRecording()
    {
        if (!Document.Settings.HideDuringScreenRecording)
        { _recordingTimer?.Dispose(); _recordingTimer = null; _recording = false; return; }
        if (_recordingTimer is not null) return;
        _recordingTimer = new() { Interval = 2000 };
        _recordingTimer.Tick += (_, _) => CheckRecording(); _recordingTimer.Start(); CheckRecording();
    }
    private void CheckRecording()
    {
        bool recording = false;
        foreach (string name in new[] { "obs64", "obs32", "bandicam", "XSplit.Core", "NVIDIA Share" })
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(name);
            recording |= processes.Length > 0; foreach (var process in processes) process.Dispose();
        }
        if (_recording == recording) return;
        _recording = recording; RefreshOverlay(); QueueRuntimeEvent();
    }
    protected override void WndProc(ref Message m)
    {
        if (Program.PerfProbesEnabled)
        {
            if (m.Msg == 0x8066) { m.Result = (IntPtr)(_pipe?.IsConnected == true ? 1 : 0); return; }
            if (m.Msg == 0x8067) { m.Result = (IntPtr)((_fps.Enabled ? 1 : 0) + (_recordingTimer?.Enabled == true ? 1 : 0)); return; }
        }
        if (m.Msg == 0x312)
        {
            int id = (int)m.WParam;
            if (_bindings.Any(a => a.HotkeyId == id)) _ = RunInputActionAsync(id);
            return;
        }
        if (m.Msg == VisibilityInputHook.InputMessage && _input is not null && (int)m.LParam == _input.Generation)
        {
            int data = (int)m.WParam;
            var input = (BindingInput)(data & 255); int key = (data >> 8) & 255, modifiers = (data >> 16) & 15;
            bool down = (data & (1 << 20)) != 0;
            foreach (var action in _bindings)
            {
                if (action.Binding.Input == BindingInput.Keyboard && action.Binding.Mode == BindingMode.Toggle) continue;
                var state = _bindingStates[action.HotkeyId];
                if (state.Process(action.Binding, input, key, modifiers, down)) _ = RunInputActionAsync(action.HotkeyId);
                if (action.HotkeyId == 9000) _holdOverride = state.Override;
            }
            RefreshOverlay(); QueueRuntimeEvent(); return;
        }
        if (m.Msg == VisibilityInputHook.ResetMessage)
        {
            foreach (var state in _bindingStates.Values) state.Reset(); _holdOverride = null;
            if (_input?.Error is { } error)
                foreach (var action in _bindings.Where(a => a.Binding.Input != BindingInput.Keyboard || a.Binding.Mode != BindingMode.Toggle))
                    if (!Runtime.HotkeyErrors.Any(e => e.Action == action.Action && e.Message == TextFor("HookRegistrationFailed")))
                        Runtime.HotkeyErrors.Add(new() { Action = action.Action, Binding = action.Input, Message = TextFor("HookRegistrationFailed") });
            RefreshOverlay(); QueueRuntimeEvent(); return;
        }
        base.WndProc(ref m);
    }
    private async Task RunInputActionAsync(int id)
    {
        if (_closing) return;
        // CONTRACT-REQUEST: Suspend actions during binding capture. The protocol has no capture lifecycle message.
        try
        {
            switch (id)
            {
                case 9000: await ToggleVisibilityAsync(); break;
                case 9001: case 9002:
                    var designs = Document.Crosshairs.Where(d => d.Favorite).ToList();
                    if (designs.Count == 0) designs = Document.Crosshairs;
                    int index = designs.FindIndex(d => d.Id == Runtime.ActiveCrosshairId);
                    index = (index + (id == 9001 ? 1 : -1) + designs.Count) % designs.Count;
                    _cycledId = designs[index].Id; RefreshContext(); break;
                case 9003:
                    if (_pipe?.IsConnected == true) _pipe.Publish(IpcEvents.CloseRequested, null); else OpenSettings();
                    break;
            }
        }
        catch (Exception ex) { Program.LogError(ex, "Input action"); }
    }
}
