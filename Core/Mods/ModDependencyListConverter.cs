using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// JSON converter for List&lt;ModDependency&gt;.
    /// Reads a JSON array of strings, parses each via ModDependency.Parse().
    /// Invalid strings are logged with a warning and added to the result
    /// with IsValid=false (the mod is processed in ValidateDependencies).
    /// On serialization, restores the raw string via ModDependency.ToString().
    /// </summary>
    public class ModDependencyListConverter : JsonConverter<List<ModDependency>>
    {
        /// <summary>Reads a JSON array of dependency strings, parsing each via <see cref="ModDependency.Parse"/>.</summary>
        public override List<ModDependency> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("depends must be an array of strings");

            var result = new List<ModDependency>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var raw = reader.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var dep = ModDependency.Parse(raw);
                if (!dep.IsValid)
                {
                    GameLogger.Log("MODS", $"Invalid dependency format: '{raw}' — {dep.Error}", LogLevel.Error);
                }
                result.Add(dep);
            }
            return result;
        }

        /// <summary>Writes each dependency back as its canonical string form.</summary>
        public override void Write(Utf8JsonWriter writer, List<ModDependency> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var dep in value)
            {
                writer.WriteStringValue(dep.ToString());
            }
            writer.WriteEndArray();
        }
    }
}
