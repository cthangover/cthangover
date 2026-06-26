using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
	/// <summary>
	/// Scenario DSL command <c>interactive_set</c>: modifies a property of an
	/// active interactive object. Supported properties: <c>enabled</c>, <c>visible</c>.
	///
	/// Usage: <c>interactive_set door_to_kitchen enabled=false</c>
	///        <c>interactive_set door_to_kitchen visible=true</c>
	/// </summary>
	public class InteractiveSetCommandStrategy : IScenarioCommandStrategy
	{
		public string Command => "interactive_set";

		public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
		{
			if (positional.Count < 2)
				return;

			var id = positional[0];

			if (named.TryGetValue("enabled", out var enabled))
				dlg.InteractiveSet(id, "enabled", enabled);
			else if (named.TryGetValue("visible", out var visible))
				dlg.InteractiveSet(id, "visible", visible);
		}
	}
}
