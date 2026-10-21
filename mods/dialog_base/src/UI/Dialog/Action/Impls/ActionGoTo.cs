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

        /// <summary>Jumps the queue index to the action whose ID matches <see cref="GoTo"/>.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            runtime.TryGoTo(GoTo);
        }
    }
}
