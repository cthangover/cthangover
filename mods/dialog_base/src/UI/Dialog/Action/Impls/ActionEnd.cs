using Cthangover.Mods.Dialog.Action;
using Godot;
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
    /// Terminates the dialog queue immediately. Calls Runtime.End() which
    /// destructs all remaining actions, processes the cleanup queue, hides
    /// the dialog box, and fires the dialog-end event.
    /// </summary>
    public class ActionEnd : ActionCommand
    {
        /// <summary>Ends the dialog queue immediately — no pause, no return.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        /// <summary>Terminates the queue via <see cref="DialogRuntime.End"/> — destructs remainders, runs cleanup, hides the box, fires the end event.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            runtime.End();
        }
    }
}
