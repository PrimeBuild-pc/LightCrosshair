using System.IO.Compression;
using System.Text;
using System.Text.Json;
using LightCrosshair.UI.Services;

namespace LightCrosshair.UI.Core;

/// <summary>Share codes for <see cref="FakeCoreClient"/> only. The real client asks Core.</summary>
internal static class ShareCodes
{
    public static string Encode(CrosshairDesign design)
    {
        if (design.Layers.Exists(layer => layer.Kind == LayerKind.Image))
            throw new CoreException(IpcErrorCodes.Unsupported);
        var copy = JsonClone.Clone(design);
        copy.Id = "";
        copy.Favorite = false;
        copy.SourcePresetId = null;
        copy.UpdatedAt = default;
        var json = JsonSerializer.Serialize(copy, ContractJson.Options);
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(Encoding.UTF8.GetBytes(json));
        var b64 = Convert.ToBase64String(output.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var code = ContractInfo.ShareCodePrefix + b64;
        if (code.Length > ContractInfo.MaxShareCodeLength)
            throw new CoreException(IpcErrorCodes.Validation);
        return code;
    }

    public static CrosshairDesign Decode(string code)
    {
        var text = (code ?? "").Trim();
        if (!text.StartsWith(ContractInfo.ShareCodePrefix, StringComparison.Ordinal))
            throw new CoreException(IpcErrorCodes.Validation);
        var b64 = text[ContractInfo.ShareCodePrefix.Length..].Replace('-', '+').Replace('_', '/');
        b64 += (b64.Length % 4) switch { 2 => "==", 3 => "=", 0 => "", _ => throw new CoreException(IpcErrorCodes.Validation) };
        byte[] raw;
        try { raw = Convert.FromBase64String(b64); }
        catch (FormatException) { throw new CoreException(IpcErrorCodes.Validation); }

        using var input = new MemoryStream(raw);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        var design = JsonSerializer.Deserialize<CrosshairDesign>(Encoding.UTF8.GetString(output.ToArray()), ContractJson.Options)
            ?? throw new CoreException(IpcErrorCodes.Validation);
        design.Id = Guid.NewGuid().ToString("N");
        design.UpdatedAt = DateTimeOffset.UtcNow;
        return design;
    }
}
