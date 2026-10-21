namespace Cthangover.Mods.Items.Tests
{
    using System.Linq;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// Stack arithmetic of <see cref="Inventory"/>: a count that is not
    /// positive is no operation at all — a negative removal must not grow a
    /// stack and an addition must not create an empty or negative one.
    /// </summary>
    public class InventoryTests
    {
        [ModTest("inventory/non_positive_counts_change_nothing")]
        static void NonPositiveCountsChangeNothing(ModTestContext ctx)
        {
            var id = Entities.Collect<ItemInfo>().Keys.First();
            var inventory = new Inventory();
            inventory.Add(id, 2);
            var before = inventory.CheckCount(id);

            ModAssert.False(inventory.Remove(id, -5), "negative removal is refused");
            ModAssert.False(inventory.Remove(id, 0), "zero removal is refused");
            inventory.Add(id, -3);
            inventory.Add(id, 0);
            ModAssert.Equal(before, inventory.CheckCount(id), "stack count after non-positive operations");

            var other = Entities.Collect<ItemInfo>().Keys.FirstOrDefault(k => !inventory.HasItem(k));
            if (other == null)
                return;
            inventory.Add(other, 0);
            ModAssert.False(inventory.HasItem(other), "zero addition creates no stack");
        }
    }
}
