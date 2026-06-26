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
        public string Text { get; set; }
        public string GoTo { get; set; }

        public static string Point => Guid.NewGuid().ToString();

        public static SelectVariant New(string text, string goTo)
        {
            return new SelectVariant { Text = text, GoTo = goTo };
        }
    }
}
