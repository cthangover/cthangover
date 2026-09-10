using System.Reflection;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes.TscnBuild
{
    /// <summary>
    /// Auto-discovering registry of <see cref="ISubResourceBuilder"/> implementations, indexed by ResourceType.
    /// <para/>
    /// Initial scan on first access picks up all already-loaded assemblies (including core).
    /// <see cref="RegisterAssembly"/> is called by <see cref="Mods.ModAssemblyLoader.RegisterAssembly"/>
    /// when a mod DLL is loaded, adding the mod's builders without re-scanning everything.
    /// First-registered-wins — core builders always take priority over mod-added ones.
    /// </summary>
    public static class SubResourceBuilderRegistry
    {
        private static System.Collections.Generic.Dictionary<string, ISubResourceBuilder> _builders;

        /// <summary>Lazy-initialises the registry by scanning all loaded assemblies for <see cref="ISubResourceBuilder"/> implementations. Idempotent.</summary>
        public static void Initialize() { if (_builders != null) return; _builders = new System.Collections.Generic.Dictionary<string, ISubResourceBuilder>(); foreach (var b in Reflections.FindAndCreate<ISubResourceBuilder>()) if (!_builders.ContainsKey(b.ResourceType)) _builders[b.ResourceType] = b; }

        /// <summary>Scans a newly-loaded mod assembly for <see cref="ISubResourceBuilder"/> implementations and registers them. Called from <see cref="Mods.ModAssemblyLoader.RegisterAssembly"/>.</summary>
        public static void RegisterAssembly(Assembly assembly) { Initialize(); foreach (var b in Reflections.FindAndCreate<ISubResourceBuilder>(assembly)) if (!_builders.ContainsKey(b.ResourceType)) _builders[b.ResourceType] = b; }

        /// <summary>Looks up a builder by resource type name. Returns null if no builder is registered for the given type.</summary>
        public static ISubResourceBuilder Get(string type) { Initialize(); _builders.TryGetValue(type, out var b); return b; }
    }
}
