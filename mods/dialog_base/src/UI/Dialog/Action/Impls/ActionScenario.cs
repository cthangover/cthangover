using Cthangover.Core.Actions;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Utils;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Action.Impls
{
    /// <summary>
    /// Bridges the dialog system to the broader scenario action system
    /// (ScenarioActionFactory). Resolves an action by name and runs it with a
    /// ScenarioActionContext wrapping the dialog runtime. Wraps execution in
    /// try/catch so a faulty scenario action doesn't crash the dialog queue.
    /// Every error log carries the owning scenario identity (see
    /// <see cref="DescribeScenario"/>): the runtime cannot report the source
    /// file and line — the parser drops them — but it can name the scenario
    /// definition, which together with the validator's file:line report
    /// pinpoints the broken command.
    /// </summary>
    public class ActionScenario : ActionCommand
    {
        /// <summary>Scenario action name resolved through <see cref="ScenarioActionFactory"/>. Empty names are logged and skipped.</summary>
        public string ActionName { get; set; }

        /// <summary>The scenario action runs and the dialog advances — no synchronization.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

		/// <summary>Resolves <see cref="ActionName"/> through <see cref="ScenarioActionFactory"/> and runs it with a dialog-backed context; exceptions are logged, never propagated.</summary>
		public override void DoRun(DialogRuntime runtime)
		{
			if (string.IsNullOrEmpty(ActionName))
			{
				GameLogger.Log("DIALOG", "ActionScenario: ActionName is null or empty" + DescribeScenario(runtime), LogLevel.Error);
				return;
			}

			var action = ScenarioActionFactory.Instance.Get(ActionName);
			if (action == null)
			{
				GameLogger.Log("DIALOG", $"ActionScenario: scenario action '{ActionName}' is not registered — the action is skipped, check the spelling and that the defining mod is loaded" + DescribeScenario(runtime), LogLevel.Error);
				return;
			}

            var ctx = new ScenarioActionContext(runtime);
            try
            {
                action.Run(ctx);
            }
            catch (System.Exception ex)
            {
	            GameLogger.Log("DIALOG", $"ActionScenario: {ex.Message}\n{ex.StackTrace}" + DescribeScenario(runtime), LogLevel.Error);
            }
		}

        /// <summary>
        /// Formats the scenario suffix for error logs. The locker that started
        /// the dialog is the event owning the scenario; its ID is the scenario
        /// definition name (ScenarioId) or, for code-driven lockers, the event
        /// type name. Returns a human-readable suffix either way, so a missing
        /// action in a shipped game can be traced to its scenario from
        /// user://logs/cthangover.log alone.
        /// </summary>
        private static string DescribeScenario(DialogRuntime runtime)
        {
            var id = runtime?.DialogBox?.Locker?.ID;
            return string.IsNullOrEmpty(id)
                ? " (scenario unknown)"
                : $" (scenario '{id}')";
        }
    }
}
