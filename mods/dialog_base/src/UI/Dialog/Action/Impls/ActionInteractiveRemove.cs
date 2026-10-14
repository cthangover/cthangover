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
	/// Dialog action that removes and destroys an interactive object by its definition ID.
	/// </summary>
	public class ActionInteractiveRemove : ActionCommand
	{
		/// <summary>The definition ID of the interactive object to remove.</summary>
		public string DefinitionId { get; set; }

		/// <summary>Removal happens immediately — the dialog does not pause.</summary>
		public override WaitType WaitType { get; set; } = WaitType.NoWait;

		/// <summary>Destroys the interactive object named by <see cref="DefinitionId"/>.</summary>
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
