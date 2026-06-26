using System;
using System.Collections.Generic;
using Cthangover.Core.Items;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Factory that assembles <c>Item</c> instances from JSON metadata and
    /// two sub-factories: <c>ItemSpriteFactory</c> for the icon and
    /// <c>ItemActionFactory</c> for the on-use behaviour. Items are
    /// treated as <b>immutable value objects</b> — <c>Get</c> always
    /// creates a fresh <c>Item</c> from the cached <c>ItemInfo</c> rather
    /// than reusing a single instance, because items stack inside
    /// inventories and each slot must hold an independent copy. The JSON
    /// cache is built lazily on first access since an inventory-less game
    /// scene will never need it.
    /// </summary>
    public class ItemFactory
    {
        private static readonly Lazy<ItemFactory> lazy = new(() => new ItemFactory());
        public static ItemFactory Instance => lazy.Value;

        private Dictionary<string, ItemInfo> items;

        public IItem Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            if (items == null)
                items = ModManager.Instance.CollectJsonGroup<ItemInfo>("items");

            if (!items.TryGetValue(id, out var info))
            {
                GameLogger.Log("FACTORY", $"item '{id}' not found", LogLevel.Error);
                return null;
            }

            return new Item
            {
                ID = info.ID,
                Name = info.Name,
                Description = info.Description,
                Cost = info.Cost,
                Sprite = ItemSpriteFactory.Instance.Get(info.Sprite),
                ItemType = info.ItemType,
                ItemAction = ItemActionFactory.Instance.Get(info.ItemAction),
            };
        }
    }
}
