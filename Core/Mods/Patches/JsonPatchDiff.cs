namespace Cthangover.Core.Mods.Patches
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    /// <summary>
    /// The inverse of <see cref="JsonPatchMerger"/>: the smallest patch that turns <c>base</c> into
    /// <c>edited</c>, so an editor writes a foreign mod's change as a patch of its own mod instead
    /// of a copy of the whole entity. Guarantee: <c>Apply(base, Diff(base, edited))</c> equals
    /// <c>edited</c> by <see cref="JsonSemantic"/> (tested over all entities of all mods).
    /// <list type="bullet">
    /// <item>an unchanged property is left out; a removed one becomes <c>null</c>;</item>
    /// <item>nested objects are diffed property by property;</item>
    /// <item>an array with a declared key (<see cref="MergeTypeMap"/>) becomes <c>Key$remove</c>
    /// (by key) and <c>Key$add</c> (only the changed fields of matched elements, new elements
    /// whole);</item>
    /// <item>an array without a key that only grew at the end becomes <c>Key$add</c>, one that only
    /// lost values becomes <c>Key$remove</c>;</item>
    /// <item>anything else is replaced whole. Every array candidate is checked by applying it, so a
    /// reorder never produces a wrong patch, only a bigger one.</item>
    /// <item>a stored layer (<see cref="PatchLayerAttribute"/>) is diffed so that composing the patch with it
    /// gives the edited layer; its operations (<c>Parts$add</c> inside a variant) are opaque — replaced,
    /// never diffed — and when composition cannot reproduce the edit, a layer property or dictionary
    /// entry is replaced whole with <c>K$set</c>. Extension-data layers (a placement's per-instance
    /// fields) have no outer value to replace: such an edit throws <see cref="JsonPatchException"/>.</item>
    /// </list>
    /// </summary>
    public static class JsonPatchDiff
    {
        /// <summary>
        /// The patch from <paramref name="baseObject"/> to <paramref name="edited"/>; empty when they are
        /// equal. <paramref name="type"/> is the DTO both describe (may be null; it supplies array keys).
        /// </summary>
        public static JsonObject Diff(JsonObject baseObject, JsonObject edited, Type type)
        {
            if (baseObject == null)
                throw new ArgumentNullException(nameof(baseObject));
            if (edited == null)
                throw new ArgumentNullException(nameof(edited));

            var patch = new JsonObject();
            var extensionLayer = MergeTypeMap.ExtensionLayerOf(type);
            if (extensionLayer != null)
                DiffExtensionLayer(baseObject, edited, type, extensionLayer, patch);

            foreach (var pair in baseObject)
            {
                if (IsLayerKey(type, extensionLayer, pair.Key))
                    continue;
                if (!IsNull(pair.Value) && IsNull(Find(edited, pair.Key)))
                    patch[pair.Key] = null;
            }

            foreach (var pair in edited)
            {
                if (IsLayerKey(type, extensionLayer, pair.Key) || IsNull(pair.Value))
                    continue;
                var before = Find(baseObject, pair.Key);
                var layer = MergeTypeMap.LayerOf(type, pair.Key, out var dictionary);
                if (layer != null && !layer.Wholesale && pair.Value is JsonObject editedLayer && (IsNull(before) || before is JsonObject))
                {
                    var beforeLayer = before as JsonObject ?? new JsonObject();
                    if (StrictEqual(beforeLayer, editedLayer) && !IsNull(before))
                        continue;
                    var layerPatch = dictionary ? DiffLayerDictionary(beforeLayer, editedLayer, layer) : DiffLayer(beforeLayer, editedLayer, layer);
                    if (layerPatch == null)
                        patch[pair.Key + "$set"] = editedLayer.DeepClone();
                    else if (layerPatch.Count > 0 || IsNull(before))
                        patch[pair.Key] = layerPatch;
                    continue;
                }
                if (IsNull(before))
                {
                    patch[pair.Key] = pair.Value.DeepClone();
                    continue;
                }
                // Semantically equal containers may still differ inside a layer (null against missing).
                if (JsonSemantic.Equal(before, pair.Value) && (StrictEqual(before, pair.Value) || before is not (JsonObject or JsonArray)))
                    continue;

                if (before is JsonObject beforeObject && pair.Value is JsonObject editedObject)
                {
                    var nested = Diff(beforeObject, editedObject, MergeTypeMap.ChildType(type, pair.Key));
                    if (nested.Count > 0)
                        patch[pair.Key] = nested;
                    continue;
                }

                if (before is JsonArray beforeArray && pair.Value is JsonArray editedArray)
                {
                    DiffArray(patch, pair.Key, beforeArray, editedArray, type);
                    continue;
                }

                patch[pair.Key] = pair.Value.DeepClone();
            }
            return patch;
        }

        private static bool IsLayerKey(Type type, PatchLayerAttribute extensionLayer, string key)
        {
            if (extensionLayer == null)
                return false;
            var dollar = key.IndexOf('$');
            return !MergeTypeMap.IsDeclared(type, dollar > 0 ? key.Substring(0, dollar) : key);
        }

        /// <summary>
        /// The undeclared properties of an object whose extension data is a layer (a placement's
        /// per-instance fields, a state's components) — compared as a layer, not as plain data.
        /// </summary>
        private static void DiffExtensionLayer(JsonObject baseObject, JsonObject edited, Type type, PatchLayerAttribute layer, JsonObject patch)
        {
            var beforeLayer = LayerPart(baseObject, type, layer);
            var editedLayer = LayerPart(edited, type, layer);
            if (StrictEqual(beforeLayer, editedLayer))
                return;

            if (layer.Wholesale)
            {
                DiffWholesale(beforeLayer, editedLayer, layer.Target, patch);
                return;
            }

            var layerPatch = DiffLayer(beforeLayer, editedLayer, layer);
            if (layerPatch == null)
                throw new JsonPatchException(string.Join(", ", ChangedNames(beforeLayer, editedLayer)),
                    "this change of the stored per-instance fields cannot be written as a patch (a patch cannot drop a field the layer sets, nor take back a list operation); change the defining file");
            foreach (var pair in layerPatch)
                patch[pair.Key] = pair.Value?.DeepClone();
        }

        private static JsonObject LayerPart(JsonObject value, Type type, PatchLayerAttribute layer)
        {
            var result = new JsonObject();
            foreach (var pair in value)
            {
                if (IsLayerKey(type, layer, pair.Key))
                    result[pair.Key] = pair.Value?.DeepClone();
            }
            return result;
        }

        /// <summary>
        /// A wholesale layer is data: a dropped value is removed, an explicit <c>null</c> (the layer removes
        /// the value when used) is written as <c>K$set: null</c>, a new value whole, a changed one by the usual diff.
        /// </summary>
        private static void DiffWholesale(JsonObject before, JsonObject edited, Type target, JsonObject patch)
        {
            foreach (var pair in before)
            {
                if (!Has(edited, pair.Key))
                    patch[pair.Key] = null;
            }
            foreach (var pair in edited)
            {
                var found = Has(before, pair.Key);
                var old = Find(before, pair.Key);
                if (found && StrictEqual(old, pair.Value))
                    continue;
                if (IsNull(pair.Value))
                    patch[pair.Key + "$set"] = null;
                else if (found && old is JsonObject oldObject && pair.Value is JsonObject editedObject)
                {
                    var nested = Diff(oldObject, editedObject, MergeTypeMap.ChildType(target, pair.Key));
                    if (nested.Count > 0)
                        patch[pair.Key] = nested;
                }
                else if (found && old is JsonArray oldArray && pair.Value is JsonArray editedArray)
                    DiffArray(patch, pair.Key, oldArray, editedArray, target);
                else
                    patch[pair.Key] = pair.Value.DeepClone();
            }
        }

        private static JsonObject DiffLayerDictionary(JsonObject before, JsonObject edited, PatchLayerAttribute layer)
        {
            var patch = new JsonObject();
            foreach (var pair in before)
            {
                if (!Has(edited, pair.Key))
                    patch[pair.Key] = null;
            }
            foreach (var pair in edited)
            {
                var found = Has(before, pair.Key);
                var old = Find(before, pair.Key);
                if (found && StrictEqual(old, pair.Value))
                    continue;
                if (pair.Value is JsonObject editedEntry && (!found || old is JsonObject))
                {
                    var entryPatch = DiffLayer(old as JsonObject ?? new JsonObject(), editedEntry, layer);
                    if (entryPatch != null)
                    {
                        patch[pair.Key] = entryPatch;
                        continue;
                    }
                }
                patch[pair.Key + "$set"] = pair.Value?.DeepClone();
            }
            return patch;
        }

        /// <summary>
        /// The patch that composes <paramref name="before"/> into <paramref name="edited"/> (see
        /// <see cref="PatchLayerComposer"/>), or null when composition cannot reproduce it — a dropped entry,
        /// a changed list operation. Operations of the layer are opaque: they are replaced, never diffed.
        /// </summary>
        private static JsonObject DiffLayer(JsonObject before, JsonObject edited, PatchLayerAttribute layer)
        {
            var patch = new JsonObject();
            foreach (var name in Names(before).Concat(Names(edited)).Distinct(StringComparer.OrdinalIgnoreCase).ToList())
            {
                var old = Entries(before, name);
                var now = Entries(edited, name);
                if (EntriesEqual(old, now))
                    continue;
                if (now.Count == 0)
                    return null;
                if (now.Count == 1 && IsReplacement(now[0]))
                {
                    patch[now[0].Key] = now[0].Value?.DeepClone();
                    continue;
                }
                if (now.Count == 1 && old.Count == 1 && now[0].Value is JsonObject nowObject && old[0].Value is JsonObject oldObject
                    && !now[0].Key.Contains('$') && !old[0].Key.Contains('$'))
                {
                    var nested = DiffLayer(oldObject, nowObject, new PatchLayerAttribute(MergeTypeMap.ChildType(layer.Target, name)));
                    if (nested == null)
                        return null;
                    patch[now[0].Key] = nested;
                    continue;
                }
                if (old.Count > 0 && !GrowOperations(patch, name, old, now))
                    return null;
                if (old.Count == 0)
                {
                    foreach (var entry in now)
                        patch[entry.Key] = entry.Value?.DeepClone();
                }
            }

            var probe = (JsonObject)before.DeepClone();
            try
            {
                PatchLayerComposer.Compose(probe, (JsonObject)patch.DeepClone(), layer, string.Empty);
            }
            catch (JsonPatchException)
            {
                return null;
            }
            return StrictEqual(probe, edited) ? patch : null;
        }

        /// <summary>
        /// List operations of a layer that only grew: the new removals and the additions not already
        /// there (a keyed addition with more fields composes into the old one). The caller verifies.
        /// </summary>
        private static bool GrowOperations(JsonObject patch, string name, List<KeyValuePair<string, JsonNode>> old, List<KeyValuePair<string, JsonNode>> now)
        {
            foreach (var entry in old.Concat(now))
            {
                var dollar = entry.Key.IndexOf('$');
                var operation = dollar > 0 ? entry.Key.Substring(dollar) : null;
                if ((operation != "$add" && operation != "$remove") || entry.Value is not JsonArray)
                    return false;
            }
            foreach (var entry in now)
            {
                var operation = entry.Key.Substring(entry.Key.IndexOf('$'));
                var before = old.FirstOrDefault(o => o.Key.EndsWith(operation, StringComparison.Ordinal)).Value as JsonArray;
                var grown = new JsonArray();
                foreach (var item in entry.Value.AsArray())
                {
                    if (before == null || !before.Any(b => StrictEqual(b, item)))
                        grown.Add(item?.DeepClone());
                }
                if (grown.Count > 0)
                    patch[name + operation] = grown;
            }
            return true;
        }

        private static IEnumerable<string> Names(JsonObject layer)
        {
            foreach (var pair in layer)
            {
                var dollar = pair.Key.IndexOf('$');
                if (dollar != 0)
                    yield return dollar > 0 ? pair.Key.Substring(0, dollar) : pair.Key;
            }
        }

        private static List<KeyValuePair<string, JsonNode>> Entries(JsonObject layer, string name)
            => layer.Where(pair => pair.Key.StartsWith(name, StringComparison.OrdinalIgnoreCase)
                    && (pair.Key.Length == name.Length || pair.Key[name.Length] == '$'))
                .ToList();

        private static bool EntriesEqual(List<KeyValuePair<string, JsonNode>> a, List<KeyValuePair<string, JsonNode>> b)
        {
            if (a.Count != b.Count)
                return false;
            for (var i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i].Key, b[i].Key, StringComparison.OrdinalIgnoreCase) || !StrictEqual(a[i].Value, b[i].Value))
                    return false;
            }
            return true;
        }

        private static bool IsReplacement(KeyValuePair<string, JsonNode> entry)
        {
            var dollar = entry.Key.IndexOf('$');
            if (dollar > 0)
                return string.Equals(entry.Key.Substring(dollar), "$set", StringComparison.Ordinal);
            return entry.Value is not JsonObject;
        }

        private static IEnumerable<string> ChangedNames(JsonObject before, JsonObject edited)
            => Names(before).Concat(Names(edited)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(name => !EntriesEqual(Entries(before, name), Entries(edited, name)));

        /// <summary>
        /// Equality where a property holding <c>null</c> differs from a missing one — inside a layer
        /// <c>null</c> means "remove", absence means "leave as it is". Values otherwise compare by <see cref="JsonSemantic"/>.
        /// </summary>
        private static bool StrictEqual(JsonNode a, JsonNode b)
        {
            if (a is JsonObject oa && b is JsonObject ob)
            {
                if (oa.Count != ob.Count)
                    return false;
                foreach (var pair in oa)
                {
                    if (!Has(ob, pair.Key) || !StrictEqual(pair.Value, Find(ob, pair.Key)))
                        return false;
                }
                return true;
            }
            if (a is JsonArray aa && b is JsonArray ab)
            {
                if (aa.Count != ab.Count)
                    return false;
                for (var i = 0; i < aa.Count; i++)
                {
                    if (!StrictEqual(aa[i], ab[i]))
                        return false;
                }
                return true;
            }
            if (a is JsonObject || b is JsonObject || a is JsonArray || b is JsonArray)
                return false;
            return JsonSemantic.Equal(a, b);
        }

        private static bool Has(JsonObject obj, string name)
            => obj.Any(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase));

        private static void DiffArray(JsonObject patch, string name, JsonArray before, JsonArray edited, Type type)
        {
            var key = MergeTypeMap.KeyOf(type, name);
            var candidates = new List<JsonObject>();
            if (key != null)
                candidates.Add(KeyedCandidate(name, before, edited, key, MergeTypeMap.ItemType(MergeTypeMap.ChildType(type, name))));
            candidates.Add(AppendCandidate(name, before, edited));
            candidates.Add(RemoveCandidate(name, before, edited));

            foreach (var candidate in candidates)
            {
                if (candidate == null || !Reproduces(name, before, edited, candidate, type))
                    continue;
                foreach (var operation in candidate)
                    patch[operation.Key] = operation.Value?.DeepClone();
                return;
            }
            patch[name] = edited.DeepClone();
        }

        private static JsonObject KeyedCandidate(string name, JsonArray before, JsonArray edited, string[] key, Type itemType)
        {
            if (!TryKeys(before, key, out var beforeKeys) || !TryKeys(edited, key, out var editedKeys))
                return null;

            var removed = new JsonArray();
            for (var i = 0; i < before.Count; i++)
            {
                if (!editedKeys.Any(k => KeysEqual(k, beforeKeys[i])))
                    removed.Add(key.Length == 1 ? beforeKeys[i][0]?.DeepClone() : KeyObject(key, beforeKeys[i]));
            }

            var added = new JsonArray();
            for (var i = 0; i < edited.Count; i++)
            {
                var match = Array.FindIndex(beforeKeys, k => KeysEqual(k, editedKeys[i]));
                if (match < 0)
                {
                    added.Add(edited[i].DeepClone());
                    continue;
                }
                var change = Diff((JsonObject)before[match], (JsonObject)edited[i], itemType);
                if (change.Count == 0)
                    continue;
                var element = KeyObject(key, editedKeys[i]);
                foreach (var field in change)
                {
                    if (!key.Any(k => string.Equals(k, field.Key, StringComparison.OrdinalIgnoreCase)))
                        element[field.Key] = field.Value?.DeepClone();
                }
                added.Add(element);
            }

            var result = new JsonObject();
            if (removed.Count > 0)
                result[name + "$remove"] = removed;
            if (added.Count > 0)
                result[name + "$add"] = added;
            return result;
        }

        private static JsonObject AppendCandidate(string name, JsonArray before, JsonArray edited)
        {
            if (edited.Count <= before.Count)
                return null;
            for (var i = 0; i < before.Count; i++)
            {
                if (!JsonSemantic.Equal(before[i], edited[i]))
                    return null;
            }
            var tail = new JsonArray();
            for (var i = before.Count; i < edited.Count; i++)
                tail.Add(edited[i]?.DeepClone());
            return new JsonObject { [name + "$add"] = tail };
        }

        private static JsonObject RemoveCandidate(string name, JsonArray before, JsonArray edited)
        {
            if (edited.Count >= before.Count)
                return null;
            var removed = new JsonArray();
            foreach (var element in before)
            {
                if (!edited.Any(e => JsonSemantic.Equal(e, element)) && !removed.Any(r => JsonSemantic.Equal(r, element)))
                    removed.Add(element?.DeepClone());
            }
            return removed.Count == 0 ? null : new JsonObject { [name + "$remove"] = removed };
        }

        private static bool Reproduces(string name, JsonArray before, JsonArray edited, JsonObject candidate, Type type)
        {
            var probe = new JsonObject { [name] = before.DeepClone() };
            try
            {
                JsonPatchMerger.Apply(probe, (JsonObject)candidate.DeepClone(), type);
            }
            catch (JsonPatchException)
            {
                return false;
            }
            return JsonSemantic.Equal(probe[name], edited);
        }

        private static bool TryKeys(JsonArray array, string[] key, out JsonNode[][] keys)
        {
            keys = new JsonNode[array.Count][];
            for (var i = 0; i < array.Count; i++)
            {
                if (array[i] is not JsonObject element)
                    return false;
                keys[i] = new JsonNode[key.Length];
                for (var k = 0; k < key.Length; k++)
                {
                    var value = Find(element, key[k]);
                    if (value == null)
                        return false;
                    keys[i][k] = value;
                }
                for (var j = 0; j < i; j++)
                {
                    if (KeysEqual(keys[j], keys[i]))
                        return false;
                }
            }
            return true;
        }

        private static JsonObject KeyObject(string[] key, JsonNode[] values)
        {
            var result = new JsonObject();
            for (var i = 0; i < key.Length; i++)
                result[key[i]] = values[i]?.DeepClone();
            return result;
        }

        private static bool KeysEqual(JsonNode[] a, JsonNode[] b)
        {
            for (var i = 0; i < a.Length; i++)
            {
                if (!JsonSemantic.Equal(a[i], b[i], ignoreStringCase: true))
                    return false;
            }
            return true;
        }

        private static JsonNode Find(JsonObject obj, string name)
        {
            foreach (var pair in obj)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }
            return null;
        }

        private static bool IsNull(JsonNode node)
            => node == null || (node is JsonValue && node.GetValueKind() == JsonValueKind.Null);
    }
}
