using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal sealed record BoundAction(string Action, InputBinding Input, VisibilityBinding Binding, int HotkeyId);

internal static class InputBindings
{
    internal static VisibilityBinding Convert(InputBinding input)
    {
        bool mouse = Enum.TryParse<BindingInput>(input.Key, out var trigger) && trigger != BindingInput.Keyboard;
        return new() { Input = mouse ? trigger : BindingInput.Keyboard, Key = mouse ? 0 : (int)Enum.Parse<Keys>(input.Key), Modifiers = (int)input.Modifiers, Mode = (BindingMode)input.Mode };
    }
    internal static List<BoundAction> Resolve(AppSettings settings, GameContext context)
    {
        var actions = new List<BoundAction>();
        Add(context.Game?.VisibilityBinding is null ? "ToggleVisibility" : "GameProfile:" + context.Game.Id, context.Binding, 9000);
        Add("NextCrosshair", settings.Hotkeys.NextCrosshair, 9001);
        Add("PreviousCrosshair", settings.Hotkeys.PreviousCrosshair, 9002);
        Add("OpenSettings", settings.Hotkeys.OpenSettings, 9003);
        Add(context.Game?.HoldVisibility is null ? "HoldVisibility" : "GameHold:" + context.Game.Id,
            context.Game?.HoldVisibility ?? settings.Hotkeys.HoldVisibility, 9004);
        Add("ToggleFpsOverlay", settings.Hotkeys.ToggleFpsOverlay, 9005);
        return actions;
        void Add(string action, InputBinding input, int id)
        { if (!string.IsNullOrEmpty(input.Key)) actions.Add(new(action, input, Convert(input), id)); }
    }
    internal static HashSet<int> Conflicts(List<BoundAction> actions) => actions
        .GroupBy(a => (a.Binding.Input, a.Binding.Key, a.Binding.Modifiers)).Where(g => g.Count() > 1)
        .SelectMany(g => g.Select(a => a.HotkeyId)).ToHashSet();
}
