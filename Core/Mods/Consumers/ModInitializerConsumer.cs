using System.Reflection;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// Delivers mod assemblies to <see cref="ModInitializerRegistry"/> — the
    /// lifecycle orchestrator that runs <c>OnModLoaded</c> catch-up. Order
    /// 1000: initializers must observe every registry already filled by the
    /// other consumers; on unload it runs first (reverse order) so mod code
    /// tears down before the registries drop its types.
    /// </summary>
    public class ModInitializerConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "mod_initializers";

        /// <inheritdoc />
        public int Order => 1000;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => ModInitializerRegistry.RegisterAssembly(assembly, modId);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => ModInitializerRegistry.NotifyModUnloading(modId);
    }
}
