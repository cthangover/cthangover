using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Utils
{
    public class FlagsStringEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                return (T)Enum.Parse(typeof(T), str, ignoreCase: true);
            }
            if (reader.TokenType == JsonTokenType.Number)
            {
                var val = reader.GetUInt32();
                return (T)Enum.ToObject(typeof(T), val);
            }
            throw new JsonException($"Unexpected token {reader.TokenType} for enum {typeof(T).Name}");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
