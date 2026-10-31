namespace Cthangover.Core.Mods.Patches
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Walks a DTO type alongside the JSON being patched, so merge keys are known at any
    /// depth: <c>States[...].Parts$add</c> finds <c>[MergeKey]</c> on the state type's
    /// <c>Parts</c>. Properties are matched by their JSON name (<see cref="JsonPropertyNameAttribute"/>
    /// or the C# name, case-insensitive, like the read options). Dictionaries with string
    /// keys pass their value type to every key (e.g. <c>Variants</c>), lists and arrays
    /// their element type. Unknown names (extension data) have no type and therefore no
    /// keys — only the explicit <c>{"$key", "$items"}</c> form works there.
    /// </summary>
    public static class MergeTypeMap
    {
        /// <summary>
        /// Per-type property lookups. Weak on the type: mod DTOs live in collectible load
        /// contexts, and a strong static cache in the core would keep an unloaded mod alive.
        /// </summary>
        private static readonly ConditionalWeakTable<Type, ConcurrentDictionary<string, PropertyInfo>> Properties = new();

        /// <summary>The extension-data layer per type (weak on the type for the same reason).</summary>
        private static readonly ConditionalWeakTable<Type, StrongBox<PatchLayerAttribute>> ExtensionLayers = new();

        /// <summary>The DTO type of the value at <paramref name="name"/> inside <paramref name="parent"/>, or null when unknown.</summary>
        public static Type ChildType(Type parent, string name)
        {
            if (parent == null)
                return null;
            var dictionaryValue = DictionaryValueType(parent);
            if (dictionaryValue != null)
                return dictionaryValue == typeof(object) || dictionaryValue == typeof(JsonElement) ? null : dictionaryValue;
            return Unwrap(FindProperty(parent, name)?.PropertyType);
        }

        /// <summary>The element type of a list or array type, or null for anything else (strings included).</summary>
        public static Type ItemType(Type listType)
        {
            if (listType == null || listType == typeof(string))
                return null;
            if (listType.IsArray)
                return Unwrap(listType.GetElementType());
            var enumerable = listType.IsGenericType && listType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? listType
                : listType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return enumerable == null ? null : Unwrap(enumerable.GetGenericArguments()[0]);
        }

        /// <summary>
        /// The declared merge key of the list at <paramref name="name"/> inside <paramref name="parent"/>:
        /// <see cref="MergeKeyAttribute"/> on the property first, then <see cref="IMergeable"/> on the
        /// element type; null when the list has no identity.
        /// </summary>
        public static string[] KeyOf(Type parent, string name)
        {
            if (parent == null || DictionaryValueType(parent) != null)
                return null;
            var property = FindProperty(parent, name);
            if (property == null)
                return null;
            var attribute = property.GetCustomAttribute<MergeKeyAttribute>();
            if (attribute != null && attribute.Properties.Length > 0)
                return attribute.Properties;
            var item = ItemType(property.PropertyType);
            if (item == null || !typeof(IMergeable).IsAssignableFrom(item) || item.IsAbstract)
                return null;
            if (item.GetConstructor(Type.EmptyTypes) == null)
                return null;
            var key = ((IMergeable)Activator.CreateInstance(item)).MergeKeyProperty;
            return string.IsNullOrEmpty(key) ? null : new[] { key };
        }

        /// <summary>
        /// The layer declared on the property <paramref name="name"/> of <paramref name="parent"/>
        /// (<see cref="PatchLayerAttribute"/>, not on extension data), or null. <paramref name="dictionary"/>
        /// tells that the property is a <c>string → layer</c> dictionary rather than one layer.
        /// </summary>
        public static PatchLayerAttribute LayerOf(Type parent, string name, out bool dictionary)
        {
            dictionary = false;
            var property = FindProperty(parent, name);
            if (property == null || property.GetCustomAttribute<JsonExtensionDataAttribute>() != null)
                return null;
            var layer = property.GetCustomAttribute<PatchLayerAttribute>();
            if (layer != null)
                dictionary = DictionaryValueType(property.PropertyType) != null;
            return layer;
        }

        /// <summary>
        /// The layer declared on the <c>[JsonExtensionData]</c> property of <paramref name="type"/>: every
        /// property of such an object that is not declared (<see cref="IsDeclared"/>) is a layer entry. Null when none.
        /// </summary>
        public static PatchLayerAttribute ExtensionLayerOf(Type type)
        {
            if (type == null)
                return null;
            return ExtensionLayers.GetValue(type, FindExtensionLayer).Value;
        }

        /// <summary>True when <paramref name="name"/> is a declared JSON property of <paramref name="type"/> (not extension data).</summary>
        public static bool IsDeclared(Type type, string name)
        {
            var property = FindProperty(type, name);
            return property != null && property.GetCustomAttribute<JsonExtensionDataAttribute>() == null;
        }

        private static StrongBox<PatchLayerAttribute> FindExtensionLayer(Type type)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetCustomAttribute<JsonExtensionDataAttribute>() != null)
                    return new StrongBox<PatchLayerAttribute>(property.GetCustomAttribute<PatchLayerAttribute>());
            }
            return new StrongBox<PatchLayerAttribute>(null);
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
                return null;
            var lookups = Properties.GetValue(type, NewLookups);
            if (lookups.TryGetValue(name, out var cached))
                return cached;
            var found = LookupProperty(type, name);
            lookups[name] = found;
            return found;
        }

        private static ConcurrentDictionary<string, PropertyInfo> NewLookups(Type type)
            => new(StringComparer.OrdinalIgnoreCase);

        private static PropertyInfo LookupProperty(Type type, string name)
        {
            PropertyInfo byName = null;
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0 || property.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                    continue;
                var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
                if (jsonName != null && string.Equals(jsonName, name, StringComparison.OrdinalIgnoreCase))
                    return property;
                if (jsonName == null && byName == null && string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    byName = property;
            }
            return byName;
        }

        private static Type DictionaryValueType(Type type)
        {
            var dictionary = type.IsGenericType && IsDictionaryDefinition(type.GetGenericTypeDefinition())
                ? type
                : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && IsDictionaryDefinition(i.GetGenericTypeDefinition()));
            if (dictionary == null)
                return null;
            var arguments = dictionary.GetGenericArguments();
            return arguments[0] == typeof(string) ? Unwrap(arguments[1]) : null;
        }

        private static bool IsDictionaryDefinition(Type definition)
            => definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>);

        private static Type Unwrap(Type type)
            => type == null ? null : Nullable.GetUnderlyingType(type) ?? type;
    }
}
