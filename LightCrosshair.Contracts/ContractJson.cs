using System.Text.Json;
using System.Text.Json.Serialization;

namespace LightCrosshair.Contracts;

/// <summary>The only JSON settings for IPC, store.json, export files and share codes, so both processes agree byte-for-byte.</summary>
public static class ContractJson
{
    public static readonly JsonSerializerOptions Options = Create(indented: false);

    /// <summary>For files humans may open (store.json, export files).</summary>
    public static readonly JsonSerializerOptions IndentedOptions = Create(indented: true);

    private static JsonSerializerOptions Create(bool indented) => new(JsonSerializerDefaults.Web)
    {
        WriteIndented = indented,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
