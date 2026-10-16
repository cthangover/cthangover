using System.Collections.Generic;
using System.Reflection;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Scenes.TscnBuild.PropertyResolvers;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes.TscnBuild
{
    /// <summary>
    /// Auto-discovering ordered list of <see cref="IPropertyResolver"/> implementations.
    /// First-match-wins via <see cref="Find"/>. DefaultPropertyResolver is always moved to
    /// the end so catch-all doesn't shadow specific resolvers.
    /// <para/>
    /// The registry instance lives in <see cref="AppServices.Current"/>; the static
    /// members below are a facade delegating to it, so existing callers keep
    /// working unchanged.
    /// </summary>
    public class PropertyResolverRegistry : IGameRegistry
    {
        private List<IPropertyResolver> _resolvers;
        private bool built;

        /// <summary>Shared instance the static facade delegates to.</summary>
        private static PropertyResolverRegistry Owned => AppServices.Current.PropertyResolvers;

        /// <summary>
        /// Creates an empty registry: cheap and side-effect free per the
        /// IGameRegistry contract. <see cref="Rebuild"/> fills the data.
        /// </summary>
        internal PropertyResolverRegistry()
        {
        }

        /// <inheritdoc />
        public string Id => "tscn_property_resolvers";

        /// <summary>
        /// Scans all loaded assemblies for <see cref="IPropertyResolver"/>
        /// implementations unless already built. Keeps the old Initialize
        /// idempotence: later calls are no-ops until the registry is cleared.
        /// </summary>
        public void EnsureBuilt()
        {
            if (!built)
                Rebuild();
        }

        /// <summary>
        /// Forced full re-scan of all loaded assemblies (assembly order equals
        /// load order), replacing the previous list; DefaultPropertyResolver is
        /// bumped to the end again so the catch-all resolver stays last. Safe to
        /// call repeatedly; picks up assemblies loaded after the previous build.
        /// </summary>
        public void Rebuild()
        {
            var scanned = new List<IPropertyResolver>();
            foreach (var r in Reflections.FindAndCreate<IPropertyResolver>())
                scanned.Add(r);
            _resolvers = scanned;
            built = true;
            BumpDefaultToEnd();
        }

        /// <summary>
        /// Drops all discovered resolvers; the next build re-scans the assemblies.
        /// Called on mod reload through AppServices.ClearRegistries.
        /// </summary>
        public void Clear()
        {
            _resolvers = null;
            built = false;
        }

        // ---- Static facade (legacy API, unchanged for callers) ------------

        /// <summary>Legacy entry point; delegates to the shared instance.</summary>
        public static void Initialize() => Owned.EnsureBuilt();

        /// <summary>Scans a newly-loaded mod assembly for <see cref="IPropertyResolver"/> implementations and appends them, keeping DefaultPropertyResolver last.</summary>
        public static void RegisterAssembly(Assembly assembly)
            => Owned.AddFromAssembly(assembly);

        /// <summary>Returns the first resolver that can handle the given key/value pair, or null when none can.</summary>
        public static IPropertyResolver Find(string key, object value)
            => Owned.FindResolver(key, value);

        /// <summary>Unload pass: drops resolvers contributed by the given assembly. Delegates to the shared instance.</summary>
        public static void UnregisterAssembly(Assembly assembly)
            => Owned.RemoveFromAssembly(assembly);

        // ---- Instance implementation --------------------------------------

        private void AddFromAssembly(Assembly assembly)
        {
            EnsureBuilt();
            foreach (var r in Reflections.FindAndCreate<IPropertyResolver>(assembly))
                _resolvers.Add(r);
            BumpDefaultToEnd();
        }

        /// <summary>
        /// Removes resolvers whose concrete type comes from the given assembly —
        /// the unload pass of the assembly consumer pipeline. Core resolvers
        /// survive and the catch-all DefaultPropertyResolver stays last.
        /// </summary>
        public void RemoveFromAssembly(Assembly assembly)
        {
            if (_resolvers == null)
                return;
            _resolvers.RemoveAll(r => r.GetType().Assembly == assembly);
        }

        private void BumpDefaultToEnd()
        {
            var idx = _resolvers.FindIndex(r => r is DefaultPropertyResolver);
            if (idx >= 0)
            {
                var d = _resolvers[idx];
                _resolvers.RemoveAt(idx);
                _resolvers.Add(d);
            }
        }

        private IPropertyResolver FindResolver(string key, object value)
        {
            EnsureBuilt();
            foreach (var r in _resolvers)
                if (r.CanResolve(key, value))
                    return r;
            return null;
        }
    }
}
