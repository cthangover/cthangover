using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// JSON envelope for the online mod catalog. Expected structure:
    /// <code>
    /// {
    ///   "catalog": { "version": 1, "updated_at": "2026-..." },
    ///   "mods": [ ...CatalogExportEntry... ]
    /// }
    /// </code>
    /// <see cref="CatalogService"/> fetches this from each configured URL,
    /// deserializes, and merges the <c>"mods"</c> arrays.
    /// </summary>
    public class CatalogRoot
    {
        /// <summary>Catalog-level metadata (version, last-updated timestamp).</summary>
        [JsonPropertyName("catalog")]
        public CatalogMeta Meta { get; set; }

        /// <summary>List of mod entries available in this catalog.</summary>
        [JsonPropertyName("mods")]
        public List<CatalogExportEntry> Mods { get; set; }
    }

    /// <summary>
    /// Metadata about the catalog file itself — version of the format
    /// and when it was last refreshed on the server.
    /// </summary>
    public class CatalogMeta
    {
        /// <summary>Schema version of this catalog file.</summary>
        [JsonPropertyName("version")]
        public int Version { get; set; }

        /// <summary>ISO-8601 timestamp of the last update on the server.</summary>
        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; set; }
    }
}
