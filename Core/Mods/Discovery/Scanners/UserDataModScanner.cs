using System.Collections.Generic;
using System.IO;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods.Discovery
{
    /// <summary>
    /// Scans the per-user mods directory (<c>user://mods/</c>) in runtime
    /// builds. This is where the mod manager downloads and installs mod
    /// subscriptions — it is the writable, user-specific mod source with
    /// the highest deduplication priority (0).
    /// 
    /// Always returns its root from <see cref="GetRoots"/>; the
    /// <see cref="IsCanProvide"/> guard filters the root out when the
    /// filesystem path does not exist (non-runtime or user directory not
    /// yet created). Uses the standard ternary pattern to avoid calling
    /// <c>ProjectSettings.GlobalizePath</c> outside the Godot runtime.
    /// </summary>
    public class UserDataModScanner : IModScanner
    {
        /// <inheritdoc />
        public string Name => "UserData";

        /// <summary>
        /// Returns <c>true</c> if the given path is an existing directory.
        /// Acts as an accessibility guard — roots returned by
        /// <see cref="GetRoots"/> are skipped when the filesystem path
        /// does not exist (mods directory was never created yet).
        /// </summary>
        public bool IsCanProvide(string url)
        {
            return Directory.Exists(url);
        }

        /// <summary>
        /// Returns the globalised <c>user://mods/</c> path when running
        /// inside Godot, or the raw <c>user://mods/</c> string as a
        /// fallback for editor and test environments. Always returns
        /// exactly one root with priority 0 (highest user precedence).
        /// </summary>
        public IEnumerable<ModSourceRoot> GetRoots()
        {
            var path = GameEnvironment.IsRuntime
                ? ProjectSettings.GlobalizePath("user://mods/")
                : "user://mods/";
            yield return new ModSourceRoot { Url = path, Priority = 0 };
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
