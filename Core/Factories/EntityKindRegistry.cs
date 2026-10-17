using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Factories
{
    /// <summary>
    /// Maps entity kind strings to DTO types declared with
    /// <see cref="EntityKindAttribute"/>. The registry is the authoritative
    /// vocabulary of the data pipeline: the entity scanner asks it whether a
    /// file's <c>"Kind"</c> is known before routing it, and factories resolve
    /// their own kind from their DTO type.
    /// <para/>
    /// The reverse map (kind → <see cref="Type"/>) is built lazily by scanning
    /// every loaded assembly, because entity DTOs also live in Roslyn-compiled
    /// mod assemblies (<c>StatusEffectInfo</c> being the first example) that
    /// only exist after mod loading. <see cref="Refresh"/> is called when the
    /// resolver cache generation turns over, so a mod reload re-discovers the
    /// vocabulary.
    /// </summary>
    public static class EntityKindRegistry
    {
        private static readonly object _lock = new();
        private static Dictionary<string, Type> _byKind;

        /// <summary>
        /// Returns the kind declared by <paramref name="type"/>, or <c>null</c>
        /// when the type carries no <see cref="EntityKindAttribute"/>. Reads the
        /// attribute directly so it works for types in dynamic mod assemblies
        /// that the AppDomain scan may not have reached yet.
        /// </summary>
        public static string KindOf(Type type)
        {
            return type?.GetCustomAttribute<EntityKindAttribute>(false)?.Kind;
        }

        /// <summary>
        /// Returns the kind declared by <typeparamref name="T"/>, or <c>null</c>
        /// when the DTO is not annotated.
        /// </summary>
        public static string KindOf<T>()
        {
            return KindOf(typeof(T));
        }

        /// <summary>
        /// Resolves a kind string from a JSON file to its DTO type. Returns
        /// <c>false</c> for unknown kinds so the scanner can report the file
        /// instead of silently dropping it.
        /// </summary>
        public static bool TryResolve(string kind, out Type type)
        {
            EnsureBuilt();
            return _byKind.TryGetValue(kind ?? string.Empty, out type);
        }

        /// <summary>True when the kind is declared by some loaded DTO.</summary>
        public static bool IsKnown(string kind)
        {
            EnsureBuilt();
            return kind != null && _byKind.ContainsKey(kind);
        }

        /// <summary>All known kinds, for diagnostics and validation.</summary>
        public static IReadOnlyCollection<string> KnownKinds
        {
            get
            {
                EnsureBuilt();
                return _byKind.Keys.ToList();
            }
        }

        /// <summary>
        /// Discards the reverse map so the next lookup re-scans the AppDomain.
        /// Called by the entity resolver when mods are (re)loaded.
        /// </summary>
        public static void Refresh()
        {
            lock (_lock)
            {
                _byKind = null;
            }
        }

        private static void EnsureBuilt()
        {
            if (_byKind != null)
                return;

            lock (_lock)
            {
                if (_byKind != null)
                    return;

                var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                foreach (var assembly in Reflections.LoadedAssemblies())
                {
                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).ToArray();
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    foreach (var type in types)
                    {
                        var kind = KindOf(type);
                        if (string.IsNullOrEmpty(kind))
                            continue;

                        if (map.TryGetValue(kind, out var existing))
                        {
                            if (existing != type)
                                GameLogger.Log("ENTITIES",
                                    $"kind '{kind}' is declared by both '{existing.FullName}' and '{type.FullName}'; keeping the first",
                                    LogLevel.Error);
                            continue;
                        }

                        map[kind] = type;
                    }
                }

                _byKind = map;
            }
        }
    }
}
