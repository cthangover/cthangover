using System.Collections.Generic;

namespace Cthangover.Core.Utils
{
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

        public BoundedCache(int maxSize, ICacheLoader<TKey, TValue> loader)
        {
            _maxSize = maxSize;
            _loader = loader;
            _dict = new Dictionary<TKey, LinkedListNode<Entry>>();
            _lru = new LinkedList<Entry>();
            _lock = new object();
        }

        public int MaxSize
        {
            get { return _maxSize; }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                    return _dict.Count;
            }
        }

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
