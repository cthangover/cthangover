namespace Cthangover.Mods.Stats
{
    using Cthangover.Core.Mods.Consumers;

    /// <summary>Registers every <see cref="ITraitSource"/> of a loaded mod assembly and drops them when it unloads.</summary>
    public class TraitSourceConsumer : ModTypeConsumer<ITraitSource>
    {
        /// <inheritdoc />
        public override string Id => "stats.trait_sources";

        /// <inheritdoc />
        protected override void Add(ITraitSource instance, string modId) => TraitSources.Add(instance);

        /// <inheritdoc />
        protected override void Remove(ITraitSource instance, string modId) => TraitSources.Remove(instance);
    }
}
