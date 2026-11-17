using System.Text.Json;

namespace LightCrosshair.UI.Services;

public static class JsonClone
{
    public static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, ContractJson.Options);
        return JsonSerializer.Deserialize<T>(json, ContractJson.Options)
            ?? throw new InvalidOperationException("Clone produced null.");
    }

    public static string Text<T>(T value) => JsonSerializer.Serialize(value, ContractJson.Options);
}
