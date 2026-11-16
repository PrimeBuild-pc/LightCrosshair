using System.Buffers.Binary;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.RegularExpressions;
using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal sealed class LibraryFiles(string imagesDirectory)
{
    private static string ArchivePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 32700 || !path.EndsWith(".lcx.json", StringComparison.OrdinalIgnoreCase)) throw Validation.Invalid("Expected a .lcx.json path.");
        return Path.GetFullPath(path);
    }
    internal static byte[] NormalizePng(byte[] bytes)
    {
        if (bytes.Length is < 33 || bytes.Length > ContractInfo.MaxImageBytes ||
            !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)) throw Validation.Invalid("Invalid PNG.");
        uint width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)), height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
        if (width is 0 or > ContractInfo.MaxImageSidePx || height is 0 or > ContractInfo.MaxImageSidePx) throw Validation.Invalid("Image dimensions exceed the limit.");
        try
        {
            using var input = new MemoryStream(bytes); using var image = Image.FromStream(input, false, true);
            if (image.RawFormat.Guid != ImageFormat.Png.Guid || image.Width != width || image.Height != height) throw Validation.Invalid("Invalid PNG.");
            using var clean = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(clean)) graphics.DrawImageUnscaled(image, 0, 0);
            using var output = new MemoryStream(); clean.Save(output, ImageFormat.Png); return output.ToArray();
        }
        catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.ExternalException) { throw Validation.Invalid("Invalid PNG."); }
    }
    public async Task<string> ImportImageAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw Validation.Invalid("Image path required.");
        var info = new FileInfo(Path.GetFullPath(path));
        if (!info.Exists) throw new CoreException(IpcErrorCodes.NotFound, "Image not found.");
        if (info.Length > ContractInfo.MaxImageBytes) throw Validation.Invalid("Image is too large.");
        byte[] bytes = await File.ReadAllBytesAsync(info.FullName).ConfigureAwait(false);
        byte[] normalized = await Task.Run(() => NormalizePng(bytes)).ConfigureAwait(false);
        return await StoreImageAsync(normalized).ConfigureAwait(false);
    }
    private async Task<string> StoreImageAsync(byte[] bytes)
    {
        Directory.CreateDirectory(imagesDirectory);
        string name = Guid.NewGuid().ToString("N") + ".png";
        await File.WriteAllBytesAsync(Path.Combine(imagesDirectory, name), bytes).ConfigureAwait(false); return name;
    }
    public async Task ExportAsync(ExportRequest request, StoreDocument store)
    {
        string path = ArchivePath(request.Path);
        if (request.CrosshairIds is null || request.CrosshairIds.Count is 0 or > 1000) throw Validation.Invalid("Select crosshairs to export.");
        var document = new CrosshairExportFile();
        foreach (string id in request.CrosshairIds.Distinct())
            document.Crosshairs.Add(Validation.Copy(store.Crosshairs.FirstOrDefault(d => d.Id == id) ?? throw new CoreException(IpcErrorCodes.NotFound, "Crosshair not found.")));
        foreach (string name in document.Crosshairs.SelectMany(d => d.Layers).Where(l => l.Kind == LayerKind.Image).Select(l => l.ImageFile!).Distinct())
        {
            if (!SafeImageName(name)) throw Validation.Invalid("Invalid image file name.");
            byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(imagesDirectory, name)).ConfigureAwait(false);
            document.Images.Add(new() { FileName = name, PngBase64 = Convert.ToBase64String(bytes) });
        }
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(document, ContractJson.IndentedOptions);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temp, json).ConfigureAwait(false);
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public async Task<ImportResult> ImportAsync(string? source)
    {
        string path = ArchivePath(source);
        var info = new FileInfo(path);
        if (!info.Exists) throw new CoreException(IpcErrorCodes.NotFound, "Export file not found.");
        if (info.Length > 32 * 1024 * 1024) throw Validation.Invalid("Export file is too large.");
        CrosshairExportFile archive;
        try { archive = JsonSerializer.Deserialize<CrosshairExportFile>(await File.ReadAllTextAsync(path).ConfigureAwait(false), ContractJson.Options) ?? throw Validation.Invalid("Invalid export file."); }
        catch (JsonException) { throw Validation.Invalid("Invalid export file."); }
        if (archive.Format != "lightcrosshair.crosshairs" || archive.Version != 1 || archive.Crosshairs is null || archive.Images is null || archive.Crosshairs.Count is 0 or > 1000 || archive.Images.Count > 1000) throw Validation.Invalid("Unsupported export file.");
        var images = new Dictionary<string, string>();
        var created = new List<string>();
        try
        {
            foreach (var image in archive.Images)
            {
                if (image is null || !SafeImageName(image.FileName) || image.PngBase64 is null || image.PngBase64.Length > 3 * 1024 * 1024 || images.ContainsKey(image.FileName)) throw Validation.Invalid("Invalid embedded image.");
                byte[] bytes;
                try { bytes = Convert.FromBase64String(image.PngBase64); }
                catch (FormatException) { throw Validation.Invalid("Invalid embedded image."); }
                bytes = await Task.Run(() => NormalizePng(bytes)).ConfigureAwait(false);
                string stored = await StoreImageAsync(bytes).ConfigureAwait(false); images.Add(image.FileName, stored); created.Add(stored);
            }
            var result = new ImportResult();
            foreach (var design in archive.Crosshairs)
            {
                try
                {
                    if (design?.Layers is null) throw Validation.Invalid("Invalid design.");
                    design.Id = Guid.NewGuid().ToString("N"); design.SourcePresetId = null; design.UpdatedAt = DateTimeOffset.UtcNow;
                    foreach (var layer in design.Layers)
                        if (layer?.Kind == LayerKind.Image)
                            layer.ImageFile = images.TryGetValue(layer.ImageFile ?? "", out string? file) ? file : throw Validation.Invalid("Missing embedded image.");
                    result.Imported.Add(Validation.Design(design, imagesDirectory));
                }
                catch (CoreException) { result.Warnings.Add("A crosshair could not be imported."); }
            }
            if (result.Imported.Count == 0) throw Validation.Invalid("No valid crosshairs in the file.");
            return result;
        }
        catch
        {
            foreach (string name in created) File.Delete(Path.Combine(imagesDirectory, name));
            throw;
        }
    }
    private static bool SafeImageName(string name) => Regex.IsMatch(name, "^[a-f0-9]{32}\\.png$", RegexOptions.CultureInvariant);
}
