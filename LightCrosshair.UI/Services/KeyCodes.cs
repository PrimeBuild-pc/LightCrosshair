using System.Windows.Input;

namespace LightCrosshair.UI.Services;

/// <summary>Maps WPF keys to the System.Windows.Forms.Keys names stored on <see cref="InputBinding"/>.</summary>
public static class KeyCodes
{
    private static readonly Dictionary<string, int> VirtualKeys = Build();

    public static string? FromKey(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.System or Key.DeadCharProcessed or Key.None or Key.ImeProcessed
            or Key.ImeAccept or Key.ImeModeChange => null,
        Key.Enter or Key.Return => "Return",
        Key.Snapshot => "Snapshot",
        Key.Prior or Key.PageUp => "PageUp",
        Key.Next or Key.PageDown => "PageDown",
        Key.Capital => "Capital",
        Key.Back => "Back",
        Key.Escape => "Escape",
        _ => key.ToString(),
    };

    public static bool TryVirtualKey(string name, out int virtualKey) => VirtualKeys.TryGetValue(name, out virtualKey);

    private static Dictionary<string, int> Build()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var c = 'A'; c <= 'Z'; c++) map[c.ToString()] = c;
        for (var d = 0; d <= 9; d++) map["D" + d] = 0x30 + d;
        for (var f = 1; f <= 24; f++) map["F" + f] = 0x70 + f - 1;
        for (var n = 0; n <= 9; n++) map["NumPad" + n] = 0x60 + n;
        map["Back"] = 0x08;
        map["Tab"] = 0x09;
        map["Return"] = 0x0D;
        map["Pause"] = 0x13;
        map["Capital"] = 0x14;
        map["Escape"] = 0x1B;
        map["Space"] = 0x20;
        map["PageUp"] = 0x21;
        map["PageDown"] = 0x22;
        map["End"] = 0x23;
        map["Home"] = 0x24;
        map["Left"] = 0x25;
        map["Up"] = 0x26;
        map["Right"] = 0x27;
        map["Down"] = 0x28;
        map["Snapshot"] = 0x2C;
        map["Insert"] = 0x2D;
        map["Delete"] = 0x2E;
        map["Multiply"] = 0x6A;
        map["Add"] = 0x6B;
        map["Subtract"] = 0x6D;
        map["Decimal"] = 0x6E;
        map["Divide"] = 0x6F;
        map["OemSemicolon"] = 0xBA;
        map["OemPlus"] = 0xBB;
        map["OemComma"] = 0xBC;
        map["OemMinus"] = 0xBD;
        map["OemPeriod"] = 0xBE;
        map["OemQuestion"] = 0xBF;
        map["OemTilde"] = 0xC0;
        map["OemOpenBrackets"] = 0xDB;
        map["OemPipe"] = 0xDC;
        map["OemCloseBrackets"] = 0xDD;
        map["OemQuotes"] = 0xDE;
        map["OemBackslash"] = 0xE2;
        map["Apps"] = 0x5D;
        map["Scroll"] = 0x91;
        map["NumLock"] = 0x90;
        return map;
    }
}
