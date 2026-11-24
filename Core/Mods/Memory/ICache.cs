namespace Cthangover.Core.Mods.Memory
{
    /// <summary>A store whose contents can be rebuilt. Its metrics can include lookup and eviction statistics.</summary>
    public interface ICache : IStorageMetrics
    {
        /// <summary>Invalidates the contents with the cache's own lifetime and resource ownership rules.</summary>
        void Clear();
    }
}
