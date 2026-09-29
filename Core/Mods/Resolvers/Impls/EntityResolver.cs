using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Factories;
using Cthangover.Core.Mods.Patches;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Single collector for every JSON-defined entity in the game. Unlike the
    /// directory-scoped resolver it replaces, this one recursively walks
    /// <b>all</b> files of <b>all</b> mods once, peeks each file's top-level
    /// <c>"Kind"</c> (see <see cref="EntityKindPeek"/>), and routes the file to
    /// the kind's cache. The kind — not the directory — decides the owner, so
    /// the old failure mode where <c>characters/actions.json</c> was parsed as
    /// a skill and killed with an enum error cannot happen: a file is only
    /// deserialised as the one DTO its kind names.
    /// <para/>
    /// Collection follows the established override semantics: mods are visited
    /// in dependency order, later mods override earlier ones by entity ID, and
    /// property patches for the kind (<c>patches/&lt;kind&gt;/</c> and
    /// <c>patches/&lt;kind&gt;.json</c>) are merged into each surviving entity.
    /// The scan and the parsed per-kind dictionaries are cached until
    /// <see cref="ClearCache"/> — one directory walk serves every factory
    /// instead of one walk per type.
    /// </summary>
    public class EntityResolver : ModAssetResolver
    {
        /// <inheritdoc />
        public override string Id => "entities";

        /// <summary>One data file queued during the scan, remembered with its owning mod for later reading.</summary>
        private sealed class EntityFile
        {
            public string ModId;
            public string Path;
        }

        private bool _scanned;
        private readonly Dictionary<string, List<EntityFile>> _filesByKind = new();
        private readonly Dictionary<string, object> _collected = new();

        /// <summary>
        /// Returns all entities of the DTO <typeparamref name="T"/>. The kind
        /// is read from the DTO's <see cref="EntityKindAttribute"/>, files are
        /// matched during the shared scan, and each file is deserialised as
        /// <see cref="FileData{T}"/>. The resulting dictionary is cached per
        /// kind and returned as-is; callers treat entries as immutable.
        /// </summary>
        public Dictionary<string, T> Collect<T>() where T : class, IIdentifiable
        {
            Context.Initialize();

            var kind = EntityKindRegistry.KindOf<T>();
            if (string.IsNullOrEmpty(kind))
            {
                GameLogger.Log("ENTITIES",
                    $"Collect<{typeof(T).Name}>: DTO has no [EntityKind] attribute, cannot resolve its files",
                    LogLevel.Error);
                return new Dictionary<string, T>();
            }

            if (_collected.TryGetValue(kind, out var cached))
                return (Dictionary<string, T>)cached;

            EnsureScanned();

            var result = new Dictionary<string, T>();
            if (_filesByKind.TryGetValue(kind, out var files))
            {
                foreach (var file in files)
                {
                    ReadFile(file, kind, result);
                }
            }

            ApplyPatches(kind, result);

            GameLogger.Log("MODS", result.Count > 0
                ? $"CollectEntities<{typeof(T).Name}>('{kind}') → {result.Count} item(s)"
                : $"CollectEntities<{typeof(T).Name}>('{kind}') — no data found",
                result.Count > 0 ? LogLevel.Debug : LogLevel.Error);

            _collected[kind] = result;
            return result;
        }

        private void ReadFile<T>(EntityFile file, string kind, Dictionary<string, T> result) where T : class, IIdentifiable
        {
            var provider = Context.GetMod(file.ModId)?.FileProvider;
            if (provider == null)
                return;

            var jsonText = Context.ReadResolvedText(file.ModId, file.Path, provider);
            if (jsonText == null)
            {
                GameLogger.Log("MODS", $"ReadResolvedText returned null for '{file.ModId}/{file.Path}'", LogLevel.Error);
                return;
            }

            try
            {
                var fileData = JsonSerializer.Deserialize<FileData<T>>(jsonText, JsonOptionsProvider.ReadOptions);
                if (fileData?.Items == null || fileData.Items.Count == 0)
                    return;

                if (!string.Equals(fileData.Kind, kind, StringComparison.OrdinalIgnoreCase))
                {
                    GameLogger.Log("MODS",
                        $"'{file.ModId}/{file.Path}' declares kind '{fileData.Kind}' but was routed as '{kind}'",
                        LogLevel.Error);
                    return;
                }

                foreach (var item in fileData.Items)
                {
                    if (string.IsNullOrEmpty(item.ID))
                    {
                        GameLogger.Log("MODS", $"entity without Id in '{file.ModId}/{file.Path}' (kind={kind})", LogLevel.Error);
                        continue;
                    }

                    if (result.ContainsKey(item.ID))
                    {
                        GameLogger.Log("MODS",
                            $"'{item.ID}' ({kind}) from mod '{file.ModId}' overrides previous definition",
                            LogLevel.Warning);
                        ModConflictRegistry.Record(kind, item.ID, file.ModId, null, false);
                    }

                    result[item.ID] = item;
                }
            }
            catch (JsonException ex)
            {
                GameLogger.Log("MODS",
                    $"JSON parse failed for '{file.ModId}/{file.Path}' (kind={kind}): {ex.Message}",
                    LogLevel.Error);
            }
        }

        /// <summary>
        /// One-time recursive discovery pass. Every <c>.json</c> file is peeked
        /// for its kind; files without one are ignored as non-data, unknown
        /// kinds are reported as authoring errors. Paths are sorted inside a
        /// mod so override order within one mod is deterministic.
        /// </summary>
        private void EnsureScanned()
        {
            if (_scanned)
                return;

            _scanned = true;
            _filesByKind.Clear();
            EntityKindRegistry.Refresh();

            foreach (var modId in Context.GetOrderedModIds())
            {
                var provider = Context.GetMod(modId)?.FileProvider;
                if (provider == null)
                    continue;

                var paths = new List<string>();
                CollectJsonPaths(provider, string.Empty, paths);
                paths.Sort(StringComparer.OrdinalIgnoreCase);

                foreach (var path in paths)
                {
                    if (!EntityKindPeek.TryRead(provider.ReadFileText(path), out var kind))
                        continue;

                    if (!EntityKindRegistry.IsKnown(kind))
                    {
                        GameLogger.Log("ENTITIES",
                            $"unknown entity kind '{kind}' in '{modId}/{path}'",
                            LogLevel.Error);
                        continue;
                    }

                    if (!_filesByKind.TryGetValue(kind, out var list))
                    {
                        list = new List<EntityFile>();
                        _filesByKind[kind] = list;
                    }

                    list.Add(new EntityFile { ModId = modId, Path = path });
                }
            }
        }

        private static void CollectJsonPaths(IModFileProvider provider, string directory, List<string> result)
        {
            foreach (var entry in provider.ListFiles(directory))
            {
                if (entry.EndsWith("/"))
                {
                    CollectJsonPaths(provider, entry.TrimEnd('/'), result);
                    continue;
                }

                if (entry.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    result.Add(entry);
            }
        }

        /// <summary>
        /// Merges kind-scoped property patches into the collected entities.
        /// The patch key is the entity kind (e.g. <c>patches/character.json</c>),
        /// which is what stops a character patch from being applied to skills
        /// and actions the way the old shared <c>patches/characters/</c> key did.
        /// </summary>
        private void ApplyPatches<T>(string kind, Dictionary<string, T> result) where T : class, IIdentifiable
        {
            var patchResolver = Context.GetResolver<PatchResolver>();
            if (patchResolver == null)
                return;

            var patches = patchResolver.Collect(kind);
            if (patches == null || patches.Count == 0)
                return;

            GameLogger.Log("MODS", $"Applying patches to kind '{kind}' ({patches.Count} id set(s))");

            var merger = new JsonPatchMerger();
            var mergeKeys = JsonPatchMerger.BuildMergeKeys<T>();
            var opts = JsonOptionsProvider.ReadOptions;
            var ids = new List<string>(result.Keys);
            foreach (var id in ids)
            {
                var itemJson = JsonSerializer.Serialize(result[id], opts);
                var mergedJson = merger.Merge(itemJson, id, patches, mergeKeys);
                result[id] = JsonSerializer.Deserialize<T>(mergedJson, opts);
            }
        }

        /// <inheritdoc />
        public override void ClearCache()
        {
            _scanned = false;
            _filesByKind.Clear();
            _collected.Clear();
            EntityKindRegistry.Refresh();
        }
    }

    /// <summary>
    /// Static convenience accessor for <see cref="EntityResolver"/>,
    /// proxying through <see cref="ModManager.Instance"/>'s resolver lookup.
    /// </summary>
    public static class Entities
    {
        public static Dictionary<string, T> Collect<T>() where T : class, IIdentifiable
            => ModManager.Instance.GetResolver<EntityResolver>()?.Collect<T>() ?? new Dictionary<string, T>();
    }
}
