using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    public class LightUseTimeCommandStrategy : IScenarioCommandStrategy
    {
        public string Command => "light_use_time";

        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            var val = positional.Count > 0 ? positional[0] : "true";
            dlg.LightUseTime(val == "true");
        }
    }
}
