using System;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
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
