using System;
using System.Text.Json;

namespace Cthangover.Validator
{
    /// <summary>
    /// Case-insensitive reads over <see cref="JsonElement"/> used by the JSON
    /// contract strategy. Mod JSON deliberately mixes naming styles
    /// (<c>"Id"</c>, <c>"id"</c>, <c>"defaultBackground"</c>) and the runtime
    /// reads it with case-insensitive options, so the validator must not depend
    /// on exact casing either.
    /// </summary>
    public static class JsonReadHelper
    {
        /// <summary>
        /// Returns the first matching property by exact name, then by
        /// case-insensitive comparison. Null when the element is not an object
        /// or no name matches.
        /// </summary>
        public static JsonElement? GetProperty(JsonElement element, params string[] names)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var name in names)
            {
                if (element.TryGetProperty(name, out var exact))
                    return exact;
            }

            foreach (var property in element.EnumerateObject())
            {
                foreach (var name in names)
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                        return property.Value;
                }
            }

            return null;
        }

        /// <summary>Returns the string value of the first matching property, or null.</summary>
        public static string GetString(JsonElement element, params string[] names)
        {
            var value = GetProperty(element, names);
            if (value == null || value.Value.ValueKind != JsonValueKind.String)
                return null;
            return value.Value.GetString();
        }

        /// <summary>True when the element exists and is an array.</summary>
        public static bool IsArray(JsonElement? element)
            => element != null && element.Value.ValueKind == JsonValueKind.Array;
    }
}
