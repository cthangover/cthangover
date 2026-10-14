using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// Concrete item instance produced by <see cref="Factories.Impls.ItemFactory"/>
    /// from an <see cref="ItemInfo"/> DTO. The factory builds a fresh instance per
    /// <c>Get</c> call (resolving the sprite and the on-use action by id), so
    /// callers may mutate their copy freely — inventories share the definition
    /// fields but each slot only tracks its own count.
    /// </summary>
    public class Item : IItem
    {
        /// <summary>Unique item ID (path-style, e.g. <c>"food/ration"</c>); the factory lookup key.</summary>
        public string      ID          { get; set; }
        /// <summary>Display name (resolved locale string).</summary>
        public string      Name        { get; set; }
        /// <summary>Tooltip/description text (resolved locale string).</summary>
        public string      Description { get; set; }
        /// <summary>Base purchase price in the game economy.</summary>
        public int         Cost        { get; set; }
        /// <summary>Icon texture resolved from the mod's <c>items</c> group via <see cref="Factories.Impls.ItemSpriteFactory"/>.</summary>
        public Texture2D   Sprite      { get; set; }
        /// <summary><see cref="ItemType"/> flags describing the item's categories.</summary>
        public ItemType    ItemType    { get; set; }
        /// <summary>On-use behaviour resolved via <see cref="ItemActionFactory"/>; <c>null</c> means no use effect.</summary>
        public IItemAction ItemAction  { get; set; }
        /// <summary>String-keyed extension bag imported from extra JSON keys, for mod-defined item data.</summary>
        public PropertyData Properties  { get; set; } = new();
    }

}
