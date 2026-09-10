using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Patches
{
    /// <summary>
    /// Merges mod patches into a raw entity JSON string before
    /// deserialisation. Operates purely at the JSON-text level.
    ///
    /// <para>
    /// <b>Identity-key detection for object arrays:</b>
    /// 1. Hardcoded fields: properties whose item type implements
    ///    <see cref="IMergeable"/> contribute their
    ///    <c>MergeKeyProperty</c> via <see cref="BuildMergeKeys{T}"/>.
    /// 2. Extra-property fields (mod-defined): identity key is detected
    ///    automatically from the first element of the incoming array:
    ///    <c>"Id"</c> or the first property ending in <c>"Id"</c>.
    /// 3. If nothing matches — raw-text concatenation is used.
    /// </para>
    /// </summary>
    public class JsonPatchMerger
    {
        /// <summary>
        /// Scans <typeparamref name="T"/> via reflection and returns a map
        /// from property name to identity-key for every list-typed
        /// property whose item type implements <see cref="IMergeable"/>.
        /// </summary>
        public static Dictionary<string, string> BuildMergeKeys<T>() where T : class
        {
            var keys = new Dictionary<string, string>();
            foreach (var prop in typeof(T).GetProperties())
            {
                var itemType = GetListItemType(prop.PropertyType);
                if (itemType == null)
                    continue;
                if (!typeof(IMergeable).IsAssignableFrom(itemType))
                    continue;

                try
                {
                    var mergeable = (IMergeable)Activator.CreateInstance(itemType);
                    if (!string.IsNullOrEmpty(mergeable.MergeKeyProperty))
                        keys[prop.Name] = mergeable.MergeKeyProperty;
                }
                catch
                {
                    // type without parameterless ctor — skip
                }
            }
            return keys;
        }

        /// <summary>
        /// Applies all relevant patches for <paramref name="entityId"/>
        /// into <paramref name="entityJson"/> and returns the merged JSON.
        /// </summary>
        /// <param name="identityKeys">Optional per-property identity-key
        /// map (from <see cref="BuildMergeKeys{T}"/>). For keys not in
        /// this map, identity is auto-detected from the incoming array
        /// structure.</param>
        public string Merge(
            string entityJson,
            string entityId,
            Dictionary<string, List<PropertyPatch>> allPatches,
            Dictionary<string, string> identityKeys = null)
        {
            if (allPatches == null || allPatches.Count == 0)
                return entityJson;

            using var doc = JsonDocument.Parse(entityJson, JsonOptionsProvider.DocumentOptions);
            var root = doc.RootElement.Clone();

            if (allPatches.TryGetValue("*", out var wildcardPatches))
            {
                foreach (var patch in wildcardPatches)
                    root = ApplyPatch(root, patch, identityKeys);
            }

            if (allPatches.TryGetValue(entityId, out var specificPatches))
            {
                foreach (var patch in specificPatches)
                    root = ApplyPatch(root, patch, identityKeys);
            }

            return root.GetRawText();
        }

        private JsonElement ApplyPatch(JsonElement target, PropertyPatch patch, Dictionary<string, string> identityKeys)
        {
            if (patch.ExtraProperties == null || patch.ExtraProperties.Count == 0)
                return target;

            using var stream = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                WritePatchedObject(writer, target, patch.ExtraProperties, identityKeys);
            }

            var doc = JsonDocument.Parse(stream.ToArray(), JsonOptionsProvider.DocumentOptions);
            return doc.RootElement.Clone();
        }

        private static void WritePatchedObject(
            Utf8JsonWriter writer,
            JsonElement source,
            Dictionary<string, object> patchProps,
            Dictionary<string, string> identityKeys)
        {
            writer.WriteStartObject();

            var patchedKeys = new HashSet<string>();
            var patchedValues = new Dictionary<string, string>();

            foreach (var kvp in patchProps)
            {
                if (!(kvp.Value is JsonElement incomingValue))
                    continue;

                ArrayMergeHelper.ParseArrayOperation(kvp.Key, out var baseKey, out var op);

                if (op == ArrayMergeHelper.ArrayOperation.Default
                    || incomingValue.ValueKind != JsonValueKind.Array)
                {
                    writer.WritePropertyName(baseKey);
                    incomingValue.WriteTo(writer);
                    patchedKeys.Add(baseKey);
                    patchedValues.Remove(baseKey);
                    continue;
                }

                var identityKey = ResolveIdentityKey(baseKey, incomingValue, source, identityKeys);

                var existingRaw = patchedValues.TryGetValue(baseKey, out var pv)
                    ? pv
                    : source.TryGetProperty(baseKey, out var ep)
                        && ep.ValueKind == JsonValueKind.Array
                        ? ep.GetRawText()
                        : null;

                var incomingRaw = incomingValue.GetRawText();

                string result = null;
                switch (op)
                {
                    case ArrayMergeHelper.ArrayOperation.Add:
                        result = ArrayMergeHelper.AddToArray(existingRaw, incomingRaw, identityKey);
                        break;
                    case ArrayMergeHelper.ArrayOperation.Remove:
                        result = ArrayMergeHelper.RemoveFromArray(existingRaw, incomingRaw, identityKey);
                        break;
                    case ArrayMergeHelper.ArrayOperation.Set:
                        result = incomingRaw;
                        break;
                }

                if (result != null)
                    patchedValues[baseKey] = result;

                patchedKeys.Add(baseKey);
            }

            foreach (var kv in patchedValues)
                WriteRawJsonProperty(writer, kv.Key, kv.Value);

            foreach (var prop in source.EnumerateObject())
            {
                if (!patchedKeys.Contains(prop.Name))
                {
                    writer.WritePropertyName(prop.Name);
                    prop.Value.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Resolves the identity key for a property: first checks
        /// <paramref name="identityKeys"/>, then auto-detects from the
        /// first element of <paramref name="incomingValue"/>.
        /// Returns null if no identity key can be determined.
        /// </summary>
        private static string ResolveIdentityKey(
            string propertyName,
            JsonElement incomingValue,
            JsonElement source,
            Dictionary<string, string> identityKeys)
        {
            if (identityKeys != null && identityKeys.TryGetValue(propertyName, out var key))
                return key;

            if (incomingValue.ValueKind == JsonValueKind.Array)
                return DetectIdentityKey(incomingValue);

            if (source.TryGetProperty(propertyName, out var existingArr)
                && existingArr.ValueKind == JsonValueKind.Array)
                return DetectIdentityKey(existingArr);

            return null;
        }

        /// <summary>
        /// Auto-detects the identity key from a JSON array by inspecting
        /// the first object element. Checks for <c>"Id"</c> first, then
        /// any property ending in <c>"Id"</c>. Returns null for
        /// non-object arrays.
        /// </summary>
        public static string DetectIdentityKey(JsonElement array)
        {
            if (array.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var el in array.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                    return null;

                if (el.TryGetProperty("Id", out _))
                    return "Id";

                foreach (var prop in el.EnumerateObject())
                {
                    if (prop.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                        return prop.Name;
                }

                return null;
            }

            return null;
        }

        private static Type GetListItemType(Type propertyType)
        {
            if (!propertyType.IsGenericType)
                return null;

            var genericDef = propertyType.GetGenericTypeDefinition();
            if (genericDef == typeof(List<>) || genericDef == typeof(IList<>)
                || genericDef == typeof(IEnumerable<>))
                return propertyType.GetGenericArguments()[0];

            return null;
        }

        private static void WriteRawJsonProperty(Utf8JsonWriter writer, string name, string rawJson)
        {
            writer.WritePropertyName(name);
            using var doc = JsonDocument.Parse(rawJson, JsonOptionsProvider.DocumentOptions);
            doc.RootElement.WriteTo(writer);
        }
    }
}
