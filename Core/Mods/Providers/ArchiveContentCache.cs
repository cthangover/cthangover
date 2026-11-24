namespace Cthangover.Core.Mods.Providers
{
    using System.Collections.Generic;
    using System.IO;
    using Cthangover.Core.Mods.Memory;

    /// <summary>A process-wide plaintext budget shared by all mounted archives, released per provider on unload.</summary>
    internal sealed class ArchiveContentCache : ICache
    {
        private const int MaximumBytes = 16 * 1024 * 1024;
        private const int MaximumFileBytes = 4 * 1024 * 1024;
        private readonly object gate = new();
        private readonly Dictionary<(object Owner, string Path), byte[]> values = new();
        private readonly Queue<(object Owner, string Path)> order = new();
        private int bytes;
        private long hits;
        private long misses;
        private long evictions;

        internal static ArchiveContentCache Instance { get; } = new();

        private ArchiveContentCache()
            => ModMemoryService.Shared.Core.Track("mods.archive_content", this);

        internal Stream Open(object owner, string path)
        {
            lock (gate)
            {
                if (values.TryGetValue((owner, path), out var data))
                {
                    hits++;
                    return new MemoryStream(data, false);
                }

                misses++;
                return null;
            }
        }

        internal void Remember(object owner, string path, byte[] data)
        {
            if (data.Length > MaximumFileBytes)
                return;
            lock (gate)
            {
                var key = (owner, path);
                if (values.ContainsKey(key))
                    return;
                while (bytes + data.Length > MaximumBytes && order.Count > 0)
                {
                    var oldest = order.Dequeue();
                    bytes -= values[oldest].Length;
                    values.Remove(oldest);
                    evictions++;
                }
                values[key] = (byte[])data.Clone();
                order.Enqueue(key);
                bytes += data.Length;
            }
        }

        internal void Release(object owner)
        {
            lock (gate)
            {
                var count = order.Count;
                for (var index = 0; index < count; index++)
                {
                    var key = order.Dequeue();
                    if (ReferenceEquals(key.Owner, owner))
                    {
                        bytes -= values[key].Length;
                        values.Remove(key);
                    }
                    else
                        order.Enqueue(key);
                }
            }
        }

        /// <inheritdoc />
        public StorageMetrics GetMetrics()
        {
            lock (gate)
                return new StorageMetrics(values.Count, values.Capacity, bytes,
                    new CacheMetrics(hits, misses, evictions, MaximumBytes));
        }

        /// <inheritdoc />
        public void Clear()
        {
            lock (gate)
            {
                values.Clear();
                order.Clear();
                bytes = 0;
            }
        }
    }
}
