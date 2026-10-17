using Cthangover.Core.Mods.Consumers;

namespace Cthangover.Mods.Dialog.Scenarios
{
    /// <summary>
    /// Feeds <see cref="ScenarioCommandStrategyFactory"/> with every mod's
    /// <see cref="IScenarioCommandStrategy"/> implementations as the mods load
    /// (dialog_base's own built-in commands included, through the join-time
    /// catch-up) and removes them when their mod unloads, so a hot-reloaded
    /// mod's commands run its fresh code and the dying assembly is not pinned.
    /// </summary>
    public class ScenarioCommandStrategyConsumer : ModTypeConsumer<IScenarioCommandStrategy>
    {
        /// <inheritdoc />
        public override string Id => "dialog.command_strategies";

        /// <inheritdoc />
        protected override void Add(IScenarioCommandStrategy instance, string modId) => ScenarioCommandStrategyFactory.Register(instance);

        /// <inheritdoc />
        protected override void Remove(IScenarioCommandStrategy instance, string modId) => ScenarioCommandStrategyFactory.Unregister(instance);
    }
}
