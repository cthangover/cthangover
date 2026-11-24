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

        /// <summary>Captures the counters and budget the cache implementation provides.</summary>
        public CacheMetrics(long? hits = null, long? misses = null, long? evictions = null, long? budgetBytes = null)
        {
            Hits = hits;
            Misses = misses;
            Evictions = evictions;
            BudgetBytes = budgetBytes;
        }
    }
}
