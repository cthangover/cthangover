using Cthangover.Core.Mods.Consumers;

namespace Cthangover.Validator
{
    /// <summary>
    /// Feeds <see cref="ValidationStrategyRegistry"/> with every mod's
    /// <see cref="IValidationStrategy"/> implementations as the mods load and
    /// removes them when their mod unloads.
    /// </summary>
    public class ValidationStrategyConsumer : ModTypeConsumer<IValidationStrategy>
    {
        /// <inheritdoc />
        public override string Id => "validator.strategies";

        /// <inheritdoc />
        protected override void Add(IValidationStrategy instance, string modId) => ValidationStrategyRegistry.Register(instance);

        /// <inheritdoc />
        protected override void Remove(IValidationStrategy instance, string modId) => ValidationStrategyRegistry.Unregister(instance);
    }
}
