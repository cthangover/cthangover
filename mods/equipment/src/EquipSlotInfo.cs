namespace Cthangover.Mods.Equipment
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Cthangover.Core.Factories;

    /// <summary>
    /// A place a character wears something (<c>"Kind": "equip_slot"</c>): weapon, body,
    /// accessory in an RPG; outfit and jewellery in a dating sim; a cyberware socket in a
    /// shooter. Slots are data, so each game has its own set and a mod adds a slot
    /// ("a second ring") without code.
    /// <para/>
    /// An item fits a slot when the item's <c>"Equip"</c> key names one of the slot's
    /// <see cref="Accepts"/> types, or the slot id itself when the slot lists none.
    /// </summary>
    [EntityKind("equip_slot")]
    public class EquipSlotInfo : IIdentifiable
    {
        /// <summary>Slot id (<c>weapon</c>, <c>body</c>, <c>ring_left</c>).</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        /// <summary>Locale key of the slot's name.</summary>
        public string Name { get; set; }

        /// <summary>Display order in the sheet (lower first).</summary>
        public int Order { get; set; }

        /// <summary>Item equip types the slot takes; empty = items whose type is the slot id.</summary>
        public List<string> Accepts { get; set; } = new();
    }
}
