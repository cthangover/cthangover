using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Factories;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Collects and merges JSON property patches from mods. Patches are
    /// organised by entity type (e.g. "items", "characters") and stored
    /// in <c>patches/{entityType}/</c> directories. Each patch specifies
    /// a target entity ID, a property path, and the value to apply.
    ///
    /// Also reads flat patch files named <c>patches/{entityType}.json</c>
    /// for simpler single-file patch sets. The collected patches are
    /// applied by <see cref="JsonGroupResolver"/> after collecting base
    /// entity data — patches from later mods override earlier ones.
    /// </summary>
    public class PatchResolver : ModAssetResolver
    {
        /// <inheritdoc />
        public override string Id => "patches";

        /// <summary>
        /// Per-entity-type patch cache. <see cref="ConcurrentDictionary"/>
        /// because resolution may be triggered concurrently (e.g. by
        /// parallel unit tests bootstrapping mods through
        /// <see cref="Context.Initialize"/>) — a plain dictionary corrupts
        /// under concurrent writes.
        /// </summary>
        private readonly ConcurrentDictionary<string, Dictionary<string, List<PropertyPatch>>> _cache = new();

        /// <summary>
        /// Collects all patches for the given entity type from every
        /// loaded mod. Searches both <c>patches/{entityType}/</c>
        /// directories and <c>patches/{entityType}.json</c> flat files.
        /// Patches are grouped by target entity ID. Caches per type.
        /// </summary>
        public Dictionary<string, List<PropertyPatch>> Collect(string entityType)
        {
            Context.Initialize();

            if (_cache.TryGetValue(entityType, out var cached))
                return cached;
            var result = new Dictionary<string, List<PropertyPatch>>();
            var prefix = $"patches/{entityType}".Replace('\\', '/').TrimEnd('/') + "/";

            foreach (var modId in Context.GetOrderedModIds())
            {
                var provider = Context.Mods[modId].FileProvider;

                foreach (var entry in provider.ListFiles($"patches/{entityType}"))
                {
                    if (entry.EndsWith("/"))
                        continue;

                    var relativePath = entry.StartsWith(prefix) ? entry.Substring(prefix.Length) : entry;
                    if (!relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var text = Context.ReadResolvedText(modId, entry, provider);
                    if (text == null)
                        continue;

                    try
                    {
                        LoadPatchesFromJson(entityType, modId, entry, text, result);
                    }
                    catch (JsonException ex)
                    {
                        GameLogger.Log("MODS", $"Failed to parse patches for '{entityType}' from mod '{modId}/{entry}': {ex.Message}", LogLevel.Error);
                    }
                }

                var flatFileName = $"{entityType}.json";
                foreach (var entry in provider.ListFiles("patches"))
                {
                    if (entry.EndsWith("/"))
                        continue;

                    var relative = entry.StartsWith("patches/") ? entry.Substring("patches/".Length) : entry;
                    if (!relative.Equals(flatFileName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var text = Context.ReadResolvedText(modId, entry, provider);
                    if (text == null)
                        continue;

                    try
                    {
                        LoadPatchesFromJson(entityType, modId, entry, text, result);
                    }
                    catch (JsonException ex)
                    {
                        GameLogger.Log("MODS", $"Failed to parse patches for '{entityType}' from mod '{modId}/{entry}': {ex.Message}", LogLevel.Error);
                    }
                }
            }

            _cache[entityType] = result;
            return result;
        }

        /// <inheritdoc />
        public override void ClearCache()
        {
            _cache.Clear();
        }

        private static void LoadPatchesFromJson(
            string entityType,
            string modId,
            string entry,
            string text,
            Dictionary<string, List<PropertyPatch>> result)
        {
            var fileData = JsonSerializer.Deserialize<FileData<PropertyPatch>>(text, JsonOptionsProvider.ReadOptions);
            if (fileData?.Items == null)
                return;

            foreach (var patch in fileData.Items)
            {
                if (string.IsNullOrEmpty(patch.ID))
                    continue;

                if (!result.TryGetValue(patch.ID, out var list))
                {
                    list = new List<PropertyPatch>();
                    result[patch.ID] = list;
                }
                list.Add(patch);
            }
        }
    }

    /// <summary>
    /// Static convenience accessor for <see cref="PatchResolver"/>,
    /// proxying through <see cref="ModManager.Instance.GetResolver{T}"/>.
    /// </summary>
    public static class Patches
    {
        public static Dictionary<string, List<PropertyPatch>> Collect(string entityType)
            => ModManager.Instance.GetResolver<PatchResolver>()?.Collect(entityType);
    }
}
