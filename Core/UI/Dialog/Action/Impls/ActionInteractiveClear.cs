using Cthangover.Core.Interactive;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
	/// <summary>
	/// Dialog action that removes and destroys all active interactive objects.
	/// Typically used on scene exit to ensure clean state.
	/// </summary>
	public class ActionInteractiveClear : ActionCommand
	{
		public override WaitType WaitType { get; set; } = WaitType.NoWait;

		public override void DoRun(DialogRuntime runtime)
		{
			GameLogger.Log("INTERACTIVE", "ActionInteractiveClear: clearing all interactives");
			InteractiveManager.Instance?.ClearAll();
		}
	}
}
