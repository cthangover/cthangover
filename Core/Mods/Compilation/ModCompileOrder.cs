using System.Collections.Generic;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Compilation
{
    /// <summary>
    /// Compilation order for mods that carry C# sources: a depth-first
    /// topological sort over the <c>depends</c> declarations, restricted to
    /// the mods that are actually being compiled. A dependency that ships no
    /// code simply does not participate; cycles are broken with a warning
    /// instead of deadlocking boot.
    ///
    /// Deliberately not <see cref="ModLoadOrder"/>: that one implements the
    /// gameplay load order with manual overrides and fallbacks. Compilation
    /// needs the stricter depends-only ordering so a mod referencing another
    /// mod's types always compiles after its dependency.
    /// </summary>
    public static class ModCompileOrder
    {
        /// <summary>
        /// Orders <paramref name="modsWithCode"/> so that every mod appears
        /// after the mods it declares in <c>depends</c> (among those present
        /// in the input list). The input list order breaks ties, which keeps
        /// the sort deterministic for a given discovery order.
        /// </summary>
        public static List<ModInfo> TopologicalSort(List<ModInfo> modsWithCode)
        {
            var result = new List<ModInfo>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            void Visit(ModInfo mod)
            {
                if (visited.Contains(mod.Id))
                    return;
                if (visiting.Contains(mod.Id))
                {
                    GameLogger.Log("MODS_COMPILE", $"Circular dependency detected involving mod '{mod.Id}'", LogLevel.Warning);
                    return;
                }

                visiting.Add(mod.Id);

                if (mod.Manifest.DependencyIds != null)
                {
                    foreach (var depId in mod.Manifest.DependencyIds)
                    {
                        var dep = modsWithCode.Find(m => m.Id == depId);
                        if (dep != null)
                            Visit(dep);
                    }
                }

                visiting.Remove(mod.Id);
                visited.Add(mod.Id);
                result.Add(mod);
            }

            foreach (var mod in modsWithCode)
                Visit(mod);

            return result;
        }
    }
}
