using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios.Impls
{
	/// <summary>
	/// Handles the <c>light_set</c> DSL command. Passes a JSON string that
	/// describes lighting configuration to the dialog engine via
	/// <see cref="DialogQueue.LightSet"/>. The JSON structure is forwarded
	/// as-is to the rendering layer for point-light setup.
	/// </summary>
	public class LightSetCommandStrategy : IScenarioCommandStrategy
	{
		/// <inheritdoc/>
		public string Command => "light_set";

		/// <inheritdoc/>
		public void Execute(DialogQueue dlg, List<string> positional,
			Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
		{
			var json = positional.Count > 0 ? positional[0] : null;
			dlg.LightSet(json);
		}
	}
}
