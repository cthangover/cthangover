namespace Cthangover.Mods.Crafting.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;

    /// <summary>Refused recipes preserve every stack; duplicate rows are a combined requirement, never a discount.</summary>
    public class CraftLedgerTests
    {
        private static List<string> Goods() => Entities.Collect<ItemInfo>().Keys.Take(2).ToList();

        private static RecipeInfo Recipe(string input, string output) => new()
        {
            ID = "test/craft", Time = 5,
            Input = new List<IngredientInfo> { new() { ID = input, Count = 2 } },
            Output = new List<IngredientInfo> { new() { ID = output, Count = 1 } },
        };

        [ModTest("craft/batch_spends_inputs_and_adds_products")]
        static void Batch(ModTestContext ctx)
        {
            var ids = Goods();
            var bag = new Inventory();
            bag.Add(ids[0], 7);
            var recipe = Recipe(ids[0], ids[1]);
            ModAssert.True(CraftLedger.CanCraft(bag, recipe, 3), "three batches fit");
            ModAssert.Equal(7, bag.CheckCount(ids[0]), "preview does not consume");
            ModAssert.True(CraftLedger.TryCraft(bag, recipe, 3), "craft succeeds");
            ModAssert.Equal(1, bag.CheckCount(ids[0]), "six ingredients consumed");
            ModAssert.Equal(3, bag.CheckCount(ids[1]), "three products created");
        }

        [ModTest("craft/missing_duplicate_and_invalid_entries_are_all_or_nothing")]
        static void Refusals(ModTestContext ctx)
        {
            var ids = Goods();
            var bag = new Inventory();
            bag.Add(ids[0], 3);
            var recipe = Recipe(ids[0], ids[1]);
            recipe.Input.Add(new IngredientInfo { ID = ids[0], Count = 2 });
            ModAssert.False(CraftLedger.TryCraft(bag, recipe), "duplicate rows require four, not two");
            recipe.Input.RemoveAt(1);
            recipe.Output[0].ID = "missing/craft/product";
            ModAssert.False(CraftLedger.TryCraft(bag, recipe), "missing product refuses consumption");
            recipe.Output[0].ID = ids[1];
            recipe.Input.Add(new IngredientInfo { ID = "missing/craft/input", Count = 1 });
            ModAssert.False(CraftLedger.TryCraft(bag, recipe), "missing ingredient is not silently dropped");
            recipe.Input.RemoveAt(1);
            ModAssert.False(CraftLedger.TryCraft(bag, recipe, 0), "zero batch");
            ModAssert.False(CraftLedger.TryCraft(bag, recipe, -1), "negative batch");
            ModAssert.False(CraftLedger.TryCraft(bag, recipe, int.MaxValue), "overflowing batch");
            recipe.Input[0].Count = -1;
            ModAssert.False(CraftLedger.TryCraft(bag, recipe), "negative ingredient");
            ModAssert.Equal(3, bag.CheckCount(ids[0]), "all refusals left inputs untouched");
            ModAssert.Equal(0, bag.CheckCount(ids[1]), "all refusals left products untouched");
        }

        [ModTest("craft/output_overflow_refuses_before_consumption")]
        static void Overflow(ModTestContext ctx)
        {
            var ids = Goods();
            var bag = new Inventory();
            bag.Add(ids[0], 4);
            bag.Add(ids[1], int.MaxValue);
            ModAssert.False(CraftLedger.TryCraft(bag, Recipe(ids[0], ids[1])), "full output stack");
            ModAssert.Equal(4, bag.CheckCount(ids[0]), "input untouched");
            ModAssert.Equal(int.MaxValue, bag.CheckCount(ids[1]), "output untouched");
        }
    }
}
