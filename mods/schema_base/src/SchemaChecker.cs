namespace Cthangover.Mods.Schemas
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// Checks values against field schemas: unknown parameters, missing required ones,
    /// numbers that are not numbers, choices outside their options and references to
    /// entities that do not exist. The same checks feed the validator report (files on
    /// disk) and the command palette (a line being built), so an author sees in the
    /// editor what the report would say.
    /// <para/>
    /// Existence is asked through a callback, because the validator knows entities from
    /// the mods' files and the palette from the running game; a null callback skips it.
    /// </summary>
    public static class SchemaChecker
    {
        private static readonly string[] BoolWords = { "0", "1", "true", "false", "yes", "no" };

        /// <summary>Checks named values of <paramref name="owner"/> (an action or command name, for messages).</summary>
        public static List<SchemaIssue> CheckNamed(string owner, IReadOnlyList<FieldSchema> fields, IReadOnlyDictionary<string, string> named, Func<string, string, bool> exists)
        {
            var issues = new List<SchemaIssue>();
            fields ??= new List<FieldSchema>();
            named ??= new Dictionary<string, string>();
            foreach (var key in named.Keys)
            {
                if (!fields.Any(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase)))
                    issues.Add(new SchemaIssue(false, "SCHEMA_UNKNOWN_PARAM", $"'{owner}' has no parameter '{key}' (known: {string.Join(", ", fields.Select(f => f.Key))})"));
            }
            foreach (var field in fields)
            {
                var value = named.FirstOrDefault(p => string.Equals(p.Key, field.Key, StringComparison.OrdinalIgnoreCase)).Value;
                issues.AddRange(CheckValue(owner, field, value, exists));
            }
            return issues;
        }

        /// <summary>Checks positional values in order against <paramref name="fields"/>.</summary>
        public static List<SchemaIssue> CheckPositional(string owner, IReadOnlyList<FieldSchema> fields, IReadOnlyList<string> positional, Func<string, string, bool> exists)
        {
            var issues = new List<SchemaIssue>();
            fields ??= new List<FieldSchema>();
            for (var i = 0; i < fields.Count; i++)
                issues.AddRange(CheckValue(owner, fields[i], positional != null && i < positional.Count ? positional[i] : null, exists));
            return issues;
        }

        /// <summary>Checks one value (null or empty = not given).</summary>
        public static List<SchemaIssue> CheckValue(string owner, FieldSchema field, string value, Func<string, string, bool> exists)
        {
            var issues = new List<SchemaIssue>();
            if (string.IsNullOrWhiteSpace(value))
            {
                if (field.Required)
                    issues.Add(new SchemaIssue(true, "SCHEMA_MISSING_PARAM", $"'{owner}' needs '{field.Key}'"));
                return issues;
            }
            value = value.Trim();
            switch (field.Type?.ToLowerInvariant())
            {
                case "int":
                    if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
                        issues.Add(new SchemaIssue(true, "SCHEMA_BAD_VALUE", $"'{owner}': '{field.Key}' must be a whole number, got '{value}'"));
                    break;
                case "number":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                        issues.Add(new SchemaIssue(true, "SCHEMA_BAD_VALUE", $"'{owner}': '{field.Key}' must be a number, got '{value}'"));
                    break;
                case "bool":
                    if (!BoolWords.Contains(value.ToLowerInvariant()))
                        issues.Add(new SchemaIssue(false, "SCHEMA_BAD_VALUE", $"'{owner}': '{field.Key}' should be 1 or 0, got '{value}'"));
                    break;
                case "choice":
                    if (field.Options is { Count: > 0 } && !field.Options.Contains(value, StringComparer.OrdinalIgnoreCase))
                        issues.Add(new SchemaIssue(false, "SCHEMA_BAD_VALUE", $"'{owner}': '{field.Key}' is one of {string.Join(", ", field.Options)}, got '{value}'"));
                    break;
                case "ref":
                    CheckRef(owner, field, field.Kind, value, exists, issues);
                    break;
                case "ref_list":
                    foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                        CheckRef(owner, field, field.Kind, part, exists, issues);
                    break;
                case "who":
                    if (!string.Equals(value, "player", StringComparison.OrdinalIgnoreCase) && !string.Equals(value, "party", StringComparison.OrdinalIgnoreCase))
                        CheckRef(owner, field, "character", value, exists, issues);
                    break;
            }
            return issues;
        }

        private static void CheckRef(string owner, FieldSchema field, string kind, string id, Func<string, string, bool> exists, List<SchemaIssue> issues)
        {
            if (exists == null || string.IsNullOrWhiteSpace(kind))
                return;
            if (!exists(kind, id))
                issues.Add(new SchemaIssue(true, "SCHEMA_UNKNOWN_REF", $"'{owner}': '{field.Key}' names {kind} '{id}', which no loaded mod defines"));
        }
    }
}
