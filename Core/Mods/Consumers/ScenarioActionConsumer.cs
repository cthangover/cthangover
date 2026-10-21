using System.Reflection;
using Cthangover.Core.Factories.Impls;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// Registers mod-shipped DSL scenario actions (<c>IScenarioAction</c>)
    /// into <see cref="ScenarioActionFactory"/>.
    /// </summary>
    public class ScenarioActionConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "scenario_actions";

        /// <inheritdoc />
        public int Order => 180;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
            => ScenarioActionFactory.Instance.RegisterAssembly(assembly, modId);

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => ScenarioActionFactory.Instance.UnregisterAssembly(assembly);
    }
}
