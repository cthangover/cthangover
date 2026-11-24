namespace Cthangover.Core.Mods.Memory
{
    using System.Collections.Generic;

    /// <summary>Opt-in diagnostics for a retained collection or a custom memory store.</summary>
    public interface IStorageMetrics
    {
        /// <summary>Samples the store without walking its object graph or changing its state.</summary>
        StorageMetrics GetMetrics();

        /// <summary>
        /// Optionally projects up to <paramref name="limit"/> entries into detached text.
        /// Return null when the store does not expose contents. Skip <paramref name="offset"/> entries
        /// in the current view, and return an empty list when that offset is past the end.
        /// </summary>
        IReadOnlyList<MemoryPreviewEntry> ReadPreview(int offset, int limit) => null;
    }
}
