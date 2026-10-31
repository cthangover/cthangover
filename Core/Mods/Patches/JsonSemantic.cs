namespace Cthangover.Core.Mods.Patches
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    /// <summary>
    /// Value equality of JSON nodes as the game reads them, not as text: escapes do not
    /// matter (an escaped <c>\u</c> sequence equals the plain letter), numbers compare by value (<c>1</c> equals
    /// <c>1.0</c>), property names are case-insensitive and a property holding <c>null</c>
    /// equals an absent one (DTO serialisation writes defaults that a hand-written patch
    /// omits). Used by <c>$remove</c> without a key and for merge-key matching.
    /// </summary>
    public static class JsonSemantic
    {
        /// <summary>
        /// True when both nodes denote the same value. <paramref name="ignoreStringCase"/>
        /// compares strings case-insensitively, as ids and merge keys are compared.
        /// </summary>
        public static bool Equal(JsonNode a, JsonNode b, bool ignoreStringCase = false)
        {
            if (IsNull(a) || IsNull(b))
                return IsNull(a) && IsNull(b);

            if (a is JsonObject oa)
                return b is JsonObject ob && ObjectsEqual(oa, ob, ignoreStringCase);
            if (a is JsonArray aa)
                return b is JsonArray ab && ArraysEqual(aa, ab, ignoreStringCase);
            if (b is JsonObject || b is JsonArray)
                return false;

            var kindA = a.GetValueKind();
            var kindB = b.GetValueKind();
            if (kindA != kindB)
                return false;

            switch (kindA)
            {
                case JsonValueKind.String:
                    return string.Equals(a.GetValue<string>(), b.GetValue<string>(),
                        ignoreStringCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                case JsonValueKind.Number:
                    return NumbersEqual(a.ToJsonString(), b.ToJsonString());
                default:
                    return true;
            }
        }

        private static bool IsNull(JsonNode node)
            => node == null || (node is JsonValue && node.GetValueKind() == JsonValueKind.Null);

        private static bool ObjectsEqual(JsonObject a, JsonObject b, bool ignoreStringCase)
        {
            foreach (var property in a)
            {
                if (!Equal(property.Value, Find(b, property.Key), ignoreStringCase))
                    return false;
            }

            foreach (var property in b)
            {
                if (!IsNull(property.Value) && !a.Any(p => string.Equals(p.Key, property.Key, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            return true;
        }

        private static JsonNode Find(JsonObject obj, string name)
        {
            foreach (var property in obj)
            {
                if (string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }
            return null;
        }

        private static bool ArraysEqual(JsonArray a, JsonArray b, bool ignoreStringCase)
        {
            if (a.Count != b.Count)
                return false;
            for (var i = 0; i < a.Count; i++)
            {
                if (!Equal(a[i], b[i], ignoreStringCase))
                    return false;
            }
            return true;
        }

        private static bool NumbersEqual(string a, string b)
        {
            if (decimal.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var da)
                && decimal.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out var db))
                return da == db;
            return double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var fa)
                && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out var fb)
                && fa.Equals(fb);
        }
    }
}
