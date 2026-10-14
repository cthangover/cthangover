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

namespace Cthangover.Mods.Dialog.Scenarios
{
	/// <summary>
	/// Scenario DSL command <c>interactive_remove</c>: removes and destroys
	/// an interactive object by its definition ID.
	///
	/// Usage: <c>interactive_remove door_to_kitchen</c>
	/// </summary>
	public class InteractiveRemoveCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
	{
		/// <summary>The lowercase DSL keyword handled by this strategy: <c>interactive_remove</c>.</summary>
		public string Command => "interactive_remove";

		/// <inheritdoc/>
		public int MinPositional => 1;

		/// <inheritdoc/>
		public int MaxPositional => 1;

		/// <summary>Queues an <see cref="ActionInteractiveRemove"/> for the definition id in <c>positional[0]</c>; lines without an id are skipped.</summary>
		public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
		{
			if (positional.Count == 0)
				return;

			var id = positional[0];
			dlg.InteractiveRemove(id);
		}
	}
}
