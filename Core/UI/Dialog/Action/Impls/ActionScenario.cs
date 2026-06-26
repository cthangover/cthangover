using Cthangover.Core.Actions;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Bridges the dialog system to the broader scenario action system
    /// (ScenarioActionFactory). Resolves an action by name and runs it with a
    /// ScenarioActionContext wrapping the dialog runtime. Wraps execution in
    /// try/catch so a faulty scenario action doesn't crash the dialog queue.
    /// </summary>
    public class ActionScenario : ActionCommand
    {
        public string ActionName { get; set; }

        public override WaitType WaitType { get; set; } = WaitType.NoWait;

		public override void DoRun(DialogRuntime runtime)
		{
			if (string.IsNullOrEmpty(ActionName))
			{
				GameLogger.Log("DIALOG", "ActionScenario: ActionName is null or empty", LogLevel.Error);
				return;
			}

			var action = ScenarioActionFactory.Instance.Get(ActionName);
			if (action == null)
				return;

            var ctx = new ScenarioActionContext(runtime);
            try
            {
                action.Run(ctx);
            }
            catch (System.Exception ex)
            {
	            GameLogger.Log("DIALOG", $"ActionScenario: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
            }
		}
    }
}
