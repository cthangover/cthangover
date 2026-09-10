using System.Collections.Generic;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// A thread-safe, fixed-capacity cache with least-recently-used (LRU)
    /// eviction. Used to avoid redundant I/O and computation when the same
    /// data is requested repeatedly — for example, parsed scenario resources,
    /// mod asset metadata, or deserialised JSON configs.
    /// </summary>
    /// <typeparam name="TKey">The lookup key type (typically <c>string</c> or numeric IDs).</typeparam>
    /// <typeparam name="TValue">The cached payload type.</typeparam>
    /// <remarks>
    /// <para>
    /// Internally, a <see cref="Dictionary{TKey, TValue}"/> maps keys to
    /// <see cref="LinkedList{T}"/> nodes, and the linked list maintains
    /// access order. On every <see cref="Get"/> hit the accessed node is
    /// moved to the tail (most-recently-used). When the dictionary reaches
    /// <see cref="MaxSize"/> an insertion evicts the head (least-recently-used).
    /// </para>
    /// <para>
    /// The loader's <see cref="ICacheLoader{TKey, TValue}.Load"/> method is
    /// called <em>outside</em> the internal lock so that slow I/O does not
    /// block concurrent reads of already-cached entries. A double-check inside
    /// the lock ensures no duplicate loads occur for the same key.
    /// </para>
    /// </remarks>
    public class BoundedCache<TKey, TValue>
    {
        private readonly int _maxSize;
        private readonly ICacheLoader<TKey, TValue> _loader;
        private readonly Dictionary<TKey, LinkedListNode<Entry>> _dict;
        private readonly LinkedList<Entry> _lru;
        private readonly object _lock;

        private class Entry
        {
            public TKey Key;
            public TValue Value;
        }

        /// <summary>
        /// Creates a cache with the given capacity and data-fetching strategy.
        /// A <paramref name="maxSize"/> of 0 disables the bound — entries will
        /// accumulate without eviction, which is suitable for immutable reference
        /// data that fits in memory.
        /// </summary>
        /// <param name="maxSize">Maximum number of entries before the LRU tail is evicted.</param>
        /// <param name="loader">Delegate that produces values for cache misses.</param>
        public BoundedCache(int maxSize, ICacheLoader<TKey, TValue> loader)
        {
            _maxSize = maxSize;
            _loader = loader;
            _dict = new Dictionary<TKey, LinkedListNode<Entry>>();
            _lru = new LinkedList<Entry>();
            _lock = new object();
        }

        /// <summary>
        /// The maximum number of entries this cache will hold before evicting
        /// the least-recently-used item. A value of 0 means unbounded.
        /// </summary>
        public int MaxSize
        {
            get { return _maxSize; }
        }

        /// <summary>
        /// Current number of cached entries. This property acquires the
        /// internal lock for a consistent snapshot.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                    return _dict.Count;
            }
        }

        /// <summary>
        /// Retrieves the value for <paramref name="key"/>, either from the LRU
        /// in-memory store or by calling <see cref="ICacheLoader{TKey, TValue}.Load"/>
        /// on a miss. Newly loaded values are inserted into the cache and may
        /// trigger eviction of the least-recently-used entry if capacity is
        /// exceeded. Returning <c>null</c> from the loader causes the entry
        /// to be skipped entirely.
        /// </summary>
        public TValue Get(TKey key)
        {
            lock (_lock)
            {
                if (_dict.TryGetValue(key, out var node))
                {
                    _lru.Remove(node);
                    _lru.AddLast(node);
                    return node.Value.Value;
                }
            }

            var value = _loader.Load(key);

            lock (_lock)
            {
                if (_dict.TryGetValue(key, out var existing))
                {
                    _lru.Remove(existing);
                    _lru.AddLast(existing);
                    return existing.Value.Value;
                }

                if (value == null)
                    return default;

                if (_maxSize > 0)
                {
                    while (_dict.Count >= _maxSize)
                    {
                        var first = _lru.First;
                        _lru.RemoveFirst();
                        _dict.Remove(first.Value.Key);
                    }
                }

                var entry = new Entry { Key = key, Value = value };
                var newNode = new LinkedListNode<Entry>(entry);
                _dict[key] = newNode;
                _lru.AddLast(newNode);
            }

            return value;
        }

        /// <summary>
        /// A non-allocating lookup that returns <c>true</c> and sets
        /// <paramref name="value"/> only when the key is already cached;
        /// otherwise returns <c>false</c> <em>without</em> invoking the loader.
        /// Useful for callers that need to probe the cache without triggering
        /// side effects.
        /// </summary>
        public bool TryGet(TKey key, out TValue value)
        {
            lock (_lock)
            {
                if (_dict.TryGetValue(key, out var node))
                {
                    _lru.Remove(node);
                    _lru.AddLast(node);
                    value = node.Value.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Removes a specific entry from the cache and the LRU tracking list,
        /// forcing the next <see cref="Get"/> call for this key to invoke the
        /// loader again. A no-op if the key is not present.
        /// </summary>
        public void Invalidate(TKey key)
        {
            lock (_lock)
            {
                if (_dict.TryGetValue(key, out var node))
                {
                    _lru.Remove(node);
                    _dict.Remove(key);
                }
            }
        }

        /// <summary>
        /// Wipes all cached entries and resets the LRU order, typically called
        /// when a mod group is reloaded or a global configuration change makes
        /// all existing cached data stale.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _dict.Clear();
                _lru.Clear();
            }
        }
    }
}
