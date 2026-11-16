using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LightCrosshair.Contracts;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public sealed class LibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lc2-library-" + Guid.NewGuid().ToString("N"));
    private string Images => Path.Combine(_root, "images");
    public LibraryTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    [Fact]
    public void PresetsAreOriginalEditableTaggedAndHaveUniqueIds()
    {
        Assert.Equal(30, PresetCatalog.Entries.Count);
        Assert.Equal(30, PresetCatalog.Entries.Select(p => p.Design.Id).Distinct().Count());
        foreach (var preset in PresetCatalog.Entries)
        {
            var design = Validation.Design(preset.Design, Images);
            Assert.NotEmpty(preset.Tags); Assert.NotEmpty(design.Layers);
            Assert.Equal(design.Id, design.SourcePresetId);
        }
    }
    [Fact]
    public void ShareCodeMatchesRawDeflateContractAndDropsPersonalMetadata()
    {
        var original = Validation.Copy(PresetCatalog.Entries[0].Design); original.Favorite = true;
        string code = ShareCode.Encode(original, Images); Assert.StartsWith("LC2:", code); Assert.DoesNotContain("=", code);
        string encoded = code[4..].Replace('-', '+').Replace('_', '/');
        using var bytes = new MemoryStream(Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '=')));
        using var deflate = new DeflateStream(bytes, CompressionMode.Decompress);
        var metadata = JsonSerializer.Deserialize<CrosshairDesign>(deflate, ContractJson.Options)!;
        Assert.Empty(metadata.Id); Assert.False(metadata.Favorite); Assert.Null(metadata.SourcePresetId); Assert.Equal(default, metadata.UpdatedAt);
        var decoded = ShareCode.Decode(" \n" + code + "\t", Images);
        Assert.NotEqual(original.Id, decoded.Id); Assert.Equal(original.Name, decoded.Name);
        Assert.False(decoded.Favorite); Assert.Null(decoded.SourcePresetId);
        Assert.Equal(original.Layers[0].Length, decoded.Layers[0].Length);
    }
    [Theory]
    [InlineData("")] [InlineData("LC1:abc")] [InlineData("LC2:====")] [InlineData("LC2:a")]
    public void MalformedCodesAreRejected(string code) => Assert.Throws<CoreException>(() => ShareCode.Decode(code, Images));
    [Fact]
    public void DecompressionBombAndImagesAreRejected()
    {
        using var compressed = new MemoryStream();
        using (var stream = new DeflateStream(compressed, CompressionLevel.SmallestSize, true)) stream.Write(new byte[70000]);
        string code = "LC2:" + Convert.ToBase64String(compressed.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Throws<CoreException>(() => ShareCode.Decode(code, Images));
        var error = Assert.Throws<CoreException>(() => ShareCode.Encode(new() { Layers = [new() { Kind = LayerKind.Image }] }, Images));
        Assert.Equal(IpcErrorCodes.Unsupported, error.Code);
    }
    [Fact]
    public async Task ExportImportEmbedsAndRemapsImagesAndAssignsNewDesignIds()
    {
        var files = new LibraryFiles(Images);
        string path = Path.Combine(_root, "source.png");
        using (var bitmap = new Bitmap(4, 4)) { bitmap.SetPixel(1, 1, Color.Red); bitmap.Save(path, ImageFormat.Png); }
        string image = await files.ImportImageAsync(path);
        var store = LegacyMigration.Defaults();
        store.Crosshairs[0].Layers = [new() { Kind = LayerKind.Image, ImageFile = image, Opacity = .5 }];
        string export = Path.Combine(_root, "design.lcx.json");
        await files.ExportAsync(new() { Path = export, CrosshairIds = [store.Crosshairs[0].Id] }, store);
        var archive = JsonSerializer.Deserialize<CrosshairExportFile>(await File.ReadAllTextAsync(export), ContractJson.Options)!;
        Assert.Single(archive.Images); Assert.NotEmpty(archive.Images[0].PngBase64);
        var imported = await files.ImportAsync(export); var design = Assert.Single(imported.Imported);
        Assert.NotEqual(store.Crosshairs[0].Id, design.Id); Assert.NotEqual(image, design.Layers[0].ImageFile);
        Assert.Equal(.5, design.Layers[0].Opacity); Assert.True(File.Exists(Path.Combine(Images, design.Layers[0].ImageFile!)));
    }
    [Fact]
    public async Task PngLimitsAndArchiveTraversalAreValidatedBeforeWriting()
    {
        Assert.Throws<CoreException>(() => LibraryFiles.NormalizePng(new byte[100]));
        using var huge = new Bitmap(513, 1); using var stream = new MemoryStream(); huge.Save(stream, ImageFormat.Png);
        Assert.Throws<CoreException>(() => LibraryFiles.NormalizePng(stream.ToArray()));
        string archivePath = Path.Combine(_root, "bad.lcx.json");
        await File.WriteAllTextAsync(archivePath, JsonSerializer.Serialize(new CrosshairExportFile { Crosshairs = [new() { Name = "x" }], Images = [new() { FileName = "../evil.png", PngBase64 = "AA==" }] }, ContractJson.Options));
        await Assert.ThrowsAsync<CoreException>(() => new LibraryFiles(Images).ImportAsync(archivePath));
        Assert.False(Directory.Exists(Images));
    }
}
