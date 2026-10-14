using System.Collections.Generic;
using Cthangover.Core.Items;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// The single instance lives in this mod assembly as a static lazy
    /// singleton; the static facade stays as the public API.
    /// </summary>
    public class ItemFactory
    {
        private static ItemFactory _instance;

        /// <summary>Static lazy singleton; kept as the public API.</summary>
        public static ItemFactory Instance => _instance ??= new ItemFactory();

        private Dictionary<string, ItemInfo> items;

        /// <summary>
        /// Builds a fresh <see cref="Item"/> from the collected <see cref="ItemInfo"/>,
        /// resolving the sprite through <see cref="ItemSpriteFactory"/>, the on-use
        /// action through <see cref="ItemActionFactory"/>, and importing extra JSON
        /// keys into <see cref="Item.Properties"/>. Unknown or empty ids return
        /// <c>null</c>.
        /// </summary>
        public IItem Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            var infos = items ?? Entities.Collect<ItemInfo>();

            // The registry refuses mod-data access until Boot.IsReady, so
            // this snapshot can never be collected from a half-built mod set;
            // the cached field is reset when ModManager.Reload rebuilds the
            // registry.
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
