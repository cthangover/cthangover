using System.IO;
using Cthangover.Core.Mods.Providers;

namespace Cthangover.Core.Mods.Discovery
{
    /// <summary>
    /// Loads mods stored as plain directories on disk. <see cref="IsCanProvide"/>
    /// returns <c>true</c> only for paths where
    /// <see cref="Directory.Exists(string)"/> succeeds, so this loader
    /// claims every filesystem directory and creates a
    /// <see cref="FolderModFileProvider"/> — the only provider type that
    /// supports <c>GetFileSystemPath</c> and therefore Godot scene
    /// instantiation via <c>ResourceLoader</c>.
    /// </summary>
    public class FolderModLoader : IModLoader
    {
        /// <inheritdoc />
        public string Name => "Folder";

        /// <summary>
        /// Returns <c>true</c> for any path that is an existing directory
        /// on disk.
        /// </summary>
        public bool IsCanProvide(string url)
        {
            return Directory.Exists(url);
        }

        /// <summary>
        /// Creates a <see cref="FolderModFileProvider"/> rooted at the
        /// given directory path. This provider type is the only one that
        /// supports <c>GetFileSystemPath</c>, enabling Godot scene
        /// instantiation from folder mods.
        /// </summary>
        public IModFileProvider CreateProvider(string url, string modName)
        {
            return new FolderModFileProvider(url, modName);
        }

        /// <summary>
        /// Uses the raw entry name (directory name) as the fallback mod ID.
        /// </summary>
        public string GetFallbackModId(string url, string entryName)
        {
            return entryName;
        }
    }
}
