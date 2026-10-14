using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Serializes a nullable <see cref="Vector2"/> as <c>[x, y]</c> or JSON
    /// <c>null</c>. Needed for optional map zone fields such as
    /// <c>label_pos</c>: a plain <see cref="Vector2JsonConverter"/> cannot be
    /// attributed onto a <c>Vector2?</c> property reliably, and the difference
    /// between "coordinate not set" and "coordinate (0,0)" is meaningful for
    /// optional overlays.
    /// </summary>
    public class Vector2NullableJsonConverter : JsonConverter<Vector2?>
    {
        /// <summary>
        /// Returns <c>null</c> for JSON <c>null</c>, otherwise reads a two-element
        /// <c>[x, y]</c> array into a <see cref="Vector2"/>.
        /// </summary>
        public override Vector2? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException($"Expected StartArray or Null for Vector2?, got {reader.TokenType}.");

            reader.Read();
            var x = reader.GetSingle();
            reader.Read();
            var y = reader.GetSingle();
            reader.Read();

            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException($"Expected EndArray for Vector2?, got {reader.TokenType}.");

            return new Vector2(x, y);
        }

        /// <summary>
        /// Writes JSON <c>null</c> for an empty value, otherwise a compact <c>[x, y]</c> array,
        /// so "coordinate not set" round-trips distinctly from <c>(0, 0)</c>.
        /// </summary>
        public override void Write(Utf8JsonWriter writer, Vector2? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartArray();
            writer.WriteNumberValue(value.Value.X);
            writer.WriteNumberValue(value.Value.Y);
            writer.WriteEndArray();
        }
    }
}
