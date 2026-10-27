namespace Cthangover.Mods.Crafting.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Testing;

    /// <summary>Stations and existing recipe definitions are portable JSON data, with no map or game reference.</summary>
    public class CraftStationTests
    {
        [ModTest("craft/station_and_recipe_json_round_trip")]
        static void RoundTrip(ModTestContext ctx)
        {
            var station = new CraftStation { ID = "test/bench", Name = "test/name", Recipes = new List<string> { "test/salve", "test/lantern" } };
            var restored = JsonSerializer.Deserialize<CraftStation>(JsonSerializer.Serialize(station));
            ModAssert.Equal(station.ID, restored.ID, "station id");
            ModAssert.Equal(station.Name, restored.Name, "station title");
            ModAssert.Equal(2, restored.Recipes.Count, "recipe count");
            ModAssert.Equal("test/lantern", restored.Recipes[1], "recipe order");
            var recipe = new RecipeInfo { ID = "test/salve", Name = "test/name", Description = "test/desc", Time = 6,
                Input = new List<IngredientInfo> { new() { ID = "test/mushroom", Count = 2 } },
                Output = new List<IngredientInfo> { new() { ID = "test/salve", Count = 1 } } };
            var again = JsonSerializer.Deserialize<RecipeInfo>(JsonSerializer.Serialize(recipe));
            ModAssert.Equal(recipe.ID, again.ID, "recipe id");
            ModAssert.Equal(recipe.Name, again.Name, "recipe name");
            ModAssert.Equal(recipe.Description, again.Description, "description");
            ModAssert.Equal((int)recipe.Type, (int)again.Type, "station type retained");
            ModAssert.Equal(6, again.Time, "duration");
            ModAssert.Equal("test/mushroom", again.Input[0].ID, "ingredient");
            ModAssert.Equal(2, again.Input[0].Count, "ingredient count");
            ModAssert.Equal("test/salve", again.Output[0].ID, "product");
            ModAssert.Equal(1, again.Output[0].Count, "product count");
        }
    }
}
