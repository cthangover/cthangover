using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Patches
{
    /// <summary>
    /// Internal helper for JSON array merge operations. All methods are
    /// pure string-in/string-out — no dependency on <c>PropertyData</c>
    /// or any other runtime state.
    /// </summary>
    internal static class ArrayMergeHelper
    {
        internal enum ArrayOperation { Default, Set, Add, Remove }

        internal static void ParseArrayOperation(string key, out string baseKey, out ArrayOperation op)
        {
            if (key.Length > 4 && key.EndsWith("$set", StringComparison.Ordinal))
            {
                baseKey = key.Substring(0, key.Length - 4);
                op = ArrayOperation.Set;
            }
            else if (key.Length > 4 && key.EndsWith("$add", StringComparison.Ordinal))
            {
                baseKey = key.Substring(0, key.Length - 4);
                op = ArrayOperation.Add;
            }
            else if (key.Length > 7 && key.EndsWith("$remove", StringComparison.Ordinal))
            {
                baseKey = key.Substring(0, key.Length - 7);
                op = ArrayOperation.Remove;
            }
            else
            {
                baseKey = key;
                op = ArrayOperation.Default;
            }
        }

        /// <summary>
        /// Merges <paramref name="existingRaw"/> and
        /// <paramref name="incomingRaw"/> into a single JSON array string.
        /// If <paramref name="identityKey"/> is provided and both arrays
        /// contain objects, elements are matched by that key and merged
        /// per-field. Otherwise simple concatenation is used.
        /// Returns <paramref name="incomingRaw"/> when there is no
        /// existing value or it is not a JSON array.
        /// </summary>
        internal static string AddToArray(string existingRaw, string incomingRaw, string identityKey)
        {
            if (string.IsNullOrEmpty(existingRaw) || !existingRaw.StartsWith("[", StringComparison.Ordinal))
                return incomingRaw;

            try
            {
                return identityKey != null
                    ? MergeObjectArrays(existingRaw, incomingRaw, identityKey)
                    : MergeJsonArrays(existingRaw, incomingRaw);
            }
            catch (JsonException ex)
            {
                Core.Utils.GameLogger.Log("MODS",
                    $"AddToArray: failed to merge, falling back to set: {ex.Message}",
                    Core.Utils.LogLevel.Warning);
                return incomingRaw;
            }
        }

        /// <summary>
        /// Removes elements present in <paramref name="removeRaw"/> from
        /// <paramref name="existingRaw"/>. If <paramref name="identityKey"/>
        /// is provided, elements in object arrays are matched by that
        /// key's value; otherwise raw text comparison is used.
        /// Returns <paramref name="existingRaw"/> unchanged when there
        /// is no existing value or it is not a JSON array.
        /// </summary>
        internal static string RemoveFromArray(string existingRaw, string removeRaw, string identityKey)
        {
            if (string.IsNullOrEmpty(existingRaw) || !existingRaw.StartsWith("[", StringComparison.Ordinal))
                return existingRaw;

            try
            {
                return identityKey != null
                    ? RemoveFromObjectArray(existingRaw, removeRaw, identityKey)
                    : RemoveFromSimpleArray(existingRaw, removeRaw);
            }
            catch (JsonException ex)
            {
                Core.Utils.GameLogger.Log("MODS",
                    $"RemoveFromArray: failed to process: {ex.Message}",
                    Core.Utils.LogLevel.Warning);
                return existingRaw;
            }
        }

        internal static string MergeJsonArrays(string existingRaw, string incomingRaw)
        {
            var items = new List<string>();
            using (var docA = JsonDocument.Parse(existingRaw, JsonOptionsProvider.DocumentOptions))
            {
                foreach (var el in docA.RootElement.EnumerateArray())
                    items.Add(el.GetRawText());
            }
            using (var docB = JsonDocument.Parse(incomingRaw, JsonOptionsProvider.DocumentOptions))
            {
                foreach (var el in docB.RootElement.EnumerateArray())
                    items.Add(el.GetRawText());
            }
            return "[" + string.Join(",", items) + "]";
        }

        private static string MergeObjectArrays(string existingRaw, string incomingRaw, string identityKey)
        {
            var elements = new List<JsonElement>();
            using (var docA = JsonDocument.Parse(existingRaw, JsonOptionsProvider.DocumentOptions))
                foreach (var el in docA.RootElement.EnumerateArray())
                    elements.Add(el.Clone());

            using var docB = JsonDocument.Parse(incomingRaw, JsonOptionsProvider.DocumentOptions);
            foreach (var incoming in docB.RootElement.EnumerateArray())
            {
                if (incoming.ValueKind != JsonValueKind.Object
                    || !incoming.TryGetProperty(identityKey, out var incomingKey))
                {
                    elements.Add(incoming.Clone());
                    continue;
                }

                var incomingKeyRaw = incomingKey.GetRawText();
                var found = false;
                for (var i = 0; i < elements.Count; i++)
                {
                    if (elements[i].ValueKind != JsonValueKind.Object
                        || !elements[i].TryGetProperty(identityKey, out var existingKey))
                        continue;

                    if (existingKey.GetRawText() != incomingKeyRaw)
                        continue;

                    elements[i] = MergeObjectFields(elements[i], incoming);
                    found = true;
                    break;
                }

                if (!found)
                    elements.Add(incoming.Clone());
            }

            var parts = new List<string>();
            foreach (var el in elements)
                parts.Add(el.GetRawText());
            return "[" + string.Join(",", parts) + "]";
        }

        private static JsonElement MergeObjectFields(JsonElement baseElement, JsonElement overlay)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                var written = new HashSet<string>();

                foreach (var prop in overlay.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    prop.Value.WriteTo(writer);
                    written.Add(prop.Name);
                }

                foreach (var prop in baseElement.EnumerateObject())
                {
                    if (!written.Contains(prop.Name))
                    {
                        writer.WritePropertyName(prop.Name);
                        prop.Value.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            var doc = JsonDocument.Parse(stream.ToArray(), JsonOptionsProvider.DocumentOptions);
            return doc.RootElement.Clone();
        }

        private static string RemoveFromSimpleArray(string existingRaw, string incomingRaw)
        {
            using var docExisting = JsonDocument.Parse(existingRaw, JsonOptionsProvider.DocumentOptions);
            using var docRemove = JsonDocument.Parse(incomingRaw, JsonOptionsProvider.DocumentOptions);

            var toRemove = new HashSet<string>();
            foreach (var el in docRemove.RootElement.EnumerateArray())
                toRemove.Add(el.GetRawText());

            var remaining = new List<string>();
            foreach (var el in docExisting.RootElement.EnumerateArray())
            {
                if (!toRemove.Contains(el.GetRawText()))
                    remaining.Add(el.GetRawText());
            }

            return "[" + string.Join(",", remaining) + "]";
        }

        private static string RemoveFromObjectArray(string existingRaw, string incomingRaw, string identityKey)
        {
            using var docExisting = JsonDocument.Parse(existingRaw, JsonOptionsProvider.DocumentOptions);
            using var docRemove = JsonDocument.Parse(incomingRaw, JsonOptionsProvider.DocumentOptions);

            var toRemove = new HashSet<string>();
            foreach (var el in docRemove.RootElement.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Object
                    && el.TryGetProperty(identityKey, out var keyProp))
                    toRemove.Add(keyProp.GetRawText());
            }

            var remaining = new List<string>();
            foreach (var el in docExisting.RootElement.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Object
                    && el.TryGetProperty(identityKey, out var keyProp)
                    && toRemove.Contains(keyProp.GetRawText()))
                    continue;
                remaining.Add(el.GetRawText());
            }

            return "[" + string.Join(",", remaining) + "]";
        }
    }
}
