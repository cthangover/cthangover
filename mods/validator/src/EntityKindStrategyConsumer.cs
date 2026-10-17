using Cthangover.Core.Mods.Consumers;

namespace Cthangover.Validator
{
    /// <summary>
    /// Feeds <see cref="EntityKindStrategyRegistry"/> with every mod's
    /// <see cref="IEntityKindStrategy"/> implementations as the mods load and
    /// removes them when their mod unloads.
    /// </summary>
    public class EntityKindStrategyConsumer : ModTypeConsumer<IEntityKindStrategy>
    {
        /// <inheritdoc />
        public override string Id => "validator.entity_kinds";

        /// <inheritdoc />
        protected override void Add(IEntityKindStrategy instance, string modId) => EntityKindStrategyRegistry.Register(instance);

        /// <inheritdoc />
        protected override void Remove(IEntityKindStrategy instance, string modId) => EntityKindStrategyRegistry.Unregister(instance);
    }
}
