using System;
using System.Collections.Generic;
using Cthangover.Core.Items;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
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

            var infos = items ?? Entities.Collect<ItemInfo>();

            // The inventory autoload resolves starter items before the
            // launcher finishes loading mods. Publishing that empty snapshot
            // would keep every item unresolvable for the whole session, since
            // ModManager.ClearCaches flushes resolvers, not factory fields.
            if (ModManager.Instance.IsInitialized)
                items ??= infos;

            if (!infos.TryGetValue(id, out var info))
            {
                GameLogger.Log("FACTORY", $"item '{id}' not found", LogLevel.Error);
                return null;
            }

            var properties = new PropertyData();
            PropertyData.ImportJsonProperties(properties, info.ExtraProperties);

            return new Item
            {
                ID = info.ID,
                Name = info.Name,
                Description = info.Description,
                Cost = info.Cost,
                Sprite = ItemSpriteFactory.Instance.Get(info.Sprite),
                ItemType = info.ItemType,
                ItemAction = ItemActionFactory.Instance.Get(info.ItemAction),
                Properties = properties,
            };
        }
    }
}
