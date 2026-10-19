using System.Reflection;
using Cthangover.Core.Scenes;

namespace Cthangover.Core.Mods.Consumers
{
    /// <summary>
    /// Drops the parsed condition cache of <see cref="ScenarioCondition"/>
    /// before a mod assembly unloads. Cached trees hold leaf nodes created by
    /// mod parsers (quest conditions of <c>quests_base</c>); without the reset
    /// a game switch that unloads such a mod leaves gutted trees in the cache,
    /// and every condition of the next game that uses them throws
    /// (<c>failed to create event 'morning_items_ensure'</c> after returning to
    /// <c>cthangover</c>). Loading needs nothing: trees are parsed on demand.
    /// </summary>
    public class ScenarioConditionCacheConsumer : IAssemblyConsumer
    {
        /// <inheritdoc />
        public string Id => "scenario_condition_cache";

        /// <inheritdoc />
        public int Order => 170;

        /// <inheritdoc />
        public void OnAssemblyLoaded(Assembly assembly, string modId)
        {
        }

        /// <inheritdoc />
        public void OnAssemblyUnloading(Assembly assembly, string modId)
            => ScenarioCondition.ClearCache();
    }
}
