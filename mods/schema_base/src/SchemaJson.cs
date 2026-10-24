namespace Cthangover.Mods.Schemas
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json.Nodes;

    /// <summary>
    /// Between a form's text value and the JSON node of a field: numbers stay numbers, bools
    /// stay bools, a <c>ref_list</c> is an array of ids, a <c>map</c> value is a number when
    /// it is one and a formula text otherwise — the shapes the engine's JSON readers expect.
    /// </summary>
    public static class SchemaJson
    {
        /// <summary>The node for <paramref name="text"/> of a field (null for an empty value).</summary>
        public static JsonNode ToNode(FieldSchema field, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            text = text.Trim();
            switch (field.Type?.ToLowerInvariant())
            {
                case "int":
                    return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var i) ? JsonValue.Create(i) : JsonValue.Create(text);
                case "number":
                    return NumberOrText(text);
                case "bool":
                    return JsonValue.Create(text is "1" or "true" or "True" or "yes");
                case "ref_list":
                    return new JsonArray(text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(s => (JsonNode)JsonValue.Create(s)).ToArray());
                case "json":
                    try
                    {
                        return JsonNode.Parse(text);
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        return JsonValue.Create(text);
                    }
                default:
                    return JsonValue.Create(text);
            }
        }

        /// <summary>A number node when <paramref name="text"/> is a number, else the text (a formula).</summary>
        public static JsonNode NumberOrText(string text)
        {
            if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var i))
                return JsonValue.Create(i);
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return JsonValue.Create(d);
            return JsonValue.Create(text);
        }

        /// <summary>The text of a node for a form (an array of strings joins with commas).</summary>
        public static string ToText(JsonNode node)
        {
            switch (node)
            {
                case null:
                    return string.Empty;
                case JsonArray array when array.All(n => n is JsonValue):
                    return string.Join(",", array.Select(n => n?.ToString()));
                case JsonValue value when value.TryGetValue(out bool b):
                    return b ? "1" : string.Empty;
                case JsonValue value when value.TryGetValue(out double d):
                    return d.ToString(CultureInfo.InvariantCulture);
                case JsonValue value:
                    return value.ToString();
                default:
                    return node.ToJsonString();
            }
        }
    }
}
