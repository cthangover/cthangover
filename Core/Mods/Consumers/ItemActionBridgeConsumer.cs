using System.Reflection;
using Cthangover.Core.Items;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// Delivers mod assemblies to <see cref="ItemActionBridge"/> — the buffered
    /// delivery bridge to the items mod's action factory.
    /// </summary>
    public class ItemActionBridgeConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "item_action_bridge";

        /// <inheritdoc />
        public int Order => 11;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => ItemActionBridge.RegisterAssembly(assembly);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => ItemActionBridge.UnregisterAssembly(assembly);
    }
}
