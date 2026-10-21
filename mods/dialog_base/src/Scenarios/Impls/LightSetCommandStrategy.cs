using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Mods.Dialog;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Scenarios.Impls
{
	/// <summary>
	/// Handles the <c>light_set</c> DSL command. Passes a JSON string that
	/// describes lighting configuration to the dialog engine via
	/// <see cref="DialogQueue.LightSet"/>. The JSON structure is forwarded
	/// as-is to the rendering layer for point-light setup.
	/// </summary>
	public class LightSetCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
	{
		/// <inheritdoc/>
		public string Command => "light_set";

		/// <inheritdoc/>
		public int MaxPositional => 1;

		/// <inheritdoc/>
		public void Execute(DialogQueue dlg, List<string> positional,
			Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
		{
			var json = positional.Count > 0 ? positional[0] : null;
			dlg.LightSet(json);
		}
	}
}
