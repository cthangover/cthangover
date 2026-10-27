namespace Cthangover.Mods.Items.Tests
{
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using System.Linq;

    /// <summary>Conditions read current inventory state and compose with the scenario language's existing operators.</summary>
    public class ItemCountConditionTests
    {
        [ModTest("items/count_condition_follows_bag_changes")]
        static void CountFollowsBag(ModTestContext ctx)
        {
            var bag = GameData.Instance.Runtime.Inventory;
            var before = bag.Items;
            bag.Items = new();
            try
            {
                var id = Entities.Collect<ItemInfo>().Keys.First();
                ModAssert.True(ScenarioCondition.Evaluate($"item('{id}') == 0"), "empty bag");
                bag.Add(id, 3);
                ModAssert.True(ScenarioCondition.Evaluate($"item('{id}') >= 3 && item('{id}') < 4"), "count range");
                ModAssert.False(ScenarioCondition.Evaluate($"item('{id}') > 3"), "upper bound");
                bag.Remove(id, 2);
                ModAssert.True(ScenarioCondition.Evaluate($"item('{id}') == 1"), "updated bag");
                ModAssert.True(ScenarioCondition.Evaluate("item('missing/item') == 0"), "unknown id counts zero");
            }
            finally { bag.Items = before; }
        }
    }
}
