using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios.Impls
{
	public class LightSetCommandStrategy : IScenarioCommandStrategy
	{
		public string Command => "light_set";

		public void Execute(DialogQueue dlg, List<string> positional,
			Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
		{
			var json = positional.Count > 0 ? positional[0] : null;
			dlg.LightSet(json);
		}
	}
}
