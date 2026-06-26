namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Runtime model for a loaded mod, combining the manifest metadata
    /// with the live <c>IModFileProvider</c> handle. <c>DisplayTitle</c>
    /// falls back to <c>Id</c> when <c>Name</c> is absent so that UI
    /// panels always have something human-readable to show even for
    /// minimally-authored mods.
    /// </summary>
    public class ModInfo : IModInfo
    {
        /// <summary>Unique mod identifier (folder name or zip name minus extension).</summary>
        public string Id { get; set; }

        /// <summary>Human-readable name from manifest.json.</summary>
        public string Name { get; set; }

        /// <summary>Author credit from manifest.json.</summary>
        public string Author { get; set; }

        /// <summary>Short description from manifest.json.</summary>
        public string Description { get; set; }

        /// <summary>The storage backend handle — folder or zip — that reads this mod's files.</summary>
        public IModFileProvider FileProvider { get; set; }

        /// <summary>Parsed manifest.json contents.</summary>
        public ModManifest Manifest { get; set; }

        /// <summary>
        /// Safe UI label: <c>Name</c> if present, otherwise the <c>Id</c>
        /// (which is always non-null since it comes from the filesystem entry).
        /// </summary>
        public string DisplayTitle => string.IsNullOrEmpty(Name) ? Id : Name;
    }
}
