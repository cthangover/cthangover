namespace Cthangover.Core.Mods.Unload
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Drops references into dying mod assemblies from the long-lived parts of
    /// the process: the static fields of the surviving assemblies (the kernel
    /// and the mods that stay) and the objects reachable from them, plus a few
    /// explicit roots (autoload nodes). This is the generic answer to the
    /// registries and "first registration wins" bridges a mod fills from its
    /// initializer and nobody ever clears — <c>LightingBridge</c> slots in the
    /// kernel, <c>SceneEventController</c> timer lists, a kept mod's own
    /// registry singleton holding another mod's types
    /// (<c>BattleCoreRegistry.Instance._cores</c>): each pins the dying mod's
    /// <see cref="System.Runtime.Loader.AssemblyLoadContext"/> forever.
    ///
    /// Rules, applied to every field value on the way:
    /// <list type="bullet">
    /// <item>a delegate loses its owned invocation-list entries;</item>
    /// <item>an owned object (instance of a mod type, a mod <see cref="Type"/>)
    /// is replaced by null;</item>
    /// <item>a list/set loses owned items; a dictionary loses entries with an
    /// owned key or value;</item>
    /// <item>objects whose type comes from a surviving host assembly, and the
    /// items of collections, are walked further (up to <see cref="MaxDepth"/>,
    /// each object once). Objects of framework or engine types are never walked —
    /// only their collection contents are — so the sweep stays bounded.</item>
    /// </list>
    /// Only fields whose declared type can hold a reference into mod code are
    /// read (<see cref="CanReferenceModCode"/>): reading a static field runs the
    /// type's static constructor, which must not happen as a side effect of an
    /// unload. Static <c>readonly</c> fields cannot be reassigned, so an owned
    /// value there is reported, not cleared; collections and objects behind
    /// them are still cleaned. Every change is reported, so the log shows which
    /// subsystem forgot to unregister — the sweep is the safety net, not the
    /// contract.
    /// </summary>
    public sealed class StaticReferenceSweeper
    {
        /// <summary>How far the walk goes from a static field: field → object → collection → object → …</summary>
        public const int MaxDepth = 6;

        private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly AssemblyOwnership ownership;
        private readonly HashSet<Assembly> hosts;
        private readonly HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
        private readonly List<string> report = new();

        private StaticReferenceSweeper(AssemblyOwnership ownership, HashSet<Assembly> hosts)
        {
            this.ownership = ownership;
            this.hosts = hosts;
        }

        /// <summary>
        /// Sweeps the static fields of every type in <paramref name="hostAssemblies"/>
        /// (owned assemblies are skipped: their statics die with them), the
        /// objects of host types reachable from them and the objects in
        /// <paramref name="instances"/> (walked even when their type is not a
        /// host type, up to the first GodotSharp base class). Returns one line
        /// per change or uncleanable reference.
        /// </summary>
        public static List<string> Sweep(AssemblyOwnership ownership, IEnumerable<Assembly> hostAssemblies, IEnumerable<object> instances = null)
        {
            if (ownership == null || ownership.IsEmpty)
                return new List<string>();

            var hosts = new HashSet<Assembly>();
            foreach (var assembly in hostAssemblies ?? Array.Empty<Assembly>())
            {
                if (assembly != null && !assembly.IsDynamic && !ownership.Owns(assembly))
                    hosts.Add(assembly);
            }

            var sweeper = new StaticReferenceSweeper(ownership, hosts);
            foreach (var assembly in hosts)
            {
                foreach (var type in LoadableTypes(assembly))
                {
                    if (type.ContainsGenericParameters)
                        continue;
                    foreach (var field in ReferenceFields(type, StaticFields))
                        sweeper.TrySweepField(field, null, MaxDepth);
                }
            }

            foreach (var instance in instances ?? Array.Empty<object>())
            {
                if (instance != null && !ownership.Owns(instance.GetType()))
                    sweeper.SweepObject(instance, MaxDepth);
            }

            return sweeper.report;
        }

        private void TrySweepField(FieldInfo field, object owner, int depth)
        {
            try
            {
                SweepField(field, owner, depth);
            }
            catch (Exception ex)
            {
                report.Add($"{field.DeclaringType?.FullName}.{field.Name}: sweep failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void SweepField(FieldInfo field, object owner, int depth)
        {
            if (field.IsLiteral || ownership.Owns(field.DeclaringType))
                return;

            var value = field.GetValue(owner);
            if (value == null)
                return;

            var where = $"{field.DeclaringType?.FullName}.{field.Name}";
            if (value is Delegate handler)
            {
                var stripped = ownership.Strip(handler, out var changed);
                if (changed)
                    Assign(field, owner, stripped, where, "delegate entries");
                return;
            }

            if (ownership.OwnsValue(value))
            {
                Assign(field, owner, null, where, "reference");
                return;
            }

            var removed = CleanValue(value, depth - 1);
            if (removed > 0)
                report.Add($"{where}: removed {removed} item(s)");
        }

        private void Assign(FieldInfo field, object owner, object value, string where, string what)
        {
            if (field.IsInitOnly && field.IsStatic)
            {
                report.Add($"{where}: readonly static holds a mod {what}, cannot clear (pins the mod)");
                return;
            }
            field.SetValue(owner, value);
            report.Add($"{where}: cleared mod {what}");
        }

        /// <summary>Cleans a surviving value: a collection's owned items, or the fields of a host object. Returns removed collection items.</summary>
        private int CleanValue(object value, int depth)
        {
            if (depth <= 0 || value is string || value is Array || value is Type || value is Assembly)
                return 0;
            if (value is IEnumerable enumerable)
                return CleanCollection(enumerable, depth);
            if (hosts.Contains(value.GetType().Assembly))
                SweepObject(value, depth);
            return 0;
        }

        /// <summary>Walks the instance fields of <paramref name="instance"/> once.</summary>
        private void SweepObject(object instance, int depth)
        {
            if (depth <= 0 || !visited.Add(instance))
                return;
            for (var type = instance.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                if (type.Assembly == typeof(Godot.GodotObject).Assembly)
                    break;
                foreach (var field in ReferenceFields(type, InstanceFields))
                    TrySweepField(field, instance, depth);
            }
        }

        private int CleanCollection(IEnumerable collection, int depth)
        {
            if (!visited.Add(collection))
                return 0;
            try
            {
                switch (collection)
                {
                    case IDictionary dictionary when !dictionary.IsReadOnly:
                        return CleanDictionary(dictionary, depth);
                    case IList list when !list.IsReadOnly && !list.IsFixedSize:
                        return CleanList(list, depth);
                    default:
                        return CleanGenericCollection(collection, depth);
                }
            }
            catch (InvalidOperationException)
            {
                // A collection mutated concurrently: skip, the next unload retries.
                return 0;
            }
        }

        private int CleanDictionary(IDictionary dictionary, int depth)
        {
            // IDictionary's own enumerator yields DictionaryEntry for every
            // implementation (ConcurrentDictionary included); the generic one
            // would yield KeyValuePair.
            var entries = new List<DictionaryEntry>();
            var enumerator = dictionary.GetEnumerator();
            while (enumerator.MoveNext())
                entries.Add(enumerator.Entry);

            var doomed = new List<object>();
            var removed = 0;
            foreach (var entry in entries)
            {
                if (ownership.OwnsValue(entry.Key) || ownership.OwnsValue(entry.Value))
                {
                    doomed.Add(entry.Key);
                    continue;
                }
                if (entry.Value is Delegate handler)
                {
                    var stripped = ownership.Strip(handler, out var changed);
                    if (!changed)
                        continue;
                    if (stripped == null)
                        doomed.Add(entry.Key);
                    else
                        dictionary[entry.Key] = stripped;
                    removed++;
                    continue;
                }
                if (entry.Value != null)
                    removed += CleanValue(entry.Value, depth - 1);
            }
            foreach (var key in doomed)
                dictionary.Remove(key);
            return removed + doomed.Count;
        }

        private int CleanList(IList list, int depth)
        {
            var removed = 0;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var item = list[i];
                if (ownership.OwnsValue(item))
                {
                    list.RemoveAt(i);
                    removed++;
                    continue;
                }
                if (item != null)
                    removed += CleanValue(item, depth - 1);
            }
            return removed;
        }

        /// <summary>Sets and other generic collections exposing <c>ICollection&lt;T&gt;.Remove</c> (HashSet, LinkedList, SortedSet).</summary>
        private int CleanGenericCollection(IEnumerable collection, int depth)
        {
            var collectionInterface = GenericCollectionInterface(collection.GetType());
            if (collectionInterface == null || (bool)collectionInterface.GetProperty("IsReadOnly").GetValue(collection))
                return 0;
            var remove = collectionInterface.GetMethod("Remove");
            var elementIsValueType = collectionInterface.GetGenericArguments()[0].IsValueType;

            var items = new List<object>();
            foreach (var item in collection)
                items.Add(item);

            var removed = 0;
            foreach (var item in items)
            {
                if (ownership.OwnsValue(item))
                {
                    if ((bool)remove.Invoke(collection, new[] { item }))
                        removed++;
                }
                else if (item != null && !elementIsValueType)
                {
                    removed += CleanValue(item, depth - 1);
                }
            }
            return removed;
        }

        private static Type GenericCollectionInterface(Type type)
        {
            foreach (var candidate in type.GetInterfaces())
            {
                if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(ICollection<>))
                    return candidate;
            }
            return null;
        }

        private static IEnumerable<Type> LoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var loaded = new List<Type>();
                foreach (var type in ex.Types)
                {
                    if (type != null)
                        loaded.Add(type);
                }
                return loaded;
            }
        }

        private static List<FieldInfo> ReferenceFields(Type type, BindingFlags flags)
        {
            var result = new List<FieldInfo>();
            FieldInfo[] fields;
            try
            {
                fields = type.GetFields(flags);
            }
            catch (Exception)
            {
                return result;
            }
            foreach (var field in fields)
            {
                if (CanReferenceModCode(field.FieldType))
                    result.Add(field);
            }
            return result;
        }

        /// <summary>
        /// Field types that can hold a reference into mod code. Strings,
        /// primitives, enums and arrays cannot; sealed non-generic classes of
        /// the framework and the engine (such as <c>StringName</c> in the
        /// Godot-generated <c>MethodName</c>/<c>SignalName</c> holders) cannot
        /// either. Sealed classes of the game itself can (a registry singleton
        /// holding mod types), so they are read.
        /// </summary>
        private static bool CanReferenceModCode(Type fieldType)
        {
            if (fieldType.IsPrimitive || fieldType.IsEnum || fieldType.IsPointer || fieldType.IsByRefLike || fieldType == typeof(string))
                return false;
            if (fieldType.IsArray)
                return false;
            if (fieldType.IsValueType)
                return fieldType.IsGenericType;
            if (fieldType.IsSealed && !fieldType.IsGenericType && !typeof(Delegate).IsAssignableFrom(fieldType) && fieldType != typeof(Type))
                return !IsPlatformAssembly(fieldType.Assembly);
            return true;
        }

        private static bool IsPlatformAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name ?? string.Empty;
            return assembly == typeof(object).Assembly
                   || assembly == typeof(Godot.GodotObject).Assembly
                   || name.StartsWith("System.", StringComparison.Ordinal)
                   || name.StartsWith("Microsoft.", StringComparison.Ordinal);
        }
    }
}
