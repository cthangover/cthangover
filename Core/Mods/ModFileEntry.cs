namespace Cthangover.Core.Mods
{
    public readonly struct FileEntry
    {
        public readonly string ModId;
        public readonly string FullPath;

        public FileEntry(string modId, string fullPath)
        {
            ModId = modId;
            FullPath = fullPath;
        }
    }
}
