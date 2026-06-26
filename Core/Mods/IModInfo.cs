namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Public read-only view of a loaded mod's identity and capabilities.
    /// The <c>FileProvider</c> property exposes the storage backend so
    /// that <c>ModManager</c> can delegate file access through it — the
    /// mod info itself doesn't read files, it just carries the handle.
    /// </summary>
    public interface IModInfo
    {
        /// <summary>Unique mod identifier.</summary>
        public string Id { get; }

        /// <summary>Human-readable name from manifest.json.</summary>
        public string Name { get; }

        /// <summary>Author credit string.</summary>
        public string Author { get; }

        /// <summary>Short description from manifest.json.</summary>
        public string Description { get; }

        /// <summary>UI-safe label: Name if present, otherwise Id.</summary>
        public string DisplayTitle { get; }

        /// <summary>The storage backend that reads this mod's files.</summary>
        public IModFileProvider FileProvider { get; }
    }
}
