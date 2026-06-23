using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;

namespace Cthangover.Core.Settings
{
	public class RecipeData
	{
		public HashSet<string> Data { get; set; } = new();

		public bool Add(string id)
		{
			return Data.Add(id);
		}
		public bool Has(string id)
		{
			return Data.Contains(id);
		}

		public List<IRecipe> GetRecipesByType(WorkbenchType type)
		{
			return Data.Select(o => RecipeFactory.Instance.Get(o))
				.Where(o => o.WorkbenchType == type)
				.ToList();
		}
	}
}
