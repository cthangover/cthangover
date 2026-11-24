namespace Cthangover.Core.Mods.HotReload.Impls
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Mods.Compilation;
    using Cthangover.Core.Mods.Resolvers;
    using Godot;

    /// <summary>Queries live nodes and registry/cache facts without resolving resource payloads.</summary>
    public sealed class ModDevelopmentInspectCommand : IModDevelopmentCommand
    {
        private const int NodeScanLimit = 10000;

        /// <summary>Combines targeted live queries under the one inspection tool.</summary>
        public IReadOnlyCollection<string> Commands { get; } = new[] { "inspect" };

        /// <summary>Advertises the inspection areas and their most useful selectors.</summary>
        public string Description => "inspect: paged node, cache, runtime, resource or mod data. Node filters include type, id, group and query; use path for an exact inspection. Resource accepts kind=texture|shader and key.";

        /// <summary>Returns a detached snapshot on the game thread with bounded list results.</summary>
        public Task<JsonNode> ExecuteAsync(ModDevelopmentContext context, string command, CancellationToken token)
        {
            var area = context.Require("area");
            var request = context.Request;
            var mod = request["mod"] == null || area == "runtime" ? null : context.ModId();
            var query = request["query"]?.GetValue<string>();
            var offset = Math.Max(0, request["offset"]?.GetValue<int>() ?? 0);
            var limit = Math.Clamp(request["limit"]?.GetValue<int>() ?? 20, 1, 100);
            var result = area switch
            {
                "node" => InspectNodes(context, query, offset, limit),
                "cache" => mod == null ? InspectCompilationCache(query, offset, limit) : InspectCompilation(mod),
                "runtime" => InspectRuntime(context),
                "resource" => InspectResources(request, mod, query, offset, limit),
                "mod" => mod == null ? InspectMods(query, offset, limit) : InspectMod(mod),
                _ => throw new ArgumentException("Unknown inspection area: " + area)
            };
            return Task.FromResult<JsonNode>(result);
        }

        internal static JsonObject InspectCompilation(string mod)
        {
            ModAssemblyCache.CurrentArtifacts.TryGetValue(mod, out var artifact);
            var result = new JsonObject
            {
                ["mod"] = mod,
                ["compiled_artifact"] = artifact,
                ["dll_exists"] = File.Exists(artifact),
                ["pdb_exists"] = artifact != null && File.Exists(Path.ChangeExtension(artifact, ".pdb")),
                ["loaded_artifacts"] = JsonSerializer.SerializeToNode(ModAssemblyLoader.ArtifactsOf(mod))
            };
            if (ModAssemblyCache.Decisions.TryGetValue(mod, out var decision))
            {
                result["outcome"] = decision.Outcome;
                result["reason"] = decision.Reason;
                result["candidate"] = decision.Path;
                result["time"] = JsonValue.Create(decision.Time);
            }
            return result;
        }

        internal static JsonObject InspectMod(string id)
        {
            var mods = ModRegistry.Instance.Mods;
            mods.TryGetValue(id, out var mod);
            return new JsonObject
            {
                ["id"] = id,
                ["active"] = mod != null,
                ["name"] = mod?.Name,
                ["version"] = mod?.Version,
                ["root"] = mod?.FileProvider?.RootPath,
                ["has_loaded_assembly"] = ModAssemblyLoader.ArtifactsOf(id).Count != 0
            };
        }

        private static JsonObject InspectCompilationCache(string query, int offset, int limit)
        {
            var ids = ModAssemblyCache.CurrentArtifacts.Keys.Union(ModAssemblyCache.Decisions.Keys,
                StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal);
            var result = Page(CompilationEntries(ids, query), offset, limit);
            result["root"] = ModAssemblyCache.CacheRoot;
            result["enabled"] = ModConfig.Instance.UseAssemblyCache;
            return result;
        }

        private static JsonObject InspectMods(string query, int offset, int limit)
        {
            return Page(ModEntries(query), offset, limit);
        }

        private static IEnumerable<JsonObject> CompilationEntries(IEnumerable<string> ids, string query)
        {
            foreach (var id in ids)
            {
                if (Contains(id, query))
                    yield return InspectCompilation(id);
            }
        }

        private static IEnumerable<JsonObject> ModEntries(string query)
        {
            var mods = ModRegistry.Instance.Mods;
            foreach (var id in mods.Keys.Order(StringComparer.Ordinal))
            {
                if (Contains(id, query) || Contains(mods[id].Name, query))
                    yield return InspectMod(id);
            }
        }

        private static JsonObject InspectRuntime(ModDevelopmentContext context)
        {
            var request = context.Request;
            var result = ModDevelopmentRuntimeQuery.InspectRuntime(context.Tree, request["mod"]?.GetValue<string>());
            if (result["state"] is JsonObject state && request["fields"] != null)
            {
                var selected = new JsonObject();
                foreach (var field in request["fields"].Deserialize<string[]>().Take(64))
                {
                    if (state.TryGetPropertyValue(field, out var value))
                        selected[field] = value?.DeepClone();
                }
                result["state"] = selected;
            }
            return result;
        }

        private static JsonObject InspectNodes(ModDevelopmentContext context, string query, int offset, int limit)
        {
            var request = context.Request;
            if (request["path"] != null)
                return ModDevelopmentNodeQuery.InspectNode(context);

            var type = request["type"]?.GetValue<string>();
            var id = request["id"]?.GetValue<ulong>();
            var group = request["group"]?.GetValue<string>();
            using var groupName = group == null ? null : new StringName(group);
            var entries = new List<JsonObject>();
            var visited = 0;
            var matches = 0;
            var pending = new Queue<Node>();
            pending.Enqueue(context.Tree.Root);
            while (pending.Count != 0 && visited < NodeScanLimit && entries.Count < limit + 1)
            {
                var node = pending.Dequeue();
                visited++;
                var nodeType = node.GetType();
                var path = node.GetPath().ToString();
                if ((!id.HasValue || node.GetInstanceId() == id) && Contains(path, query)
                    && Contains(nodeType.FullName, type) && (groupName == null || node.IsInGroup(groupName))
                    && ++matches > offset)
                {
                    entries.Add(new JsonObject
                    {
                        ["id"] = node.GetInstanceId(),
                        ["path"] = path,
                        ["type"] = nodeType.FullName,
                        ["children"] = node.GetChildCount()
                    });
                }

                var searchableChildren = Math.Min(node.GetChildCount(), NodeScanLimit - visited - pending.Count);
                for (var child = 0; child < searchableChildren; child++)
                    pending.Enqueue(node.GetChild(child));
            }

            return BuildPage(entries, offset, limit, visited == NodeScanLimit);
        }

        private static JsonObject InspectResources(JsonObject request, string mod, string query, int offset, int limit)
        {
            var kind = request["kind"]?.GetValue<string>() ?? "texture";
            var key = request["key"]?.GetValue<string>();
            var entries = kind switch
            {
                "texture" => Textures.Collect(),
                "shader" => Shaders.Collect(),
                _ => throw new ArgumentException("Resource kind must be texture or shader.")
            };
            if (entries == null)
                return BuildPage(new List<JsonObject>(), offset, limit);

            return Page(ResourceEntries(entries, kind, mod, key, query), offset, limit);
        }

        private static IEnumerable<JsonObject> ResourceEntries(Dictionary<string, FileEntry> entries, string kind,
            string mod, string key, string query)
        {
            foreach (var resourceKey in entries.Keys.Order(StringComparer.Ordinal))
            {
                var entry = entries[resourceKey];
                if ((mod != null && !string.Equals(entry.ModId, mod, StringComparison.OrdinalIgnoreCase))
                    || (key != null && !string.Equals(resourceKey, key, StringComparison.OrdinalIgnoreCase))
                    || (!Contains(resourceKey, query) && !Contains(entry.FullPath, query)))
                    continue;

                yield return new JsonObject
                {
                    ["key"] = resourceKey,
                    ["kind"] = kind,
                    ["mod"] = entry.ModId,
                    ["path"] = entry.FullPath,
                    ["cached"] = kind == "texture" ? Textures.TryGetCached(resourceKey) != null : Shaders.TryGetCached(resourceKey) != null
                };
            }
        }

        private static JsonObject Page(IEnumerable<JsonObject> items, int offset, int limit)
        {
            return BuildPage(items.Skip(offset).Take(limit + 1).ToList(), offset, limit);
        }

        private static JsonObject BuildPage(List<JsonObject> items, int offset, int limit, bool scanTruncated = false)
        {
            var page = new JsonArray();
            foreach (var item in items.Take(limit))
                page.Add(item);
            return new JsonObject
            {
                ["entries"] = page,
                ["next_offset"] = items.Count > limit ? JsonValue.Create(offset + limit) : null,
                ["scan_truncated"] = scanTruncated
            };
        }

        private static bool Contains(string value, string query)
            => string.IsNullOrWhiteSpace(query) || (value?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
