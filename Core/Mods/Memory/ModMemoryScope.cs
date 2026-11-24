namespace Cthangover.Core.Mods.Memory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Named diagnostic registrations for one mod load generation or for the kernel.</summary>
    public sealed class ModMemoryScope
    {
        private readonly SortedDictionary<string, WeakReference<IStorageMetrics>> stores = new(StringComparer.Ordinal);
        private long instanceSequence;
        private bool released;

        internal ModMemoryScope(string modId, long generation)
        {
            ModId = modId;
            Generation = generation;
        }

        /// <summary>Owner id used by memory queries.</summary>
        public string ModId { get; }

        /// <summary>Identifies this scope among loads of the same mod.</summary>
        public long Generation { get; }

        /// <summary>Creates and tracks a dictionary for retained data.</summary>
        public ObservableDictionary<TKey, TValue> CreateDictionary<TKey, TValue>(string name,
            IEqualityComparer<TKey> comparer = null, Func<TKey, TValue, MemoryPreviewEntry> preview = null) where TKey : notnull
        {
            var storage = new ObservableDictionary<TKey, TValue>(comparer, preview);
            Track(name, storage);
            return storage;
        }

        /// <summary>Creates and tracks a dictionary of rebuildable values with hit and miss counters.</summary>
        public DictionaryCache<TKey, TValue> CreateCache<TKey, TValue>(string name,
            IEqualityComparer<TKey> comparer = null, Func<TKey, TValue, MemoryPreviewEntry> preview = null) where TKey : notnull
        {
            var storage = new DictionaryCache<TKey, TValue>(comparer, preview);
            Track(name, storage);
            return storage;
        }

        /// <summary>Creates and tracks a list for retained data.</summary>
        public ObservableList<T> CreateList<T>(string name, Func<T, MemoryPreviewEntry> preview = null)
        {
            var storage = new ObservableList<T>(preview);
            Track(name, storage);
            return storage;
        }

        /// <summary>Creates and tracks a set for retained data.</summary>
        public ObservableHashSet<T> CreateHashSet<T>(string name, IEqualityComparer<T> comparer = null,
            Func<T, MemoryPreviewEntry> preview = null)
        {
            var storage = new ObservableHashSet<T>(comparer, preview);
            Track(name, storage);
            return storage;
        }

        /// <summary>
        /// Publishes a store or adapter under a stable name of up to 128 characters.
        /// The owner keeps the strong reference and retains responsibility for the entries and cleanup.
        /// </summary>
        public void Track(string name, IStorageMetrics storage)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
                throw new ArgumentException("A storage name must contain 1 to 128 characters.", nameof(name));
            ArgumentNullException.ThrowIfNull(storage);

            lock (stores)
            {
                if (released)
                    throw new ObjectDisposedException(nameof(ModMemoryScope));
                if (stores.TryGetValue(name, out var previous) && previous.TryGetTarget(out _))
                    throw new ArgumentException("A live storage already uses this name: " + name, nameof(name));

                stores[name] = new WeakReference<IStorageMetrics>(storage);
            }
        }

        /// <summary>
        /// Registers one of several concurrent instances of a store. Use the returned name with
        /// <see cref="Untrack"/> when the instance stops owning memory.
        /// </summary>
        public string TrackInstance(string prefix, IStorageMetrics storage)
        {
            if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > 108)
                throw new ArgumentException("A storage prefix must contain 1 to 108 characters.", nameof(prefix));
            ArgumentNullException.ThrowIfNull(storage);

            lock (stores)
            {
                string name;
                do
                {
                    name = prefix + "/" + (++instanceSequence).ToString(System.Globalization.CultureInfo.InvariantCulture);
                } while (stores.ContainsKey(name));

                Track(name, storage);
                return name;
            }
        }

        /// <summary>Removes the registration if it still points to the supplied store.</summary>
        public bool Untrack(string name, IStorageMetrics storage)
        {
            ArgumentNullException.ThrowIfNull(storage);

            lock (stores)
            {
                if (!stores.TryGetValue(name, out var current) || !current.TryGetTarget(out var target)
                    || !ReferenceEquals(target, storage))
                    return false;

                return stores.Remove(name);
            }
        }

        internal KeyValuePair<string, WeakReference<IStorageMetrics>>[] Registrations()
        {
            lock (stores)
            {
                var registrations = stores.ToArray();
                foreach (var registration in registrations)
                    if (!registration.Value.TryGetTarget(out _))
                        stores.Remove(registration.Key);
                return registrations;
            }
        }

        internal IStorageMetrics Find(string name)
        {
            lock (stores)
                return stores.TryGetValue(name, out var current) && current.TryGetTarget(out var storage) ? storage : null;
        }

        internal void Release()
        {
            lock (stores)
            {
                released = true;
                stores.Clear();
            }
        }
    }
}
