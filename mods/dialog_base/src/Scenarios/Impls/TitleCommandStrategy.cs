using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Mods.Dialog;
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
    /// Handles the <c>title</c> DSL command. Displays a centered title/caption
    /// text on screen, typically used for chapter headings or location labels.
    /// Text can be literal or resolved from a <c>key=</c> localization entry.
    /// Silently does nothing if no text value is provided or resolved.
    /// </summary>
    public class TitleCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "title";
        /// <inheritdoc/>
        public int MinArguments => 1;
        /// <inheritdoc/>
        public int MaxPositional => 1;
        /// <inheritdoc/>
        public IReadOnlyList<string> AllowedNamed => new[] { "key" };

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            var textVal = positional.Count > 0 ? positional[0] : null;
            if (named.TryGetValue("key", out var key) && locale != null)
            {
                var loc = locale.Get(key);
                if (loc != null) textVal = loc;
            }
            if (textVal != null)
                dlg.Title(textVal);
        }
    }
}
