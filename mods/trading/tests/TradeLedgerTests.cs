namespace Cthangover.Mods.Trading.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// Deals are all or nothing: a refused buy or sell must leave both the money
    /// and the goods exactly as they were.
    /// </summary>
    public class TradeLedgerTests
    {
        private static string Goods() =>
            Entities.Collect<ItemInfo>().Keys.First(k => k != ShopInfo.DefaultCurrency);

        private static ShopInfo Shop(string goods) => new()
        {
            ID = "test_shop",
            Sells = new List<ShopOffer> { new() { Item = goods, Price = 3 } },
            Buys = new List<ShopOffer> { new() { Item = goods, Price = 2 } },
        };

        [ModTest("ledger/buy_takes_money_and_adds_goods")]
        static void BuyTakesMoneyAndAddsGoods(ModTestContext ctx)
        {
            var goods = Goods();
            var shop = Shop(goods);
            var inventory = new Inventory();
            inventory.Add(shop.Currency, 10);
            var before = inventory.CheckCount(goods);

            ModAssert.True(TradeLedger.Buy(inventory, shop, goods, 3), "affordable buy succeeds");
            ModAssert.Equal(1, inventory.CheckCount(shop.Currency), "10 - 3 x 3 coins left");
            ModAssert.Equal(before + 3, inventory.CheckCount(goods), "three units bought");
            ModAssert.Equal(1, TradeLedger.Money(inventory, shop), "money reads the currency stack");
        }

        [ModTest("ledger/buy_refused_changes_nothing")]
        static void BuyRefusedChangesNothing(ModTestContext ctx)
        {
            var goods = Goods();
            var shop = Shop(goods);
            var inventory = new Inventory();
            inventory.Add(shop.Currency, 5);
            var before = inventory.CheckCount(goods);

            ModAssert.False(TradeLedger.Buy(inventory, shop, goods, 2), "6 coins needed, 5 owned");
            ModAssert.False(TradeLedger.Buy(inventory, shop, goods, 0), "zero units");
            ModAssert.False(TradeLedger.Buy(inventory, shop, goods, -1), "negative units");
            ModAssert.False(TradeLedger.Buy(inventory, shop, "no/such_item", 1), "not on sale");
            ModAssert.Equal(5, inventory.CheckCount(shop.Currency), "money untouched");
            ModAssert.Equal(before, inventory.CheckCount(goods), "goods untouched");
        }

        [ModTest("ledger/sell_pays_and_removes_goods")]
        static void SellPaysAndRemovesGoods(ModTestContext ctx)
        {
            var goods = Goods();
            var shop = Shop(goods);
            var inventory = new Inventory();
            inventory.Add(goods, 4);
            var before = inventory.CheckCount(goods);
            var money = inventory.CheckCount(shop.Currency);

            ModAssert.True(TradeLedger.Sell(inventory, shop, goods, 3), "owned units sell");
            ModAssert.Equal(money + 6, inventory.CheckCount(shop.Currency), "3 x 2 coins paid");
            ModAssert.Equal(before - 3, inventory.CheckCount(goods), "three units gone");
        }

        [ModTest("ledger/sell_refused_changes_nothing")]
        static void SellRefusedChangesNothing(ModTestContext ctx)
        {
            var goods = Goods();
            var shop = Shop(goods);
            var inventory = new Inventory();
            inventory.Add(goods, 2);
            inventory.Add(shop.Currency, 7);
            var before = inventory.CheckCount(goods);
            shop.Buys.Add(new ShopOffer { Item = shop.Currency, Price = 1 });

            ModAssert.False(TradeLedger.Sell(inventory, shop, goods, before + 1), "more than owned");
            ModAssert.False(TradeLedger.Sell(inventory, shop, goods, 0), "zero units");
            ModAssert.False(TradeLedger.Sell(inventory, shop, shop.Currency, 1), "money is never sold");
            ModAssert.False(TradeLedger.Sell(inventory, new ShopInfo { ID = "picky" }, goods, 1), "not bought by this shop");
            ModAssert.Equal(before, inventory.CheckCount(goods), "goods untouched");
            ModAssert.Equal(7, inventory.CheckCount(shop.Currency), "money untouched");
        }

        [ModTest("ledger/max_counts")]
        static void MaxCounts(ModTestContext ctx)
        {
            var goods = Goods();
            var shop = Shop(goods);
            var inventory = new Inventory();
            inventory.Add(shop.Currency, 11);
            inventory.Add(goods, 2);

            ModAssert.Equal(3, TradeLedger.MaxBuy(inventory, shop, goods), "11 coins buy three at 3");
            ModAssert.Equal(inventory.CheckCount(goods), TradeLedger.MaxSell(inventory, shop, goods), "the whole stack sells");
            ModAssert.Equal(0, TradeLedger.MaxBuy(inventory, shop, "no/such_item"), "not on sale");
            ModAssert.Equal(0, TradeLedger.MaxSell(inventory, new ShopInfo { ID = "picky" }, goods), "not bought");
            ModAssert.Equal(0, TradeLedger.Money(null, shop), "no inventory, no money");
        }
    }
}
