using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Serializes a <c>List&lt;Vector2&gt;</c> as a JSON array of <c>[x, y]</c> arrays.
    /// Used with <c>[JsonConverter]</c> on collection-typed properties to handle
    /// polygon point lists in map zone definitions.
    /// </summary>
    public class Vector2ListJsonConverter : JsonConverter<List<Vector2>>
    {
        public override List<Vector2> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException($"Expected StartArray for Vector2 list, got {reader.TokenType}.");

            var list = new List<Vector2>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return list;

                if (reader.TokenType != JsonTokenType.StartArray)
                    throw new JsonException($"Expected StartArray for Vector2 element, got {reader.TokenType}.");

                reader.Read();
                var x = reader.GetSingle();
                reader.Read();
                var y = reader.GetSingle();
                reader.Read();

                if (reader.TokenType != JsonTokenType.EndArray)
                    throw new JsonException($"Expected EndArray for Vector2 element, got {reader.TokenType}.");

                list.Add(new Vector2(x, y));
            }

            return list;
        }

        public override void Write(Utf8JsonWriter writer, List<Vector2> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var v in value)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(v.X);
                writer.WriteNumberValue(v.Y);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }
    }
}
