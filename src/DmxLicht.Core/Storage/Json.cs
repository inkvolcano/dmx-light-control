using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DmxLicht.Core.Storage;

/// <summary>Shared JSON settings: camelCase, enums as text, colours as "#RRGGBB".</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = Create(indented: true);
    public static readonly JsonSerializerOptions Compact = Create(indented: false);

    static JsonSerializerOptions Create(bool indented)
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        o.Converters.Add(new ColorConverter());
        o.Converters.Add(new ByteArrayAsNumbersConverter());
        return o;
    }

    sealed class ColorConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            ColorUtil.ParseHex(reader.GetString() ?? "", Color.White);

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options) =>
            writer.WriteStringValue(ColorUtil.Hex(value));
    }

    /// <summary>Fader values as a readable number array instead of base64.</summary>
    sealed class ByteArrayAsNumbersConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var list = new List<byte>();
            if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected array");
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                list.Add((byte)Math.Clamp(reader.GetInt32(), 0, 255));
            return list.ToArray();
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (byte b in value) writer.WriteNumberValue(b);
            writer.WriteEndArray();
        }
    }
}
