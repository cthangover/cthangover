namespace Cthangover.Mods.Equipment
{
    using Cthangover.Core.Items;
    using Cthangover.Core.Settings;
    using Cthangover.Mods.Items;
    using Cthangover.Mods.Stats;

    /// <summary>
    /// The item action <c>"ItemAction": "action/stat_up"</c>: a tonic, a seed, a book that
    /// permanently raises stats of the party member it is applied to, by the item's own
    /// <c>"StatUp"</c> key (<c>{"str": 1}</c>, negative lowers). It lives here because this
    /// mod is where items meet the stats; it uses the inventory's target picker like gear.
    /// </summary>
    public class StatUpItemAction : ITargetedItemAction
    {
        /// <inheritdoc />
        public string ID => "action/stat_up";

        /// <inheritdoc />
        public bool UseAction(IItem item) => false;

        /// <inheritdoc />
        public bool UseOn(IItem item, CharacterInfoData target)
        {
            if (item == null || target == null)
                return false;
            var ups = ExtraData.Read<System.Collections.Generic.Dictionary<string, double>>(EquipmentService.Item(item.ID)?.ExtraProperties, "StatUp", item.ID);
            if (ups == null || ups.Count == 0)
                return false;
            foreach (var pair in ups)
                StatsService.AddBonus(target.ID, pair.Key, pair.Value);
            return true;
        }
    }
}
