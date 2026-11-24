namespace Cthangover.Core.Mods.Memory
{
    /// <summary>Counters are cumulative for one store instance, including across invalidations.</summary>
    public readonly struct CacheMetrics
    {
        /// <summary>Number of lookups served by the cache.</summary>
        public long? Hits { get; }

        /// <summary>Number of lookups the cache could not serve.</summary>
        public long? Misses { get; }

        /// <summary>Entries removed by the eviction policy. Explicit invalidation is separate.</summary>
        public long? Evictions { get; }

        /// <summary>Configured payload budget in bytes, when the cache enforces one.</summary>
        public long? BudgetBytes { get; }

        /// <summary>Maximum number of retained entries when the cache enforces an entry limit.</summary>
        public int? EntryLimit { get; }

        /// <summary>Configured resource-byte retention target. Owners may keep pinned entries above the target.</summary>
        public long? ResourceBudgetBytes { get; }

        /// <summary>Captures the counters and budget the cache implementation provides.</summary>
        public CacheMetrics(long? hits = null, long? misses = null, long? evictions = null, long? budgetBytes = null)
            : this(hits, misses, evictions, budgetBytes, null, null) { }

        /// <summary>Captures counters with optional entry and engine-resource limits.</summary>
        public CacheMetrics(long? hits, long? misses, long? evictions, long? budgetBytes, int? entryLimit, long? resourceBudgetBytes)
        {
            Hits = hits;
            Misses = misses;
            Evictions = evictions;
            BudgetBytes = budgetBytes;
            EntryLimit = entryLimit;
            ResourceBudgetBytes = resourceBudgetBytes;
        }
    }
}
