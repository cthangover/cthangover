namespace Cthangover.Core.Mods.Patches
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;

    /// <summary>
    /// Flattens a JSON tree into its leaves (scalars, nulls, empty objects and arrays) keyed by a
    /// path: <c>Spawn.X</c>, <c>Commands[2]</c>, and for an array with a declared key (see
    /// <see cref="MergeTypeMap"/>) the key instead of the index — <c>Parts[Id="roof"].Image</c> — so
    /// an element keeps its path when others are added or removed. Paths compare case-insensitively,
    /// like entity JSON is read. Keys with a <c>$</c> operation are skipped (they are not data), except
    /// inside a stored layer (<see cref="PatchLayerAttribute"/>): there an operation is what the layer
    /// holds, so <c>Variants.winter.Parts$add</c> is one opaque leaf with the whole list as its value.
    /// Used by <c>EntityResolver.Explain</c> to tell which layer set each field.
    /// </summary>
    public static class JsonLeafPaths
    {
        /// <summary>Every leaf of <paramref name="node"/> by path; <paramref name="type"/> (may be null) supplies array keys.</summary>
        public static Dictionary<string, JsonNode> Collect(JsonNode node, Type type)
        {
            var result = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);
            Walk(node, type, null, string.Empty, false, result);
            return result;
        }

        private static void Walk(JsonNode node, Type type, string[] key, string path, bool inLayer, Dictionary<string, JsonNode> result)
        {
            switch (node)
            {
                case JsonObject obj when obj.Count > 0:
                    var extensionLayer = inLayer ? null : MergeTypeMap.ExtensionLayerOf(type);
                    foreach (var pair in obj)
                    {
                        var childPath = path.Length == 0 ? pair.Key : path + "." + pair.Key;
                        var dollar = pair.Key.IndexOf('$');
                        var name = dollar > 0 ? pair.Key.Substring(0, dollar) : pair.Key;
                        var layerKey = extensionLayer != null && !MergeTypeMap.IsDeclared(type, name);
                        if (dollar >= 0)
                        {
                            if (inLayer || layerKey)
                                result[childPath] = pair.Value;
                            continue;
                        }
                        if (layerKey)
                        {
                            Walk(pair.Value, MergeTypeMap.ChildType(extensionLayer.Target, name), MergeTypeMap.KeyOf(extensionLayer.Target, name), childPath, true, result);
                            continue;
                        }
                        var dictionary = false;
                        var layer = inLayer ? null : MergeTypeMap.LayerOf(type, name, out dictionary);
                        if (layer != null && dictionary && pair.Value is JsonObject entries && entries.Count > 0)
                        {
                            foreach (var entry in entries)
                                Walk(entry.Value, layer.Target, null, childPath + "." + entry.Key, true, result);
                        }
                        else if (layer != null)
                            Walk(pair.Value, layer.Target, null, childPath, true, result);
                        else
                            Walk(pair.Value, MergeTypeMap.ChildType(type, name), MergeTypeMap.KeyOf(type, name), childPath, inLayer, result);
                    }
                    break;
                case JsonArray array when array.Count > 0:
                    var itemType = MergeTypeMap.ItemType(type);
                    for (var i = 0; i < array.Count; i++)
                        Walk(array[i], itemType, null, path + "[" + ElementName(array[i], key, i) + "]", inLayer, result);
                    break;
                default:
                    result[path] = node;
                    break;
            }
        }

        private static string ElementName(JsonNode element, string[] key, int index)
        {
            if (key == null || element is not JsonObject obj)
                return index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var parts = new List<string>();
            foreach (var name in key)
            {
                var value = obj.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
                if (value == null)
                    return index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                parts.Add(name + "=" + value.ToJsonString());
            }
            return string.Join(",", parts);
        }
    }
}
