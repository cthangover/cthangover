using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Maps script file paths and class names to compiled C# <see cref="System.Type"/> objects
    /// across all loaded mods, respecting mod load-order priority.
    /// <para/>
    /// Populated by <see cref="ModCompiler"/> after each mod's C# source is compiled.
    /// Each mod contributes a {path → Type} and {name → Type} mapping.
    /// <para/>
    /// Resolution order:
    /// 1. <see cref="ResolveByPath"/> — iterates mods in priority order, returns first match
    /// 2. <see cref="ResolveByName"/> — same priority-ordered search by class name / full name
    /// 3. <see cref="Resolve"/> — tries path first, falls back to file-name → class-name lookup
    /// <para/>
    /// This is the bridge between a .tscn's <c>path="scripts/MyScript.cs"</c> reference
    /// and the actual <c>Type</c> in the compiled mod assembly. Paths are normalized
    /// (stripped of <c>res://</c> prefix, backslashes → forward slashes) before lookup.
    /// </summary>
    public static class ScriptTypeRegistry
    {
        private static readonly Dictionary<string, Dictionary<string, Type>> _modPathToType = new();
        private static readonly Dictionary<string, Dictionary<string, Type>> _modNameToType = new();
        private static readonly List<Type> _allTypes = new();

        private static List<(string ModId, int Priority)> _orderedModIds;

        static ScriptTypeRegistry()
        {
            RegisterCoreAssembly(typeof(ScriptTypeRegistry).Assembly);
        }

        /// <summary>
        /// Registers the main game assembly's Node types as the lowest-priority
        /// fallback. Mod-provided scenes reference core behaviours by their
        /// <c>res://Core/...cs</c> path; <see cref="Resolve"/> has no path entry for
        /// them, so it falls through to <see cref="ResolveByName"/>, which needs
        /// these types. Mod assemblies registered later still win because their
        /// name maps are consulted before the fallback list.
        /// </summary>
        public static void RegisterCoreAssembly(Assembly assembly)
        {
            if (assembly == null)
                return;

            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!type.IsClass || type.IsAbstract || !typeof(Godot.Node).IsAssignableFrom(type))
                        continue;
                    if (!_allTypes.Contains(type))
                        _allTypes.Add(type);
                }
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS", $"ScriptTypeRegistry: failed to scan core assembly: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>Sets the mod priority order (id + load position) used by all lookups; supplied by <see cref="ModRegistry"/> after load order is computed.</summary>
        public static void SetModOrder(List<(string ModId, int Priority)> orderedMods)
        {
            _orderedModIds = orderedMods;
        }

        /// <summary>
        /// Indexes a freshly compiled mod assembly: adds its non-abstract
        /// <see cref="Godot.Node"/> subclasses to the name maps and, for every
        /// source file whose file name matches a class, records the
        /// path-to-type mapping used by .tscn script resolution.
        /// </summary>
        public static void RegisterAssembly(Assembly assembly, string modId, IEnumerable<(string FilePath, string Source)> sourceFiles)
        {
            if (!_modPathToType.ContainsKey(modId))
            {
                _modPathToType[modId] = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                _modNameToType[modId] = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            }

            var pathDict = _modPathToType[modId];
            var nameDict = _modNameToType[modId];
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                if (!type.IsClass || type.IsAbstract)
                    continue;

                if (!typeof(Godot.Node).IsAssignableFrom(type))
                    continue;

                _allTypes.Add(type);

                var typeName = type.Name;
                if (!nameDict.ContainsKey(typeName))
                    nameDict[typeName] = type;

                var fullName = type.FullName;
                if (!nameDict.ContainsKey(fullName))
                    nameDict[fullName] = type;
            }

            foreach (var (filePath, _) in sourceFiles)
            {
                var relative = NormalizePath(filePath);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(relative);

                if (nameDict.TryGetValue(fileName, out var t))
                {
                    if (!pathDict.ContainsKey(relative))
                        pathDict[relative] = t;
                }
                else
                {
                    foreach (var type in types)
                    {
                        if (!type.IsClass || type.IsAbstract || !typeof(Godot.Node).IsAssignableFrom(type))
                            continue;

                        if (type.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!pathDict.ContainsKey(relative))
                                pathDict[relative] = type;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>Priority-ordered lookup by normalized script path; first matching mod wins, then registration order.</summary>
        public static Type ResolveByPath(string tscnPath)
        {
            var normalized = NormalizePath(tscnPath);

            if (_orderedModIds != null)
            {
                foreach (var (modId, _) in _orderedModIds)
                {
                    if (_modPathToType.TryGetValue(modId, out var pathDict) &&
                        pathDict.TryGetValue(normalized, out var type))
                        return type;
                }
            }

            foreach (var kvp in _modPathToType)
            {
                if (kvp.Value.TryGetValue(normalized, out var type))
                    return type;
            }

            return null;
        }

        /// <summary>Priority-ordered lookup by class name or full name, with a case-insensitive fallback scan over all indexed types.</summary>
        public static Type ResolveByName(string className)
        {
            if (_orderedModIds != null)
            {
                foreach (var (modId, _) in _orderedModIds)
                {
                    if (_modNameToType.TryGetValue(modId, out var nameDict) &&
                        nameDict.TryGetValue(className, out var type))
                        return type;
                }
            }

            foreach (var kvp in _modNameToType)
            {
                if (kvp.Value.TryGetValue(className, out var type))
                    return type;
            }

            return _allTypes.FirstOrDefault(t =>
                t.Name.Equals(className, StringComparison.OrdinalIgnoreCase) ||
                t.FullName.Equals(className, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Combined resolver for a .tscn script reference: input without
        /// separators or a dot is treated as a class name; otherwise the path
        /// is tried first and the bare file name second (as a class name).
        /// </summary>
        public static Type Resolve(string tscnPath)
        {
            if (string.IsNullOrEmpty(tscnPath))
                return null;

            if (!tscnPath.Contains("/") && !tscnPath.Contains("\\") && !tscnPath.Contains("."))
                return ResolveByName(tscnPath);

            var result = ResolveByPath(tscnPath);
            if (result != null)
                return result;

            var fileName = System.IO.Path.GetFileNameWithoutExtension(tscnPath);
            if (!string.IsNullOrEmpty(fileName))
                return ResolveByName(fileName);

            return null;
        }

        /// <summary>Wipes every mapping and re-registers the core assembly fallback; called on mod reload.</summary>
        public static void Clear()
        {
            _modPathToType.Clear();
            _modNameToType.Clear();
            _allTypes.Clear();
            _orderedModIds = null;
            RegisterCoreAssembly(typeof(ScriptTypeRegistry).Assembly);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (path.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(6);

            return ModPath.Normalize(path);
        }
    }
}
