namespace Cthangover.Core.Mods.Host
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Runtime.Loader;
    using Godot;

    /// <summary>
    /// Owns only the emitted script. External type references bind to existing game assemblies,
    /// including the exact live copies of explicitly requested mods.
    /// </summary>
    internal sealed class DebugScriptLoadContext : AssemblyLoadContext
    {
        private static readonly Assembly Core = typeof(IDebugRuntimeScript).Assembly;
        private static readonly Assembly GodotAssembly = typeof(SceneTree).Assembly;
        private readonly IReadOnlyDictionary<string, Assembly> mods;

        internal DebugScriptLoadContext(string name, IReadOnlyDictionary<string, Assembly> mods)
            : base(name, isCollectible: true)
        {
            this.mods = mods;
        }

        /// <summary>Resolves explicit game references and leaves .NET runtime assemblies to the default context.</summary>
        protected override Assembly Load(AssemblyName assemblyName)
        {
            var name = assemblyName.Name;
            if (string.Equals(name, Core.GetName().Name, StringComparison.OrdinalIgnoreCase))
                return Core;
            if (string.Equals(name, GodotAssembly.GetName().Name, StringComparison.OrdinalIgnoreCase))
                return GodotAssembly;
            return name != null && mods.TryGetValue(name, out var assembly) ? assembly : null;
        }
    }
}
