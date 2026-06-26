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
        public string GoTo { get; set; }

        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            runtime.TryGoTo(GoTo);
        }
    }
}
