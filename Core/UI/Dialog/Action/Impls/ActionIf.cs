using System;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Conditional branch: evaluates a Func&lt;bool&gt; and jumps to TrueGoTo or
    /// FalseGoTo based on the result. If the chosen target is null/empty, falls
    /// through to Next(). NoWait so branching is transparent to the player.
    /// </summary>
    public class ActionIf : ActionCommand
    {
        /// <summary>Delegate evaluated at runtime. Must return true/false to select the branch target.</summary>
        public Func<bool> Condition { get; set; }
        /// <summary>Target action ID if <see cref="Condition"/> returns true. Empty or null causes fall-through.</summary>
        public string TrueGoTo { get; set; }
        /// <summary>Target action ID if <see cref="Condition"/> returns false. Empty or null causes fall-through.</summary>
        public string FalseGoTo { get; set; }

        /// <summary>Branch evaluation is transparent — the dialog jumps to the target without user input.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            if (Condition == null)
            {
                GameLogger.Log("DIALOG", "ActionIf: Condition delegate is null", LogLevel.Error);
                runtime.Next();
                return;
            }

            var result = Condition();
            var goTo = result ? TrueGoTo : FalseGoTo;

            if (!string.IsNullOrEmpty(goTo))
                runtime.TryGoTo(goTo);
            else
                runtime.Next();
        }
    }
}
