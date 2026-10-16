using System.Reflection;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// Registers <c>IModAssetResolver</c> implementations from mod assemblies
    /// into <see cref="ModManager"/>. Unload removes only the resolvers whose
    /// concrete types come from the unloading assembly.
    /// </summary>
    public class ModAssetResolverConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "mod_asset_resolvers";

        /// <inheritdoc />
        public int Order => 100;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => ModManager.Instance.RegisterResolversFromAssembly(assembly);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => ModManager.Instance.UnregisterResolversFromAssembly(assembly);
    }
}
