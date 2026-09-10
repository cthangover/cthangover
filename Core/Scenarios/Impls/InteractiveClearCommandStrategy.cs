using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
	/// <summary>
	/// Scenario DSL command <c>interactive_clear</c>: removes all active
	/// interactive objects from the scene.
	///
	/// Usage: <c>interactive_clear</c>
	/// </summary>
	public class InteractiveClearCommandStrategy : IScenarioCommandStrategy
	{
		public string Command => "interactive_clear";

		public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
		{
			dlg.InteractiveClear();
		}
	}
}
