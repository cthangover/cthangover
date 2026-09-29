using System;
using System.Text;
using System.Text.Json;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Reads the top-level <c>"Kind"</c> discriminator from a JSON text without
    /// deserialising the document. The entity resolver walks every <c>.json</c>
    /// file of every mod, and the overwhelming majority of those files are not
    /// entity data at all (manifests, collections, live2d models, scene
    /// definitions, patches). Peeking the first property with
    /// <see cref="Utf8JsonReader"/> keeps that discovery pass cheap and, more
    /// importantly, makes the routing decision before any DTO-specific parsing
    /// happens — a file is only ever deserialised as the one type its kind
    /// names.
    /// <para/>
    /// The peek is deliberately silent: files without a kind are simply not
    /// data, not errors. Callers report unknown kinds separately via
    /// <see cref="Factories.EntityKindRegistry.IsKnown"/>.
    /// </summary>
    public static class EntityKindPeek
    {
        /// <summary>
        /// Attempts to read the root object's <c>"Kind"</c> string. Returns
        /// <c>false</c> for invalid JSON, non-object roots, missing kinds and
        /// non-string kinds.
        /// </summary>
        /// <param name="json">Raw file text, before include expansion — the kind always lives in the file itself.</param>
        /// <param name="kind">Normalised lowercase kind when the method returns <c>true</c>.</param>
        public static bool TryRead(string json, out string kind)
        {
            kind = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });

                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                    return false;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        return false;
                    if (reader.TokenType != JsonTokenType.PropertyName)
                        return false;

                    var propertyName = reader.GetString();
                    if (!reader.Read())
                        return false;

                    if (string.Equals(propertyName, "Kind", StringComparison.OrdinalIgnoreCase))
                    {
                        if (reader.TokenType != JsonTokenType.String)
                            return false;

                        kind = reader.GetString()?.Trim().ToLowerInvariant();
                        return !string.IsNullOrEmpty(kind);
                    }

                    if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                        reader.Skip();
                }

                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
