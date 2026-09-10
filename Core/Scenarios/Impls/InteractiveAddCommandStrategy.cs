using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
	/// <summary>
	/// Scenario DSL command <c>interactive_add</c>: instantiates an interactive
	/// object from its definition ID. Optional <c>layer</c> parameter overrides
	/// the layer from the definition.
	///
	/// Usage: <c>interactive_add door_to_kitchen</c>
	///        <c>interactive_add door_to_kitchen layer=foreground</c>
	/// </summary>
	public class InteractiveAddCommandStrategy : IScenarioCommandStrategy
	{
		public string Command => "interactive_add";

		public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
		{
			if (positional.Count == 0)
				return;

			var id = positional[0];
			dlg.InteractiveAdd(id);
		}
	}
}
