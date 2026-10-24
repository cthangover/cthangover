namespace Cthangover.Mods.Equipment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Events;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Settings;
    using Cthangover.Mods.Stats;

    /// <summary>
    /// Who wears what. An item is equipment when its JSON has an <c>"Equip"</c> key (its
    /// equip type); what it does while worn is its <c>"Traits"</c> list, read by the stats
    /// through <see cref="EquipmentTraitSource"/>. Neither key needs the items mod to know
    /// about equipment, so any game's items — and items of other mods, through patches —
    /// become gear by data alone. An optional <c>"Archetypes"</c> list limits who may wear
    /// an item (a staff for mages only).
    /// <para/>
    /// Equipping takes the item from the inventory and returns the one it replaces; taking
    /// off returns the item. Every change tells the stats (sheets, battle attributes and
    /// conditions follow) and publishes <see cref="EquipmentChangedEvent"/>.
    /// </summary>
    public static class EquipmentService
    {
        /// <summary>The running game's worn items (replaced on a new game and a load).</summary>
        public static EquipmentStateData Data { get; set; } = new();

        /// <summary>Slots of the loaded mods in display order.</summary>
        public static IReadOnlyList<EquipSlotInfo> Slots =>
            Entities.Collect<EquipSlotInfo>().Values.Where(s => !string.IsNullOrWhiteSpace(s.ID))
                .OrderBy(s => s.Order).ThenBy(s => s.ID, StringComparer.Ordinal).ToList();

        /// <summary>The slot <paramref name="id"/>, or null.</summary>
        public static EquipSlotInfo Slot(string id)
            => Slots.FirstOrDefault(s => string.Equals(s.ID, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>The item definition <paramref name="itemId"/>, or null.</summary>
        public static ItemInfo Item(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;
            var all = Entities.Collect<ItemInfo>();
            if (all.TryGetValue(itemId, out var info))
                return info;
            return all.Values.FirstOrDefault(i => string.Equals(i.ID, itemId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The equip type of an item (its <c>"Equip"</c> key), or null for an item that is not gear.</summary>
        public static string EquipType(string itemId) => ExtraData.Text(Item(itemId)?.ExtraProperties, "Equip");

        /// <summary>Whether the item can be worn at all.</summary>
        public static bool IsEquipment(string itemId) => !string.IsNullOrWhiteSpace(EquipType(itemId));

        /// <summary>Whether <paramref name="slot"/> takes items of <paramref name="equipType"/>.</summary>
        public static bool Accepts(EquipSlotInfo slot, string equipType)
        {
            if (slot == null || string.IsNullOrWhiteSpace(equipType))
                return false;
            return slot.Accepts == null || slot.Accepts.Count == 0
                ? string.Equals(slot.ID, equipType, StringComparison.OrdinalIgnoreCase)
                : slot.Accepts.Any(a => string.Equals(a, equipType, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The slots an item fits, in display order.</summary>
        public static List<EquipSlotInfo> SlotsFor(string itemId)
        {
            var type = EquipType(itemId);
            return Slots.Where(s => Accepts(s, type)).ToList();
        }

        /// <summary>Slot id → item id of what <paramref name="who"/> wears (empty when nothing).</summary>
        public static IReadOnlyDictionary<string, string> Worn(string who)
        {
            var id = StatsService.Resolve(who);
            return Find(id) ?? new Dictionary<string, string>();
        }

        /// <summary>The item in a slot of <paramref name="who"/>, or null.</summary>
        public static string ItemIn(string who, string slot)
            => slot != null && Worn(who).TryGetValue(slot, out var item) ? item : null;

        /// <summary>Whether <paramref name="who"/> wears <paramref name="itemId"/> in any slot.</summary>
        public static bool Wears(string who, string itemId)
            => Worn(who).Values.Any(i => string.Equals(i, itemId, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Whether <paramref name="who"/> may wear the item (in <paramref name="slot"/>, or in
        /// any slot that fits); <paramref name="reasonKey"/> is the locale key of why not.
        /// </summary>
        public static bool CanEquip(string who, string itemId, string slot, out string reasonKey)
        {
            reasonKey = null;
            if (!IsEquipment(itemId))
            {
                reasonKey = "equipment/not_gear";
                return false;
            }
            var fits = slot == null ? SlotsFor(itemId).Count > 0 : Accepts(Slot(slot), EquipType(itemId));
            if (!fits)
            {
                reasonKey = "equipment/no_slot";
                return false;
            }
            var allowed = ExtraData.Read<List<string>>(Item(itemId)?.ExtraProperties, "Archetypes", itemId);
            if (allowed is { Count: > 0 } && !allowed.Any(a => string.Equals(a, StatsService.ArchetypeOf(who), StringComparison.OrdinalIgnoreCase)))
            {
                reasonKey = "equipment/wrong_archetype";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Puts the item on <paramref name="who"/>. Without a slot it takes the first fitting
        /// empty slot, else the first fitting one. With <paramref name="fromInventory"/> the
        /// item must be in the inventory and one is taken from it; the replaced item goes back
        /// to the inventory either way. False (nothing changed) when the item cannot be worn.
        /// </summary>
        public static bool Equip(string who, string itemId, string slot = null, bool fromInventory = true)
        {
            var id = StatsService.Resolve(who);
            if (!CanEquip(id, itemId, slot, out _))
                return false;
            var target = slot != null ? Slot(slot) : PickSlot(id, itemId);
            if (target == null)
                return false;
            var inventory = Inventory;
            if (fromInventory && (inventory == null || !inventory.Remove(itemId, 1)))
                return false;

            var worn = Of(id);
            if (worn.TryGetValue(target.ID, out var previous) && !string.IsNullOrEmpty(previous))
                inventory?.Add(previous, 1);
            worn[target.ID] = Item(itemId)?.ID ?? itemId;
            Changed(id);
            return true;
        }

        /// <summary>Takes off what <paramref name="who"/> wears in <paramref name="slot"/>, back into the inventory.</summary>
        public static bool Unequip(string who, string slot, bool toInventory = true)
        {
            var id = StatsService.Resolve(who);
            var worn = Find(id);
            if (worn == null || slot == null || !worn.TryGetValue(slot, out var item))
                return false;
            worn.Remove(slot);
            if (toInventory && !string.IsNullOrEmpty(item))
                Inventory?.Add(item, 1);
            Changed(id);
            return true;
        }

        /// <summary>Back to a new game: nobody wears anything.</summary>
        public static void Reset()
        {
            Data = new EquipmentStateData();
            StatsService.Changed(null);
        }

        private static IInventory Inventory => GameData.Instance?.Runtime?.Inventory;

        private static EquipSlotInfo PickSlot(string who, string itemId)
        {
            var fitting = SlotsFor(itemId);
            var worn = Find(who);
            return fitting.FirstOrDefault(s => worn == null || !worn.ContainsKey(s.ID)) ?? fitting.FirstOrDefault();
        }

        private static Dictionary<string, string> Find(string who)
        {
            if (who == null)
                return null;
            if (Data.Characters.TryGetValue(who, out var worn))
                return worn;
            return Data.Characters.FirstOrDefault(p => string.Equals(p.Key, who, StringComparison.OrdinalIgnoreCase)).Value;
        }

        private static Dictionary<string, string> Of(string who)
        {
            var worn = Find(who);
            if (worn != null)
                return worn;
            worn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Data.Characters[who] = worn;
            return worn;
        }

        private static void Changed(string who)
        {
            StatsService.Changed(who);
            ModEventBus.Publish(new EquipmentChangedEvent { CharacterId = who });
        }
    }
}
