

namespace Cthangover.Core.Items
{
    /// <summary>
    /// Plain POCO implementing <c>IIngredient</c>. Used by
    /// <c>RecipeFactory</c> to materialise ingredient entries from
    /// <c>IngredientInfo</c> DTOs after resolving the item reference
    /// through <c>ItemFactory</c>.
    /// </summary>
    public class Ingredient : IIngredient{
        /// <summary>The resolved item this formula entry references.</summary>
        public IItem Item  { get; set; }
        /// <summary>How many copies the recipe consumes or produces.</summary>
        public int   Count { get; set; }
    }
}
