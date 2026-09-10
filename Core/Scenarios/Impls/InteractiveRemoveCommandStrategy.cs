using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
	/// <summary>
	/// Scenario DSL command <c>interactive_remove</c>: removes and destroys
	/// an interactive object by its definition ID.
	///
	/// Usage: <c>interactive_remove door_to_kitchen</c>
	/// </summary>
	public class InteractiveRemoveCommandStrategy : IScenarioCommandStrategy
	{
		public string Command => "interactive_remove";

		public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
		{
			if (positional.Count == 0)
				return;

			var id = positional[0];
			dlg.InteractiveRemove(id);
		}
	}
}
