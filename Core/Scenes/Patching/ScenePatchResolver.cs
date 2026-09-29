using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes.Patching
{
    /// <summary>
    /// Discovers and caches scene patches from every loaded mod. Patch files live under
    /// <c>patches/scenes/</c> (recursively) and are grouped by the canonical target key they
    /// declare in their inherited-scene header; within one mod files apply in path order, across
    /// mods in load order, so the returned layer list is exactly the cascade
    /// <see cref="ScenePatchApplier"/> expects.
    /// <para/>
    /// Registered as the <c>"scene_patches"</c> resolver. Unlike JSON patches the key is not an
    /// entity kind but the target scene path, because a patch already names its base scene.
    /// </summary>
    public class ScenePatchResolver : ModAssetResolver
    {
        /// <inheritdoc />
        public override string Id => "scene_patches";

        /// <summary>Directory scanned inside every mod, recursively.</summary>
        public const string PatchDirectory = "patches/scenes";

        private Dictionary<string, List<ScenePatch>> _cache;

        /// <summary>
        /// Collects every patch from every loaded mod, keyed by canonical target scene path.
        /// Cached until <see cref="ClearCache"/>; later mods append to a key's layer list.
        /// </summary>
        public Dictionary<string, List<ScenePatch>> Collect()
        {
            Context.Initialize();

            if (_cache != null)
                return _cache;

            var sources = new List<(string ModId, IModFileProvider Provider)>();
            foreach (var modId in Context.GetOrderedModIds())
            {
                var provider = Context.GetMod(modId)?.FileProvider;
                if (provider != null)
                    sources.Add((modId, provider));
            }

            _cache = CollectFromProviders(sources);

            if (_cache.Count > 0)
                GameLogger.Log("TSCN", $"ScenePatchResolver: collected {_cache.Sum(kv => kv.Value.Count)} patch(es) for {_cache.Count} scene(s)");

            return _cache;
        }

        /// <summary>
        /// All layers targeting <paramref name="sceneKey"/>, in application order. Returns an
        /// empty list for a scene without patches so callers can treat "no patches" uniformly.
        /// </summary>
        public List<ScenePatch> GetPatches(string sceneKey)
        {
            if (string.IsNullOrEmpty(sceneKey))
                return new List<ScenePatch>();

            var result = new List<ScenePatch>();
            foreach (var kvp in Collect())
            {
                if (ScenePatchTargetResolver.MatchesSceneKey(sceneKey, kvp.Key))
                    result.AddRange(kvp.Value);
            }
            return result;
        }

        /// <summary>True when at least one mod patches the scene.</summary>
        public bool HasPatches(string sceneKey) => GetPatches(sceneKey).Count > 0;

        /// <inheritdoc />
        public override void ClearCache()
        {
            _cache = null;
        }

        /// <summary>
        /// Testable collection core: walks each provider's <c>patches/scenes</c> tree in the
        /// given order (mod load order), sorts files inside one mod for determinism, and parses
        /// every <c>.tscn</c>. Invalid files are logged and skipped so one bad patch cannot break
        /// scene loading.
        /// </summary>
        public static Dictionary<string, List<ScenePatch>> CollectFromProviders(IEnumerable<(string ModId, IModFileProvider Provider)> sources)
        {
            var result = new Dictionary<string, List<ScenePatch>>(StringComparer.Ordinal);

            foreach (var (modId, provider) in sources)
            {
                var files = new List<string>();
                CollectPatchFiles(provider, PatchDirectory, files);
                files.Sort(StringComparer.OrdinalIgnoreCase);

                foreach (var file in files)
                {
                    if (!file.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var text = provider.ReadFileText(file);
                    if (!ScenePatchParser.TryParse(text, modId, file, out var patch, out var error))
                    {
                        GameLogger.Log("TSCN", $"ScenePatchResolver: '{modId}/{file}' is not a valid scene patch: {error}", LogLevel.Error);
                        continue;
                    }

                    if (!result.TryGetValue(patch.TargetPath, out var list))
                    {
                        list = new List<ScenePatch>();
                        result[patch.TargetPath] = list;
                    }
                    list.Add(patch);
                }
            }

            return result;
        }

        private static void CollectPatchFiles(IModFileProvider provider, string directory, List<string> result)
        {
            foreach (var entry in provider.ListFiles(directory))
            {
                if (entry.EndsWith("/"))
                {
                    CollectPatchFiles(provider, entry.TrimEnd('/'), result);
                    continue;
                }

                var prefix = ModPath.NormalizeDirectory(PatchDirectory);
                if (entry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    result.Add(entry);
            }
        }
    }
}
