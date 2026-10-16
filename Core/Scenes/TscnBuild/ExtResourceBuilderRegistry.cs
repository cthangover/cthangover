using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes.TscnBuild
{
    /// <summary>
    /// Auto-discovering registry of <see cref="IExtResourceBuilder"/> implementations, indexed by ResourceType.
    /// <para/>
    /// Initial scan on first access picks up all already-loaded assemblies (including core).
    /// <see cref="RegisterAssembly"/> is called by <see cref="Mods.ModAssemblyLoader.RegisterAssembly"/>
    /// when a mod DLL is loaded, adding the mod's builders without re-scanning everything.
    /// First-registered-wins — core builders always take priority over mod-added ones.
    /// <para/>
    /// The registry instance lives in <see cref="AppServices.Current"/>; the static
    /// members below are a facade delegating to it, so existing callers keep
    /// working unchanged.
    /// </summary>
    public class ExtResourceBuilderRegistry : IGameRegistry
    {
        private Dictionary<string, IExtResourceBuilder> _builders;
        private bool built;

        /// <summary>Shared instance the static facade delegates to.</summary>
        private static ExtResourceBuilderRegistry Owned => AppServices.Current.ExtResourceBuilders;

        /// <summary>
        /// Creates an empty registry: cheap and side-effect free per the
        /// IGameRegistry contract. <see cref="Rebuild"/> fills the data.
        /// </summary>
        internal ExtResourceBuilderRegistry()
        {
        }

        /// <inheritdoc />
        public string Id => "tscn_ext_builders";

        /// <summary>
        /// Scans all loaded assemblies for <see cref="IExtResourceBuilder"/>
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
        /// load order, so first-registered-wins keeps core builders ahead of
        /// mod-added ones), replacing the previous table. Safe to call
        /// repeatedly; picks up assemblies loaded after the previous build.
        /// </summary>
        public void Rebuild()
        {
            var scanned = new Dictionary<string, IExtResourceBuilder>();
            foreach (var b in Reflections.FindAndCreate<IExtResourceBuilder>())
                if (!scanned.ContainsKey(b.ResourceType))
                    scanned[b.ResourceType] = b;
            _builders = scanned;
            built = true;
        }

        /// <summary>
        /// Drops all registered builders; the next build re-scans the assemblies.
        /// Called on mod reload through AppServices.ClearRegistries.
        /// </summary>
        public void Clear()
        {
            _builders = null;
            built = false;
        }

        // ---- Static facade (legacy API, unchanged for callers) ------------

        /// <summary>Legacy entry point; delegates to the shared instance.</summary>
        public static void Initialize() => Owned.EnsureBuilt();

        /// <summary>Scans a newly-loaded mod assembly for <see cref="IExtResourceBuilder"/> implementations and registers them. Called from <see cref="Mods.ModAssemblyLoader.RegisterAssembly"/>.</summary>
        public static void RegisterAssembly(Assembly assembly)
            => Owned.AddFromAssembly(assembly);

        /// <summary>Returns the builder registered for the given resource type, or null when none is.</summary>
        public static IExtResourceBuilder Get(string type)
            => Owned.GetBuilder(type);

        /// <summary>Unload pass: drops builders contributed by the given assembly. Delegates to the shared instance.</summary>
        public static void UnregisterAssembly(Assembly assembly)
            => Owned.RemoveFromAssembly(assembly);

        // ---- Instance implementation --------------------------------------

        private void AddFromAssembly(Assembly assembly)
        {
            EnsureBuilt();
            foreach (var b in Reflections.FindAndCreate<IExtResourceBuilder>(assembly))
                if (!_builders.ContainsKey(b.ResourceType))
                    _builders[b.ResourceType] = b;
        }

        /// <summary>
        /// Removes builders whose concrete type comes from the given assembly —
        /// the unload pass of the assembly consumer pipeline. Built-in builders
        /// survive; a slot left empty falls back to a full rebuild on next use.
        /// </summary>
        public void RemoveFromAssembly(Assembly assembly)
        {
            if (_builders == null)
                return;
            foreach (var key in _builders.Where(kvp => kvp.Value.GetType().Assembly == assembly).Select(kvp => kvp.Key).ToList())
                _builders.Remove(key);
        }

        private IExtResourceBuilder GetBuilder(string type)
        {
            EnsureBuilt();
            _builders.TryGetValue(type, out var b);
            return b;
        }
    }
}
