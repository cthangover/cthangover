using System.Collections.Generic;
using System.Reflection;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Tracking record for one mod's loaded assemblies inside its
    /// <see cref="ModLoadContext"/>: which assemblies were fanned out to the
    /// consumer registry and under which mod id. The unload path walks these
    /// records — the exact reverse of what was registered gets unregistered,
    /// so a mod's presence in the registries is fully symmetric.
    /// </summary>
    public sealed class LoadedModAssembly
    {
        /// <summary>Mod id the assemblies were registered under.</summary>
        public string ModId;

        /// <summary>The mod's collectible context; null when loaded in the default context.</summary>
        public ModLoadContext Context;

        /// <summary>Assemblies that went through the consumer fan-out, in registration order.</summary>
        public List<Assembly> Assemblies = new();
    }
}
