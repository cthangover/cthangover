using System;
using System.Collections.Generic;
using Cthangover.Core.Items;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Factories.Impls
{
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
