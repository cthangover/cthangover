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
    /// Replays the resolution of one entity layer by layer — definition, <c>Extends</c> parent
    /// (explained recursively), patches in order — with the same merge engine and the same
    /// per-patch step (<see cref="PatchResolver.ApplyOne"/>) as <see cref="EntityExtendsResolution{T}"/>,
    /// recording for each leaf which layer set it. Rules: a field written by the child's definition,
    /// or changed by it relative to the parent, belongs to the child; an unchanged inherited field
    /// keeps the parent's origin; a patch owns every leaf it changed. Nothing is logged: errors were
    /// already reported when the kind was collected.
    /// </summary>
    public sealed class EntityExplainer
    {
        private readonly string _kind;
        private readonly Type _type;
        private readonly IReadOnlyDictionary<string, EntityDefinitionSource> _definitions;
        private readonly PatchResolver _patches;
        private readonly HashSet<string> _cyclic = new();

        /// <summary>
        /// <paramref name="definitions"/> are the winning definitions of the kind by id;
        /// <paramref name="patches"/> may be null (no patches).
        /// </summary>
        public EntityExplainer(string kind, Type type, IReadOnlyDictionary<string, EntityDefinitionSource> definitions, PatchResolver patches)
        {
            _kind = kind;
            _type = type;
            _definitions = definitions;
            _patches = patches;
        }

        /// <summary>The explanation of <paramref name="id"/>, or null when it is not defined or sits in an <c>Extends</c> cycle.</summary>
        public EntityExplanation Explain(string id) => Trace(id, new List<string>());

        private EntityExplanation Trace(string id, List<string> stack)
        {
            if (id == null || !_definitions.TryGetValue(id, out var definition))
                return null;
            var at = stack.IndexOf(id);
            if (at >= 0)
            {
                foreach (var member in stack.Skip(at))
                    _cyclic.Add(member);
                return null;
            }

            stack.Add(id);
            var origins = new Dictionary<string, EntityFieldOrigin>(StringComparer.OrdinalIgnoreCase);
            var written = definition.Written;
            var writtenLeaves = JsonLeafPaths.Collect(written, _type);
            var parentId = EntityExtendsResolution<object>.ExtendsOf(written);
            var parentKey = parentId == null ? null : FindKey(parentId);
            var parent = parentKey == null ? null : Trace(parentKey, stack);
            stack.RemoveAt(stack.Count - 1);
            if (_cyclic.Contains(id))
                return null;

            var node = parent?.Value != null ? Inherit(parent.Value, written) : null;
            if (node != null)
            {
                var parentLeaves = JsonLeafPaths.Collect(parent.Value, _type);
                foreach (var leaf in JsonLeafPaths.Collect(node, _type))
                {
                    if (writtenLeaves.ContainsKey(leaf.Key) || !parentLeaves.TryGetValue(leaf.Key, out var inherited)
                        || !JsonSemantic.Equal(inherited, leaf.Value))
                        origins[leaf.Key] = Origin(leaf.Key, EntityLayer.Definition, id, definition.ModId, definition.File);
                    else if (parent.Fields.TryGetValue(leaf.Key, out var origin))
                        origins[leaf.Key] = origin;
                }
            }
            else
            {
                node = Normalize(written);
                if (node == null)
                    return null;
                foreach (var path in writtenLeaves.Keys)
                    origins[path] = Origin(path, EntityLayer.Definition, id, definition.ModId, definition.File);
            }

            var patches = _patches?.For(_kind, id) ?? Array.Empty<PropertyPatch>();
            foreach (var patch in patches)
            {
                switch (PatchResolver.ApplyOne(node, patch, id, _type, false, out var after, out _))
                {
                    case PatchStep.Deleted:
                        return new EntityExplanation { Kind = _kind, Id = id, Deleted = true, Fields = origins };
                    case PatchStep.Applied:
                        var before = JsonLeafPaths.Collect(node, _type);
                        foreach (var leaf in JsonLeafPaths.Collect(after, _type))
                        {
                            if (!before.TryGetValue(leaf.Key, out var old) || !JsonSemantic.Equal(old, leaf.Value))
                                origins[leaf.Key] = Origin(leaf.Key, EntityLayer.Patch, patch.ID == "*" ? "*" : id, patch.ModId, patch.File);
                        }
                        node = after;
                        break;
                }
            }

            var value = Normalize(node) ?? node;
            var leaves = JsonLeafPaths.Collect(value, _type);
            foreach (var path in origins.Keys.Where(p => !leaves.ContainsKey(p)).ToList())
                origins.Remove(path);
            return new EntityExplanation { Kind = _kind, Id = id, Value = value, Fields = origins };
        }

        private JsonObject Inherit(JsonObject parent, JsonObject written)
        {
            var merged = (JsonObject)parent.DeepClone();
            try
            {
                JsonPatchMerger.Apply(merged, (JsonObject)written.DeepClone(), _type);
            }
            catch (JsonPatchException)
            {
                return null;
            }
            return Normalize(merged);
        }

        /// <summary>The JSON the game sees: read as the DTO and written back (defaults filled in).</summary>
        private JsonObject Normalize(JsonObject node)
        {
            try
            {
                var options = JsonOptionsProvider.ReadOptions;
                var read = JsonSerializer.Deserialize(node.ToJsonString(), _type, options);
                return read == null ? null : JsonPatchMerger.ParseObject(JsonSerializer.Serialize(read, _type, options));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private string FindKey(string id)
            => _definitions.ContainsKey(id)
                ? id
                : _definitions.Keys.FirstOrDefault(key => string.Equals(key, id, StringComparison.OrdinalIgnoreCase));

        private static EntityFieldOrigin Origin(string path, EntityLayer layer, string entityId, string modId, string file)
            => new() { Path = path, Layer = layer, EntityId = entityId, ModId = modId, File = file };
    }
}
