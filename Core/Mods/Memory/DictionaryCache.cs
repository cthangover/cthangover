namespace Cthangover.Core.Mods.Memory
{
    using System;
    using System.Collections.Generic;

    /// <summary>A dictionary for rebuildable values with explicit invalidation and lookup counters.</summary>
    public sealed class DictionaryCache<TKey, TValue> : ObservableDictionary<TKey, TValue>, ICache where TKey : notnull
    {
        private long hits;
        private long misses;

        /// <summary>Creates a cache that can also be used outside the mod pipeline and registered later.</summary>
        public DictionaryCache(IEqualityComparer<TKey> comparer = null, Func<TKey, TValue, MemoryPreviewEntry> preview = null)
            : base(comparer, preview) { }

        private protected override void RecordLookup(bool found)
        {
            if (found)
                hits++;
            else
                misses++;
        }

        private protected override CacheMetrics? ReadCacheMetrics() => new CacheMetrics(hits, misses);
    }
}
