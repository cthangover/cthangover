namespace Cthangover.Core.Mods.Memory
{
    using System;
    using System.Collections.Generic;

    /// <summary>A synchronized list whose retained slots can be inspected without copying the list.</summary>
    public sealed class ObservableList<T> : IStorageMetrics
    {
        private readonly List<T> items = new();
        private readonly Func<T, MemoryPreviewEntry> preview;

        internal ObservableList(Func<T, MemoryPreviewEntry> preview) => this.preview = preview;

        /// <summary>Gets or replaces the entry at an index.</summary>
        public T this[int index]
        {
            get { lock (items) return items[index]; }
            set { lock (items) items[index] = value; }
        }

        /// <summary>Appends an entry.</summary>
        public void Add(T item)
        {
            lock (items)
                items.Add(item);
        }

        /// <summary>Removes the first equal entry.</summary>
        public bool Remove(T item)
        {
            lock (items)
                return items.Remove(item);
        }

        /// <summary>Removes the entry at an index.</summary>
        public void RemoveAt(int index)
        {
            lock (items)
                items.RemoveAt(index);
        }

        /// <summary>Clears entries for reuse of the reserved slots.</summary>
        public void Clear()
        {
            lock (items)
                items.Clear();
        }

        /// <summary>Releases excess capacity according to the standard list policy.</summary>
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
            {
                var start = Math.Min(offset, items.Count);
                page = new T[Math.Min(limit, items.Count - start)];
                items.CopyTo(start, page, 0, page.Length);
            }

            return Array.ConvertAll(page, ConvertEntry);
        }

        private MemoryPreviewEntry ConvertEntry(T item) => preview(item);
    }
}
