using System;
using System.Collections.Generic;
using System.Linq;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Computes deterministic mod load order from declared dependencies
    /// and optional manual ordering. The algorithm assigns each mod a
    /// <c>dependsLevel</c> — zero for mods without dependencies, one for
    /// mods that depend on level-zero mods, etc. Within the same level,
    /// mods are ordered by user-defined manual position (if any), then
    /// alphabetically by ID for determinism.
    ///
    /// The result is a list where every mod appears after all its
    /// transitive dependencies.
    /// </summary>
    public static class ModLoadOrder
    {
        /// <summary>
        /// Computes a deterministic order for the given mods.
        /// <paramref name="mods"/> maps mod ID to a list of dependency IDs
        /// (from <c>manifest.json</c>'s <c>depends</c> field).
        /// <paramref name="manualOrder"/> is an optional user-defined
        /// list of mod IDs. Mods in the manual list are ordered by their
        /// position within it; mods not present are placed at the end.
        /// Manual ordering is secondary to dependencies — a mod cannot
        /// appear before its dependency regardless of manual position.
        /// </summary>
        public static List<string> Compute(
            IReadOnlyDictionary<string, IReadOnlyList<string>> mods,
            List<string> manualOrder = null)
        {
            var allIds = mods.Keys.ToList();
            if (allIds.Count == 0)
                return new List<string>();

            var level = ComputeDependsLevels(mods, allIds);

            var manualIndex = new Dictionary<string, int>();
            if (manualOrder != null)
            {
                for (int i = 0; i < manualOrder.Count; i++)
                {
                    if (!manualIndex.ContainsKey(manualOrder[i]))
                        manualIndex[manualOrder[i]] = i;
                }
            }

            allIds.Sort((a, b) =>
            {
                var levelA = level.TryGetValue(a, out var la) ? la : 0;
                var levelB = level.TryGetValue(b, out var lb) ? lb : 0;
                var levelCmp = levelA.CompareTo(levelB);
                if (levelCmp != 0)
                    return levelCmp;

                var manualA = manualIndex.TryGetValue(a, out var ma) ? ma : int.MaxValue;
                var manualB = manualIndex.TryGetValue(b, out var mb) ? mb : int.MaxValue;
                var manualCmp = manualA.CompareTo(manualB);
                if (manualCmp != 0)
                    return manualCmp;

                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });

            return allIds;
        }

        /// <summary>
        /// Computes the dependency depth for every mod. Returns a
        /// dictionary mapping mod ID to its <c>dependsLevel</c> —
        /// zero for root mods, incremented for each dependency layer.
        /// Circular dependencies are broken at zero with a warning
        /// delivered via <paramref name="onCycle"/>.
        /// </summary>
        public static Dictionary<string, int> ComputeDependsLevels(
            IReadOnlyDictionary<string, IReadOnlyList<string>> mods,
            List<string> allIds,
            System.Action<string> onCycle = null)
        {
            var result = new Dictionary<string, int>();

            int ComputeLevel(string modId, HashSet<string> visiting)
            {
                if (result.TryGetValue(modId, out var cached))
                    return cached;

                if (!visiting.Add(modId))
                {
                    onCycle?.Invoke(modId);
                    result[modId] = 0;
                    return 0;
                }

                if (!mods.TryGetValue(modId, out var deps) || deps == null || deps.Count == 0)
                {
                    result[modId] = 0;
                    visiting.Remove(modId);
                    return 0;
                }

                int maxDepth = 0;
                foreach (var depId in deps)
                {
                    if (!mods.ContainsKey(depId))
                        continue;

                    var depDepth = ComputeLevel(depId, visiting);
                    if (depDepth + 1 > maxDepth)
                        maxDepth = depDepth + 1;
                }

                result[modId] = maxDepth;
                visiting.Remove(modId);
                return maxDepth;
            }

            foreach (var id in allIds)
            {
                if (!result.ContainsKey(id))
                    ComputeLevel(id, new HashSet<string>());
            }

            return result;
        }
    }
}
