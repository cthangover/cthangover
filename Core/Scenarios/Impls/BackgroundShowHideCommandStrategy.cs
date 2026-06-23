using System.Collections.Generic;
using System.Globalization;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Dialog.Action.Impls;

namespace Cthangover.Core.Scenarios
{
    public class BackgroundShowHideCommandStrategy : IScenarioCommandStrategy
    {
        public string Command => "background_show_hide";

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
