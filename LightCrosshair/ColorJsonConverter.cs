using System;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LightCrosshair
{
    public class ColorJsonConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // GetString can return null; normalize to empty string to satisfy non-null contract.
            return reader.GetString() ?? string.Empty;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
