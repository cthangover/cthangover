using System;
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
    /// <para>
    /// Every value that leaves the cache without being handed to a caller is
    /// reported to the optional eviction callback passed to the constructor:
    /// LRU eviction on insertion, a value that loses the double-check race
    /// (two threads loaded the same key, the loser is discarded un-cached),
    /// and values dropped by <see cref="Invalidate"/> or <see cref="Clear"/>.
    /// Like the loader, the callback is invoked <em>outside</em> the internal
    /// lock. This hook is the deterministic release point for payloads that
    /// own unmanaged memory — Godot resources such as <c>Texture2D</c>,
    /// <c>ImageTexture</c> or <c>AudioStream</c> keep their native/GPU
    /// allocation alive until the managed wrapper is disposed, and waiting
    /// for the .NET GC finalizer turns a bounded cache into a VRAM/audio
    /// memory leak during long sessions or frequent location changes.
    /// </para>
    /// </remarks>
    public class BoundedCache<TKey, TValue>
    {
        private readonly int _maxSize;
        private readonly ICacheLoader<TKey, TValue> _loader;
        private readonly Dictionary<TKey, LinkedListNode<Entry>> _dict;
        private readonly LinkedList<Entry> _lru;
        private readonly object _lock;

        /// <summary>
        /// Optional sink for values leaving the cache (see class remarks);
        /// <c>null</c> for caches of plain managed values that need no
        /// explicit release. Never invoked while the internal lock is held.
        /// </summary>
        private readonly Action<TValue> _onEvicted;

        private class Entry
        {
            /// <summary>Cache key of the entry; kept alongside the value so eviction can remove the dictionary slot.</summary>
            public TKey Key;
            /// <summary>Cached payload produced by the loader.</summary>
            public TValue Value;
        }

        /// <summary>
        /// Creates a cache with the given capacity and data-fetching strategy.
        /// A <paramref name="maxSize"/> of 0 disables the bound — entries will
        /// accumulate without eviction, which is suitable for immutable reference
        /// data that fits in memory. Values are dropped silently; use the
        /// second constructor for payloads that own unmanaged memory.
        /// </summary>
        /// <param name="maxSize">Maximum number of entries before the LRU tail is evicted.</param>
        /// <param name="loader">Delegate that produces values for cache misses.</param>
        public BoundedCache(int maxSize, ICacheLoader<TKey, TValue> loader)
            : this(maxSize, loader, null) { }

        /// <summary>
        /// Creates a cache that additionally reports every dropped value
        /// through <paramref name="onEvicted"/> — the hook Godot-resource
        /// caches use to dispose evicted payloads deterministically (see the
        /// class remarks). The callback runs outside the internal lock, may
        /// fire from any thread that triggers a drop, and must not call back
        /// into the same cache: this instance would deadlock on re-entry.
        /// </summary>
        /// <param name="maxSize">Maximum number of entries before the LRU tail is evicted; 0 means unbounded.</param>
        /// <param name="loader">Delegate that produces values for cache misses.</param>
        /// <param name="onEvicted">Called for each value as it leaves the cache: LRU eviction, losing a duplicate-load race, <see cref="Invalidate"/>, <see cref="Clear"/>. May be <c>null</c>.</param>
        public BoundedCache(int maxSize, ICacheLoader<TKey, TValue> loader, Action<TValue> onEvicted)
        {
            _maxSize = maxSize;
            _loader = loader;
            _onEvicted = onEvicted;
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

            List<TValue> dropped = null;
            TValue result;

            lock (_lock)
            {
                if (_dict.TryGetValue(key, out var existing))
                {
                    _lru.Remove(existing);
                    _lru.AddLast(existing);
                    result = existing.Value.Value;
                    if (value != null && !ReferenceEquals(value, result))
                    {
                        // This thread lost the double-check race: the freshly
                        // loaded value is discarded un-cached. Report it so
                        // unmanaged payloads don't leak.
                        if (dropped == null)
                            dropped = new List<TValue>();
                        dropped.Add(value);
                    }
                }
                else if (value == null)
                {
                    result = default;
                }
                else
                {
                    if (_maxSize > 0)
                    {
                        while (_dict.Count >= _maxSize)
                        {
                            var first = _lru.First;
                            _lru.RemoveFirst();
                            _dict.Remove(first.Value.Key);
                            if (dropped == null)
                                dropped = new List<TValue>();
                            dropped.Add(first.Value.Value);
                        }
                    }

                    var entry = new Entry { Key = key, Value = value };
                    var newNode = new LinkedListNode<Entry>(entry);
                    _dict[key] = newNode;
                    _lru.AddLast(newNode);
                    result = value;
                }
            }

            if (dropped != null)
            {
                for (var i = 0; i < dropped.Count; i++)
                    _onEvicted?.Invoke(dropped[i]);
            }

            return result;
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
        /// loader again. The removed value is reported to the eviction
        /// callback (outside the lock). A no-op if the key is not present.
        /// </summary>
        public void Invalidate(TKey key)
        {
            TValue dropped = default;
            lock (_lock)
            {
                if (_dict.TryGetValue(key, out var node))
                {
                    _lru.Remove(node);
                    _dict.Remove(key);
                    dropped = node.Value.Value;
                }
            }
            if (dropped != null)
                _onEvicted?.Invoke(dropped);
        }

        /// <summary>
        /// Wipes all cached entries and resets the LRU order, typically called
        /// when a mod group is reloaded or a global configuration change makes
        /// all existing cached data stale. Every removed value is reported to
        /// the eviction callback (outside the lock).
        /// </summary>
        public void Clear()
        {
            List<TValue> dropped = null;
            lock (_lock)
            {
                if (_dict.Count > 0)
                {
                    dropped = new List<TValue>(_dict.Count);
                    foreach (var node in _lru)
                        dropped.Add(node.Value);
                    _dict.Clear();
                    _lru.Clear();
                }
            }
            if (dropped != null)
            {
                for (var i = 0; i < dropped.Count; i++)
                    _onEvicted?.Invoke(dropped[i]);
            }
        }
    }
}
