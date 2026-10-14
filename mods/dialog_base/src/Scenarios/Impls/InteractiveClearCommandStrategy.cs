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
	/// Scenario DSL command <c>interactive_clear</c>: removes all active
	/// interactive objects from the scene.
	///
	/// Usage: <c>interactive_clear</c>
	/// </summary>
	public class InteractiveClearCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
	{
		/// <summary>The lowercase DSL keyword handled by this strategy: <c>interactive_clear</c>.</summary>
		public string Command => "interactive_clear";

		/// <inheritdoc/>
		public int MaxPositional => 0;

		/// <summary>Queues an <see cref="ActionInteractiveClear"/> that removes every active interactive object from the scene.</summary>
		public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
		{
			dlg.InteractiveClear();
		}
	}
}
