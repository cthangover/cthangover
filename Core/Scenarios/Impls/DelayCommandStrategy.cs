using System.Collections.Generic;
using System.Globalization;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>delay</c> DSL command. Pauses dialog execution for a
    /// number of seconds, optionally displaying a text label during the pause.
    /// If the <c>hidden</c> flag is set, the delay runs without visible text.
    /// Supports localization via the <c>key=</c> named parameter.
    /// </summary>
    public class DelayCommandStrategy : IScenarioCommandStrategy
    {
        /// <inheritdoc/>
        public string Command => "delay";

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            var time = positional.Count > 0 ? ParseFloat(positional[0]) : 1f;
            var hidden = positional.Count > 1 && positional[1] == "hidden"
                         || named.TryGetValue("hidden", out var h) && h == "true";
            var showText = hidden ? null
                : positional.Count > 1 ? positional[1]
                : null;

            if (named.TryGetValue("key", out var key) && locale != null && showText != null)
            {
                var loc = locale.Get(key);
                if (loc != null) showText = loc;
            }

            if (showText != null)
                dlg.Delay(time, showText);
            else
                dlg.Delay(time, hidden);
        }

        private static float ParseFloat(string s)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
        }
    }
}
