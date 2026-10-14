using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Serializes a <see cref="Vector2"/> as a JSON array <c>[x, y]</c>.
    /// Applied via <c>[JsonConverter]</c> attribute on individual properties
    /// so that <see cref="System.Text.Json"/> can round-trip Godot vectors
    /// without a custom contract resolver.
    /// </summary>
    public class Vector2JsonConverter : JsonConverter<Vector2>
    {
        /// <summary>
        /// Reads a two-element <c>[x, y]</c> array into a <see cref="Vector2"/>.
        /// Malformed JSON raises <see cref="JsonException"/> naming the offending token.
        /// </summary>
        public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException($"Expected StartArray for Vector2, got {reader.TokenType}.");

            reader.Read();
            var x = reader.GetSingle();
            reader.Read();
            var y = reader.GetSingle();
            reader.Read();

            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException($"Expected EndArray for Vector2, got {reader.TokenType}.");

            return new Vector2(x, y);
        }

        /// <summary>Writes the vector as a compact <c>[x, y]</c> array.</summary>
        public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.X);
            writer.WriteNumberValue(value.Y);
            writer.WriteEndArray();
        }
    }
}
