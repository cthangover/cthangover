using System.Reflection;
using Cthangover.Core.Scenarios;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// Delivers mod assemblies to <see cref="ScenarioCommandBridge"/> — the
    /// buffered delivery bridge to the dialog_base mod's strategy factory.
    /// Bridge order (first decade) so the buffering happens before any
    /// registry that might pull strategies.
    /// </summary>
    public class ScenarioCommandBridgeConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "scenario_command_bridge";

        /// <inheritdoc />
        public int Order => 10;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => ScenarioCommandBridge.RegisterAssembly(assembly);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => ScenarioCommandBridge.UnregisterAssembly(assembly);
    }
}
