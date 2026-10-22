namespace Cthangover.Mods.Trading.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;

    /// <summary>A shop is hand-written JSON: it must survive a round trip and its lookups must skip broken lines.</summary>
    public class ShopInfoTests
    {
        [ModTest("shop/json_round_trip")]
        static void JsonRoundTrip(ModTestContext ctx)
        {
            var original = new ShopInfo
            {
                ID = "rpg_demo/village_merchant",
                Name = "rpg_demo/shop/village_merchant",
                Currency = "money/shell",
                Sells = new List<ShopOffer> { new() { Item = "rpg_demo/elixir", Price = 10 } },
                Buys = new List<ShopOffer> { new() { Item = "rpg_demo/stones", Price = 2 } },
            };

            var restored = JsonSerializer.Deserialize<ShopInfo>(JsonSerializer.Serialize(original));

            ModAssert.Equal(original.ID, restored.ID, "Id");
            ModAssert.Equal(original.Name, restored.Name, "Name");
            ModAssert.Equal(original.Currency, restored.Currency, "Currency");
            ModAssert.Equal(1, restored.Sells.Count, "Sells");
            ModAssert.Equal("rpg_demo/elixir", restored.Sells[0].Item, "Sells item");
            ModAssert.Equal(10, restored.Sells[0].Price, "Sells price");
            ModAssert.Equal(1, restored.Buys.Count, "Buys");
            ModAssert.Equal("rpg_demo/stones", restored.Buys[0].Item, "Buys item");
            ModAssert.Equal(2, restored.Buys[0].Price, "Buys price");
        }

        [ModTest("shop/defaults_from_minimal_json")]
        static void DefaultsFromMinimalJson(ModTestContext ctx)
        {
            var shop = JsonSerializer.Deserialize<ShopInfo>("{\"Id\":\"bare\"}");

            ModAssert.Equal("bare", shop.ID, "Id");
            ModAssert.Equal(ShopInfo.DefaultCurrency, shop.Currency, "default currency");
            ModAssert.Equal(0, shop.Sells.Count, "no goods");
            ModAssert.Null(shop.FindBuy("anything"), "buys nothing");
        }

        [ModTest("shop/find_skips_broken_offers")]
        static void FindSkipsBrokenOffers(ModTestContext ctx)
        {
            var shop = new ShopInfo
            {
                ID = "s",
                Sells = new List<ShopOffer> { null, new() { Item = "a", Price = 0 }, new() { Item = "a", Price = 4 } },
                Buys = null,
            };

            ModAssert.Equal(4, shop.FindSell("a")?.Price ?? -1, "the first offer with a real price wins");
            ModAssert.Null(shop.FindSell("b"), "unknown item");
            ModAssert.Null(shop.FindSell(null), "null id");
            ModAssert.Null(shop.FindBuy("a"), "null list buys nothing");
        }
    }
}
