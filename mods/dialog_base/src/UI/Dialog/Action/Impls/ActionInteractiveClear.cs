using Cthangover.Core.Interactive;
using Cthangover.Core.Utils;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;

namespace Cthangover.Mods.Dialog.Action.Impls
{
	/// <summary>
	/// Dialog action that removes and destroys all active interactive objects.
	/// Typically used on scene exit to ensure clean state.
	/// </summary>
	public class ActionInteractiveClear : ActionCommand
	{
		/// <summary>Clearing happens immediately — the dialog does not pause.</summary>
		public override WaitType WaitType { get; set; } = WaitType.NoWait;

		/// <summary>Destroys every active interactive object via <see cref="InteractiveManager"/>.</summary>
		public override void DoRun(DialogRuntime runtime)
		{
			GameLogger.Log("INTERACTIVE", "ActionInteractiveClear: clearing all interactives");
			InteractiveManager.Instance?.ClearAll();
		}
	}
}
