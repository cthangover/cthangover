using Cthangover.Core.Items;

namespace Cthangover.Mods.Trading
{
    /// <summary>
    /// The arithmetic of a deal, apart from the window so it can be tested and
    /// reused by a scenario action later. A deal is all or nothing: it is checked
    /// first (offer exists, positive quantity, enough money or goods) and only
    /// then both stacks change, so a refused deal leaves the inventory untouched.
    /// </summary>
    public static class TradeLedger
    {
        /// <summary>The player's money in the shop's currency.</summary>
        public static int Money(IInventory inventory, ShopInfo shop)
        {
            return inventory == null || shop == null ? 0 : inventory.CheckCount(shop.Currency);
        }

        /// <summary>How many units of <paramref name="itemId"/> the player can afford; 0 when the shop does not sell it.</summary>
        public static int MaxBuy(IInventory inventory, ShopInfo shop, string itemId)
        {
            var offer = shop?.FindSell(itemId);
            return offer == null ? 0 : Money(inventory, shop) / offer.Price;
        }

        /// <summary>How many units of <paramref name="itemId"/> the player can sell; 0 when the merchant does not buy it.</summary>
        public static int MaxSell(IInventory inventory, ShopInfo shop, string itemId)
        {
            if (inventory == null || shop?.FindBuy(itemId) == null || itemId == shop.Currency)
                return 0;
            return inventory.CheckCount(itemId);
        }

        /// <summary>
        /// Buys <paramref name="count"/> units: takes their price in money and adds
        /// the items. Returns <c>false</c> and changes nothing when the shop does not
        /// sell the item, the count is not positive or the money is short.
        /// </summary>
        public static bool Buy(IInventory inventory, ShopInfo shop, string itemId, int count)
        {
            if (count <= 0 || count > MaxBuy(inventory, shop, itemId))
                return false;
            if (!inventory.Remove(shop.Currency, shop.FindSell(itemId).Price * count))
                return false;
            inventory.Add(itemId, count);
            return true;
        }

        /// <summary>
        /// Sells <paramref name="count"/> units: removes the items and pays their
        /// price. Returns <c>false</c> and changes nothing when the merchant does not
        /// buy the item, the count is not positive or the player has fewer units.
        /// </summary>
        public static bool Sell(IInventory inventory, ShopInfo shop, string itemId, int count)
        {
            if (count <= 0 || count > MaxSell(inventory, shop, itemId))
                return false;
            if (!inventory.Remove(itemId, count))
                return false;
            inventory.Add(shop.Currency, shop.FindBuy(itemId).Price * count);
            return true;
        }
    }
}
