namespace Cthangover.Core.Mods.Patches
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;

    /// <summary>
    /// Patch over patch: merges a patch into a stored layer (<see cref="PatchLayerAttribute"/>) so that
    /// applying the result to any value equals applying the old layer and then the patch —
    /// <c>apply(apply(X, L), P) == apply(X, compose(L, P))</c>. Per property <c>K</c> of the layer
    /// (<see cref="PatchLayerEntry"/>):
    /// <list type="bullet">
    /// <item><c>K</c> with a scalar, array or <c>null</c>, and <c>K$set</c> replace whatever the layer did to <c>K</c>;</item>
    /// <item>an object composes recursively with a layer object, and is merged into a concrete value
    /// (the result stays concrete, written as <c>K$set</c>);</item>
    /// <item><c>K$add</c>/<c>K$remove</c> run on a concrete list; otherwise removals are united with the
    /// layer's and strike the matching elements from its additions, additions with a key compose with the
    /// layer's addition of the same key or are appended. The layer always keeps removals before additions:
    /// an addition followed by a removal of the same element is the same as the removal alone, so
    /// every order can be written that way and no case is lost;</item>
    /// <item>a list operation on a property the layer merges an object into fails as it would when applied.</item>
    /// </list>
    /// Layers nested inside a layer (a variant of a placement's object) compose as ordinary objects.
    /// A <see cref="PatchLayerAttribute.Wholesale"/> layer is data that replaces whole values: a patch changes
    /// the values it names as plain JSON and may give a new whole value, nothing else.
    /// </summary>
    internal static class PatchLayerComposer
    {
        /// <summary>Composes every entry of <paramref name="patch"/> into <paramref name="layer"/> in place.</summary>
        internal static void Compose(JsonObject layer, JsonObject patch, PatchLayerAttribute attribute, string path)
        {
            foreach (var pair in patch.ToList())
            {
                JsonPatchMerger.SplitOperation(pair.Key, path, out var name, out var operation);
                var childPath = path.Length == 0 ? name : path + "." + name;
                ComposeEntry(layer, name, operation, pair.Value, attribute, childPath);
            }
        }

        /// <summary>Composes the patch entry <c>name</c>+<c>operation</c> into the layer's entries of <c>name</c>.</summary>
        internal static void ComposeEntry(JsonObject layer, string name, string operation, JsonNode value, PatchLayerAttribute attribute, string path)
        {
            if (attribute.Wholesale)
            {
                ComposeWholesale(layer, name, operation, value, attribute, path);
                return;
            }

            var entry = Take(layer, name, attribute.Target, path, out var spelling);
            Step(entry, name, operation, value, attribute.Target, path);
            Write(layer, spelling ?? name, entry);
        }

        private static void ComposeWholesale(JsonObject layer, string name, string operation, JsonNode value, PatchLayerAttribute attribute, string path)
        {
            if (!JsonPatchMerger.TryGet(layer, name, out _))
            {
                if (operation == "$add" || operation == "$remove")
                    throw new JsonPatchException(path + operation,
                        $"the layer does not name '{name}' and replaces it whole when used, so there is nothing to change; give the whole value");
                if (operation == null && HasOperation(value))
                    throw new JsonPatchException(path,
                        $"the layer does not name '{name}' and replaces it whole when used; give the whole value without operations");
            }
            JsonPatchMerger.ApplyEntry(layer, name, operation, value, attribute.Target, path);
        }

        /// <summary>Removes the layer's entries of <paramref name="name"/> and folds them, in order, into one entry.</summary>
        private static PatchLayerEntry Take(JsonObject layer, string name, Type target, string path, out string spelling)
        {
            spelling = null;
            var entry = new PatchLayerEntry();
            foreach (var pair in layer.ToList())
            {
                if (!pair.Key.StartsWith(name, StringComparison.OrdinalIgnoreCase)
                    || (pair.Key.Length > name.Length && pair.Key[name.Length] != '$'))
                    continue;
                JsonPatchMerger.SplitOperation(pair.Key, path, out var entryName, out var operation);
                spelling ??= entryName;
                layer.Remove(pair.Key);
                Step(entry, name, operation, pair.Value, target, path);
            }
            return entry;
        }

        private static void Step(PatchLayerEntry entry, string name, string operation, JsonNode value, Type target, string path)
        {
            switch (operation)
            {
                case "$set":
                    SetConcrete(entry, value?.DeepClone(), setForm: true);
                    return;
                case null when JsonPatchMerger.IsNull(value):
                    entry.Kind = PatchLayerEntryKind.Concrete;
                    entry.Absent = true;
                    entry.Value = null;
                    entry.SetForm = false;
                    return;
                case null when value is JsonObject patchObject:
                    MergeObject(entry, name, patchObject, target, path);
                    return;
                case null:
                    SetConcrete(entry, value.DeepClone(), setForm: false);
                    return;
                case "$add":
                case "$remove":
                    ListOperation(entry, name, operation, value, target, path);
                    return;
                default:
                    throw new JsonPatchException(path + operation, $"unknown operation '{operation}' (known: $add, $remove, $set)");
            }
        }

        private static void MergeObject(PatchLayerEntry entry, string name, JsonObject patch, Type target, string path)
        {
            switch (entry.Kind)
            {
                case PatchLayerEntryKind.None:
                    entry.Kind = PatchLayerEntryKind.Fragment;
                    entry.Fragment = new JsonObject();
                    Compose(entry.Fragment, patch, new PatchLayerAttribute(MergeTypeMap.ChildType(target, name)), path);
                    return;
                case PatchLayerEntryKind.Fragment:
                    Compose(entry.Fragment, patch, new PatchLayerAttribute(MergeTypeMap.ChildType(target, name)), path);
                    return;
                case PatchLayerEntryKind.Concrete:
                    var holder = Holder(entry, name);
                    JsonPatchMerger.MergeProperty(holder, name, patch, target, path);
                    FromHolder(entry, holder, name);
                    return;
                default:
                    // After list operations the value is a list or missing: an object replaces it.
                    var fresh = new JsonObject();
                    JsonPatchMerger.MergeProperty(fresh, name, patch, target, path);
                    entry.Removes = null;
                    entry.Adds = null;
                    entry.ExplicitKey = null;
                    FromHolder(entry, fresh, name);
                    return;
            }
        }

        private static void ListOperation(PatchLayerEntry entry, string name, string operation, JsonNode value, Type target, string path)
        {
            switch (entry.Kind)
            {
                case PatchLayerEntryKind.Concrete:
                    var holder = Holder(entry, name);
                    JsonPatchMerger.ApplyEntry(holder, name, operation, value, target, path);
                    FromHolder(entry, holder, name);
                    return;
                case PatchLayerEntryKind.Fragment:
                    throw new JsonPatchException(path + operation, "the layer merges an object into it, not an array");
            }

            var items = JsonPatchMerger.ReadItems(value, path + operation, out var explicitKey);
            if (explicitKey != null)
            {
                if (entry.ExplicitKey != null && !SameKey(entry.ExplicitKey, explicitKey))
                    throw new JsonPatchException(path + operation, $"the layer uses the key {string.Join("+", entry.ExplicitKey)}, the patch {string.Join("+", explicitKey)}");
                entry.ExplicitKey = explicitKey;
            }
            entry.Kind = PatchLayerEntryKind.Operations;
            var key = entry.ExplicitKey ?? MergeTypeMap.KeyOf(target, name);
            var itemType = MergeTypeMap.ItemType(MergeTypeMap.ChildType(target, name));

            if (operation == "$remove")
            {
                entry.Removes ??= new List<JsonNode>();
                foreach (var item in items)
                {
                    entry.Removes.Add(item?.DeepClone());
                    if (entry.Adds != null)
                        Strike(entry.Adds, item, key, itemType, path + operation);
                }
                return;
            }

            entry.Adds ??= new List<JsonNode>();
            foreach (var item in items)
            {
                if (key != null && item is JsonObject keyed && JsonPatchMerger.TryKey(keyed, key, out var keyValues))
                {
                    var match = FindByKey(entry.Adds, key, keyValues);
                    if (match != null)
                    {
                        Compose(match, keyed, new PatchLayerAttribute(itemType), path + operation);
                        continue;
                    }
                }
                entry.Adds.Add(item?.DeepClone());
            }
        }

        /// <summary>Drops from the layer's additions every element the removal <paramref name="item"/> would remove after them.</summary>
        private static void Strike(List<JsonNode> adds, JsonNode item, string[] key, Type itemType, string path)
        {
            if (key == null)
            {
                adds.RemoveAll(added => JsonSemantic.Equal(Materialize(added, itemType, path), item));
                return;
            }

            JsonNode[] keyValues;
            if (item is JsonObject keyed)
            {
                if (!JsonPatchMerger.TryKey(keyed, key, out keyValues))
                    throw new JsonPatchException(path, $"an element to remove has no key {string.Join("+", key)}");
            }
            else if (key.Length == 1)
                keyValues = new[] { item };
            else
                throw new JsonPatchException(path, $"the key {string.Join("+", key)} has several parts, give the element as an object");

            adds.RemoveAll(added => added is JsonObject candidate && JsonPatchMerger.TryKey(candidate, key, out var values)
                && JsonPatchMerger.KeysEqual(values, keyValues));
        }

        /// <summary>An added element as the list holds it once added: an object's own operations run on an empty object.</summary>
        private static JsonNode Materialize(JsonNode added, Type itemType, string path)
        {
            if (added is not JsonObject element)
                return added;
            var fresh = new JsonObject();
            JsonPatchMerger.MergeObject(fresh, (JsonObject)element.DeepClone(), itemType, path);
            return fresh;
        }

        private static JsonObject FindByKey(List<JsonNode> adds, string[] key, JsonNode[] keyValues)
        {
            foreach (var added in adds)
            {
                if (added is JsonObject candidate && JsonPatchMerger.TryKey(candidate, key, out var values)
                    && JsonPatchMerger.KeysEqual(values, keyValues))
                    return candidate;
            }
            return null;
        }

        private static void SetConcrete(PatchLayerEntry entry, JsonNode value, bool setForm)
        {
            entry.Kind = PatchLayerEntryKind.Concrete;
            entry.Value = value;
            entry.Absent = false;
            entry.SetForm = setForm;
            entry.Fragment = null;
            entry.Removes = null;
            entry.Adds = null;
            entry.ExplicitKey = null;
        }

        /// <summary>A scratch object holding the concrete value, so the plain merge engine can run on it.</summary>
        private static JsonObject Holder(PatchLayerEntry entry, string name)
        {
            var holder = new JsonObject();
            if (!entry.Absent)
                holder[name] = entry.Value?.DeepClone();
            return holder;
        }

        private static void FromHolder(PatchLayerEntry entry, JsonObject holder, string name)
        {
            var setForm = entry.Kind == PatchLayerEntryKind.Concrete && entry.SetForm;
            if (JsonPatchMerger.TryGet(holder, name, out var value))
                SetConcrete(entry, value?.DeepClone(), setForm);
            else
            {
                SetConcrete(entry, null, false);
                entry.Absent = true;
            }
        }

        private static void Write(JsonObject layer, string name, PatchLayerEntry entry)
        {
            switch (entry.Kind)
            {
                case PatchLayerEntryKind.Concrete:
                    if (entry.Absent)
                        layer[name] = null;
                    else if (entry.SetForm || entry.Value is JsonObject)
                        layer[name + "$set"] = entry.Value?.DeepClone();
                    else
                        layer[name] = entry.Value?.DeepClone();
                    return;
                case PatchLayerEntryKind.Fragment:
                    layer[name] = entry.Fragment;
                    return;
                case PatchLayerEntryKind.Operations:
                    if (entry.Removes != null)
                        layer[name + "$remove"] = Items(entry.Removes, entry.ExplicitKey);
                    if (entry.Adds != null)
                        layer[name + "$add"] = Items(entry.Adds, entry.ExplicitKey);
                    return;
            }
        }

        private static JsonNode Items(List<JsonNode> items, string[] explicitKey)
        {
            var array = new JsonArray(items.Select(i => i?.DeepClone()).ToArray());
            if (explicitKey == null)
                return array;
            JsonNode key = explicitKey.Length == 1
                ? JsonValue.Create(explicitKey[0])
                : new JsonArray(explicitKey.Select(k => (JsonNode)JsonValue.Create(k)).ToArray());
            return new JsonObject { ["$key"] = key, ["$items"] = array };
        }

        private static bool SameKey(string[] a, string[] b)
            => a.Length == b.Length && a.Zip(b, (x, y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase)).All(same => same);

        private static bool HasOperation(JsonNode node)
        {
            switch (node)
            {
                case JsonObject obj:
                    return obj.Any(pair => pair.Key.Contains('$') || HasOperation(pair.Value));
                case JsonArray array:
                    return array.Any(HasOperation);
                default:
                    return false;
            }
        }
    }
}
