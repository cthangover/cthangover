using System.Reflection;
using Cthangover.Core.Mods.Discovery;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// Registers mod-shipped <c>IModScanner</c>/<c>IModLoader</c>
    /// implementations into <see cref="ModDiscoveryRegistry"/>.
    /// </summary>
    public class ModDiscoveryConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "mod_discovery";

        /// <inheritdoc />
        public int Order => 130;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => ModDiscoveryRegistry.RegisterAssembly(assembly);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => ModDiscoveryRegistry.UnregisterAssembly(assembly);
    }
}
