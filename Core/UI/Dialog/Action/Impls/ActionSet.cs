using System;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Sets a dialog runtime variable (Name=Value) for use by subsequent actions
    /// via the ${var} substitution syntax. The optional Callback Func&lt;string&gt;
    /// allows lazy evaluation — if set, Value is ignored and Callback is invoked
    /// at runtime. This enables dynamic values (e.g. quest state, character names)
    /// that can't be known when the dialog script is authored.
    /// </summary>
    public class ActionSet : ActionCommand
    {
        /// <summary>Variable name. Referenced by subsequent actions via ${name} syntax.</summary>
        public string Name { get; set; }
        /// <summary>Literal value to store. Ignored if <see cref="Callback"/> is set.</summary>
        public string Value { get; set; }
        /// <summary>Lazy evaluation delegate. When non-null, invoked at runtime to compute the value dynamically.</summary>
        public Func<string> Callback { get; set; }

        /// <summary>Variable assignment is instant — the dialog continues without pause.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            var val = Callback != null ? Callback() : Value;
            runtime.SetVariable(Name, val ?? string.Empty);
        }
    }
}
