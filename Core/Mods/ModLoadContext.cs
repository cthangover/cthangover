using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// One collectible <see cref="AssemblyLoadContext"/> per mod — the unit of
    /// assembly unload. Replaces the flat default-context load for mod
    /// assemblies: unloading a mod (reload, disable, uninstall) can now
    /// actually release its assemblies, JIT code and static state instead of
    /// rewriting registries over a still-loaded copy.
    ///
    /// Resolution rules, in order:
    /// 1. the mod's own staged assemblies (main dll and mod-shipped dll/),
    /// 2. assemblies of the mod's dependency mods — resolved through the
    ///    dependency's own context so inter-mod types are reference-equal
    ///    across both sides,
    /// 3. <c>null</c> — falls through to the default context, which holds the
    ///    kernel and GodotSharp (this is what makes "mods can use core types"
    ///    work without any special casing).
    ///
    /// File-backed loading keeps the debugger story of plan-mod-testing §9.2:
    /// a portable PDB written next to the dll binds by naming convention and
    /// breakpoints in mod code keep working.
    ///
    /// Unload is a protocol, not a button: the caller must run the consumer
    /// reverse fan-out (<c>AssemblyConsumerRegistry.FanOutUnloading</c>) and
    /// drop every external reference first; then <see cref="TryUnload"/>
    /// forces GC and asks the context to release. The returned diagnostic
    /// lists surviving assemblies when someone still holds a reference —
    /// that is the leak to fix, not a reason to retry.
    /// </summary>
    public sealed class ModLoadContext : AssemblyLoadContext
    {
        private readonly Dictionary<string, string> assemblyPaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly Func<string, ModLoadContext> dependencyResolver;
        private readonly string modId;

        /// <summary>
        /// Creates the context for one mod. <paramref name="dependencyResolver"/>
        /// maps a dependency mod id to its already-created context (or null when
        /// the dependency ships no assemblies of its own).
        /// </summary>
        public ModLoadContext(string modId, Func<string, ModLoadContext> dependencyResolver)
            : base("mod:" + modId, isCollectible: true)
        {
            this.modId = modId;
            this.dependencyResolver = dependencyResolver;
        }

        /// <summary>Mod id this context belongs to; also the context name suffix.</summary>
        public string ModId => modId;

        /// <summary>
        /// Registers a file-backed assembly (main compiled dll or a mod-shipped
        /// precompiled one) by its simple name. Must happen before the first
        /// resolve. Returns the assembly's simple name — the key for
        /// <see cref="LoadStaged"/>. It differs from the file name for cached
        /// compiled mods (<c>cooking_&lt;hash&gt;.dll</c> holds <c>cooking</c>).
        /// </summary>
        public string StageAssembly(string dllPath)
        {
            var name = AssemblyName.GetAssemblyName(dllPath).Name;
            assemblyPaths[name] = dllPath;
            return name;
        }

        /// <summary>True when the mod staged an assembly with this simple name.</summary>
        public bool HasAssembly(string simpleName)
            => assemblyPaths.ContainsKey(simpleName);

        /// <summary>
        /// Loads a staged assembly file-backed. Called by the loader for the
        /// mod's entry assembly; other assemblies resolve lazily.
        /// </summary>
        public Assembly LoadStaged(string simpleName)
        {
            if (!assemblyPaths.TryGetValue(simpleName, out var path))
                return null;
            return LoadFromAssemblyPath(path);
        }

        /// <inheritdoc />
        protected override Assembly Load(AssemblyName assemblyName)
        {
            var name = assemblyName?.Name;
            if (string.IsNullOrEmpty(name))
                return null;

            // 1. Own assemblies.
            if (assemblyPaths.TryGetValue(name, out var path))
                return LoadFromAssemblyPath(path);

            // 2. Dependency mod assemblies — through the dependency's context,
            //    so a type from the dependency is reference-equal on both sides.
            var dependency = dependencyResolver?.Invoke(name);
            if (dependency != null && dependency.HasAssembly(name))
                return dependency.LoadStaged(name);

            // 3. Default context: kernel, GodotSharp, framework.
            return null;
        }

        /// <inheritdoc />
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            // Native libraries keep the ModNativeLoader path; no custom probing.
            => IntPtr.Zero;

        /// <summary>
        /// Attempts to unload the context: drops the path table, runs GC until
        /// no progress, then unloads. Returns null on success; a diagnostic
        /// string when the caller should verify collection separately (see
        /// <see cref="CollectAndVerify"/>) — the honest signal lives in the
        /// caller's <c>WeakReference</c>, because a weak reference taken
        /// inside this very frame would see <c>this</c> alive by definition.
        /// </summary>
        public string TryUnload()
        {
            // Drop our own path table first so nothing resolves into us anymore.
            assemblyPaths.Clear();

            for (var attempts = 0; attempts < 5; attempts++)
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
            }

            Unload();
            return null;
        }

        /// <summary>
        /// Collection verification for the unload protocol: after
        /// <see cref="TryUnload"/> and dropping every external reference, the
        /// caller's weak reference must die. Returns null when the context
        /// was collected, otherwise a diagnostic naming the surviving
        /// assemblies — the leak to fix.
        /// </summary>
        public static string CollectAndVerify(WeakReference contextRef, string modId)
        {
            for (var attempts = 0; attempts < 30 && contextRef.IsAlive; attempts++)
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                // Yield: under parallel test load other threads keep the GC
                // busy; a short pause lets pending collections settle.
                if (contextRef.IsAlive)
                    Thread.Sleep(10);
            }

            if (!contextRef.IsAlive)
                return null;

            var survivors = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => string.Equals(a.GetName().Name, modId, StringComparison.OrdinalIgnoreCase))
                .Select(a => a.GetName().Name)
                .Distinct()
                .ToList();
            return survivors.Count > 0
                ? $"assemblies still alive: {string.Join(", ", survivors)}"
                : "context still referenced (types or delegates held outside the registries)";
        }
    }
}
