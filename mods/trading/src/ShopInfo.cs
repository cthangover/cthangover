using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Mods.Trading
{
    /// <summary>
    /// A merchant as data (<c>"Kind": "shop"</c>), opened by
    /// <c>action trade.open shop=&lt;id&gt;</c>. The stock of <see cref="Sells"/> is
    /// unlimited and the shop has no state of its own: what the player sells
    /// disappears into the merchant's pocket, so nothing needs saving except the
    /// player's inventory. Money is an ordinary inventory item
    /// (<see cref="Currency"/>), which keeps it in the items save slot and lets
    /// scenarios pay with <c>item.add</c>.
    /// </summary>
    [EntityKind("shop")]
    public class ShopInfo : IIdentifiable
    {
        /// <summary>Currency item used when a shop does not name one.</summary>
        public const string DefaultCurrency = "money/coin";

        /// <summary>Shop id, the <c>shop=</c> argument of <c>trade.open</c>.</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        /// <summary>Locale key of the window title (the merchant's name).</summary>
        public string Name { get; set; }

        /// <summary>Item id of the money this shop takes and pays.</summary>
        public string Currency { get; set; } = DefaultCurrency;

        /// <summary>What the merchant sells to the player, in display order.</summary>
        public List<ShopOffer> Sells { get; set; } = new();

        /// <summary>What the merchant buys from the player; anything else is refused.</summary>
        public List<ShopOffer> Buys { get; set; } = new();

        /// <summary>The offer for buying <paramref name="itemId"/> from the merchant, or <c>null</c>.</summary>
        public ShopOffer FindSell(string itemId) => Find(Sells, itemId);

        /// <summary>The offer for selling <paramref name="itemId"/> to the merchant, or <c>null</c>.</summary>
        public ShopOffer FindBuy(string itemId) => Find(Buys, itemId);

        private static ShopOffer Find(List<ShopOffer> offers, string itemId)
        {
            if (offers == null || string.IsNullOrEmpty(itemId))
                return null;
            foreach (var offer in offers)
            {
                if (offer != null && offer.Price > 0 && string.Equals(offer.Item, itemId, StringComparison.Ordinal))
                    return offer;
            }
            return null;
        }
    }
}
