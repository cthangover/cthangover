namespace Cthangover.Core.Mods.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;

    /// <summary>
    /// Inventories explicitly registered stores. Snapshots carry only values and text, so a client can
    /// keep a result across mod reload without retaining the original store or its assembly.
    /// </summary>
    public sealed class ModMemoryService
    {
        /// <summary>Owner id for process-wide kernel stores.</summary>
        public const string CoreOwner = "__core__";

        private const int MaxPageSize = 100;
        private const int PreviewWindow = 10000;
        private readonly SortedDictionary<long, ModMemoryScope> scopes = new();
        private long generation;

        private ModMemoryService()
        {
            Core = new ModMemoryScope(CoreOwner, 0);
            scopes.Add(Core.Generation, Core);
        }

        /// <summary>Registry for the current game process.</summary>
        public static ModMemoryService Shared { get; } = new();

        internal ModMemoryScope Core { get; }

        /// <summary>
        /// Selects the scope for the assembly that owns a store, typically the assembly of a mod's marker
        /// type. The assembly must be loaded by the mod pipeline.
        /// </summary>
        public ModMemoryScope For(Assembly assembly)
        {
            return TryFor(assembly)
                ?? throw new ArgumentException("The assembly does not belong to a mod load context.", nameof(assembly));
        }

        /// <summary>Finds the mod scope when present. Standalone tools and tests may use their stores without a scope.</summary>
        public ModMemoryScope TryFor(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);
            return (AssemblyLoadContext.GetLoadContext(assembly) as ModLoadContext)?.Memory;
        }

        /// <summary>
        /// Reads one page of registered storage metrics. Owner matching is case insensitive and a null
        /// owner includes every scope. Only providers on the requested page are sampled.
        /// </summary>
        public (IReadOnlyList<StorageSnapshot> Storages, int Total) GetStatistics(string modId = null, int offset = 0, int limit = 20)
        {
            ValidatePage(offset, limit);
            var page = new List<StorageSnapshot>();
            var total = 0;
            foreach (var scope in Scopes())
            {
                if (modId != null && !string.Equals(scope.ModId, modId, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var registration in scope.Registrations())
                {
                    if (!registration.Value.TryGetTarget(out var storage))
                        continue;

                    if (total++ >= offset && page.Count < limit)
                        page.Add(Sample(scope, registration.Key, storage));
                }
            }

            return (page.ToArray(), total);
        }

        /// <summary>
        /// Reads metrics and, when the store opts in, a page of preview entries. Offset addresses the
        /// store's current order, so concurrent writes can shift later pages. Preview walks are limited
        /// to the first 10000 entries. Pass the generation from statistics to pin the lookup to one load.
        /// </summary>
        public (StorageSnapshot? Storage, IReadOnlyList<MemoryPreviewEntry> Entries, int? NextOffset, bool Truncated)
            Inspect(string modId, string name, int offset = 0, int limit = 20, long? generation = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(modId);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ValidatePage(offset, limit);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(offset, PreviewWindow);
            ArgumentOutOfRangeException.ThrowIfLessThan(generation.GetValueOrDefault(), 0);

            ModMemoryScope owner = null;
            IStorageMetrics storage = null;
            foreach (var scope in Scopes())
            {
                if (!string.Equals(scope.ModId, modId, StringComparison.OrdinalIgnoreCase)
                    || (generation.HasValue && scope.Generation != generation.Value))
                    continue;

                var target = scope.Find(name);
                if (target == null)
                    continue;
                if (storage != null)
                    throw new ArgumentException("Several loads own this storage. Specify its generation.", nameof(generation));

                owner = scope;
                storage = target;
            }

            if (storage == null)
                return (null, null, null, false);

            var snapshot = Sample(owner, name, storage);
            if (snapshot.Error != null)
                return (snapshot, null, null, false);

            try
            {
                var size = Math.Min(limit, PreviewWindow - offset);
                var rows = storage.ReadPreview(offset, size + 1);
                if (rows == null)
                    return (snapshot, null, null, false);

                var end = offset + size;
                var hasMore = rows.Count > size;
                return (snapshot, rows.Take(size).ToArray(),
                    hasMore && end < PreviewWindow ? end : null, hasMore && end == PreviewWindow);
            }
            catch (Exception error)
            {
                return (new StorageSnapshot(owner, name, snapshot.IsCache, snapshot.Metrics, error.GetType().Name), null, null, false);
            }
        }

        /// <summary>Samples the shared managed heap without forcing a collection.</summary>
        public (long EstimatedManagedBytes, long? LastGcHeapBytes, long? LastGcCommittedBytes) ReadManagedHeap()
        {
            var info = GC.GetGCMemoryInfo();
            return (GC.GetTotalMemory(forceFullCollection: false), info.Index == 0 ? null : info.HeapSizeBytes,
                info.Index == 0 ? null : info.TotalCommittedBytes);
        }

        internal ModMemoryScope CreateScope(string modId)
        {
            lock (scopes)
            {
                var scope = new ModMemoryScope(modId, ++generation);
                scopes.Add(scope.Generation, scope);
                return scope;
            }
        }

        internal void Release(ModMemoryScope scope)
        {
            scope.Release();
            lock (scopes)
                scopes.Remove(scope.Generation);
        }

        private ModMemoryScope[] Scopes()
        {
            lock (scopes)
                return scopes.Values.ToArray();
        }

        private static void ValidatePage(int offset, int limit)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxPageSize);
        }

        private static StorageSnapshot Sample(ModMemoryScope scope, string name, IStorageMetrics source)
        {
            try
            {
                return new StorageSnapshot(scope, name, source is ICache, source.GetMetrics(), null);
            }
            catch (Exception error)
            {
                return new StorageSnapshot(scope, name, source is ICache, null, error.GetType().Name);
            }
        }
    }
}
