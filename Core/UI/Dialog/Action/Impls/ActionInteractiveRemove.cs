using Cthangover.Core.Interactive;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
	/// <summary>
	/// Dialog action that removes and destroys an interactive object by its definition ID.
	/// </summary>
	public class ActionInteractiveRemove : ActionCommand
	{
		/// <summary>The definition ID of the interactive object to remove.</summary>
		public string DefinitionId { get; set; }

		public override WaitType WaitType { get; set; } = WaitType.NoWait;

		public override void DoRun(DialogRuntime runtime)
		{
			if (string.IsNullOrEmpty(DefinitionId))
			{
				GameLogger.Log("INTERACTIVE", "ActionInteractiveRemove: DefinitionId is empty", LogLevel.Error);
				return;
			}

			InteractiveManager.Instance?.Remove(DefinitionId);
		}
	}
}
