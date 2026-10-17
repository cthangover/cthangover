namespace Cthangover.Core.Mods.Unload
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// "Does this reference belong to the code being unloaded?" — the single
    /// rule every unload sweeper applies. A value is owned when its runtime
    /// type (or any generic argument / element type of it) is declared in one
    /// of the dying assemblies; a delegate is owned when any entry of its
    /// invocation list points into them, by method or by target (lambdas
    /// close over compiler-generated classes of the mod assembly, and kernel
    /// wrappers such as the event bus close over the mod's delegate).
    /// Answers for types are cached: a sweep asks about the same few hundred
    /// types thousands of times.
    /// </summary>
    public sealed class AssemblyOwnership
    {
        private readonly HashSet<Assembly> assemblies;
        private readonly Dictionary<Type, bool> typeCache = new();

        /// <summary>Creates the rule for <paramref name="assemblies"/> (the assemblies of every mod released in one unload pass).</summary>
        public AssemblyOwnership(IEnumerable<Assembly> assemblies)
        {
            this.assemblies = new HashSet<Assembly>(assemblies ?? Array.Empty<Assembly>());
        }

        /// <summary>The dying assemblies.</summary>
        public IReadOnlyCollection<Assembly> Assemblies => assemblies;

        /// <summary>True when nothing is being unloaded; every query then answers false.</summary>
        public bool IsEmpty => assemblies.Count == 0;

        /// <summary>True when <paramref name="assembly"/> is one of the dying assemblies.</summary>
        public bool Owns(Assembly assembly) => assembly != null && assemblies.Contains(assembly);

        /// <summary>True when <paramref name="type"/> or a type it is built from is declared in a dying assembly.</summary>
        public bool Owns(Type type)
        {
            if (type == null || IsEmpty)
                return false;
            if (typeCache.TryGetValue(type, out var cached))
                return cached;

            // Placeholder against recursive generic shapes (class A : IEquatable<A>).
            typeCache[type] = false;
            var owned = assemblies.Contains(type.Assembly);
            if (!owned && type.HasElementType)
                owned = Owns(type.GetElementType());
            if (!owned && type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    if (Owns(argument))
                    {
                        owned = true;
                        break;
                    }
                }
            }

            typeCache[type] = owned;
            return owned;
        }

        /// <summary>True when one invocation-list entry of <paramref name="handler"/> belongs to a dying assembly.</summary>
        public bool Owns(Delegate handler)
        {
            if (handler == null || IsEmpty)
                return false;
            foreach (var entry in handler.GetInvocationList())
            {
                if (OwnsSingle(entry))
                    return true;
            }
            return false;
        }

        /// <summary>Owned value: an owned delegate or an instance of an owned type (a Type/Assembly/member object counts by itself).</summary>
        public bool OwnsValue(object value)
        {
            switch (value)
            {
                case null:
                    return false;
                case Delegate d:
                    return Owns(d);
                case Type t:
                    return Owns(t);
                case Assembly a:
                    return Owns(a);
                case MemberInfo m:
                    return Owns(m.DeclaringType) || Owns(m.Module.Assembly);
                default:
                    return Owns(value.GetType());
            }
        }

        /// <summary>
        /// Removes the owned entries from a (multicast) delegate. Returns the
        /// survivor (null when nothing survives); <paramref name="changed"/>
        /// tells whether anything was removed.
        /// </summary>
        public Delegate Strip(Delegate handler, out bool changed)
        {
            changed = false;
            if (handler == null)
                return null;
            Delegate result = null;
            foreach (var entry in handler.GetInvocationList())
            {
                if (OwnsSingle(entry))
                {
                    changed = true;
                    continue;
                }
                result = Delegate.Combine(result, entry);
            }
            return changed ? result : handler;
        }

        private bool OwnsSingle(Delegate entry)
        {
            var method = entry.Method;
            if (method != null && (Owns(method.DeclaringType) || Owns(method.Module.Assembly)))
                return true;
            var target = entry.Target;
            if (target == null)
                return false;
            if (Owns(target.GetType()))
                return true;
            if (target is Delegate inner)
                return Owns(inner);
            // A kernel wrapper delegate (a closure capturing a mod delegate) is
            // owned through its closure: look one level into its delegate fields.
            foreach (var field in target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!typeof(Delegate).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(object))
                    continue;
                if (field.GetValue(target) is Delegate captured && !ReferenceEquals(captured, entry) && Owns(captured))
                    return true;
            }
            return false;
        }
    }
}
