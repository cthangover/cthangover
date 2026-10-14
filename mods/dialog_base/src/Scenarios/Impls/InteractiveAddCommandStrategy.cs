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
	/// Scenario DSL command <c>interactive_add</c>: instantiates an interactive
	/// object from its definition ID. Optional <c>layer</c> parameter overrides
	/// the layer from the definition.
	///
	/// Usage: <c>interactive_add door_to_kitchen</c>
	///        <c>interactive_add door_to_kitchen layer=foreground</c>
	/// </summary>
	public class InteractiveAddCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
	{
		/// <summary>The lowercase DSL keyword handled by this strategy: <c>interactive_add</c>.</summary>
		public string Command => "interactive_add";

		/// <inheritdoc/>
		public int MinPositional => 1;

		/// <inheritdoc/>
		public int MaxPositional => 1;

		/// <inheritdoc/>
		public IReadOnlyList<string> AllowedNamed => new[] { "layer" };

		/// <summary>Queues an <see cref="ActionInteractiveAdd"/> for the definition id in <c>positional[0]</c>; lines without an id are skipped.</summary>
		public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
		{
			if (positional.Count == 0)
				return;

			var id = positional[0];
			dlg.InteractiveAdd(id);
		}
	}
}
