using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// On-disk envelope for <c>user://catalog_cache.json</c>. Replaced the bare
    /// mod-entry array when collections were added to the catalog: the cache now
    /// stores both lists together so a refresh persists everything in one write.
    /// <see cref="CatalogService.LoadCache"/> still understands the legacy
    /// array-only format and falls back to it when the file does not start with
    /// an object.
    /// </summary>
    public class CatalogCacheRoot
    {
        /// <summary>Merged mod entries, as produced by <see cref="CatalogMerger"/>.</summary>
        [JsonPropertyName("mods")]
        public List<CatalogExportEntry> Mods { get; set; }

        /// <summary>Merged collection definitions, as produced by <see cref="CatalogCollectionMerger"/>.</summary>
        [JsonPropertyName("collections")]
        public List<ModCollection> Collections { get; set; }
    }
}
