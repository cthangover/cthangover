using Cthangover.Core.Items;
using Cthangover.Core.Settings;

namespace Cthangover.Mods.Items
{
    /// <summary>
    /// Item action that needs to know whom it is applied to. The core
    /// <see cref="IItemAction"/> contract has no target, so an effect on a party
    /// member (healing) could not tell which member was picked in
    /// <see cref="Cthangover.Core.UI.Inventory.CharacterTargetPanel"/>. Items with
    /// <see cref="ItemType.TargetUsed"/> whose action implements this interface get
    /// <see cref="UseOn"/> with the chosen character; other actions keep receiving
    /// the plain <see cref="IItemAction.UseAction"/>.
    /// </summary>
    public interface ITargetedItemAction : IItemAction
    {
        /// <summary>
        /// Applies the item to <paramref name="target"/>. Returns <c>false</c> when
        /// nothing happened (e.g. healing at full health), so the item is not spent.
        /// </summary>
        bool UseOn(IItem item, CharacterInfoData target);
    }
}
