using System.Collections.Generic;
using Cthangover.Core.Items;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Contract for unlocked-recipe tracking. The concrete implementation
    /// (<c>RecipeData</c>) lives in the items mod; the core and other
    /// subsystems depend only on this interface through
    /// <see cref="RuntimeData.RecipeData"/>.
    /// </summary>
    public interface IRecipeData
    {
        /// <summary>Set of known recipe IDs (duplicate insertion is silently ignored).</summary>
        HashSet<string> Data { get; set; }

        /// <summary>Unlocks a recipe by ID; returns <c>false</c> if it was already known.</summary>
        bool Add(string id);

        /// <summary><c>true</c> if the recipe with the given ID is already unlocked.</summary>
        bool Has(string id);

        /// <summary>Returns all known recipes that belong to the specified workbench type.</summary>
        List<IRecipe> GetRecipesByType(WorkbenchType type);
    }
}
