using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// JSON-serializable item descriptor collected by the entity resolver
    /// (<c>[EntityKind("item")]</c> — files declare <c>"Kind": "item"</c>).
    /// Typed fields cover the stock item schema; any additional JSON keys are
    /// captured by <see cref="ExtraProperties"/> and imported by
    /// <see cref="Factories.Impls.ItemFactory"/> into <see cref="Item.Properties"/>,
    /// so mods can attach custom data to items without schema changes.
    /// </summary>
    [Serializable]
    [EntityKind("item")]
    public class ItemInfo : IIdentifiable
    {
        /// <summary>Unique item ID, serialized as "Id" in JSON; path-style ids (e.g. "food/ration") double as save-data references.</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }
        /// <summary>Locale key for the item's display name, resolved from .properties files.</summary>
        public string Name { get; set; }
        /// <summary>Locale key for the item's tooltip/description text.</summary>
        public string Description { get; set; }
        /// <summary>Base purchase price in the game economy.</summary>
        public int    Cost { get; set; }
        /// <summary>Sprite path inside the mod's <c>items</c> texture group, resolved by <see cref="Factories.Impls.ItemSpriteFactory"/>.</summary>
        public string Sprite { get; set; }
        /// <summary>Category flags parsed from a comma-separated string (e.g. "Resource,Food") via the flags enum converter.</summary>
        [JsonConverter(typeof(FlagsStringEnumConverter<ItemType>))]
        public ItemType ItemType { get; set; }
        /// <summary>ID of the on-use behaviour, resolved through <see cref="Factories.Impls.ItemActionFactory"/>; "null"/empty means no use effect.</summary>
        public string ItemAction { get; set; }
        /// <summary>Captures every JSON key not matched by the typed fields; imported into <see cref="Item.Properties"/> at factory time.</summary>
        [JsonExtensionData]
        public Dictionary<string, object> ExtraProperties { get; set; }
    }
}
