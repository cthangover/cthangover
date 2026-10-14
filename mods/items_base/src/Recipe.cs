using System.Collections.Generic;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// Concrete recipe fulfilling <c>IRecipe</c>. Populated by
    /// <c>RecipeFactory.CreateRecipe</c> from a <c>RecipeInfo</c> DTO
    /// after resolving every ingredient entry's item ID through
    /// <c>ItemFactory</c>. Recipes are treated as immutable value
    /// objects by the crafting UI — <c>RecipeFactory.Get</c> returns
    /// the cached instance directly without copying.
    /// </summary>
    public class Recipe : IRecipe
    {
        /// <summary>Unique recipe ID (path-style, e.g. <c>"recipe/wolf_meat_to_ration"</c>).</summary>
        public string           ID            { get; set; }
        /// <summary>Locale key for the recipe display name.</summary>
        public string           Name          { get; set; }
        /// <summary>Locale key for the recipe description text.</summary>
        public string           Description   { get; set; }
        /// <summary>Crafting time in in-game minutes.</summary>
        public int              Time          { get; set; }
        /// <summary>Required crafting station (only <see cref="WorkbenchType.Cooking"/> exists).</summary>
        public WorkbenchType    WorkbenchType { get; set; }
        /// <summary>Ingredients consumed on a successful craft; item references resolved by <see cref="Factories.Impls.RecipeFactory"/>.</summary>
        public List<IIngredient> Input         { get; set; }
        /// <summary>Items produced on a successful craft.</summary>
        public List<IIngredient> Output        { get; set; }
    }
}
