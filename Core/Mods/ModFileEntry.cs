namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Immutable value type that ties a file path to its owning mod,
    /// used as the value type in all file-list caches inside
    /// <c>ModManager</c>. Marked <c>readonly struct</c> to avoid heap
    /// allocations during the hot scanning loop — these entries can
    /// number in the thousands across all mods.
    /// </summary>
    public readonly struct FileEntry
    {
        /// <summary>The owning mod's unique identifier.</summary>
        public readonly string ModId;

        /// <summary>Full path within the mod (e.g. "avatars/marao/smile.png").</summary>
        public readonly string FullPath;

        public FileEntry(string modId, string fullPath)
        {
            ModId = modId;
            FullPath = fullPath;
        }
    }
}
