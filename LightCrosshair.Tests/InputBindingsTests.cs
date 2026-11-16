using LightCrosshair.Contracts;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public class InputBindingsTests
{
    [Theory]
    [InlineData("MiddleClick", BindingInput.MiddleClick)] [InlineData("RightClick", BindingInput.RightClick)]
    [InlineData("Mouse4", BindingInput.Mouse4)] [InlineData("Mouse5", BindingInput.Mouse5)]
    [InlineData("WheelUp", BindingInput.WheelUp)] [InlineData("WheelDown", BindingInput.WheelDown)]
    public void ContractMouseBindingsPreserveTheirTriggerAndModifiers(string key, BindingInput expected)
    {
        var binding = InputBindings.Convert(new() { Key = key, Modifiers = KeyModifiers.Control | KeyModifiers.Win });
        Assert.Equal(expected, binding.Input); Assert.Equal(10, binding.Modifiers); Assert.True(binding.IsValid);
    }
    [Fact]
    public void AllActionsSupportMouseAndGameOverrideReplacesOnlyVisibility()
    {
        var store = LegacyMigration.Defaults(); store.Settings.Hotkeys.OpenSettings = new() { Key = "Mouse4" };
        store.Settings.Hotkeys.NextCrosshair = new() { Key = "WheelUp" };
        store.GameProfiles.Add(new() { Id = "game", ProcessName = "game.exe", CrosshairId = store.Settings.DefaultCrosshairId,
            VisibilityBinding = new() { Key = "F9", Mode = Contracts.BindingMode.HoldToHide } });
        var actions = InputBindings.Resolve(store.Settings, GameRules.Evaluate(store, "game.exe"));
        Assert.Equal("GameProfile:game", actions[0].Action); Assert.Equal(BindingMode.HoldToHide, actions[0].Binding.Mode);
        Assert.Equal(BindingInput.WheelUp, actions[1].Binding.Input); Assert.Equal(BindingInput.Mouse4, actions[3].Binding.Input);
        Assert.Empty(InputBindings.Conflicts(actions));
        store.Settings.Hotkeys.PreviousCrosshair = new() { Key = "WheelUp" };
        Assert.Equal(2, InputBindings.Conflicts(InputBindings.Resolve(store.Settings, GameRules.Evaluate(store, "game.exe"))).Count);
    }
}
