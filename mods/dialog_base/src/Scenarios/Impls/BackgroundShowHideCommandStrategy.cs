using System.Collections.Generic;
using System.Globalization;
using Cthangover.Core.Localization;
using Cthangover.Mods.Dialog;
using Cthangover.Mods.Dialog.Action.Impls;
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
    /// Handles the <c>background_show_hide</c> DSL command. Animates the background
    /// in (<c>show</c>) or out (<c>hide</c>) with configurable <c>duration</c> and
    /// <c>wait</c> flag. Delegates to <see cref="DialogQueue.BackgroundShowHide"/>.
    /// </summary>
    public class BackgroundShowHideCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "background_show_hide";
        /// <inheritdoc/>
        public int MaxPositional => 1;
        /// <inheritdoc/>
        public IReadOnlyList<string> AllowedNamed => new[] { "duration", "wait" };

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            var typeStr = positional.Count > 0 ? positional[0] : "show";
            var type = typeStr.ToLowerInvariant() == "show"
                ? BackgroundActionType.Show
                : BackgroundActionType.Hide;
            var duration = named.TryGetValue("duration", out var dur) ? ParseFloat(dur) : 1f;
            var wait = named.TryGetValue("wait", out var w) && w == "true";
            dlg.BackgroundShowHide(type, duration, wait);
        }

        private static float ParseFloat(string s)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
        }
    }
}
