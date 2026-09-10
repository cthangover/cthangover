namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Abstraction for on-demand value retrieval consumed by <see cref="BoundedCache{TKey, TValue}"/>.
    /// When a cache miss occurs, the bounded cache delegates to the implementation's
    /// <see cref="Load"/> method rather than using a closure or factory delegate.
    /// This interface-based design allows loaders to carry their own state (e.g. file handles,
    /// network connections, database context) without capturing it in lambda allocations
    /// inside the cache construction site. Implementations are invoked outside the cache's
    /// internal lock to minimise contention.
    /// </summary>
    public interface ICacheLoader<TKey, TValue>
    {
        /// <summary>
        /// Produces the value for the given <paramref name="key"/>.
        /// May return <c>null</c> for reference-type values, which the cache treats
        /// as "not cacheable" and will not insert into the LRU list.
        /// </summary>
        TValue Load(TKey key);
    }
}
