using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Games;
using Cthangover.Core.Mods.Testing;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods.Discovery
{
    /// <summary>
    /// Scans the mods folders of the game directory
    /// (<see cref="ModRootFolders"/>: <c>mods/</c> and its optional siblings).
    /// Each is a root of its own with the same priority: project-local
    /// relative paths in editor/test environments (priority 0), globalised
    /// <c>res://</c> paths in runtime builds (priority 1). Equal priority
    /// means a mod works the same in any of the folders; the same id in two
    /// of them is settled by version like any other duplicate.
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
        /// Returns one root per folder of <see cref="ModRootFolders.Names"/>:
        /// the relative folder name in editor/test environments (priority 0),
        /// the globalised <c>res://</c> path in runtime builds (priority 1).
        /// <see cref="ModRootFolders.Primary"/> is always returned; the other
        /// folders only when they exist at the time of the call. Uses the
        /// standard ternary pattern to avoid calling
        /// <c>ProjectSettings.GlobalizePath</c> outside the Godot runtime.
        /// </summary>
        public IEnumerable<ModSourceRoot> GetRoots()
        {
            foreach (var folder in ModRootFolders.Names)
            {
                var url = GameEnvironment.IsRuntime ? GamePaths.Globalize("res://" + folder + "/") : folder;

                // The hot reload creates every root it watches; an optional folder
                // must not appear in a game dir that never had it.
                if (folder != ModRootFolders.Primary && !Directory.Exists(url))
                    continue;

                yield return new ModSourceRoot { Url = url, Priority = GameEnvironment.IsRuntime ? 1 : 0 };
            }

            if (GameEnvironment.IsRuntime && (TestExecutionContextRuntime.Current != null
                || GameLaunchArgs.Startup.UserArgs.Contains("--test-fixtures")))
            {
                var fixtures = GamePaths.Globalize("res://" + ModRootFolders.TestFixtures + "/");
                if (Directory.Exists(fixtures))
                    yield return new ModSourceRoot { Url = fixtures, Priority = 1 };
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
