using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LightCrosshair.Contracts;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public class HoldVisibilityTests
{
    [Theory]
    [InlineData(BindingMode.HoldToHide, false)]
    [InlineData(BindingMode.HoldToShow, true)]
    public void HoldIsTransientAndFocusOrCaptureCancellationRequiresARelease(BindingMode mode, bool shown)
    {
        var state = new VisibilityBindingState();
        var binding = new VisibilityBinding { Input = BindingInput.RightClick, Key = 0, Modifiers = 0, Mode = mode };
        Assert.False(state.Process(binding, BindingInput.RightClick, 0, 0, true)); Assert.Equal(shown, state.Override);
        state.CancelUntilRelease(); Assert.Null(state.Override);
        state.CancelUntilRelease();
        state.Process(binding, BindingInput.RightClick, 0, 0, true); Assert.Null(state.Override);
        state.Process(binding, BindingInput.RightClick, 0, 0, false); Assert.Null(state.Override);
        state.Process(binding, BindingInput.RightClick, 0, 0, true); Assert.Equal(shown, state.Override);
        state.Process(binding, BindingInput.RightClick, 0, 0, false); Assert.Null(state.Override);
    }
    [Theory]
    [InlineData("follow", "RightClick")]
    [InlineData("off", null)]
    [InlineData("own", "Mouse5")]
    public void GameCanInheritDisableOrOverrideTheIndependentHold(string policy, string? expected)
    {
        var store = LegacyMigration.Defaults();
        store.Settings.Hotkeys.HoldVisibility = new() { Key = "RightClick", Mode = Contracts.BindingMode.HoldToHide };
        store.GameProfiles.Add(new() { Id = "game", ProcessName = "game.exe", CrosshairId = store.Settings.DefaultCrosshairId,
            HoldVisibility = policy == "follow" ? null : new() { Key = policy == "off" ? "" : "Mouse5", Mode = Contracts.BindingMode.HoldToShow } });
        var bindings = InputBindings.Resolve(store.Settings, GameRules.Evaluate(store, "game.exe"));
        Assert.Equal(expected, bindings.FirstOrDefault(b => b.HotkeyId == 9004)?.Input.Key);
        Assert.Equal("X", bindings.Single(b => b.HotkeyId == 9000).Input.Key);
        Assert.True(store.Settings.CrosshairVisible);
        Assert.Equal("RightClick", InputBindings.Resolve(store.Settings, GameRules.Evaluate(store, "other.exe")).Single(b => b.HotkeyId == 9004).Input.Key);
    }
    [Theory]
    [InlineData("WheelUp", Contracts.BindingMode.HoldToHide)]
    [InlineData("RightClick", Contracts.BindingMode.Toggle)]
    public void HoldRejectsWheelAndToggle(string key, Contracts.BindingMode mode)
        => Assert.Throws<CoreException>(() => Validation.HoldBinding(new() { Key = key, Mode = mode }));
    [Fact]
    public async Task LegacyHoldMovesToItsOwnBindingAndKeepsAltXToggle()
    {
        string root = Path.Combine(Path.GetTempPath(), "lc-final-hold-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string source = "{\"VisibilityBinding\":{\"Input\":\"RightClick\",\"Key\":0,\"Modifiers\":0,\"Mode\":\"HoldToHide\"}}";
            await File.WriteAllTextAsync(Path.Combine(root, "crosshair_settings.json"), source);
            var doc = await LegacyMigration.LoadAsync(root, Path.Combine(root, "v2", "images"));
            Assert.Equal("RightClick", doc.Settings.Hotkeys.HoldVisibility.Key);
            Assert.Equal(Contracts.BindingMode.HoldToHide, doc.Settings.Hotkeys.HoldVisibility.Mode);
            Assert.Equal("X", doc.Settings.Hotkeys.ToggleVisibility.Key);
            Assert.Equal(KeyModifiers.Alt, doc.Settings.Hotkeys.ToggleVisibility.Modifiers);
            Assert.Equal(source, await File.ReadAllTextAsync(Path.Combine(root, "crosshair_settings.json")));
        }
        finally { Directory.Delete(root, true); }
    }
}
