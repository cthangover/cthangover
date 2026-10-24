namespace Cthangover.Mods.Equipment
{
    using Cthangover.Core.Items;
    using Cthangover.Core.Settings;
    using Cthangover.Mods.Items;

    /// <summary>
    /// The item action <c>"ItemAction": "action/equip"</c>. With the item type
    /// <c>TargetUsed</c> the inventory's own "apply to whom" picker chooses the party member,
    /// and the inventory takes the item away once this returns true — so gear needs no
    /// equipment screen of its own to be put on. The replaced item comes back to the bag.
    /// </summary>
    public class EquipItemAction : ITargetedItemAction
    {
        /// <inheritdoc />
        public string ID => "action/equip";

        /// <inheritdoc />
        public bool UseAction(IItem item) => false;

        /// <inheritdoc />
        public bool UseOn(IItem item, CharacterInfoData target)
        {
            if (item == null || target == null)
                return false;
            // The inventory removes the applied item itself, so the action must not take it too.
            return EquipmentService.Equip(target.ID, item.ID, null, fromInventory: false);
        }
    }
}
