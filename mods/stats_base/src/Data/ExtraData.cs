namespace Cthangover.Mods.Stats
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Reads the stats keys other mods' entities carry in their JSON extension data
    /// (<c>[JsonExtensionData] ExtraProperties</c> of characters, items, …). This is how the
    /// stats reach entities they do not own: a character gets <c>"Archetype"</c>,
    /// <c>"Stats"</c> and <c>"Traits"</c>, an item gets <c>"Traits"</c>, and neither the
    /// characters nor the items mod has to know. Keys match in any case, like the entity
    /// JSON itself; a malformed value is logged under <c>STATS</c> and read as empty.
    /// </summary>
    public static class ExtraData
    {
        private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

        /// <summary>The value of <paramref name="key"/> (any case), or null.</summary>
        public static object Find(IDictionary<string, object> extra, string key)
        {
            if (extra == null)
                return null;
            foreach (var pair in extra)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }
            return null;
        }

        /// <summary>A string value (a number is written invariantly), or null.</summary>
        public static string Text(IDictionary<string, object> extra, string key)
        {
            return Find(extra, key) switch
            {
                JsonElement { ValueKind: JsonValueKind.String } s => s.GetString(),
                JsonElement { ValueKind: JsonValueKind.Number } n => n.GetDouble().ToString(CultureInfo.InvariantCulture),
                string s => s,
                _ => null
            };
        }

        /// <summary>The <c>"Traits"</c> list, or an empty list.</summary>
        public static List<TraitInfo> Traits(IDictionary<string, object> extra, string owner)
            => Read<List<TraitInfo>>(extra, "Traits", owner) ?? new List<TraitInfo>();

        /// <summary>
        /// The <c>"Stats"</c> object as stat id (lowercase) → value or formula text: numbers
        /// and strings are both accepted, <c>{"str": 8, "max_hp": "20 + vit * 4"}</c>.
        /// </summary>
        public static Dictionary<string, string> Stats(IDictionary<string, object> extra, string owner)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Find(extra, "Stats") is not JsonElement { ValueKind: JsonValueKind.Object } obj)
                return result;
            foreach (var prop in obj.EnumerateObject())
            {
                var value = prop.Value.ValueKind switch
                {
                    JsonValueKind.Number => prop.Value.GetDouble().ToString(CultureInfo.InvariantCulture),
                    JsonValueKind.String => prop.Value.GetString(),
                    _ => null
                };
                if (value == null)
                    GameLogger.Log("STATS", $"{owner}: Stats.{prop.Name} must be a number or a formula text", LogLevel.Error);
                else
                    result[prop.Name.ToLowerInvariant()] = value;
            }
            return result;
        }

        /// <summary>Deserializes <paramref name="key"/> into <typeparamref name="T"/>; null when absent or malformed.</summary>
        public static T Read<T>(IDictionary<string, object> extra, string key, string owner) where T : class
        {
            if (Find(extra, key) is not JsonElement element || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;
            try
            {
                return element.Deserialize<T>(Options);
            }
            catch (JsonException ex)
            {
                GameLogger.Log("STATS", $"{owner}: '{key}' is malformed: {ex.Message}", LogLevel.Error);
                return null;
            }
        }
    }
}
