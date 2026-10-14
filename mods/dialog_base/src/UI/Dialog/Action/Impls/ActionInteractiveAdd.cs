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
	/// Dialog action that instantiates an interactive object from its definition
	/// and places it on the scene. Executes immediately (NoWait).
	/// </summary>
	public class ActionInteractiveAdd : ActionCommand
	{
		/// <summary>The interactive definition ID to load and instantiate.</summary>
		public string DefinitionId { get; set; }

		/// <summary>Placement happens immediately — the dialog does not pause.</summary>
		public override WaitType WaitType { get; set; } = WaitType.NoWait;

		/// <summary>Instantiates the interactive object named by <see cref="DefinitionId"/> through <see cref="InteractiveManager"/>.</summary>
		public override void DoRun(DialogRuntime runtime)
		{
			if (string.IsNullOrEmpty(DefinitionId))
			{
				GameLogger.Log("INTERACTIVE", "ActionInteractiveAdd: DefinitionId is empty", LogLevel.Error);
				return;
			}

			var mgr = InteractiveManager.Instance;
			if (mgr == null)
			{
				GameLogger.Log("INTERACTIVE", "ActionInteractiveAdd: InteractiveManager not initialized yet", LogLevel.Error);
				return;
			}

			mgr.Add(DefinitionId);
		}
	}
}
