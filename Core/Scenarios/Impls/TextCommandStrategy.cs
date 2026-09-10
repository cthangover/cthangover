using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Dialog.Action;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>text</c> DSL command, the primary dialog display instruction.
    /// Shows character text with optional <c>first</c>/<c>second</c> speaker
    /// avatar faces, <c>hide_color</c> tint suppression, and a <c>wait</c> flag
    /// that controls whether the dialog waits for user input before continuing.
    /// Text can be literal or resolved from a <c>key=</c> localization entry.
    /// </summary>
    public class TextCommandStrategy : IScenarioCommandStrategy
    {
        /// <inheritdoc/>
        public string Command => "text";

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            var textVal = positional.Count > 0 ? positional[0] : "";
            if (named.TryGetValue("key", out var key) && locale != null)
            {
                var loc = locale.Get(key);
                if (loc != null) textVal = loc;
            }

            var first = named.TryGetValue("first", out var f) ? f : null;
            var second = named.TryGetValue("second", out var s) ? s : null;
            var hideColor = named.TryGetValue("hide_color", out var hc) && hc == "true";

            var action = dlg.Text(textVal, first, second, hideColor);

            if (named.TryGetValue("wait", out var wait) && wait == "false")
                action.WaitType = WaitType.NoWait;
        }
    }
}
