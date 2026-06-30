using Cthangover.Core.UI.Dialog.Action;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Unconditional jump to a labeled action in the dialog queue. NoWait so
    /// the jump happens transparently. The target is resolved by action ID
    /// (set via ActionCommand.SetID or the Point()/Empty() fluent methods).
    /// </summary>
    public class ActionGoTo : ActionCommand
    {
        /// <summary>Target action ID to jump to. Resolved by linear search through the dialog queue.</summary>
        public string GoTo { get; set; }

        /// <summary>Jump happens transparently — the dialog continues from the target without pausing.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            runtime.TryGoTo(GoTo);
        }
    }
}
