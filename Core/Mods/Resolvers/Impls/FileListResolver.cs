using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Provides a generic file-list collection grouped by directory name.
    /// Iterates all loaded mods in dependency order, recursively
    /// collecting every file under a given directory group. Later mods
    /// override earlier ones for files with the same relative key
    /// (last-in-wins deduplication). Used as a building block by other
    /// resolvers — <see cref="ShaderResolver"/> and
    /// <see cref="TextureResolver"/> both delegate to this resolver for
    /// raw file discovery, then apply their own filtering and processing.
    /// </summary>
    public class FileListResolver : ModAssetResolver
    {
        /// <inheritdoc />
        public override string Id => "file_list";

        /// <summary>
        /// Per-group file cache. <see cref="ConcurrentDictionary"/> because
        /// <see cref="Collect"/> may be entered concurrently (factories,
        /// parallel tests); the inner dictionaries are built locally and only
        /// published after they are complete, so they stay read-only in the
        /// cache. A plain dictionary here used to corrupt its internal state
        /// when two threads collected the same group at once.
        /// </summary>
        private readonly ConcurrentDictionary<string, Dictionary<string, FileEntry>> _cache = new();

        /// <summary>
        /// Collects all files under the given directory group (e.g.
        /// "shaders", "textures/portraits") from every loaded mod.
        /// Files are keyed by their path relative to the group directory.
        /// Later mods in load order override earlier ones with the same
        /// relative key. Caches the result per group.
        /// </summary>
        public Dictionary<string, FileEntry> Collect(string group)
        {
            Context.Initialize();

            if (_cache.TryGetValue(group, out var cached))
                return cached;

            GameLogger.Log("MODS", $"CollectFileList('{group}'): CACHE MISS, collecting from {Context.Mods.Count} mod(s)...");

            var result = new Dictionary<string, FileEntry>();
            var prefix = ModPath.NormalizeDirectory(group);

            foreach (var modId in Context.GetOrderedModIds())
            {
                var provider = Context.Mods[modId].FileProvider;
                var before = result.Count;

                CollectGroupFiles(provider, modId, group, prefix, result);

                if (result.Count > before)
                {
                    GameLogger.Log("MODS", $"mod '{modId}' contributed {result.Count - before} file(s) to group '{group}'");
                }
            }

            _cache[group] = result;

            GameLogger.Log("MODS", $"CollectFileList('{group}') → {result.Count} file(s), keys=[{string.Join(", ", result.Keys)}]");
            return result;
        }

        /// <inheritdoc />
        public override void ClearCache()
        {
            _cache.Clear();
        }

        private static void CollectGroupFiles(IModFileProvider provider, string modId, string dir, string prefix, Dictionary<string, FileEntry> result)
        {
            var entries = provider.ListFiles(dir).ToList();
            GameLogger.Log("MODS", $"[{modId}]  CollectGroupFiles: dir='{dir}', provider entries count={entries.Count}, prefix='{prefix}'", LogLevel.Debug);
            foreach (var entry in entries)
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    GameLogger.Log("MODS", $"    -> subdir '{subDir}'", LogLevel.Debug);
                    CollectGroupFiles(provider, modId, subDir, prefix, result);
                }
                else if (entry.StartsWith(prefix))
                {
                    var key = entry.Substring(prefix.Length);
                    if (result.ContainsKey(key))
                    {
                        GameLogger.Log("MODS", $"conflict: '{key}' in mod '{modId}' overrides previous entry", LogLevel.Warning);
                        AppServices.Current.Conflicts.RecordConflict("file", key, modId, result[key].ModId, false);
                    }
                    result[key] = new FileEntry(modId, entry);
                    GameLogger.Log("MODS", $"    + file '{key}' <- {entry} (mod={modId})", LogLevel.Debug);
                }
                else
                {
                    GameLogger.Log("MODS", $"    SKIP '{entry}': does not start with prefix '{prefix}'", LogLevel.Warning);
                }
            }
        }
    }

    /// <summary>
    /// Static convenience accessor for <see cref="FileListResolver"/>,
    /// proxying through <see cref="ModManager.Instance.GetResolver{T}"/>.
    /// </summary>
    public static class Files
    {
        /// <summary>Collects one directory group's files from all mods, keyed by group-relative path (last mod wins).</summary>
        public static Dictionary<string, FileEntry> Collect(string group)
            => ModManager.Instance.GetResolver<FileListResolver>()?.Collect(group);
    }
}
