using Cthangover.Core.Interactive;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
	/// <summary>
	/// Dialog action that instantiates an interactive object from its definition
	/// and places it on the scene. Executes immediately (NoWait).
	/// </summary>
	public class ActionInteractiveAdd : ActionCommand
	{
		/// <summary>The interactive definition ID to load and instantiate.</summary>
		public string DefinitionId { get; set; }

		public override WaitType WaitType { get; set; } = WaitType.NoWait;

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
