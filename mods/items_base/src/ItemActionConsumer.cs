using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;
using Cthangover.Core.Mods.Consumers;

namespace Cthangover.Mods.Items
{
    /// <summary>
    /// Feeds <see cref="ItemActionFactory"/> with every mod's C#
    /// <see cref="IItemAction"/> implementations as the mods load (for example
    /// cooking's recipe actions) and removes them when their mod unloads, so a
    /// hot-reloaded mod's actions run its fresh code and the dying assembly is
    /// not pinned. GDScript-declared actions do not come from an assembly and
    /// still arrive through <see cref="ItemActionBridge"/>.
    /// </summary>
    public class ItemActionConsumer : ModTypeConsumer<IItemAction>
    {
        /// <inheritdoc />
        public override string Id => "items.actions";

        /// <inheritdoc />
        protected override void Add(IItemAction instance, string modId) => ItemActionFactory.Instance.Register(instance);

        /// <inheritdoc />
        protected override void Remove(IItemAction instance, string modId) => ItemActionFactory.Instance.Unregister(instance);
    }
}
