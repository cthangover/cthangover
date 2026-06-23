using System;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
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
