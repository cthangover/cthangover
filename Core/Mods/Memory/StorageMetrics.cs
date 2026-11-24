namespace Cthangover.Core.Mods.Memory
{
    /// <summary>A point-in-time measurement of one store. Null fields indicate unavailable metrics.</summary>
    public readonly struct StorageMetrics
    {
        /// <summary>Number of entries currently held.</summary>
        public int Count { get; }

        /// <summary>Reserved entry slots, including slots currently unused.</summary>
        public int? Capacity { get; }

        /// <summary>Owner-reported payload bytes. May exclude collection overhead and shared objects.</summary>
        public long? EstimatedPayloadBytes { get; }

        /// <summary>Cumulative cache counters and the optional payload budget.</summary>
        public CacheMetrics? Cache { get; }

        /// <summary>Captures the measures available from this storage implementation.</summary>
        public StorageMetrics(int count, int? capacity = null, long? estimatedPayloadBytes = null, CacheMetrics? cache = null)
        {
            Count = count;
            Capacity = capacity;
            EstimatedPayloadBytes = estimatedPayloadBytes;
            Cache = cache;
        }
    }
}
