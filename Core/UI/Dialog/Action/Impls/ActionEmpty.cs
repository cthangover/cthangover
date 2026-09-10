using Cthangover.Core.UI.Dialog.Action;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// No-op placeholder action. Used as a labeled jump target (Point) for GoTo
    /// commands — provides an addressable marker in the dialog sequence without
    /// any visible effect. NoWait so the dialog skips through labels instantly.
    /// </summary>
    public class ActionEmpty : ActionCommand
    {
        /// <summary>Label actions skip immediately so the dialog doesn't pause on markers.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
        }
    }
}
