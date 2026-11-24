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
        private long generation;

        /// <summary>Creates a dictionary that can be used on its own and registered later.</summary>
        public ObservableDictionary(IEqualityComparer<TKey> comparer = null, Func<TKey, TValue, MemoryPreviewEntry> preview = null)
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

        /// <summary>
        /// Looks up an entry whose current stamp or revision matches the request. The predicate runs
        /// under the storage lock and should compare data already available to the caller.
        /// </summary>
        public bool TryGetValue<TStamp>(TKey key, TStamp stamp, Func<TValue, TStamp, bool> isCurrent, out TValue value)
        {
            ArgumentNullException.ThrowIfNull(isCurrent);
            lock (items)
            {
                if (items.TryGetValue(key, out value) && isCurrent(value, stamp))
                {
                    RecordLookup(true);
                    return true;
                }

                value = default;
                RecordLookup(false);
                return false;
            }
        }

        /// <summary>
        /// Looks up or publishes a value. The factory runs outside the lock and concurrent callers
        /// may compute the same key. The first publication wins. A clear lets already running calls
        /// finish with their computed result and prevents those calls from refilling the cache.
        /// </summary>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
        {
            ArgumentNullException.ThrowIfNull(factory);
            long observedGeneration;
            lock (items)
            {
                if (items.TryGetValue(key, out var existing))
                {
                    RecordLookup(true);
                    return existing;
                }
                RecordLookup(false);
                observedGeneration = generation;
            }

            var value = factory(key);
            lock (items)
            {
                if (items.TryGetValue(key, out var existing))
                    return existing;

                if (observedGeneration == generation)
                    items.Add(key, value);
                return value;
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
            {
                generation++;
                items.Clear();
            }
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
