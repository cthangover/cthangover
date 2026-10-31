using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cthangover.Core.Factories;
using Cthangover.Core.Mods.Patches;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Collects and merges JSON property patches from mods. Patches are
    /// organised per entity kind (e.g. "item", "character") and stored
    /// in <c>patches/{kind}/</c> directories. Each patch specifies
    /// a target entity ID, a property path, and the value to apply.
    ///
    /// Also reads flat patch files named <c>patches/{kind}.json</c>
    /// for simpler single-file patch sets. The collected patches are
    /// applied by <see cref="EntityResolver"/> after collecting base
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
            var result = new Dictionary<string, List<PropertyPatch>>(StringComparer.OrdinalIgnoreCase);
            var prefix = ModPath.NormalizeDirectory($"patches/{entityType}");

            foreach (var source in Context.GetOrderedModIds().Select(id => (Id: id, Provider: Context.Mods[id].FileProvider))
                .Concat(ModOverlays.All.Select(pair => (Id: pair.Key, Provider: pair.Value))))
            {
                var modId = source.Id;
                var provider = source.Provider;

                foreach (var entry in provider.ListFiles($"patches/{entityType}").OrderBy(path => path, StringComparer.Ordinal))
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

        /// <summary>
        /// Applies the patches of <paramref name="kind"/> to one entity: wildcard (<c>*</c>) patches
        /// first, then those addressed to <paramref name="id"/> (case-insensitive), each in load order.
        /// Every patch is applied to a copy and checked by deserialising into <typeparamref name="T"/>;
        /// a patch that fails either way is logged as <c>ERROR [PATCH] mod/file#id: path: message</c>
        /// and skipped, the others still apply — one broken mod never takes down the whole kind.
        /// Returns null when a patch says <c>"$delete": true</c> (callers drop the entity), the
        /// unchanged instance when nothing applies, otherwise a new instance.
        /// </summary>
        public T Apply<T>(string kind, string id, T value) where T : class
        {
            if (value == null)
                return null;
            var ordered = For(kind, id);
            if (ordered.Count == 0)
                return value;

            JsonObject node = null;
            var current = value;
            foreach (var patch in ordered)
            {
                node ??= JsonPatchMerger.ParseObject(JsonSerializer.Serialize(value, JsonOptionsProvider.ReadOptions));
                switch (ApplyOne(node, patch, id, typeof(T), true, out var after, out var read))
                {
                    case PatchStep.Deleted:
                        return null;
                    case PatchStep.Applied:
                        node = after;
                        current = (T)read;
                        break;
                }
            }
            return current;
        }

        /// <summary>
        /// The patches that apply to <paramref name="id"/>, in application order: wildcard (<c>*</c>)
        /// patches first, then the addressed ones (id compared case-insensitively), each in load order.
        /// </summary>
        public IReadOnlyList<PropertyPatch> For(string kind, string id)
        {
            var patches = Collect(kind);
            var ordered = new List<PropertyPatch>();
            if (patches.TryGetValue("*", out var wildcard))
                ordered.AddRange(wildcard);
            if (id != "*" && patches.TryGetValue(id, out var addressed))
                ordered.AddRange(addressed);
            return ordered;
        }

        /// <summary>
        /// One patch step shared by resolution and <see cref="EntityResolver.Explain"/>: merges
        /// <paramref name="patch"/> into a copy of <paramref name="node"/> and reads the copy back as
        /// <paramref name="type"/>. On success <paramref name="result"/> is the merged JSON and
        /// <paramref name="read"/> the DTO; on failure both are null and, with <paramref name="report"/>,
        /// <c>ERROR [PATCH] mod/file#id: path: message</c> is logged. <paramref name="node"/> is never changed.
        /// </summary>
        public static PatchStep ApplyOne(JsonObject node, PropertyPatch patch, string id, Type type, bool report, out JsonObject result, out object read)
        {
            result = null;
            read = null;
            var body = JsonPatchMerger.ToNode(patch);
            if (TakeDelete(body, patch, id, report, out var delete) && delete)
            {
                if (report)
                    GameLogger.Log("PATCH", $"{Describe(patch, id)}: entity removed ($delete)");
                return PatchStep.Deleted;
            }

            var candidate = (JsonObject)node.DeepClone();
            string error;
            try
            {
                JsonPatchMerger.Apply(candidate, body, type);
                read = JsonSerializer.Deserialize(candidate.ToJsonString(), type, JsonOptionsProvider.ReadOptions);
                result = candidate;
                return PatchStep.Applied;
            }
            catch (JsonPatchException ex)
            {
                error = $"{ex.Path}: {ex.Message}";
            }
            catch (JsonException ex)
            {
                error = $"{ex.Path ?? "$"}: {FirstLine(ex.Message)}";
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is NotSupportedException || ex is ArgumentException || ex is FormatException)
            {
                error = $"$: {FirstLine(ex.Message)}";
            }

            read = null;
            if (report)
                GameLogger.Log("PATCH", $"{Describe(patch, id)}: {error}; patch skipped", LogLevel.Error);
            return PatchStep.Skipped;
        }

        /// <summary>
        /// Removes <c>$delete</c> from the patch body and reports whether it asked for deletion.
        /// Deleting through a wildcard is refused: one key would wipe out a whole kind.
        /// </summary>
        private static bool TakeDelete(JsonObject body, PropertyPatch patch, string id, bool report, out bool delete)
        {
            delete = false;
            string key = null;
            foreach (var pair in body)
            {
                if (string.Equals(pair.Key, "$delete", StringComparison.OrdinalIgnoreCase))
                    key = pair.Key;
            }
            if (key == null)
                return false;

            var value = body[key];
            body.Remove(key);
            if (value is not JsonValue flag || flag.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False))
            {
                if (report)
                    GameLogger.Log("PATCH", $"{Describe(patch, id)}: $delete: must be true or false", LogLevel.Error);
                return true;
            }
            if (patch.ID == "*")
            {
                if (report)
                    GameLogger.Log("PATCH", $"{Describe(patch, id)}: $delete: a wildcard patch cannot delete entities", LogLevel.Error);
                return true;
            }
            delete = flag.GetValue<bool>();
            return true;
        }

        private static string Describe(PropertyPatch patch, string id)
            => $"{patch.ModId ?? "?"}/{patch.File ?? "?"}#{(patch.ID == "*" ? "*→" + id : patch.ID)}";

        /// <summary>The first line of a serializer message without its trailing " Path: … | LineNumber: …" (the path is logged separately).</summary>
        private static string FirstLine(string message)
        {
            var index = message.IndexOf('\n');
            var line = index < 0 ? message : message.Substring(0, index);
            var path = line.IndexOf(" Path: ", StringComparison.Ordinal);
            return (path < 0 ? line : line.Substring(0, path)).TrimEnd();
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
                {
                    GameLogger.Log("PATCH", $"{modId}/{entry}: a '{entityType}' patch without Id is ignored", LogLevel.Error);
                    continue;
                }

                patch.ModId = modId;
                patch.File = entry;

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
        /// <summary>Collects the patches for one entity kind, grouped by target entity id.</summary>
        public static Dictionary<string, List<PropertyPatch>> Collect(string entityType)
            => ModManager.Instance.GetResolver<PatchResolver>()?.Collect(entityType);
    }
}
