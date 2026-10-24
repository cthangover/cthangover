namespace Cthangover.Mods.Schemas
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Writes scenario lines from schema values, in the DSL's own syntax: values with spaces
    /// or quotes are quoted (inner quotes escaped; a named pair is quoted whole,
    /// <c>"text=two words"</c>), empty values are left out, named
    /// parameters follow the schema's order, the arrow target comes last. What the palette
    /// inserts is exactly what an author would have typed, so the text stays the source of
    /// truth and every other tool (validator, studio, atlases) reads it unchanged.
    /// </summary>
    public static class SchemaLine
    {
        /// <summary><c>action &lt;id&gt; k=v …</c> for an action schema.</summary>
        public static string Action(ActionSchema schema, IReadOnlyDictionary<string, string> values)
        {
            var line = new StringBuilder("action ").Append(schema.Id);
            AppendNamed(line, schema.Params, values);
            return line.ToString();
        }

        /// <summary>A command line: positional values in order, then named ones, then <c>-&gt; :label</c>.</summary>
        public static string Command(CommandSchema schema, IReadOnlyDictionary<string, string> values, string arrow)
        {
            var line = new StringBuilder(schema.Id);
            foreach (var field in schema.Positional ?? new List<FieldSchema>())
            {
                var value = Get(values, field.Key);
                if (string.IsNullOrEmpty(value))
                    continue;
                line.Append(' ').Append(Quote(value));
            }
            AppendNamed(line, schema.Params, values);
            if (!string.IsNullOrWhiteSpace(arrow))
                line.Append(" -> :").Append(arrow.Trim().TrimStart(':'));
            return line.ToString();
        }

        /// <summary>A value as one DSL token: as it is when it has no spaces or quotes, else quoted.</summary>
        public static string Quote(string value)
        {
            if (value.Length > 0 && !value.Any(c => char.IsWhiteSpace(c) || c == '"'))
                return value;
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void AppendNamed(StringBuilder line, List<FieldSchema> fields, IReadOnlyDictionary<string, string> values)
        {
            foreach (var field in fields ?? new List<FieldSchema>())
            {
                var value = Get(values, field.Key);
                if (string.IsNullOrEmpty(value))
                    continue;
                // The DSL splits a token at its first '=' after reading it whole, and quotes
                // only work at the start of a token: a value with spaces quotes the pair.
                line.Append(' ').Append(Quote(field.Key + "=" + value));
            }
        }

        private static string Get(IReadOnlyDictionary<string, string> values, string key)
            => key != null && values != null && values.TryGetValue(key, out var value) ? value?.Trim() : null;
    }
}
