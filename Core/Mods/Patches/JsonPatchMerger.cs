namespace Cthangover.Core.Mods.Patches
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Utils;

    /// <summary>
    /// The one deep-merge engine of mod data (patch engine v2): <c>patches/</c>, and later
    /// <c>Extends</c>, variants and placements use the same rules, so an author learns them
    /// once. It works on <see cref="JsonNode"/> trees, never on JSON text.
    /// <list type="bullet">
    /// <item>an object merges recursively into the target object (a missing target
    /// becomes an empty object first);</item>
    /// <item>a scalar or an array without an operation replaces the value;</item>
    /// <item><c>null</c> removes the property (the reader then sees its default);</item>
    /// <item><c>Key$set</c> replaces wholesale (also an object, without merging);</item>
    /// <item><c>Key$add</c> / <c>Key$remove</c> work on arrays at any depth.</item>
    /// </list>
    /// Array identity comes only from declared sources — <see cref="MergeKeyAttribute"/> on the
    /// property, <see cref="IMergeable"/> on the element type (found by walking the DTO type,
    /// see <see cref="MergeTypeMap"/>) or the explicit value form
    /// <c>{"$key": "Condition" | ["Layer","X","Y"], "$items": [...]}</c>. Nothing is guessed from
    /// property names. With a key, <c>$add</c> deep-merges an element into the existing one with
    /// an equal key (strings case-insensitive) or appends it, and <c>$remove</c> removes by key
    /// (an element may be given as the bare key value). Without a key, <c>$add</c> appends and
    /// <c>$remove</c> removes elements equal by value (<see cref="JsonSemantic"/>).
    /// <para>
    /// A property marked <see cref="PatchLayerAttribute"/> (a stored variant, a placement's per-instance
    /// fields, a state's components) holds a layer applied later, not data: patching it composes the patch
    /// with the layer (<see cref="PatchLayerComposer"/>) instead of running the operations on it, so
    /// <c>Interact.Commands$add</c> on a placement still adds to the object's commands when used.
    /// </para>
    /// <para>
    /// Property names match case-insensitively, as entity JSON is read; a replaced property
    /// keeps the target's spelling. A patch that cannot apply throws
    /// <see cref="JsonPatchException"/> with the path inside the entity; the caller decides
    /// to skip it (the target may then be partially changed, so callers apply onto a copy).
    /// </para>
    /// </summary>
    public static class JsonPatchMerger
    {
        private const string KeyProperty = "$key";
        private const string ItemsProperty = "$items";

        /// <summary>
        /// Applies one patch object to <paramref name="target"/> in place. <paramref name="targetType"/>
        /// is the DTO the JSON describes (may be null): it supplies merge keys at every depth.
        /// Throws <see cref="JsonPatchException"/> on a patch that cannot apply.
        /// </summary>
        public static void Apply(JsonObject target, JsonObject patch, Type targetType)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (patch != null)
                MergeObject(target, patch, targetType, string.Empty);
        }

        /// <summary>
        /// Applies the wildcard patches (<c>*</c>) and then those addressed to <paramref name="entityId"/>
        /// to <paramref name="entityJson"/> and returns the merged JSON. A failing patch throws; per-patch
        /// isolation is the resolver's job.
        /// </summary>
        public static string Merge(string entityJson, string entityId, Dictionary<string, List<PropertyPatch>> allPatches, Type targetType = null)
        {
            if (allPatches == null || allPatches.Count == 0)
                return entityJson;

            var root = ParseObject(entityJson);
            foreach (var id in new[] { "*", entityId })
            {
                if (id == null || !allPatches.TryGetValue(id, out var patches))
                    continue;
                foreach (var patch in patches)
                    Apply(root, ToNode(patch), targetType);
            }
            return root.ToJsonString();
        }

        /// <summary>Parses entity JSON (comments allowed) into an object node.</summary>
        public static JsonObject ParseObject(string json)
        {
            var node = JsonNode.Parse(json, null, JsonOptionsProvider.DocumentOptions);
            return node as JsonObject ?? throw new JsonException("entity JSON is not an object");
        }

        /// <summary>
        /// The patch's operations as a JSON object: every key except <c>Id</c> (which addresses the entity).
        /// </summary>
        public static JsonObject ToNode(PropertyPatch patch)
        {
            var result = new JsonObject();
            if (patch?.ExtraProperties == null)
                return result;
            foreach (var pair in patch.ExtraProperties)
                result[pair.Key] = ToNode(pair.Value);
            return result;
        }

        private static JsonNode ToNode(object value)
        {
            switch (value)
            {
                case null:
                    return null;
                case JsonNode node:
                    return node.DeepClone();
                case JsonElement element:
                    return element.ValueKind == JsonValueKind.Null ? null : JsonNode.Parse(element.GetRawText());
                default:
                    return JsonSerializer.SerializeToNode(value);
            }
        }

        internal static void MergeObject(JsonObject target, JsonObject patch, Type type, string path)
        {
            var extensionLayer = MergeTypeMap.ExtensionLayerOf(type);
            foreach (var pair in patch.ToList())
            {
                SplitOperation(pair.Key, path, out var name, out var operation);
                var childPath = path.Length == 0 ? name : path + "." + name;
                if (extensionLayer != null && !MergeTypeMap.IsDeclared(type, name))
                    PatchLayerComposer.ComposeEntry(target, name, operation, pair.Value, extensionLayer, childPath);
                else
                    ApplyEntry(target, name, operation, pair.Value, type, childPath);
            }
        }

        /// <summary>One patch entry <c>name</c>+<c>operation</c> applied to <paramref name="target"/> as plain data.</summary>
        internal static void ApplyEntry(JsonObject target, string name, string operation, JsonNode value, Type type, string path)
        {
            switch (operation)
            {
                case null:
                    MergeProperty(target, name, value, type, path);
                    break;
                case "$set":
                    Set(target, name, value?.DeepClone());
                    break;
                case "$add":
                    AddItems(target, name, value, type, path + operation);
                    break;
                case "$remove":
                    RemoveItems(target, name, value, type, path + operation);
                    break;
                default:
                    throw new JsonPatchException(path + operation,
                        $"unknown operation '{operation}' (known: $add, $remove, $set)");
            }
        }

        internal static void SplitOperation(string key, string path, out string name, out string operation)
        {
            var index = key.IndexOf('$');
            if (index < 0)
            {
                name = key;
                operation = null;
                return;
            }
            if (index == 0)
                throw new JsonPatchException(path.Length == 0 ? key : path + "." + key,
                    $"'{key}' is not a property (operations are written as Name$add, Name$remove, Name$set)");
            name = key.Substring(0, index);
            operation = key.Substring(index);
        }

        internal static void MergeProperty(JsonObject target, string name, JsonNode value, Type type, string path)
        {
            if (IsNull(value))
            {
                Remove(target, name);
                return;
            }

            if (value is JsonObject patchObject)
            {
                if (Get(target, name) is not JsonObject existing)
                {
                    existing = new JsonObject();
                    Set(target, name, existing);
                }
                var layer = MergeTypeMap.LayerOf(type, name, out var dictionary);
                if (layer == null)
                    MergeObject(existing, patchObject, MergeTypeMap.ChildType(type, name), path);
                else if (dictionary)
                    MergeLayerDictionary(existing, patchObject, layer, path);
                else
                    PatchLayerComposer.Compose(existing, patchObject, layer, path);
                return;
            }

            Set(target, name, value.DeepClone());
        }

        /// <summary>
        /// A patch of a <c>string → layer</c> dictionary: an entry given as an object is composed with the
        /// stored layer of that name (an empty one when new); <c>null</c>, plain values and <c>name$set</c>
        /// replace the entry as written.
        /// </summary>
        private static void MergeLayerDictionary(JsonObject dictionary, JsonObject patch, PatchLayerAttribute layer, string path)
        {
            foreach (var pair in patch.ToList())
            {
                SplitOperation(pair.Key, path, out var name, out var operation);
                var childPath = path.Length == 0 ? name : path + "." + name;
                if (operation == null && pair.Value is JsonObject entryPatch)
                {
                    if (Get(dictionary, name) is not JsonObject entry)
                    {
                        entry = new JsonObject();
                        Set(dictionary, name, entry);
                    }
                    PatchLayerComposer.Compose(entry, entryPatch, layer, childPath);
                }
                else
                    ApplyEntry(dictionary, name, operation, pair.Value, null, childPath);
            }
        }

        internal static bool IsNull(JsonNode node)
            => node == null || (node is JsonValue && node.GetValueKind() == JsonValueKind.Null);

        internal static void AddItems(JsonObject target, string name, JsonNode value, Type type, string path)
        {
            var items = ReadItems(value, path, out var explicitKey);
            var key = explicitKey ?? MergeTypeMap.KeyOf(type, name);
            var itemType = MergeTypeMap.ItemType(MergeTypeMap.ChildType(type, name));
            var array = ExistingArray(target, name, path, create: true);

            foreach (var item in items)
            {
                if (key != null && item is JsonObject keyed && TryKey(keyed, key, out var keyValues))
                {
                    var match = FindByKey(array, key, keyValues);
                    if (match != null)
                    {
                        MergeObject(match, keyed, itemType, ElementPath(path, key, keyValues));
                        continue;
                    }
                }

                if (item is JsonObject itemObject)
                {
                    var fresh = new JsonObject();
                    MergeObject(fresh, itemObject, itemType, path + "[" + array.Count.ToString(CultureInfo.InvariantCulture) + "]");
                    array.Add(fresh);
                }
                else
                    array.Add(item?.DeepClone());
            }
        }

        internal static void RemoveItems(JsonObject target, string name, JsonNode value, Type type, string path)
        {
            var items = ReadItems(value, path, out var explicitKey);
            var key = explicitKey ?? MergeTypeMap.KeyOf(type, name);
            var array = ExistingArray(target, name, path, create: false);
            if (array == null)
                return;

            foreach (var item in items)
            {
                if (key == null)
                {
                    for (var i = array.Count - 1; i >= 0; i--)
                    {
                        if (JsonSemantic.Equal(array[i], item))
                            array.RemoveAt(i);
                    }
                    continue;
                }

                JsonNode[] keyValues;
                if (item is JsonObject keyed)
                {
                    if (!TryKey(keyed, key, out keyValues))
                        throw new JsonPatchException(path, $"an element to remove has no key {string.Join("+", key)}");
                }
                else if (key.Length == 1)
                    keyValues = new[] { item };
                else
                    throw new JsonPatchException(path, $"the key {string.Join("+", key)} has several parts, give the element as an object");

                for (var i = array.Count - 1; i >= 0; i--)
                {
                    if (array[i] is JsonObject candidate && TryKey(candidate, key, out var candidateValues)
                        && KeysEqual(candidateValues, keyValues))
                        array.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// The elements of an <c>$add</c>/<c>$remove</c> value: a plain array, or the explicit form
        /// <c>{"$key": name | [names], "$items": [...]}</c> which also yields the key.
        /// </summary>
        internal static List<JsonNode> ReadItems(JsonNode value, string path, out string[] explicitKey)
        {
            explicitKey = null;
            if (value is JsonArray array)
                return array.ToList();

            if (value is JsonObject form)
            {
                JsonNode keyNode = null;
                JsonNode itemsNode = null;
                foreach (var pair in form)
                {
                    if (string.Equals(pair.Key, KeyProperty, StringComparison.OrdinalIgnoreCase))
                        keyNode = pair.Value;
                    else if (string.Equals(pair.Key, ItemsProperty, StringComparison.OrdinalIgnoreCase))
                        itemsNode = pair.Value;
                    else
                        throw new JsonPatchException(path, $"unexpected '{pair.Key}' in the explicit form (only $key and $items)");
                }

                if (itemsNode is not JsonArray items)
                    throw new JsonPatchException(path, "the explicit form needs \"$items\": [...]");
                explicitKey = ReadKey(keyNode, path);
                return items.ToList();
            }

            throw new JsonPatchException(path, $"needs an array or {{\"$key\": ..., \"$items\": [...]}}, got {Describe(value)}");
        }

        private static string[] ReadKey(JsonNode node, string path)
        {
            if (node is JsonValue && node.GetValueKind() == JsonValueKind.String && !string.IsNullOrEmpty(node.GetValue<string>()))
                return new[] { node.GetValue<string>() };
            if (node is JsonArray names && names.Count > 0
                && names.All(n => n is JsonValue && n.GetValueKind() == JsonValueKind.String && !string.IsNullOrEmpty(n.GetValue<string>())))
                return names.Select(n => n.GetValue<string>()).ToArray();
            throw new JsonPatchException(path, "\"$key\" must be a property name or a list of names");
        }

        private static JsonArray ExistingArray(JsonObject target, string name, string path, bool create)
        {
            var existing = Get(target, name);
            if (existing is JsonArray array)
                return array;
            if (existing == null || (existing is JsonValue && existing.GetValueKind() == JsonValueKind.Null))
            {
                if (!create)
                    return null;
                array = new JsonArray();
                Set(target, name, array);
                return array;
            }
            throw new JsonPatchException(path, $"the value is {Describe(existing)}, not an array");
        }

        internal static JsonObject FindByKey(JsonArray array, string[] key, JsonNode[] keyValues)
        {
            foreach (var element in array)
            {
                if (element is JsonObject candidate && TryKey(candidate, key, out var values) && KeysEqual(values, keyValues))
                    return candidate;
            }
            return null;
        }

        internal static bool TryKey(JsonObject element, string[] key, out JsonNode[] values)
        {
            values = new JsonNode[key.Length];
            for (var i = 0; i < key.Length; i++)
            {
                if (!TryGet(element, key[i], out var value))
                    return false;
                values[i] = value;
            }
            return true;
        }

        internal static bool KeysEqual(JsonNode[] a, JsonNode[] b)
        {
            for (var i = 0; i < a.Length; i++)
            {
                if (!JsonSemantic.Equal(a[i], b[i], ignoreStringCase: true))
                    return false;
            }
            return true;
        }

        private static string ElementPath(string path, string[] key, JsonNode[] values)
        {
            var parts = key.Select((name, i) => name + "=" + (values[i]?.ToJsonString() ?? "null"));
            return path + "[" + string.Join(",", parts) + "]";
        }

        internal static string Describe(JsonNode node)
        {
            switch (node)
            {
                case null:
                    return "null";
                case JsonObject:
                    return "an object";
                case JsonArray:
                    return "an array";
                default:
                    return "a " + node.GetValueKind().ToString().ToLowerInvariant();
            }
        }

        internal static JsonNode Get(JsonObject target, string name)
            => TryGet(target, name, out var value) ? value : null;

        internal static bool TryGet(JsonObject target, string name, out JsonNode value)
        {
            foreach (var pair in target)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }
            value = null;
            return false;
        }

        internal static string ExistingName(JsonObject target, string name)
        {
            foreach (var pair in target)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                    return pair.Key;
            }
            return null;
        }

        internal static void Set(JsonObject target, string name, JsonNode value)
        {
            target[ExistingName(target, name) ?? name] = value;
        }

        internal static void Remove(JsonObject target, string name)
        {
            var existing = ExistingName(target, name);
            if (existing != null)
                target.Remove(existing);
        }
    }
}
