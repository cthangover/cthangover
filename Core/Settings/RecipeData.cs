using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Tracks which crafting recipes the player has unlocked. Internally
    /// a <see cref="HashSet{String}"/> of recipe IDs that provides <c>O(1)</c>
    /// lookup. Recipes can be queried by <see cref="IRecipe.WorkbenchType"/>
    /// via <see cref="GetRecipesByType"/>, which resolves each ID through
    /// <see cref="Cthangover.Core.Factories.Impls.RecipeFactory"/> and
    /// filters the resulting <see cref="IRecipe"/> objects.
    /// </summary>
	public class RecipeData
	{
        /// <summary>Set of known recipe IDs (duplicate insertion is silently ignored).</summary>
		public HashSet<string> Data { get; set; } = new();

        /// <summary>
        /// Unlocks a recipe by ID. Returns <c>false</c> if the recipe
        /// was already known (duplicate).
        /// </summary>
		public bool Add(string id)
		{
			return Data.Add(id);
		}

        /// <summary><c>true</c> if the recipe with the given ID is already unlocked.</summary>
		public bool Has(string id)
		{
			return Data.Contains(id);
		}

        /// <summary>
        /// Returns all known recipes that belong to the specified
        /// <see cref="WorkbenchType"/>. Each ID is resolved via
        /// <see cref="Cthangover.Core.Factories.Impls.RecipeFactory.Instance"/>
        /// and filtered; IDs that resolve to <c>null</c> are silently
        /// dropped.
        /// </summary>
		public List<IRecipe> GetRecipesByType(WorkbenchType type)
		{
			return Data.Select(o => RecipeFactory.Instance.Get(o))
				.Where(o => o.WorkbenchType == type)
				.ToList();
		}
	}
}
