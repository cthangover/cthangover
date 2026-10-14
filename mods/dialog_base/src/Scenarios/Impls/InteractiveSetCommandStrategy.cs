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
	/// Scenario DSL command <c>interactive_set</c>: modifies a property of an
	/// active interactive object. Supported properties: <c>enabled</c>, <c>visible</c>.
	///
	/// Usage: <c>interactive_set door_to_kitchen enabled=false</c>
	///        <c>interactive_set door_to_kitchen visible=true</c>
	/// </summary>
	public class InteractiveSetCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
	{
		/// <summary>The lowercase DSL keyword handled by this strategy: <c>interactive_set</c>.</summary>
		public string Command => "interactive_set";

		/// <inheritdoc/>
		public int MinPositional => 1;

		/// <inheritdoc/>
		public int MinArguments => 2;

		/// <inheritdoc/>
		public IReadOnlyList<string> AllowedNamed => new[] { "enabled", "visible" };

		/// <summary>Queues an <see cref="ActionInteractiveSet"/> for <c>positional[0]</c>; <c>enabled=</c> wins over <c>visible=</c> when both are given, and lines without both an id and a property are skipped.</summary>
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
