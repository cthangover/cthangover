using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Factories;
using Cthangover.Core.Mods.Patches;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Generic JSON entity collector. Reads <c>.json</c> files from a
    /// given directory (e.g. <c>data/items/</c>) across all mods,
    /// deserialises them as <c>FileData{T}</c>, and builds a dictionary
    /// keyed by each item's <see cref="IIdentifiable.ID"/>. Later mods
    /// override earlier ones for the same ID.
    ///
    /// After collecting base data, applies JSON property patches from
    /// <see cref="PatchResolver"/> using
    /// <see cref="JsonPatchMerger.Merge"/> — enabling mods to tweak
    /// individual properties of entities defined by other mods without
    /// replacing the entire entity.
    /// </summary>
    public class JsonGroupResolver : ModAssetResolver
    {
        /// <inheritdoc />
        public override string Id => "json_group";

        /// <summary>
        /// Type+directory keyed entity cache. <see cref="ConcurrentDictionary"/>
        /// because <see cref="Collect{T}"/> can be invoked concurrently (e.g.
        /// by parallel unit tests or factory lookups during startup) — a plain
        /// dictionary corrupts under concurrent writes.
        /// </summary>
        private readonly ConcurrentDictionary<string, object> _cache = new();

        /// <summary>
        /// Collects all entities of type <typeparamref name="T"/> from
        /// the given directory across all loaded mods. Deserialises each
        /// <c>.json</c> file as <c>FileData{T}</c>, deduplicates by ID,
        /// and applies patches from <see cref="PatchResolver"/>. Caches
        /// by type + directory combination.
        /// </summary>
        public Dictionary<string, T> Collect<T>(string directory) where T : class, IIdentifiable
        {
            Context.Initialize();

            var cacheKey = typeof(T).FullName + "@" + directory;
            if (_cache.TryGetValue(cacheKey, out var cached))
                return (Dictionary<string, T>)cached;

            GameLogger.Log("MODS", $"CollectJsonGroup<{typeof(T).Name}>('{directory}'): CACHE MISS, scanning {Context.Mods.Count} mod(s)...");

            var result = new Dictionary<string, T>();
            var prefix = directory.Replace('\\', '/').TrimEnd('/') + "/";
            var foundAny = false;

            foreach (var modId in Context.GetOrderedModIds())
            {
                var provider = Context.Mods[modId].FileProvider;

                foreach (var entry in provider.ListFiles(directory))
                {
                    if (entry.EndsWith("/"))
                        continue;

                    var relativePath = entry.StartsWith(prefix) ? entry.Substring(prefix.Length) : entry;
                    if (!relativePath.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    GameLogger.Log("MODS", $"  Reading json: mod='{modId}', entry='{entry}'", LogLevel.Debug);
                    var jsonText = Context.ReadResolvedText(modId, entry, provider);
                    if (jsonText == null)
                    {
                        GameLogger.Log("MODS", $"  ReadResolvedText returned null for '{modId}/{entry}'", LogLevel.Error);
                        continue;
                    }

                    try
                    {
                        var fileData = JsonSerializer.Deserialize<FileData<T>>(jsonText, JsonOptionsProvider.ReadOptions);
                        if (fileData?.Items == null || fileData.Items.Count == 0)
                        {
                            GameLogger.Log("MODS", $"  No items in file '{entry}'", LogLevel.Error);
                            continue;
                        }

                        foundAny = true;
                        foreach (var item in fileData.Items)
                        {
                            if (result.ContainsKey(item.ID))
                            {
                                GameLogger.Log("MODS", $"'{item.ID}' from mod '{modId}' overrides previous definition", LogLevel.Warning);
                                ModConflictRegistry.Record("entity", item.ID, modId, null, false);
                            }
                            result[item.ID] = item;

                            GameLogger.Log("MODS", $"  + item: id={item.ID}", LogLevel.Debug);
                        }
                    }
                    catch (JsonException ex)
                    {
                        GameLogger.Log("MODS", $"JSON parse failed for '{modId}/{entry}': {ex.Message}", LogLevel.Error);
                    }
                }
            }

            if (!foundAny)
            {
                GameLogger.Log("MODS", $"CollectJsonGroup<{typeof(T).Name}>('{directory}') — no data found", LogLevel.Error);
            }

            var patchResolver = Context.GetResolver<PatchResolver>();
            if (patchResolver != null)
            {
                var patches = patchResolver.Collect(directory);
                if (patches != null && patches.Count > 0)
                {
                    var merger = new JsonPatchMerger();
                    var mergeKeys = JsonPatchMerger.BuildMergeKeys<T>();
                    var opts = JsonOptionsProvider.ReadOptions;
                    var ids = new System.Collections.Generic.List<string>(result.Keys);
                    foreach (var id in ids)
                    {
                        var itemJson = JsonSerializer.Serialize(result[id], opts);
                        var mergedJson = merger.Merge(itemJson, id, patches, mergeKeys);
                        result[id] = JsonSerializer.Deserialize<T>(mergedJson, opts);
                    }
                }
            }

            _cache[cacheKey] = result;

            GameLogger.Log("MODS", $"CollectJsonGroup<{typeof(T).Name}>('{directory}') → {result.Count} item(s)");
            return result;
        }

        /// <inheritdoc />
        public override void ClearCache()
        {
            _cache.Clear();
        }
    }

    /// <summary>
    /// Static convenience accessor for <see cref="JsonGroupResolver"/>,
    /// proxying through <see cref="ModManager.Instance.GetResolver{T}"/>.
    /// </summary>
    public static class JsonGroup
    {
        public static Dictionary<string, T> Collect<T>(string directory) where T : class, IIdentifiable
            => ModManager.Instance.GetResolver<JsonGroupResolver>()?.Collect<T>(directory);
    }
}
