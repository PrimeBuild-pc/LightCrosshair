using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LightCrosshair.Core;
using LightCrosshair.Contracts;
using Xunit;

namespace LightCrosshair.Tests;

public sealed class CoreStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lc2-" + Guid.NewGuid().ToString("N"));
    private string V2 => Path.Combine(_root, "v2");
    public CoreStoreTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public async Task UnreadableLegacyFileUsesTemporaryDefaultsWithoutWritingAStoreAndRetriesOnRestart()
    {
        string legacy = Path.Combine(_root, "profiles.json");
        await File.WriteAllTextAsync(legacy, "[]");
        var store = new CoreStore(V2);
        using (var locked = new FileStream(legacy, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var defaults = await store.LoadAsync(_root);
            Assert.IsType<IOException>(store.LoadError);
            Assert.Single(defaults.Crosshairs);
            await store.SaveAsync(defaults);
            Assert.False(File.Exists(Path.Combine(V2, "store.json")));
            string log = await File.ReadAllTextAsync(Path.Combine(V2, "logs", "core.log"));
            Assert.Contains(store.LoadError.Message, log);
            Assert.Contains("System.IO.IOException", log);
            Assert.Contains("at LightCrosshair.ProfileStore.LoadAsync", log);
        }
        Assert.Null((await Restart()).LoadError);
        Assert.True(File.Exists(Path.Combine(V2, "store.json")));
        async Task<CoreStore> Restart() { var next = new CoreStore(V2); await next.LoadAsync(_root); return next; }
    }

    [Fact]
    public async Task RealV19WorkingCopyMergesIntoItsSourceWithoutAddingADesign()
    {
        foreach (string file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "v1.9-working")))
            File.Copy(file, Path.Combine(_root, Path.GetFileName(file)));
        var document = await new CoreStore(V2).LoadAsync(_root);
        var design = Assert.Single(document.Crosshairs);
        Assert.Equal("default-immutable", design.Id);
        Assert.Equal("Default edited", design.Name);
        Assert.Equal(20, Assert.Single(design.Layers).Length);
        Assert.Equal(design.Id, document.Settings.DefaultCrosshairId);
        Assert.Single((await new CoreStore(V2).LoadAsync(_root)).Crosshairs);
    }

    [Fact]
    public async Task InvalidLegacyIdsKeepDefaultWorkingCopyAndGameLinksWhileBadProfilesAreSkipped()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "profiles.json"), """
            [{"Id":"old/id","Name":"Valid","SchemaVersion":6,"DisplayTargetProcessName":"game.exe"},
             {"Id":"bad","Name":"Invalid shape","EnumShape":999}, {"Id":"invalid-payload","Size":"broken"}, null]
            """);
        await File.WriteAllTextAsync(Path.Combine(_root, "prefs.json"), """{"LastProfileId":"old/id"}""");
        await File.WriteAllTextAsync(Path.Combine(_root, "current_state.json"), """
            {"CurrentProfile":{"Id":"working::0123456789abcdef0123456789abcdef","SourceProfileId":"old/id","Name":"Edited"}}
            """);
        var document = await new CoreStore(V2).LoadAsync(_root);
        var design = Assert.Single(document.Crosshairs);
        Assert.Equal("Edited", design.Name);
        Assert.Equal(design.Id, Validation.Id(design.Id));
        Assert.Equal(design.Id, document.Settings.DefaultCrosshairId);
        Assert.Equal(design.Id, Assert.Single(document.GameProfiles).CrosshairId);
    }

    [Fact]
    public async Task RoundTripCoalescesAndRecoversPreviousStore()
    {
        var store = new CoreStore(V2);
        var doc = await store.LoadAsync(_root);
        Assert.Contains(doc.Crosshairs, d => d.Id == doc.Settings.DefaultCrosshairId);
        doc.Crosshairs[0].Name = "Second";
        Task first = store.SaveAsync(doc);
        doc.Crosshairs[0].Name = "Final";
        await Task.WhenAll(first, store.SaveAsync(doc));
        Assert.Equal("Final", (await new CoreStore(V2).LoadAsync(_root)).Crosshairs[0].Name);
        Assert.Single(Directory.GetFiles(Path.Combine(V2, "backups"), "store-*.json"));
        await File.WriteAllTextAsync(Path.Combine(V2, "store.json"), "{broken");
        var recovery = new CoreStore(V2);
        var restored = await recovery.LoadAsync(_root);
        Assert.True(recovery.Recovered);
        Assert.Equal("Classic Cross", restored.Crosshairs[0].Name);
        Assert.Single(Directory.GetFiles(V2, "store.corrupt-*.json"));
    }

    [Fact]
    public async Task InvalidNewestBackupFallsBackToValidOlderBackup()
    {
        var store = new CoreStore(V2);
        var doc = await store.LoadAsync(_root);
        string backups = Path.Combine(V2, "backups");
        File.Copy(Path.Combine(V2, "store.json"), Path.Combine(backups, "store-20000101.json"));
        await File.WriteAllTextAsync(Path.Combine(backups, "store-99999999.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(V2, "store.json"), "null");
        var recovered = await new CoreStore(V2).LoadAsync(_root);
        Assert.Equal(doc.Settings.DefaultCrosshairId, recovered.Settings.DefaultCrosshairId);
    }

    [Theory]
    [InlineData(CrosshairShape.Dot, LayerKind.Dot)]
    [InlineData(CrosshairShape.Cross, LayerKind.Arms)]
    [InlineData(CrosshairShape.CrossOutlined, LayerKind.Arms)]
    [InlineData(CrosshairShape.Circle, LayerKind.Ring)]
    [InlineData(CrosshairShape.CircleOutlined, LayerKind.Ring)]
    [InlineData(CrosshairShape.T, LayerKind.Arms)]
    [InlineData(CrosshairShape.X, LayerKind.Arms)]
    [InlineData(CrosshairShape.Box, LayerKind.Box)]
    [InlineData(CrosshairShape.GapCross, LayerKind.Arms)]
    [InlineData(CrosshairShape.Custom, LayerKind.Arms)]
    public void EveryLegacyShapeProducesEditableLayers(CrosshairShape shape, LayerKind expected)
    {
        var p = new CrosshairProfile { EnumShape = shape, Shape = shape.ToString(), Size = 24, Thickness = 3, GapSize = 2, OuterColor = System.Drawing.Color.Cyan, AntiAlias = false };
        var d = LegacyMigration.Convert(p);
        var layer = Assert.Single(d.Layers);
        Assert.Equal(expected, layer.Kind);
        Assert.Equal("#00FFFF", layer.Color);
        Assert.Equal(shape == CrosshairShape.X ? 45 : 0, layer.Rotation);
        Assert.False(d.AntiAlias);
        if (shape == CrosshairShape.T) Assert.False(layer.Arms.HasFlag(ArmMask.Top));
    }

    [Theory]
    [InlineData(CompositeShapeType.CircleDot, LayerKind.Ring, LayerKind.Dot)]
    [InlineData(CompositeShapeType.CrossDot, LayerKind.Arms, LayerKind.Dot)]
    [InlineData(CompositeShapeType.CircleCross, LayerKind.Ring, LayerKind.Arms)]
    [InlineData(CompositeShapeType.CircleX, LayerKind.Ring, LayerKind.Arms)]
    public void EveryCompositePreservesBothComponents(CompositeShapeType shape, LayerKind outer, LayerKind inner)
    {
        var d = LegacyMigration.Convert(new() { EnumShape = CrosshairShape.Custom, Shape = shape.ToString(), InnerSize = 8, InnerGapSize = 1 });
        Assert.Equal(2, d.Layers.Count); Assert.Equal(outer, d.Layers[0].Kind); Assert.Equal(inner, d.Layers[1].Kind);
    }

    [Fact]
    public async Task MigrationPreservesProcessBindingActiveProfileAndAllSourceBytes()
    {
        var profile = new CrosshairProfile { Id = "legacy", Name = "Old", HasDisplayColorProfile = true, DisplayEnableGammaOverride = true,
            DisplayTargetProcessName = "Game.EXE", DisplayGammaValue = 120, VisibilityBinding = new() { Input = BindingInput.Mouse5, Mode = BindingMode.HoldToHide } };
        string source = JsonSerializer.Serialize(new[] { profile });
        await File.WriteAllTextAsync(Path.Combine(_root, "profiles.json"), source);
        await File.WriteAllTextAsync(Path.Combine(_root, "prefs.json"), "{\"LastProfileId\":\"legacy\",\"OverlayVisible\":false}");
        await File.WriteAllTextAsync(Path.Combine(_root, "crosshair_settings.json"), "{\"HotkeyKey\":120,\"HotkeyUseAlt\":false,\"HotkeyUseControl\":true,\"CrosshairMonitorDeviceName\":\"monitor\"}");
        var store = new CoreStore(V2);
        var doc = await store.LoadAsync(_root);
        var game = Assert.Single(doc.GameProfiles);
        Assert.Equal("game.exe", game.ProcessName); Assert.Equal(120, game.DisplayColors!.Gamma);
        Assert.Equal("Mouse5", game.VisibilityBinding!.Key);
        Assert.Equal("F9", doc.Settings.Hotkeys.ToggleVisibility.Key);
        Assert.Equal(KeyModifiers.Control, doc.Settings.Hotkeys.ToggleVisibility.Modifiers);
        Assert.Equal("legacy", doc.Settings.DefaultCrosshairId); Assert.False(doc.Settings.CrosshairVisible);
        Assert.Equal(source, await File.ReadAllTextAsync(Path.Combine(_root, "profiles.json")));
        var again = await new CoreStore(V2).LoadAsync(_root);
        Assert.Single(again.Crosshairs); Assert.Single(again.GameProfiles);
    }

    [Fact]
    public void TrustBoundaryRejectsMalformedPayloadsAndClampsGeometry()
    {
        Assert.Throws<CoreException>(() => Validation.Design(new() { Name = "x", Layers = [new() { Color = "red" }] }, V2));
        Assert.Throws<CoreException>(() => Validation.Design(new() { Name = "x", Layers = [new() { Kind = LayerKind.Image, ImageFile = "../secret.png" }] }, V2));
        Assert.Throws<CoreException>(() => Validation.Binding(new() { Key = "WheelUp", Mode = Contracts.BindingMode.HoldToHide }));
        Assert.Throws<CoreException>(() => Validation.ProcessName("../game.exe"));
        var d = Validation.Design(new() { Name = " x ", Layers = [new() { Length = -10, Opacity = 99 }] }, V2);
        Assert.Equal("x", d.Name); Assert.Equal(1, d.Layers[0].Length); Assert.Equal(1, d.Layers[0].Opacity);
    }
    [Fact]
    public void ProcessUniquenessIsCheckedAfterNormalizingEveryProfile()
    {
        var store = LegacyMigration.Defaults();
        store.GameProfiles = [new() { Name = "One", ProcessName = "Game.EXE", CrosshairId = store.Settings.DefaultCrosshairId },
            new() { Name = "Two", ProcessName = "GAME.EXE", CrosshairId = store.Settings.DefaultCrosshairId }];
        Assert.Throws<CoreException>(() => Validation.Store(store, V2));
    }
    [Fact]
    public async Task HourlyBackupTimestampDoesNotInheritAnOldStoreTimestamp()
    {
        var store = new CoreStore(V2); var doc = await store.LoadAsync(_root);
        File.SetLastWriteTimeUtc(Path.Combine(V2, "store.json"), DateTime.UtcNow.AddDays(-3));
        doc.Crosshairs[0].Name = "Changed"; await store.SaveAsync(doc);
        var restarted = new CoreStore(V2); doc = await restarted.LoadAsync(_root);
        doc.Crosshairs[0].Name = "Changed again"; await restarted.SaveAsync(doc);
        Assert.Single(Directory.GetFiles(Path.Combine(V2, "backups"), "store-*.json"));
    }
    [Fact]
    public async Task VisibilityPreferenceMigratesEvenWithoutGlobalConfig()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "prefs.json"), "{\"OverlayVisible\":false}");
        var document = await new CoreStore(V2).LoadAsync(_root);
        Assert.False(document.Settings.CrosshairVisible);
    }
    [Fact]
    public void LegacyContinuousTAndXAndPenCenteredRingPreserveTheirGeometry()
    {
        var t = LegacyMigration.Convert(new() { Name = "T", EnumShape = CrosshairShape.T, Size = 20, GapSize = 8 });
        Assert.Equal(0, t.Layers[0].Gap); Assert.Equal(10, t.Layers[0].Length);
        var x = LegacyMigration.Convert(new() { Name = "X", EnumShape = CrosshairShape.X, Size = 20, GapSize = 8 });
        Assert.Equal(0, x.Layers[0].Gap); Assert.Equal(14, x.Layers[0].Length);
        var ring = LegacyMigration.Convert(new() { Name = "Ring", EnumShape = CrosshairShape.Circle, Size = 20, Thickness = 4 });
        Assert.Equal(24, ring.Layers[0].Length);
        Assert.All(LegacyMigration.Convert(new() { EnableCustomCrosshair = false }).Layers, layer => Assert.False(layer.Visible));
    }
    [Fact]
    public async Task AStoreThatCannotFitInASnapshotIsRejectedBeforeReplacingTheLastGoodFile()
    {
        var store = new CoreStore(V2); var document = await store.LoadAsync(_root);
        string previous = await File.ReadAllTextAsync(Path.Combine(V2, "store.json"));
        document.Crosshairs = Enumerable.Range(0, 3000).Select(i => new CrosshairDesign
        {
            Id = i.ToString(), Name = new string('n', 48),
            Layers = Enumerable.Range(0, 8).Select(j => new CrosshairLayer { Id = j.ToString(), Name = new string('l', 48) }).ToList()
        }).ToList();
        document.Settings.DefaultCrosshairId = "0";
        await Assert.ThrowsAsync<CoreException>(() => store.SaveAsync(document));
        Assert.Equal(previous, await File.ReadAllTextAsync(Path.Combine(V2, "store.json")));
    }
    [Fact]
    public void LargeLegacyBoxFillCoversItsCenterWithinContractLayerAndStrokeLimits()
    {
        var design = LegacyMigration.Convert(new() { EnumShape = CrosshairShape.Box, Shape = "Box", Size = 256, FillColor = System.Drawing.Color.Blue, OuterColor = System.Drawing.Color.Green });
        Assert.Equal(3, design.Layers.Count); Assert.All(design.Layers, l => Assert.InRange(l.Thickness, 1, 64));
        using var bitmap = new LayerRenderer(V2).Render(design, 320);
        Assert.Equal(System.Drawing.Color.Blue.ToArgb(), bitmap.GetPixel(160, 160).ToArgb());
        Assert.Equal(System.Drawing.Color.Blue.ToArgb(), bitmap.GetPixel(240, 160).ToArgb());
    }
}
