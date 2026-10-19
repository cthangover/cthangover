using System.Collections.Generic;
using System.IO;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods.Discovery
{
    /// <summary>
    /// Scans the built-in mods directory shipped with the game. Returns
    /// the project-local <c>mods/</c> folder in editor/test environments
    /// (priority 0), or the globalised <c>res://mods/</c> path in runtime
    /// builds (priority 1).
    ///
    /// Priority 1 means user-installed mods from
    /// <see cref="UserDataModScanner"/> override built-in mods with the
    /// same ID — users can replace or update shipped content without
    /// modifying game files.
    /// </summary>
    public class GameDirModScanner : IModScanner
    {
        /// <inheritdoc />
        public string Name => "GameDir";

        /// <summary>
        /// Returns <c>true</c> if the given path is an existing directory.
        /// Acts as an accessibility guard — roots returned by
        /// <see cref="GetRoots"/> are skipped when the directory does not
        /// exist.
        /// </summary>
        public bool IsCanProvide(string url)
        {
            return Directory.Exists(url);
        }

        /// <summary>
        /// Returns the project-local <c>mods/</c> path in editor
        /// (priority 0), or the globalised <c>res://mods/</c> path in
        /// runtime builds (priority 1). Uses the standard ternary pattern
        /// to avoid calling <c>ProjectSettings.GlobalizePath</c> outside
        /// the Godot runtime.
        /// </summary>
        public IEnumerable<ModSourceRoot> GetRoots()
        {
            if (GameEnvironment.IsRuntime)
            {
                var path = GamePaths.Globalize("res://mods/");
                yield return new ModSourceRoot { Url = path, Priority = 1 };
            }
            else
            {
                yield return new ModSourceRoot { Url = "mods", Priority = 0 };
            }
        }

        /// <summary>
        /// Enumerates all filesystem entries (folders and files) inside
        /// the given root directory. Normalises backslashes to forward
        /// slashes for cross-platform consistency.
        /// </summary>
        public IEnumerable<DiscoveredModEntry> Discover(string rootUrl)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(rootUrl))
            {
                yield return new DiscoveredModEntry
                {
                    Url = entry.Replace('\\', '/'),
                    EntryName = Path.GetFileName(entry),
                };
            }
        }
    }
}
