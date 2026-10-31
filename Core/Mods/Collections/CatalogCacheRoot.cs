using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// On-disk envelope for <c>user://catalog_cache.json</c>: both lists are
    /// stored together so a refresh persists everything in one write. A cache
    /// in any other format is ignored (no reading of old formats).
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
