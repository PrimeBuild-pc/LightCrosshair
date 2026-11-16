using System.IO.Compression;
using System.Text;
using System.Text.Json;
using LightCrosshair.Contracts;

namespace LightCrosshair.Core;

internal static class ShareCode
{
    public static string Encode(CrosshairDesign design, string imagesDirectory)
    {
        if (design?.Layers?.Any(l => l?.Kind == LayerKind.Image) == true) throw new CoreException(IpcErrorCodes.Unsupported, "Images require file export.");
        var copy = Validation.Design(design, imagesDirectory);
        copy.Id = ""; copy.Favorite = false; copy.SourcePresetId = null; copy.UpdatedAt = default;
        using var stream = new MemoryStream();
        using (var deflate = new DeflateStream(stream, CompressionLevel.SmallestSize, true)) JsonSerializer.Serialize(deflate, copy, ContractJson.Options);
        string code = ContractInfo.ShareCodePrefix + Convert.ToBase64String(stream.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        if (code.Length > ContractInfo.MaxShareCodeLength) throw Validation.Invalid("Share code is too long.");
        return code;
    }
    public static CrosshairDesign Decode(string? code, string imagesDirectory)
    {
        code = code?.Trim();
        if (code is null || code.Length > ContractInfo.MaxShareCodeLength || !code.StartsWith(ContractInfo.ShareCodePrefix, StringComparison.Ordinal)) throw Validation.Invalid("Invalid share code.");
        string encoded = code[ContractInfo.ShareCodePrefix.Length..];
        if (encoded.Length == 0 || encoded.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) throw Validation.Invalid("Invalid share code.");
        try
        {
            byte[] compressed = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/').PadRight((encoded.Length + 3) / 4 * 4, '='));
            using var input = new MemoryStream(compressed); using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            byte[] buffer = new byte[4096]; int count;
            while ((count = deflate.Read(buffer)) != 0)
            {
                if (output.Length + count > 64 * 1024) throw Validation.Invalid("Share code expands beyond the limit.");
                output.Write(buffer, 0, count);
            }
            var design = JsonSerializer.Deserialize<CrosshairDesign>(output.ToArray(), ContractJson.Options) ?? throw Validation.Invalid("Invalid share code.");
            if (design.Layers?.Any(l => l?.Kind == LayerKind.Image) == true) throw new CoreException(IpcErrorCodes.Unsupported, "Images require file export.");
            design.Id = Guid.NewGuid().ToString("N"); design.Favorite = false; design.SourcePresetId = null; design.UpdatedAt = DateTimeOffset.UtcNow;
            return Validation.Design(design, imagesDirectory);
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or JsonException or IOException) { throw Validation.Invalid("Invalid share code."); }
    }
}
