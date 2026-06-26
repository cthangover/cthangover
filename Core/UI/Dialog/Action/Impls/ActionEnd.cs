using Cthangover.Core.UI.Dialog.Action;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Terminates the dialog queue immediately. Calls Runtime.End() which
    /// destructs all remaining actions, processes the cleanup queue, hides
    /// the dialog box, and fires the dialog-end event.
    /// </summary>
    public class ActionEnd : ActionCommand
    {
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            runtime.End();
        }
    }
}
