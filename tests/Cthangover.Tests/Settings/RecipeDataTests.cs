using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Items;
using Cthangover.Core.Settings;

namespace Engine.Tests.Settings
{
    public class RecipeDataTests
    {
        [Fact]
        public void Add_NewRecipe_ReturnsTrue()
        {
            var rd = new RecipeData();
            Assert.True(rd.Add("recipe_1"));
        }

        [Fact]
        public void Add_Duplicate_ReturnsFalse()
        {
            var rd = new RecipeData();
            rd.Add("recipe_1");
            Assert.False(rd.Add("recipe_1"));
        }

        [Fact]
        public void Has_ExistingRecipe_ReturnsTrue()
        {
            var rd = new RecipeData();
            rd.Add("recipe_1");
            Assert.True(rd.Has("recipe_1"));
        }

        [Fact]
        public void Has_MissingRecipe_ReturnsFalse()
        {
            var rd = new RecipeData();
            Assert.False(rd.Has("nonexistent"));
        }

        [Fact]
        public void Data_InitialState_IsEmpty()
        {
            var rd = new RecipeData();
            Assert.NotNull(rd.Data);
            Assert.Empty(rd.Data);
        }
    }
}
