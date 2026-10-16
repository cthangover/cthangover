namespace Cthangover.Core.Mods.Pipeline
{
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Computes and caches the mod load order: a topological sort over
    /// manifest dependencies, refined by the user's saved manual order
    /// (<c>ModLoadOrderStore</c>) with an alphabetical fallback for
    /// determinism. Split out of <see cref="ModRegistry"/> because the cache
    /// has its own lifecycle — it is invalidated on every registry rebuild
    /// (discover/reload) and on user re-ordering, and it must never be
    /// persisted from inside the launcher boot window (see
    /// <see cref="GetOrderedModIds"/>).
    ///
    /// One instance per <see cref="ModRegistry"/>; the mod dictionaries are
    /// shared by reference with the registry, so freshly discovered or
    /// reloaded mods are visible on the next computation.
    /// </summary>
    public class ModLoadOrderService
    {
        private readonly ModRegistry registry;
        private readonly ModBootPipeline pipeline;
        private readonly Dictionary<string, IModInfo> mods;
        private readonly Dictionary<string, IModInfo> allDiscoveredMods;

        /// <summary>Cached order for the enabled (filtered) set; <c>null</c> when stale.</summary>
        private List<string> orderedModIds;

        /// <summary>
        /// Creates the service for one <see cref="ModRegistry"/>. The pipeline
        /// reference is only used to read the boot-window state
        /// (<see cref="ModBootPipeline.IsLauncherBootWindow"/>) so the guard
        /// semantics stay identical to the one in Initialize — the policy
        /// itself lives in the pipeline, not here.
        /// </summary>
        public ModLoadOrderService(ModRegistry registry, ModBootPipeline pipeline, Dictionary<string, IModInfo> mods, Dictionary<string, IModInfo> allDiscoveredMods)
        {
            this.registry = registry;
            this.pipeline = pipeline;
            this.mods = mods;
            this.allDiscoveredMods = allDiscoveredMods;
        }

        /// <summary>
        /// Invalidates the cached ordered mod list so the next call to
        /// <see cref="GetOrderedModIds"/> recomputes it. Called by the UI after
        /// the user changes the manual load order, and by the pipeline/Reload
        /// whenever the mod set is rebuilt.
        /// </summary>
        public void InvalidateOrder()
        {
            orderedModIds = null;
        }

        /// <summary>
        /// Returns mod IDs ordered by <c>depends</c> (topological sort),
        /// then by user-defined manual order, with alphabetical fallback
        /// for determinism. Dependencies always precede their dependents —
        /// manual ordering only applies between unrelated mods.
        /// When <paramref name="filterDisabled"/> is <c>true</c> (default),
        /// the result is cached and persisted, but only once the registry
        /// has been finalized: during the launcher boot window a main-thread
        /// caller would otherwise freeze an order computed from the
        /// half-built mod set for the rest of the session — and overwrite
        /// the saved manual order with an empty list. When
        /// <paramref name="filterDisabled"/> is <c>false</c>, the order is
        /// computed from <see cref="ModRegistry.AllDiscoveredMods"/> every
        /// call (no caching) and is NOT persisted to
        /// <see cref="ModLoadOrderStore"/> — this variant is intended for
        /// the mod configuration UI.
        /// </summary>
        public IReadOnlyList<string> GetOrderedModIds(bool filterDisabled = true)
        {
            registry.Initialize();

            // Initialize above already refused and logged when the launcher boot
            // owns the pipeline. Computing here would return a misleading empty
            // order built from a half-built registry, so refuse as well. The
            // pipeline's own re-entrant call from FinalizeGodotInit is exempt —
            // pipelineRunning marks it, exactly as in Initialize. Ends at
            // ModsReady: the starter-inventory seeding legitimately reads the
            // (now final) mod set during that phase.
            if (pipeline.IsLauncherBootWindow)
                return new List<string>();

            if (filterDisabled && orderedModIds != null)
                return orderedModIds;

            var source = filterDisabled ? mods : allDiscoveredMods;

            var dependsMap = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var kvp in source)
            {
                var manifest = ((ModInfo)kvp.Value).Manifest;
                dependsMap[kvp.Key] = manifest?.DependencyIds ?? new List<string>();
            }

            var manualOrder = ModLoadOrderStore.Sync(source.Keys.ToList());
            var result = ModLoadOrder.Compute(dependsMap, manualOrder);

            if (filterDisabled && registry.IsInitialized)
            {
                orderedModIds = result;
                ModLoadOrderStore.Save(orderedModIds);
            }

            GameLogger.Log("MODS_REGISTRY", $"GetOrderedModIds(filterDisabled={filterDisabled}): {result.Count} mod(s) ordered");
            for (int i = 0; i < result.Count; i++)
                GameLogger.Log("MODS_REGISTRY", $"  [{i}] {result[i]}", LogLevel.Debug);

            return result;
        }
    }
}
