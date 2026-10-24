using System;
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
    /// Data model for a dialog choice option: display Text and a GoTo target
    /// action ID. The static Point property returns a fresh GUID for runtime
    /// goto targets. The New factory provides concise C# construction without
    /// object-initializer syntax.
    /// </summary>
    public class SelectVariant
    {
        /// <summary>The text displayed on the choice button. Supports runtime variable substitution.</summary>
        public string Text { get; set; }
        /// <summary>Target action ID to jump to via <see cref="DialogRuntime.TryGoTo"/> when this variant is selected.</summary>
        public string GoTo { get; set; }

        /// <summary>
        /// Condition of the shared condition language (<c>option "…" if=… -> :label</c>); the
        /// option is shown only while it holds. Null = always shown. Evaluated each time the
        /// choice appears, so a stat, a flag or an item gained a moment ago counts.
        /// </summary>
        public string Condition { get; set; }

        /// <summary>Whether the option is offered now.</summary>
        public bool IsShown() => string.IsNullOrWhiteSpace(Condition) || ScenarioCondition.Evaluate(Condition);

        /// <summary>Generates a new unique action ID for use as a GoTo target (e.g. with <see cref="DialogQueue.Point"/>).</summary>
        public static string Point => Guid.NewGuid().ToString();

        /// <summary>Factory for concise C# construction without object-initializer syntax.</summary>
        public static SelectVariant New(string text, string goTo)
        {
            return new SelectVariant { Text = text, GoTo = goTo };
        }
    }
}
