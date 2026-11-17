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
    private bool _blocked;
    public bool? Override { get; private set; }
    public void Reset() { _down = false; _blocked = false; Override = null; }
    public void CancelUntilRelease(bool held = false) { _blocked |= _down || held; _down = false; Override = null; }

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
        if (_blocked) return false;
        if (!trigger || _down) return false;
        _down = !binding.IsWheel;
        // A hold only needs its own modifiers: players sprint or crouch (Shift/Ctrl) while aiming.
        if (binding.Mode == BindingMode.Toggle ? !binding.Matches(input, key, modifiers) : (binding.Modifiers & ~modifiers) != 0) return false;
        if (binding.Mode == BindingMode.Toggle) return true;
        Override = binding.Mode == BindingMode.HoldToShow;
        return false;
    }
}

internal static class RawMouse
{
    private static readonly (int Flag, BindingInput Input, bool Down)[] Buttons =
    [(0x4, BindingInput.RightClick, true), (0x8, BindingInput.RightClick, false), (0x10, BindingInput.MiddleClick, true), (0x20, BindingInput.MiddleClick, false),
     (0x40, BindingInput.Mouse4, true), (0x80, BindingInput.Mouse4, false), (0x100, BindingInput.Mouse5, true), (0x200, BindingInput.Mouse5, false)];
    /// <summary>RAWMOUSE usButtonFlags/usButtonData to binding transitions in packet order. Wheel notches are presses only.</summary>
    internal static List<(BindingInput Input, bool Down)> Decode(int flags, short wheel)
    {
        var events = new List<(BindingInput, bool)>(2);
        foreach (var button in Buttons) if ((flags & button.Flag) != 0) events.Add((button.Input, button.Down));
        if ((flags & 0x400) != 0 && wheel != 0) events.Add((wheel > 0 ? BindingInput.WheelUp : BindingInput.WheelDown, true));
        return events;
    }
}

// A raw-input observation without a corresponding hook callback is evidence of silent removal.
internal sealed class HookHealth
{
    private long _lastHook, _lastRaw, _firstMissingRaw;
    public void HookCalled(long now) { _lastHook = now; _firstMissingRaw = 0; }
    public void RawInput(long now)
    {
        _lastRaw = now;
        if (_firstMissingRaw == 0 && now - _lastHook > 500) _firstMissingRaw = now;
    }
    public bool NeedsReinstall(long now)
    {
        // Give fresh raw input a delivery window before declaring its callback missing,
        // including the first event after a long idle period.
        if (_lastRaw - _lastHook <= 500 || now - _lastHook < 2000 ||
            _firstMissingRaw == 0 || now - _firstMissingRaw < 500) return false;
        _lastHook = _lastRaw = now; _firstMissingRaw = 0;
        return true;
    }
}
