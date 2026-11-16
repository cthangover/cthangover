using System.Collections.Generic;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Scenes;

namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Mod-derived registry of scene definitions. The collection itself is
    /// delegated to <see cref="SceneDefinitionResolver"/> through
    /// <see cref="ModManager.Instance.GetResolver{T}"/>; this class adds an
    /// instance-level cache so repeated collects do not re-resolve.
    /// <para/>
    /// The registry instance lives in <see cref="AppServices.Current"/>
    /// (AppServices.SceneDefs) and participates in the data-driven rebuild loop;
    /// the static members below are a facade delegating to it, so existing
    /// callers keep working unchanged.
    /// </summary>
    public class SceneDefinitions : IGameRegistry
    {
        private Dictionary<string, List<SceneDefinition>> cache;

        /// <summary>Shared instance the static facade delegates to.</summary>
        private static SceneDefinitions Owned => AppServices.Current.SceneDefs;

        /// <summary>
        /// Creates an empty registry: cheap and side-effect free per the
        /// IGameRegistry contract.
        /// </summary>
        internal SceneDefinitions()
        {
        }

        /// <summary>Registry key used by the application rebuild and clear lifecycle.</summary>
        public string Id => "scene_defs";

        /// <summary>
        /// Re-collects all scene definitions from every loaded mod, dropping the
        /// cached result first so a hot-reloaded mod set is picked up. The
        /// resolver re-reads only if its own cache was flushed (ModManager.Reload
        /// does that before the registries are rebuilt).
        /// </summary>
        public void Rebuild()
        {
            Clear();
            CollectAll();
        }

        /// <summary>Drops the cached collection; the next collect re-resolves.</summary>
        public void Clear()
        {
            cache = null;
        }

        /// <summary>
        /// Collects all scene definitions from every loaded mod, caching the
        /// result. The first mod that defines a scene name becomes the base;
        /// subsequent mods merge their values on top. Returns null when no
        /// <see cref="SceneDefinitionResolver"/> is registered.
        /// </summary>
        public Dictionary<string, List<SceneDefinition>> CollectAll()
        {
            if (cache != null)
                return cache;

            var collected = ModManager.Instance.GetResolver<SceneDefinitionResolver>()?.Collect();
            if (collected == null)
                return null;

            cache = collected;
            return cache;
        }

        // ---- Static facade (legacy API, unchanged for callers) ------------

        /// <summary>Legacy entry point; delegates to the shared instance.</summary>
        public static Dictionary<string, List<SceneDefinition>> Collect()
        {
            return Owned.CollectAll();
        }
    }
}
