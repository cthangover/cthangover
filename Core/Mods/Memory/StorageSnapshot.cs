namespace Cthangover.Core.Mods.Memory
{
    /// <summary>A detached metrics result with its owner and load generation.</summary>
    public readonly struct StorageSnapshot
    {
        /// <summary>Owning mod id or the kernel owner id.</summary>
        public string ModId { get; }

        /// <summary>The lifetime to which these metrics belong.</summary>
        public long Generation { get; }

        /// <summary>Storage name in the owner scope.</summary>
        public string Name { get; }

        /// <summary>True for an explicitly rebuildable cache.</summary>
        public bool IsCache { get; }

        /// <summary>Reported metrics, if the provider completed successfully.</summary>
        public StorageMetrics? Metrics { get; }

        /// <summary>Type of error reported by the storage provider, if any.</summary>
        public string Error { get; }

        internal StorageSnapshot(ModMemoryScope scope, string name, bool isCache, StorageMetrics? metrics, string error)
        {
            ModId = scope.ModId;
            Generation = scope.Generation;
            Name = name;
            IsCache = isCache;
            Metrics = metrics;
            Error = error;
        }
    }
}
