namespace Cthangover.Mods.Trading
{
    /// <summary>
    /// One line of a shop's price list: an item id and its unit price in the
    /// shop's currency. The same type serves both directions — in
    /// <see cref="ShopInfo.Sells"/> it is what the player pays, in
    /// <see cref="ShopInfo.Buys"/> what the merchant pays. An offer with a price
    /// below 1 is ignored by <see cref="TradeLedger"/>: a free deal is a gift and
    /// belongs in a scenario.
    /// </summary>
    public class ShopOffer
    {
        /// <summary>Item id (<c>"Kind": "item"</c>).</summary>
        public string Item { get; set; }

        /// <summary>Unit price in the shop's currency.</summary>
        public int Price { get; set; }
    }
}
