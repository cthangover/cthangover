namespace Cthangover.Core.Mods.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>A synchronized set with membership operations and on-demand diagnostics.</summary>
    public sealed class ObservableHashSet<T> : IStorageMetrics
    {
        private readonly HashSet<T> items;
        private readonly Func<T, MemoryPreviewEntry> preview;

        internal ObservableHashSet(IEqualityComparer<T> comparer, Func<T, MemoryPreviewEntry> preview)
        {
            items = new HashSet<T>(comparer);
            this.preview = preview;
        }

        /// <summary>Adds an entry if no equal value is present.</summary>
        public bool Add(T item)
        {
            lock (items)
                return items.Add(item);
        }

        /// <summary>Checks membership using the configured comparer.</summary>
        public bool Contains(T item)
        {
            lock (items)
                return items.Contains(item);
        }

        /// <summary>Removes an entry if present.</summary>
        public bool Remove(T item)
        {
            lock (items)
                return items.Remove(item);
        }

        /// <summary>Clears entries for reuse of the reserved slots.</summary>
        public void Clear()
        {
            lock (items)
                items.Clear();
        }

        /// <summary>Releases excess reserved entry capacity.</summary>
        public void TrimExcess()
        {
            lock (items)
                items.TrimExcess();
        }

        /// <summary>Copies the current entries for enumeration independently of later writes.</summary>
        public T[] ToArray()
        {
            lock (items)
                return items.ToArray();
        }

        /// <inheritdoc />
        public StorageMetrics GetMetrics()
        {
            lock (items)
                return new StorageMetrics(items.Count, items.Capacity);
        }

        /// <inheritdoc />
        public IReadOnlyList<MemoryPreviewEntry> ReadPreview(int offset, int limit)
        {
            if (preview == null)
                return null;

            T[] page;
            lock (items)
                page = items.Skip(offset).Take(limit).ToArray();

            return Array.ConvertAll(page, ConvertEntry);
        }

        private MemoryPreviewEntry ConvertEntry(T item) => preview(item);
    }
}
