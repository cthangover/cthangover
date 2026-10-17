namespace Cthangover.Core.Mods.Consumers
{
    using System.Collections.Generic;
    using System.Reflection;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Base for a mod-owned plugin registry: "give me every <typeparamref name="T"/>
    /// any mod ships". A mod declares a subclass (public, parameterless) in its
    /// assembly and the kernel does the rest — the same consumer machinery the
    /// kernel's own registries use (<see cref="AssemblyConsumerRegistry"/>):
    /// <list type="bullet">
    /// <item>every mod assembly the pipeline registers is scanned for public,
    /// non-abstract <typeparamref name="T"/> implementations, one instance each
    /// is created and handed to <see cref="Add"/>;</item>
    /// <item>assemblies registered before the consumer itself (dependencies,
    /// and the consumer's own assembly) are replayed to it when it joins, so
    /// load order does not matter;</item>
    /// <item>when a mod is unloaded (game switch, hot reload) each instance it
    /// contributed comes back through <see cref="Remove"/> — the registry stays
    /// exact without rescanning the AppDomain, and never re-registers a dying
    /// assembly.</item>
    /// </list>
    /// This replaces the per-mod pattern of looking an assembly up by mod id
    /// and rescanning every loaded assembly "as a safety net": the kernel stays
    /// the single place that knows which assemblies exist.
    /// </summary>
    public abstract class ModTypeConsumer<T> : IAssemblyConsumer where T : class
    {
        private readonly Dictionary<Assembly, List<T>> created = new();

        /// <inheritdoc />
        public abstract string Id { get; }

        /// <summary>Normal registry range (see <see cref="IAssemblyConsumer.Order"/>).</summary>
        public virtual int Order => 500;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
        {
            if (created.ContainsKey(assembly))
                return;
            var instances = new List<T>(Reflections.FindAndCreate<T>(assembly));
            created[assembly] = instances;
            foreach (var instance in instances)
                Add(instance, modId);
        }

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
        {
            if (!created.Remove(assembly, out var instances))
                return;
            foreach (var instance in instances)
                Remove(instance, modId);
        }

        /// <summary>A new <typeparamref name="T"/> from mod <paramref name="modId"/>.</summary>
        protected abstract void Add(T instance, string modId);

        /// <summary>An instance previously passed to <see cref="Add"/> leaves with its mod.</summary>
        protected abstract void Remove(T instance, string modId);
    }
}
