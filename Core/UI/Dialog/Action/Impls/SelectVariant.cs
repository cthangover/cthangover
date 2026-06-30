using System;

namespace Cthangover.Core.UI.Dialog.Action.Impls
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

        /// <summary>Generates a new unique action ID for use as a GoTo target (e.g. with <see cref="DialogQueue.Point"/>).</summary>
        public static string Point => Guid.NewGuid().ToString();

        /// <summary>Factory for concise C# construction without object-initializer syntax.</summary>
        public static SelectVariant New(string text, string goTo)
        {
            return new SelectVariant { Text = text, GoTo = goTo };
        }
    }
}
