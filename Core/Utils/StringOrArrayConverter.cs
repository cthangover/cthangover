using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// A <see cref="System.Text.Json"/> converter that handles JSON fields
    /// which can be either a single string or an array of strings, normalising
    /// both into a <see cref="List{String}"/>. This accommodates the common
    /// mod-authoring convention where a single-value property may be written
    /// as a plain string for brevity but expanded to an array later.
    /// </summary>
    /// <remarks>
    /// On serialisation, single-element lists are collapsed back to a plain
    /// string value; empty lists emit <c>null</c>; multi-element lists produce
    /// a JSON array. This round-trips the "terse authoring" pattern.
    /// </remarks>
    public class StringOrArrayConverter : JsonConverter<List<string>>
    {
        /// <summary>
        /// Normalises a JSON string token into a single-element list, or
        /// iterates a JSON array token to collect all string elements.
        /// Any other token type throws <see cref="JsonException"/>.
        /// </summary>
        public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return new List<string> { reader.GetString() };
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = new List<string>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                        break;
                    if (reader.TokenType == JsonTokenType.String)
                        list.Add(reader.GetString());
                }
                return list;
            }

            throw new JsonException($"Expected string or array, got {reader.TokenType}");
        }

        /// <summary>
        /// Writes the list back to JSON. A single-element list is serialised
        /// as a string for readability; an empty list emits <c>null</c>;
        /// otherwise a full JSON array is produced.
        /// </summary>
        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            if (value == null || value.Count == 0)
            {
                writer.WriteNullValue();
                return;
            }

            if (value.Count == 1)
            {
                writer.WriteStringValue(value[0]);
            }
            else
            {
                writer.WriteStartArray();
                foreach (var item in value)
                    writer.WriteStringValue(item);
                writer.WriteEndArray();
            }
        }
    }
}
