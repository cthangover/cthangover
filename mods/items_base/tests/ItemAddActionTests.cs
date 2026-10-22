namespace Cthangover.Mods.Items.Tests
{
    using System.Linq;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;

    /// <summary><c>action item.add</c> from a scenario: a count defaults to one, a bad one adds nothing.</summary>
    public class ItemAddActionTests
    {
        private static FakeActionContext Context(string id, string count)
        {
            var ctx = new FakeActionContext { Inventory = new Inventory() };
            if (id != null)
                ctx.Params["id"] = id;
            if (count != null)
                ctx.Params["count"] = count;
            return ctx;
        }

        [ModTest("item_add/adds_count")]
        static void AddsCount(ModTestContext test)
        {
            var id = Entities.Collect<ItemInfo>().Keys.First();
            var ctx = Context(id, "3");
            var before = ctx.Inventory.CheckCount(id);

            new ItemAddAction().Run(ctx);

            ModAssert.Equal(before + 3, ctx.Inventory.CheckCount(id), "three added");
            ModAssert.Equal(0, ctx.Warnings.Count, "no warnings");
        }

        [ModTest("item_add/count_defaults_to_one")]
        static void CountDefaultsToOne(ModTestContext test)
        {
            var id = Entities.Collect<ItemInfo>().Keys.First();
            var ctx = Context(id, null);
            var before = ctx.Inventory.CheckCount(id);

            new ItemAddAction().Run(ctx);

            ModAssert.Equal(before + 1, ctx.Inventory.CheckCount(id), "one added");
        }

        [ModTest("item_add/bad_arguments_add_nothing")]
        static void BadArgumentsAddNothing(ModTestContext test)
        {
            var id = Entities.Collect<ItemInfo>().Keys.First();
            foreach (var count in new[] { "0", "-2", "many" })
            {
                var ctx = Context(id, count);
                var before = ctx.Inventory.CheckCount(id);
                new ItemAddAction().Run(ctx);
                ModAssert.Equal(before, ctx.Inventory.CheckCount(id), "count=" + count);
                ModAssert.Equal(1, ctx.Warnings.Count, "warned about count=" + count);
            }

            var noId = Context(null, "1");
            new ItemAddAction().Run(noId);
            ModAssert.Equal(1, noId.Warnings.Count, "warned about the missing id");

            var noInventory = new FakeActionContext();
            noInventory.Params["id"] = id;
            new ItemAddAction().Run(noInventory);
            ModAssert.Equal(1, noInventory.Warnings.Count, "warned about the missing inventory");
        }

        [ModTest("item_add/name")]
        static void Name(ModTestContext test)
        {
            ModAssert.Equal("item.add", new ItemAddAction().Name, "DSL name");
        }
    }
}
