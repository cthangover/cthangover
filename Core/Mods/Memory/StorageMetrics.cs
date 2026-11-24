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

        /// <summary>Owner-reported estimate for engine resources, which may include both managed and native buffers.</summary>
        public long? EstimatedResourceBytes { get; }

        /// <summary>Cumulative cache counters and the optional payload budget.</summary>
        public CacheMetrics? Cache { get; }

        /// <summary>Captures the measures available from this storage implementation.</summary>
        public StorageMetrics(int count, int? capacity = null, long? estimatedPayloadBytes = null, CacheMetrics? cache = null)
            : this(count, capacity, estimatedPayloadBytes, cache, null) { }

        /// <summary>Captures a resource estimate separately from accounted managed payload.</summary>
        public StorageMetrics(int count, int? capacity, long? estimatedPayloadBytes, CacheMetrics? cache, long? estimatedResourceBytes)
        {
            Count = count;
            Capacity = capacity;
            EstimatedPayloadBytes = estimatedPayloadBytes;
            Cache = cache;
            EstimatedResourceBytes = estimatedResourceBytes;
        }
    }
}
