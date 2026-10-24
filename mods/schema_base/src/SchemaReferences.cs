namespace Cthangover.Mods.Schemas
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The entity references a value makes, by its field type: a <c>ref</c> names one entity
    /// of the field's kind, a <c>ref_list</c> a comma list, a <c>who</c> a character unless it
    /// is <c>player</c> or <c>party</c>, a <c>scene</c> a logical scene. Values built at run
    /// time (<c>${var}</c>) are no references. The validator turns these into graph
    /// references, the same graph its own strategies fill, so missing targets are reported
    /// once, by one rule.
    /// </summary>
    public static class SchemaReferences
    {
        /// <summary>The kind of a scene reference.</summary>
        public const string SceneKind = "scene";

        /// <summary>(kind, id) pairs <paramref name="value"/> of <paramref name="field"/> refers to.</summary>
        public static IEnumerable<(string Kind, string Id)> Of(FieldSchema field, string value)
        {
            if (field == null || string.IsNullOrWhiteSpace(value) || value.Contains("${", StringComparison.Ordinal))
                yield break;
            value = value.Trim();
            switch (field.Type?.ToLowerInvariant())
            {
                case "ref" when !string.IsNullOrWhiteSpace(field.Kind):
                    yield return (field.Kind, value);
                    break;
                case "ref_list" when !string.IsNullOrWhiteSpace(field.Kind):
                    foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                        yield return (field.Kind, part);
                    break;
                case "who" when !string.Equals(value, "player", StringComparison.OrdinalIgnoreCase) && !string.Equals(value, "party", StringComparison.OrdinalIgnoreCase):
                    yield return ("character", value);
                    break;
                case "scene":
                    yield return (SceneKind, value);
                    break;
            }
        }
    }
}
