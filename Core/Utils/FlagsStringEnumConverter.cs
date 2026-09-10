using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// A <see cref="System.Text.Json"/> converter that deserialises a
    /// <c>[Flags]</c> enum from either a comma-separated string (e.g.
    /// <c>"OptionA, OptionB"</c>) or a raw integer value, and serialises
    /// it back as its <see cref="object.ToString"/> representation.
    /// </summary>
    /// <typeparam name="T">The <c>[Flags]</c> enum type.</typeparam>
    /// <remarks>
    /// This converter is registered with <c>JsonSerializerOptions</c> in
    /// the mod configuration pipeline, allowing human-editable JSON files
    /// to express bitmask enums as readable strings instead of opaque integers.
    /// </remarks>
    public class FlagsStringEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        /// <summary>
        /// Reads a <typeparamref name="T"/> value from JSON. When the token
        /// is a <see cref="JsonTokenType.String"/>, delegates to
        /// <see cref="Enum.Parse(Type, string, bool)"/> with case-insensitive
        /// matching so that <c>"OptionA, OptionB"</c> maps to the combined
        /// flags value. When the token is a <see cref="JsonTokenType.Number"/>,
        /// casts the raw <c>uint</c> to the enum's underlying type.
        /// </summary>
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

        /// <summary>
        /// Serialises the enum as its default <see cref="object.ToString"/>
        /// string, which for <c>[Flags]</c> types produces the comma-separated
        /// form (e.g. <c>"OptionA, OptionB"</c>).
        /// </summary>
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
