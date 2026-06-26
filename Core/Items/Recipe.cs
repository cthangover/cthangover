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
        public string           ID            { get; set; }
        public string           Name          { get; set; }
        public string           Description   { get; set; }
        public int              Time          { get; set; }
        public WorkbenchType    WorkbenchType { get; set; }
        public List<IIngredient> Input         { get; set; }
        public List<IIngredient> Output        { get; set; }
    }
}
