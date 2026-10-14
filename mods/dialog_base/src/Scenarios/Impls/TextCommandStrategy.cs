using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Mods.Dialog;
using Cthangover.Mods.Dialog.Action;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Scenarios
{
    /// <summary>
    /// Handles the <c>text</c> DSL command, the primary dialog display instruction.
    /// Shows character text with optional <c>first</c>/<c>second</c> speaker
    /// avatar faces, <c>hide_color</c> tint suppression, and a <c>wait</c> flag
    /// that controls whether the dialog waits for user input before continuing.
    /// Text can be literal or resolved from a <c>key=</c> localization entry.
    /// </summary>
    public class TextCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "text";
        /// <inheritdoc/>
        public int MinArguments => 1;
        /// <inheritdoc/>
        public int MaxPositional => 1;
        /// <inheritdoc/>
        public IReadOnlyList<string> AllowedNamed => new[] { "key", "first", "second", "hide_color", "wait" };

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
