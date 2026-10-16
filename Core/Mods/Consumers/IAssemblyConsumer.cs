using System;
using System.Reflection;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// One reflection-driven subsystem that consumes mod assemblies. The
    /// declarative successor of the hard-coded fan-out list in
    /// <see cref="Cthangover.Core.Mods.ModAssemblyLoader.RegisterAssembly"/>:
    /// every subsystem that used to be one line there becomes a consumer
    /// class, discovered automatically in the kernel assembly, and new
    /// subsystems no longer edit shared files.
    ///
    /// The contract is symmetric on purpose - <see cref="OnAssemblyUnloading"/>
    /// is the reverse pass that makes collectible assembly unload possible.
    /// A consumer that cannot release everything it took (Godot nodes alive,
    /// delegates captured elsewhere) must log loudly here, because a silent
    /// leak keeps the <c>AssemblyLoadContext</c> alive forever and the unload
    /// silently degrades to the old "registry rewrite" behaviour.
    ///
    /// Consumers may also be contributed by mod assemblies themselves
    /// (discovered by <see cref="AssemblyConsumerRegistry.RegisterFromAssembly"/>),
    /// giving a mod the ability to own its own reflection registry - for
    /// example a registry of custom status effects - and automatically
    /// receive every assembly loaded after it, without any kernel edit.
    /// </summary>
    public interface IAssemblyConsumer
    {
        /// <summary>Stable consumer id, used in logs and for duplicate detection.</summary>
        string Id { get; }

        /// <summary>
        /// Explicit execution order, replacing the implicit order of the old
        /// fan-out lines. Lower runs earlier. Ranges: 0-99 infrastructure
        /// bridges (buffers), 100-899 normal registries, 900+ lifecycle
        /// orchestrators that must observe fully populated registries
        /// (the initializer registry runs last at 1000).
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Receives a newly loaded mod assembly. Must not throw for expected
        /// conditions (no matching types); the registry isolates unexpected
        /// failures per consumer so one broken subsystem cannot break the
        /// others.
        /// </summary>
        void OnAssemblyLoaded(Assembly assembly, string modId);

        /// <summary>
        /// Reverse pass before an assembly is unloaded: drop every reference
        /// this consumer took through <see cref="OnAssemblyLoaded"/> for the
        /// given mod. Called in reverse consumer order (lifecycle orchestrators
        /// first). Must not throw; report failures through the log.
        /// </summary>
        void OnAssemblyUnloading(Assembly assembly, string modId);
    }
}
