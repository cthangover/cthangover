using System;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
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
