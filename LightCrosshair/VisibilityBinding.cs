using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace LightCrosshair;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BindingInput { Keyboard, MiddleClick, Mouse4, Mouse5, WheelUp, WheelDown, RightClick }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BindingMode { Toggle, HoldToHide, HoldToShow }

public sealed record VisibilityBinding
{
    public BindingInput Input { get; init; } = BindingInput.Keyboard;
    public int Key { get; init; } = (int)Keys.X;
    // Same bit flags as RegisterHotKey: Alt=1, Ctrl=2, Shift=4, Win=8.
    public int Modifiers { get; init; } = HotkeyManager.MOD_ALT;
    public BindingMode Mode { get; init; }
    [JsonIgnore] public bool IsWheel => Input is BindingInput.WheelUp or BindingInput.WheelDown;
    [JsonIgnore] public bool IsValid => Enum.IsDefined(Input) && Enum.IsDefined(Mode) &&
        (Modifiers & ~15) == 0 && (Input != BindingInput.Keyboard ||
        (Key > 6 && Key < 255 && !IsModifier(Key) && Enum.IsDefined(typeof(Keys), Key))) &&
        (!IsWheel || Mode == BindingMode.Toggle);

    public bool Matches(BindingInput input, int key, int modifiers) =>
        Input == input && (input != BindingInput.Keyboard || Key == key) && Modifiers == modifiers;

    public static bool IsModifier(int key) => key is 16 or 17 or 18 or 91 or 92 or >= 160 and <= 165;

    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & 2) != 0) parts.Add("Ctrl");
        if ((Modifiers & 1) != 0) parts.Add("Alt");
        if ((Modifiers & 4) != 0) parts.Add("Shift");
        if ((Modifiers & 8) != 0) parts.Add("Win");
        parts.Add(Input == BindingInput.Keyboard ? ((Keys)Key).ToString() : Input.ToString());
        return string.Join("+", parts);
    }

    public static bool TryParse(string? text, out VisibilityBinding binding)
    {
        binding = new VisibilityBinding();
        if (string.IsNullOrWhiteSpace(text)) return false;
        string[] parts = text.Split('+', StringSplitOptions.TrimEntries);
        int modifiers = 0;
        foreach (string part in parts[..^1])
        {
            int flag = part.ToLowerInvariant() switch { "alt" => 1, "ctrl" => 2, "shift" => 4, "win" => 8, _ => 0 };
            if (flag == 0 || (modifiers & flag) != 0) return false;
            modifiers |= flag;
        }
        string trigger = parts[^1];
        if (Enum.TryParse(trigger, true, out BindingInput input) && input != BindingInput.Keyboard)
            binding = new() { Input = input, Key = 0, Modifiers = modifiers };
        else if (Enum.TryParse(trigger, true, out Keys key))
            binding = new() { Key = (int)key, Modifiers = modifiers };
        else return false;
        return binding.IsValid;
    }
}

// UI-thread state: hold overrides never change the user's persistent visibility preference.
internal sealed class VisibilityBindingState
{
    private bool _down;
    public bool? Override { get; private set; }
    public void Reset() { _down = false; Override = null; }

    public bool Process(VisibilityBinding binding, BindingInput input, int key, int modifiers, bool down)
    {
        bool trigger = binding.Input == input && (input != BindingInput.Keyboard || binding.Key == key);
        if (!down && trigger)
        {
            Reset();
            return false;
        }
        if (!down)
        {
            if ((binding.Modifiers & modifiers) != binding.Modifiers) Override = null;
            return false;
        }
        if (!trigger || _down) return false;
        _down = !binding.IsWheel;
        if (!binding.Matches(input, key, modifiers)) return false;
        if (binding.Mode == BindingMode.Toggle) return true;
        Override = binding.Mode == BindingMode.HoldToShow;
        return false;
    }
}

// A raw-input observation without a corresponding hook callback is evidence of silent removal.
internal sealed class HookHealth
{
    private long _lastHook, _lastRaw;
    public void HookCalled(long now) => _lastHook = now;
    public void RawInput(long now) => _lastRaw = now;
    public bool NeedsReinstall(long now)
    {
        // Allow for raw input being delivered just after its hook callback and for batching.
        if (_lastRaw - _lastHook <= 500 || now - _lastHook < 2000) return false;
        _lastHook = _lastRaw = now;
        return true;
    }
}
