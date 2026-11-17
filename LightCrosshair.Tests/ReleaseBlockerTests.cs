using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LightCrosshair.Contracts;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public class ReleaseBlockerTests
{
    [Theory]
    [InlineData(4)] [InlineData(2)] [InlineData(6)]
    public void HoldStillWorksWhileSprintingOrCrouching(int extraModifiers)
    {
        var binding = new VisibilityBinding { Input = BindingInput.RightClick, Key = 0, Modifiers = 0, Mode = LightCrosshair.BindingMode.HoldToHide };
        var state = new VisibilityBindingState();
        state.Process(binding, BindingInput.RightClick, 0, extraModifiers, true);
        Assert.False(state.Override);
        state.Process(binding, BindingInput.RightClick, 0, extraModifiers, false);
        Assert.Null(state.Override);
        var toggle = binding with { Mode = LightCrosshair.BindingMode.Toggle };
        Assert.False(new VisibilityBindingState().Process(toggle, BindingInput.RightClick, 0, extraModifiers, true)); // toggles keep exact chords
        var ctrlHold = binding with { Modifiers = 2 };
        state = new VisibilityBindingState();
        state.Process(ctrlHold, BindingInput.RightClick, 0, 4, true);
        Assert.Null(state.Override); // its own modifier is still required
    }

    [Fact]
    public void RawMouseFlagsDecodeInPacketOrder()
    {
        Assert.Empty(RawMouse.Decode(0, 0));
        Assert.Equal([(BindingInput.RightClick, true)], RawMouse.Decode(0x4, 0));
        Assert.Equal([(BindingInput.RightClick, true), (BindingInput.RightClick, false)], RawMouse.Decode(0xC, 0));
        Assert.Equal([(BindingInput.MiddleClick, false), (BindingInput.Mouse4, true), (BindingInput.Mouse5, false)], RawMouse.Decode(0x20 | 0x40 | 0x200, 0));
        Assert.Equal([(BindingInput.WheelUp, true)], RawMouse.Decode(0x400, 120));
        Assert.Equal([(BindingInput.WheelDown, true)], RawMouse.Decode(0x400, -120));
        Assert.Empty(RawMouse.Decode(0x1 | 0x2, 0)); // left button is never a binding
    }

    [Fact]
    public void FpsOverlayHotkeyDefaultsToAltFAndJoinsConflictDetection()
    {
        var store = LegacyMigration.Defaults();
        Assert.Equal("F", store.Settings.Hotkeys.ToggleFpsOverlay.Key);
        Assert.Equal(KeyModifiers.Alt, store.Settings.Hotkeys.ToggleFpsOverlay.Modifiers);
        var bindings = InputBindings.Resolve(store.Settings, GameRules.Evaluate(store, null));
        Assert.Equal("ToggleFpsOverlay", bindings.Single(b => b.HotkeyId == 9005).Action);
        Assert.Empty(InputBindings.Conflicts(bindings));
        store.Settings.Hotkeys.NextCrosshair = new() { Key = "F", Modifiers = KeyModifiers.Alt };
        Assert.Equal([9001, 9005], InputBindings.Conflicts(InputBindings.Resolve(store.Settings, GameRules.Evaluate(store, null))).Order());
        Assert.Throws<CoreException>(() => Validation.Settings(new AppSettings { DefaultCrosshairId = store.Settings.DefaultCrosshairId,
            Hotkeys = new() { ToggleFpsOverlay = new() { Key = "F", Mode = Contracts.BindingMode.HoldToHide } } }, store.Crosshairs));
    }

    [Fact]
    public void GraphColorIsOptionalAndValidated()
    {
        var store = LegacyMigration.Defaults();
        var settings = Validation.Settings(store.Settings, store.Crosshairs);
        Assert.Null(settings.FpsOverlay.GraphColor);
        settings.FpsOverlay.GraphColor = "#3ddc97";
        Assert.Equal("#3ddc97", Validation.Settings(settings, store.Crosshairs).FpsOverlay.GraphColor, ignoreCase: true);
        settings.FpsOverlay.GraphColor = "green";
        Assert.Throws<CoreException>(() => Validation.Settings(settings, store.Crosshairs));
        Assert.False(Validation.Settings(store.Settings, store.Crosshairs).CloseButtonExits);
    }

    [Theory]
    [InlineData("F", true, false)]
    [InlineData("G", true, true)]
    [InlineData("F", false, true)]
    public async Task MigrationLeavesTheFpsHotkeyUnboundWhenAltFIsTaken(string key, bool alt, bool expectBound)
    {
        string root = Path.Combine(Path.GetTempPath(), "lc-fps-hotkey-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string source = "{\"VisibilityBinding\":{\"Input\":\"Keyboard\",\"Key\":" + (int)Enum.Parse<System.Windows.Forms.Keys>(key) + ",\"Modifiers\":" + (alt ? 1 : 0) + ",\"Mode\":\"Toggle\"}}";
            await File.WriteAllTextAsync(Path.Combine(root, "crosshair_settings.json"), source);
            var doc = await LegacyMigration.LoadAsync(root, Path.Combine(root, "v2", "images"));
            Assert.Equal(expectBound ? "F" : "", doc.Settings.Hotkeys.ToggleFpsOverlay.Key);
        }
        finally { Directory.Delete(root, true); }
    }
}
