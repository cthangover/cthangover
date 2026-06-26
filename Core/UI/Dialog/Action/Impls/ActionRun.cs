using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Executes an arbitrary Action delegate inside the dialog queue.
    /// Allows imperative code injection (e.g. quest state changes, UI toggles)
    /// without creating a dedicated action class. NoWait so the queue advances
    /// immediately after the delegate returns.
    /// </summary>
    public class ActionRun : ActionCommand
    {
        public System.Action Executable { get; set; }

        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            if (Executable == null)
            {
                GameLogger.Log("DIALOG", "ActionRun: Executable delegate is null", LogLevel.Error);
                return;
            }

            Executable();
        }
    }
}
