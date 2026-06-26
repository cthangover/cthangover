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
        public Func<bool> Condition { get; set; }
        public string TrueGoTo { get; set; }
        public string FalseGoTo { get; set; }

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
