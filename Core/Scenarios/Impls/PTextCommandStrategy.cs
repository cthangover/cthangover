using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Dialog.Action;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>ptext</c> DSL command. A "plain text" variant of
    /// <see cref="TextCommandStrategy"/> that skips speaker attribution
    /// but still supports <c>first</c>/<c>second</c> avatar faces and
    /// <c>hide_color</c> and <c>wait</c> behavior flags. Text content
    /// can be provided directly or resolved via a <c>key=</c> localization key.
    /// </summary>
    public class PTextCommandStrategy : IScenarioCommandStrategy
    {
        /// <inheritdoc/>
        public string Command => "ptext";

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

            var action = dlg.PText(textVal, first, second, hideColor);

            if (named.TryGetValue("wait", out var wait) && wait == "false")
                action.WaitType = WaitType.NoWait;
        }
    }
}
