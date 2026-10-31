namespace Cthangover.Core.Mods.Resolvers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Patches;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Final resolution of the entities of one kind (plan 2.2, contract section 7): for every
    /// entity, its <c>Extends</c> parent is resolved completely first (the parent's own
    /// <c>Extends</c> and patches), the child's JSON as written is deep-merged over it with the
    /// patch engine (so a child may also say <c>Parts$add</c>), and then the child's own patches
    /// apply. The child's JSON as written is needed because its DTO, serialised, would carry
    /// defaults for everything it did not set and wipe the parent's values.
    /// <para>
    /// A cycle is an ERROR and every entity in it is skipped. An unknown parent, or a parent
    /// that is itself unavailable, is an ERROR and the child stays as written. A patch with
    /// <c>"$delete": true</c> removes the entity (its children then fall back the same way).
    /// </para>
    /// </summary>
    public sealed class EntityExtendsResolution<T> where T : class
    {
        private readonly string _kind;
        private readonly Dictionary<string, T> _items;
        private readonly Dictionary<string, JsonObject> _written;
        private readonly PatchResolver _patches;
        private readonly Dictionary<string, T> _done = new();
        private readonly HashSet<string> _cyclic = new();

        /// <summary>
        /// <paramref name="written"/> holds the JSON as written of the entities that declare
        /// <c>Extends</c> (keyed like <paramref name="items"/>); <paramref name="patches"/> may be null.
        /// </summary>
        public EntityExtendsResolution(string kind, Dictionary<string, T> items, Dictionary<string, JsonObject> written, PatchResolver patches)
        {
            _kind = kind;
            _items = items;
            _written = written ?? new Dictionary<string, JsonObject>();
            _patches = patches;
        }

        /// <summary>The value of the <c>Extends</c> property of an entity as written, or null.</summary>
        public static string ExtendsOf(JsonObject written)
        {
            if (written == null)
                return null;
            foreach (var pair in written)
            {
                if (string.Equals(pair.Key, "Extends", StringComparison.OrdinalIgnoreCase)
                    && pair.Value is JsonValue value && value.GetValueKind() == JsonValueKind.String)
                {
                    var parent = value.GetValue<string>();
                    return string.IsNullOrWhiteSpace(parent) ? null : parent;
                }
            }
            return null;
        }

        /// <summary>Resolves every entity; the result keeps the input order and lacks skipped and deleted ones.</summary>
        public Dictionary<string, T> ResolveAll()
        {
            var result = new Dictionary<string, T>();
            foreach (var id in _items.Keys.ToList())
            {
                var resolved = Resolve(id, new List<string>());
                if (resolved != null)
                    result[id] = resolved;
            }
            return result;
        }

        private T Resolve(string id, List<string> stack)
        {
            if (_done.TryGetValue(id, out var done))
                return done;

            var at = stack.IndexOf(id);
            if (at >= 0)
            {
                var cycle = stack.Skip(at).Append(id).ToList();
                GameLogger.Log("ENTITIES", $"'{_kind}': Extends cycle {string.Join(" → ", cycle)}; these entities are skipped", LogLevel.Error);
                foreach (var member in cycle)
                    _cyclic.Add(member);
                return null;
            }

            stack.Add(id);
            var item = _items[id];
            if (_written.TryGetValue(id, out var written) && ExtendsOf(written) is { } parentId)
                item = Inherit(id, item, written, parentId, stack);
            stack.RemoveAt(stack.Count - 1);

            if (_cyclic.Contains(id))
            {
                _done[id] = null;
                return null;
            }

            item = _patches != null ? _patches.Apply(_kind, id, item) : item;
            _done[id] = item;
            return item;
        }

        private T Inherit(string id, T item, JsonObject written, string parentId, List<string> stack)
        {
            var parentKey = _items.ContainsKey(parentId)
                ? parentId
                : _items.Keys.FirstOrDefault(key => string.Equals(key, parentId, StringComparison.OrdinalIgnoreCase));
            if (parentKey == null)
            {
                GameLogger.Log("ENTITIES", $"'{_kind}' '{id}': Extends unknown '{parentId}'; used as written", LogLevel.Error);
                return item;
            }

            var parent = Resolve(parentKey, stack);
            if (_cyclic.Contains(id))
                return item;
            if (parent == null)
            {
                GameLogger.Log("ENTITIES", $"'{_kind}' '{id}': Extends '{parentKey}', which is not available (skipped or deleted); used as written", LogLevel.Error);
                return item;
            }

            var options = JsonOptionsProvider.ReadOptions;
            try
            {
                var node = JsonPatchMerger.ParseObject(JsonSerializer.Serialize(parent, options));
                JsonPatchMerger.Apply(node, (JsonObject)written.DeepClone(), typeof(T));
                return JsonSerializer.Deserialize<T>(node.ToJsonString(), options) ?? item;
            }
            catch (JsonPatchException ex)
            {
                GameLogger.Log("ENTITIES", $"'{_kind}' '{id}': Extends '{parentKey}': {ex.Path}: {ex.Message}; used as written", LogLevel.Error);
            }
            catch (JsonException ex)
            {
                GameLogger.Log("ENTITIES", $"'{_kind}' '{id}': Extends '{parentKey}': {ex.Path ?? "$"}: {ex.Message}; used as written", LogLevel.Error);
            }
            return item;
        }
    }
}
