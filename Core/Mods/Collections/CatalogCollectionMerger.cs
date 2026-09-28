using System;
using System.Collections.Generic;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Pure merge rules for collections published by several online catalogs,
    /// the collection counterpart of <see cref="CatalogMerger"/>. Collections
    /// carry no content hashes, so the rules are simpler: entries with the same
    /// id collapse into the one with the highest version, and every catalog that
    /// published the id is recorded in <see cref="ModCollection.SourceUrls"/> so
    /// the UI can show provenance (and future per-source variants).
    /// </summary>
    public static class CatalogCollectionMerger
    {
        /// <summary>
        /// Tags a freshly fetched collection with the catalog URL it came from,
        /// preserving URLs already present.
        /// </summary>
        public static void TagSource(ModCollection collection, string url)
        {
            if (collection == null || string.IsNullOrWhiteSpace(url))
                return;

            collection.SourceUrls ??= new List<string>();
            if (!collection.SourceUrls.Contains(url))
                collection.SourceUrls.Add(url);
        }

        /// <summary>
        /// Merges raw catalog collections into the deduplicated list consumed by
        /// <see cref="CatalogService.CachedCollections"/>. Returns a new list; the
        /// winning collection objects are mutated in place with the merged sources.
        /// </summary>
        public static List<ModCollection> MergeAndDeduplicate(List<ModCollection> collections)
        {
            var result = new List<ModCollection>();
            if (collections == null)
                return result;

            var byId = new Dictionary<string, ModCollection>(StringComparer.OrdinalIgnoreCase);

            foreach (var collection in collections)
            {
                if (collection == null || string.IsNullOrEmpty(collection.Id))
                    continue;

                if (!byId.TryGetValue(collection.Id, out var existing))
                {
                    byId[collection.Id] = collection;
                    result.Add(collection);
                    continue;
                }

                if (VersionHelper.Compare(collection.Version ?? "0.0.0", existing.Version ?? "0.0.0") > 0)
                {
                    MergeSources(collection, existing);
                    var index = result.IndexOf(existing);
                    if (index >= 0)
                        result[index] = collection;
                    byId[collection.Id] = collection;
                }
                else
                {
                    MergeSources(existing, collection);
                }
            }

            return result;
        }

        private static void MergeSources(ModCollection target, ModCollection other)
        {
            if (other.SourceUrls == null)
                return;

            target.SourceUrls ??= new List<string>();
            foreach (var url in other.SourceUrls)
            {
                if (!string.IsNullOrWhiteSpace(url) && !target.SourceUrls.Contains(url))
                    target.SourceUrls.Add(url);
            }
        }
    }
}
