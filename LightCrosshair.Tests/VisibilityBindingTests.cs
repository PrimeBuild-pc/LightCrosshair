using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xunit;

namespace LightCrosshair.Tests;

public class VisibilityBindingTests
{
    [Theory]
    [InlineData("Alt+Mouse4", BindingInput.Mouse4, 1)]
    [InlineData("Ctrl+RightClick", BindingInput.RightClick, 2)]
    [InlineData("Ctrl+Alt+Shift+Win+Mouse5", BindingInput.Mouse5, 15)]
    [InlineData("MiddleClick", BindingInput.MiddleClick, 0)]
    [InlineData("WheelUp", BindingInput.WheelUp, 0)]
    [InlineData("Shift+WheelDown", BindingInput.WheelDown, 4)]
    [InlineData("Win+F12", BindingInput.Keyboard, 8)]
    [InlineData("ctrl + alt + x", BindingInput.Keyboard, 3)]
    public void ParseAndSerializeRoundTrip(string text, BindingInput input, int modifiers)
    {
        Assert.True(VisibilityBinding.TryParse(text, out var binding));
        Assert.Equal(input, binding.Input);
        Assert.Equal(modifiers, binding.Modifiers);
        Assert.True(VisibilityBinding.TryParse(binding.ToString(), out var reparsed));
        Assert.Equal(binding, reparsed);
        Assert.Equal(binding, JsonSerializer.Deserialize<VisibilityBinding>(JsonSerializer.Serialize(binding)));
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("Ctrl+")] [InlineData("Ctrl+Ctrl+X")]
    [InlineData("Ctrl+UnknownButton")] [InlineData("ShiftKey")] [InlineData("LWin")]
    [InlineData("Keyboard")] [InlineData("LButton")] [InlineData("Control+X")]
    [InlineData("999")] [InlineData("Ctrl+None")]
    public void RejectInvalidBinding(string? text) => Assert.False(VisibilityBinding.TryParse(text, out _));

    [Theory]
    [InlineData(BindingMode.Toggle)] [InlineData(BindingMode.HoldToHide)] [InlineData(BindingMode.HoldToShow)]
    public void ModesRoundTripAndClone(BindingMode mode)
    {
        var binding = new VisibilityBinding { Input = BindingInput.Mouse5, Key = 0, Modifiers = 3, Mode = mode };
        var profile = new CrosshairProfile { VisibilityBinding = binding };
        Assert.Equal(binding, profile.Clone().VisibilityBinding);
        Assert.True(profile.ContentEquals(profile.Clone()));
        var changed = profile.Clone();
        changed.VisibilityBinding = binding with { Modifiers = 15 };
        Assert.False(profile.ContentEquals(changed));
        Assert.Equal(binding, JsonSerializer.Deserialize<CrosshairProfile>(JsonSerializer.Serialize(profile))!.VisibilityBinding);
    }

    [Theory]
    [InlineData(BindingInput.Mouse4, 1, true)] [InlineData(BindingInput.Mouse4, 0, false)]
    [InlineData(BindingInput.Mouse4, 3, false)] [InlineData(BindingInput.Mouse5, 1, false)]
    [InlineData(BindingInput.Keyboard, 1, false)]
    public void MatchingRequiresExactModifiersAndButton(BindingInput input, int modifiers, bool expected)
    {
        var binding = new VisibilityBinding { Input = BindingInput.Mouse4, Modifiers = 1 };
        Assert.Equal(expected, binding.Matches(input, 0, modifiers));
    }

    [Fact]
    public void ToggleIgnoresRepeatUntilTriggerReleased()
    {
        var binding = new VisibilityBinding();
        var state = new VisibilityBindingState();
        Assert.True(state.Process(binding, BindingInput.Keyboard, (int)Keys.X, 1, true));
        Assert.False(state.Process(binding, BindingInput.Keyboard, (int)Keys.X, 1, true));
        Assert.False(state.Process(binding, BindingInput.Keyboard, (int)Keys.LMenu, 0, false));
        Assert.False(state.Process(binding, BindingInput.Keyboard, (int)Keys.X, 1, true));
        Assert.False(state.Process(binding, BindingInput.Keyboard, (int)Keys.X, 0, false));
        Assert.True(state.Process(binding, BindingInput.Keyboard, (int)Keys.X, 1, true));
        Assert.Null(state.Override);
    }

    [Theory]
    [InlineData(BindingMode.HoldToHide, false)] [InlineData(BindingMode.HoldToShow, true)]
    public void HoldRestoresBaselineOnReleaseOrModifierLoss(BindingMode mode, bool active)
    {
        var binding = new VisibilityBinding { Input = BindingInput.RightClick, Modifiers = 2, Mode = mode };
        var state = new VisibilityBindingState();
        Assert.False(state.Process(binding, BindingInput.RightClick, 0, 2, true));
        Assert.Equal(active, state.Override);
        state.Process(binding, BindingInput.Mouse5, 0, 2, false);
        Assert.Equal(active, state.Override);
        state.Process(binding, BindingInput.Keyboard, (int)Keys.LControlKey, 0, false);
        Assert.Null(state.Override);
        state.Process(binding, BindingInput.RightClick, 0, 2, true);
        Assert.Null(state.Override); // no reactivation until the physical trigger is released
        state.Process(binding, BindingInput.RightClick, 0, 0, false);
        state.Process(binding, BindingInput.RightClick, 0, 2, true);
        Assert.Equal(active, state.Override);
        state.Process(binding, BindingInput.RightClick, 0, 0, false);
        Assert.Null(state.Override);
        state.Process(binding, BindingInput.RightClick, 0, 2, true);
        state.Reset();
        Assert.Null(state.Override);
    }

    [Fact]
    public void KeyPressedBeforeModifiersDoesNotActivateOnRepeat()
    {
        var state = new VisibilityBindingState();
        var binding = new VisibilityBinding { Mode = BindingMode.HoldToShow };
        state.Process(binding, BindingInput.Keyboard, binding.Key, 0, true);
        state.Process(binding, BindingInput.Keyboard, binding.Key, 1, true);
        Assert.Null(state.Override);
    }

    [Theory]
    [InlineData(BindingInput.WheelUp)] [InlineData(BindingInput.WheelDown)]
    public void WheelTogglesEveryNotchAndRejectsHold(BindingInput input)
    {
        var binding = new VisibilityBinding { Input = input, Modifiers = 0 };
        var state = new VisibilityBindingState();
        Assert.True(binding.IsValid);
        Assert.True(state.Process(binding, input, 0, 0, true));
        Assert.True(state.Process(binding, input, 0, 0, true));
        Assert.False((binding with { Mode = BindingMode.HoldToHide }).IsValid);
        Assert.False((binding with { Mode = BindingMode.HoldToShow }).IsValid);
    }

    [Fact]
    public void LegacyConfigRetainsKeyboardAndAllModifiers()
    {
        // Pure snapshots own no resources; Dispose would autosave them to the user settings path.
        var config = new CrosshairConfig(loadSettings: false);
        config.HotkeyKey = Keys.F9;
        config.HotkeyUseAlt = false; config.HotkeyUseControl = true;
        config.HotkeyUseShift = true; config.HotkeyUseWin = true;
        var migrated = CrosshairConfig.DeserializeSettings(JsonSerializer.Serialize(config))!;
        Assert.Null(migrated.VisibilityBinding);
        Assert.Equal(new VisibilityBinding { Key = (int)Keys.F9, Modifiers = 14 }, migrated.ResolveVisibilityBinding());
        migrated.VisibilityBinding = new() { Input = BindingInput.WheelUp, Mode = BindingMode.HoldToHide };
        Assert.Equal((int)Keys.F9, migrated.ResolveVisibilityBinding().Key);
    }

    [Fact]
    public void SettingsFileMigratesLegacyFieldsAndPersistsNewBinding()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "{\"HotkeyKey\":120,\"HotkeyUseAlt\":false,\"HotkeyUseControl\":true,\"HotkeyUseWin\":true,\"Visible\":false}");
            using var cfg = new CrosshairConfig(loadSettings: true, path);
            Assert.False(cfg.Visible);
            Assert.Equal(new VisibilityBinding { Key = (int)Keys.F9, Modifiers = 10 }, cfg.ResolveVisibilityBinding());
            cfg.VisibilityBinding = new() { Input = BindingInput.Mouse5, Key = 0, Modifiers = 15, Mode = BindingMode.HoldToHide };
            cfg.SaveSettings();
            using var loaded = new CrosshairConfig(loadSettings: true, path);
            Assert.Equal(cfg.VisibilityBinding, loaded.VisibilityBinding);
            Assert.False(loaded.Visible);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task LegacyProfilesMigrateWithoutChangingHotkeyOrAddingOverride()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var legacy = new CrosshairProfile { Id = "old", Name = "Legacy", SchemaVersion = 5,
                HotKey = Keys.F9, EnableCustomCrosshair = false, Shape = "CirclePlus",
                EnumShape = CrosshairShape.Dot, OuterColor = Color.Transparent,
                InnerShapeColor = Color.Transparent, InnerShapeEdgeColor = Color.Red };
            await File.WriteAllTextAsync(Path.Combine(directory, "profiles.json"), JsonSerializer.Serialize(new[] { legacy }));
            var profile = Assert.Single(await ProfileStore.LoadAsync(Path.Combine(directory, "profiles.json")));
            Assert.Equal(Keys.F9, profile.HotKey);
            Assert.False(profile.EnableCustomCrosshair);
            Assert.Null(profile.VisibilityBinding);
            Assert.Equal(ProfileSchema.Current, profile.SchemaVersion);
            legacy.SchemaVersion = ProfileSchema.Current;
            Assert.True(legacy.ContentEquals(profile));
            profile.VisibilityBinding = new() { Input = BindingInput.Mouse4, Modifiers = 1, Mode = BindingMode.HoldToHide };
            await ProfileStore.SaveAtomicAsync(new List<CrosshairProfile> { profile }, Path.Combine(directory, "profiles.json"));
            Assert.Equal(profile.VisibilityBinding, Assert.Single(await ProfileStore.LoadAsync(Path.Combine(directory, "profiles.json"))).VisibilityBinding);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void ConflictsIncludeGlobalAndProfileHotkeysForAllModes()
    {
        var cfg = new CrosshairConfig(loadSettings: false);
        foreach (BindingMode mode in Enum.GetValues<BindingMode>())
        {
            Assert.Contains("Forward", VisibilityBindingConflicts.Find(new() { Key = (int)Keys.C, Mode = mode }, cfg, [])!);
            Assert.Contains("Back", VisibilityBindingConflicts.Find(new() { Key = (int)Keys.V, Mode = mode }, cfg, [])!);
            Assert.Contains("Settings", VisibilityBindingConflicts.Find(new() { Key = (int)Keys.L, Mode = mode }, cfg, [])!);
            Assert.Contains("Game", VisibilityBindingConflicts.Find(new() { Key = (int)Keys.F2, Modifiers = 0, Mode = mode }, cfg,
                [new() { Name = "Game", HotKey = Keys.F2 }])!);
        }
        Assert.Null(VisibilityBindingConflicts.Find(new() { Input = BindingInput.Mouse4 }, cfg, []));
        Assert.Null(VisibilityBindingConflicts.Find(new() { Key = (int)Keys.C, Modifiers = 3 }, cfg, []));
    }

    [Fact]
    public void WatchdogRecoversSilentRemovalWithoutTreatingIdleOrDeliveryOrderAsFailure()
    {
        var health = new HookHealth();
        health.HookCalled(1000); health.RawInput(1002);
        Assert.False(health.NeedsReinstall(4000));
        Assert.False(health.NeedsReinstall(60000)); // idle is not a hook failure
        health.RawInput(61000);
        Assert.True(health.NeedsReinstall(63000));
        Assert.False(health.NeedsReinstall(64000));
        health.RawInput(65000); health.HookCalled(65010);
        Assert.False(health.NeedsReinstall(68000));
    }
}
