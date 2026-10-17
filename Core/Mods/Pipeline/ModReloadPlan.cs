namespace Cthangover.Core.Mods.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Decides what an in-process mod-set reload does with each mod that has
    /// code. A mod's code identity is the set of files its context runs —
    /// the content-addressed compile artifact and precompiled DLL copies — so
    /// "did it change" is a plain set comparison, no timestamps involved.
    /// <list type="bullet">
    /// <item><see cref="Unload"/> — loaded mods that leave the set, changed, were
    /// forced, or depend (transitively) on any of those: their context
    /// resolves dependency types through the old dependency context, so they
    /// must reload with it. Dependents first.</item>
    /// <item><see cref="Keep"/> — loaded mods whose code and dependencies are
    /// unchanged: the context, its static state and its live nodes survive;
    /// the registries are rebuilt, so they re-register.</item>
    /// <item><see cref="Load"/> — mods whose code loads into a fresh context
    /// (new to the set or reloaded), in dependency order.</item>
    /// </list>
    /// </summary>
    public sealed class ModReloadPlan
    {
        private ModReloadPlan(List<string> unload, List<string> keep, List<string> load)
        {
            Unload = unload;
            Keep = keep;
            Load = load;
        }

        /// <summary>Mods whose contexts are released, in unload order (reverse load order).</summary>
        public IReadOnlyList<string> Unload { get; }

        /// <summary>Loaded mods that stay, in target order.</summary>
        public IReadOnlyList<string> Keep { get; }

        /// <summary>Mods that load into a new context, in target order.</summary>
        public IReadOnlyList<string> Load { get; }

        /// <summary>
        /// Computes the plan.
        /// </summary>
        /// <param name="loadedOrder">Mods with a live context, in the order they loaded.</param>
        /// <param name="loadedArtifacts">Files each loaded mod's context runs.</param>
        /// <param name="targetOrder">Mods with code in the new set, in dependency order.</param>
        /// <param name="targetArtifacts">Files each target mod compiled/staged to now.</param>
        /// <param name="dependencies">Direct <c>depends</c> of the target mods.</param>
        /// <param name="forceReload">Mods to reload even when unchanged; <c>*</c> forces every loaded mod.</param>
        public static ModReloadPlan Compute(
            IReadOnlyList<string> loadedOrder,
            IReadOnlyDictionary<string, IReadOnlyCollection<string>> loadedArtifacts,
            IReadOnlyList<string> targetOrder,
            IReadOnlyDictionary<string, IReadOnlyCollection<string>> targetArtifacts,
            IReadOnlyDictionary<string, IReadOnlyCollection<string>> dependencies,
            IReadOnlyCollection<string> forceReload)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var loaded = (loadedOrder ?? Array.Empty<string>()).Distinct(comparer).ToList();
            var target = (targetOrder ?? Array.Empty<string>()).Distinct(comparer).ToList();
            var targetSet = new HashSet<string>(target, comparer);
            var force = new HashSet<string>(forceReload ?? Array.Empty<string>(), comparer);
            var forceAll = force.Contains("*");

            var leaving = new HashSet<string>(comparer);
            foreach (var id in loaded)
            {
                if (!targetSet.Contains(id) || forceAll || force.Contains(id) || !SameFiles(Lookup(loadedArtifacts, id), Lookup(targetArtifacts, id)))
                    leaving.Add(id);
            }

            // Cascade to dependents until nothing changes: a kept mod must not
            // keep resolving into a released dependency context.
            bool grew;
            do
            {
                grew = false;
                foreach (var id in loaded)
                {
                    if (leaving.Contains(id))
                        continue;
                    if (Lookup(dependencies, id).Any(leaving.Contains))
                    {
                        leaving.Add(id);
                        grew = true;
                    }
                }
            }
            while (grew);

            var loadedSet = new HashSet<string>(loaded, comparer);
            var unload = loaded.Where(leaving.Contains).Reverse().ToList();
            var keep = new List<string>();
            var load = new List<string>();
            foreach (var id in target)
            {
                if (loadedSet.Contains(id) && !leaving.Contains(id))
                    keep.Add(id);
                else
                    load.Add(id);
            }
            return new ModReloadPlan(unload, keep, load);
        }

        private static IReadOnlyCollection<string> Lookup(IReadOnlyDictionary<string, IReadOnlyCollection<string>> map, string id)
        {
            if (map == null)
                return Array.Empty<string>();
            if (map.TryGetValue(id, out var value) && value != null)
                return value;
            foreach (var pair in map)
            {
                if (string.Equals(pair.Key, id, StringComparison.OrdinalIgnoreCase))
                    return pair.Value ?? Array.Empty<string>();
            }
            return Array.Empty<string>();
        }

        private static bool SameFiles(IReadOnlyCollection<string> a, IReadOnlyCollection<string> b)
        {
            var left = new HashSet<string>(a.Select(NormalizePath), StringComparer.OrdinalIgnoreCase);
            return left.SetEquals(b.Select(NormalizePath));
        }

        private static string NormalizePath(string path) => (path ?? string.Empty).Replace('\\', '/');
    }
}
