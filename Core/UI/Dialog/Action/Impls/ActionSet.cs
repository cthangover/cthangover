using System;
using Cthangover.Core.UI.Dialog.Action;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    public class ActionSet : ActionCommand
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public Func<string> Callback { get; set; }

        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            var val = Callback != null ? Callback() : Value;
            runtime.SetVariable(Name, val ?? string.Empty);
        }
    }
}
