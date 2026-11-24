namespace Cthangover.Core.Mods.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>A synchronized dictionary with on-demand metrics and an optional preview projection.</summary>
    public class ObservableDictionary<TKey, TValue> : IStorageMetrics where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> items;
        private readonly Func<TKey, TValue, MemoryPreviewEntry> preview;

        internal ObservableDictionary(IEqualityComparer<TKey> comparer, Func<TKey, TValue, MemoryPreviewEntry> preview)
        {
            items = new Dictionary<TKey, TValue>(comparer);
            this.preview = preview;
        }

        /// <summary>Gets or replaces one entry. Use <see cref="TryGetValue"/> for a tracked cache lookup.</summary>
        public TValue this[TKey key]
        {
            get { lock (items) return items[key]; }
            set { lock (items) items[key] = value; }
        }

        /// <summary>Adds an entry if its key is absent.</summary>
        public bool TryAdd(TKey key, TValue value)
        {
            lock (items)
                return items.TryAdd(key, value);
        }

        /// <summary>Checks membership without recording a cache lookup.</summary>
        public bool ContainsKey(TKey key)
        {
            lock (items)
                return items.ContainsKey(key);
        }

        /// <summary>Looks up an entry and records the outcome for cache implementations.</summary>
        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (items)
            {
                var found = items.TryGetValue(key, out value);
                RecordLookup(found);
                return found;
            }
        }

        /// <summary>Removes an entry explicitly.</summary>
        public bool Remove(TKey key)
        {
            lock (items)
                return items.Remove(key);
        }

        /// <summary>Clears the entries and retains the reserved capacity for reuse.</summary>
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
        public KeyValuePair<TKey, TValue>[] ToArray()
        {
            lock (items)
                return items.ToArray();
        }

        /// <inheritdoc />
        public StorageMetrics GetMetrics()
        {
            lock (items)
                return new StorageMetrics(items.Count, items.Capacity, cache: ReadCacheMetrics());
        }

        /// <inheritdoc />
        public IReadOnlyList<MemoryPreviewEntry> ReadPreview(int offset, int limit)
        {
            if (preview == null)
                return null;

            KeyValuePair<TKey, TValue>[] page;
            lock (items)
                page = items.Skip(offset).Take(limit).ToArray();

            var rows = new MemoryPreviewEntry[page.Length];
            for (var index = 0; index < page.Length; index++)
                rows[index] = preview(page[index].Key, page[index].Value);
            return rows;
        }

        private protected virtual void RecordLookup(bool found) { }

        private protected virtual CacheMetrics? ReadCacheMetrics() => null;
    }
}
